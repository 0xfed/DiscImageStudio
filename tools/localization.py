"""Shared catalog access and UI-language selection for repository Python tools.

Numeric locale, command/option names, paths, and serialized field names stay intact.
"""

import argparse
from contextlib import contextmanager
from functools import lru_cache, wraps
import json
import locale
import os
from pathlib import Path
import sys
from threading import RLock


ROOT = Path(__file__).resolve().parents[1]
ENVIRONMENT_VARIABLE = "DISC_IMAGE_STUDIO_LANGUAGE"
LANGUAGES = ("zh-CN", "zh-TW", "en", "ja", "tr")


def normalize(language):
    name = (language or "system").strip().lower().replace("_", "-")
    if name == "system":
        return "system"
    for short in ("en", "ja", "tr"):
        if name == short or name.startswith(short + "-"):
            return short
    if name in ("zh-tw", "zh-hk", "zh-mo", "zh-cht", "zh-hant") or name.startswith("zh-hant-"):
        return "zh-TW"
    if name in ("zh", "zh-cn", "zh-sg", "zh-hans", "zh-chs") or name.startswith("zh-hans-"):
        return "zh-CN"
    return "system"


def _system_locale():
    if sys.platform == "win32":
        import ctypes
        tag = locale.windows_locale.get(ctypes.windll.kernel32.GetUserDefaultUILanguage())
        if tag:
            return tag
    for name in ("LC_ALL", "LC_MESSAGES", "LANG"):
        if os.environ.get(name):
            return os.environ[name].split(".")[0].split("@")[0]
    return locale.getlocale()[0] or "en"


STARTUP_LOCALE = _system_locale()


def resolve(language, system_locale=None):
    normalized = normalize(language)
    if normalized != "system":
        return normalized
    name = (system_locale or STARTUP_LOCALE).replace("_", "-").lower()
    if name == "zh" or name.startswith("zh-"):
        return "zh-TW" if normalize(name) == "zh-TW" else "zh-CN"
    return next((short for short in ("ja", "tr") if name == short or name.startswith(short + "-")), "en")


language = resolve(os.environ.get(ENVIRONMENT_VARIABLE))


@lru_cache(maxsize=1)
def catalog():
    result = {}
    duplicates = []
    for path in sorted((ROOT / "src/DiscImageStudio.Localization/Catalogs").glob("*.json")):
        for key, translations in json.loads(path.read_text(encoding="utf-8-sig")).items():
            if key in result:
                duplicates.append(key)
            result[key] = translations
    if duplicates:
        key = "Duplicate localization key: {0}"
        translations = result.get(key, {})
        raise ValueError(translations.get(language, translations.get("zh-CN", key)).format(duplicates[0]))
    return result


@lru_cache(maxsize=1)
def _error_catalog():
    return json.loads((ROOT / "src/DiscImageStudio.Localization/Catalogs/CatalogLoader.json").read_text(encoding="utf-8-sig"))


def t(key, *arguments):
    # Error handlers must not retry the main catalog after it has failed to load.
    translations = (_error_catalog() if key == "Cli.Error" else catalog()).get(key, {})
    text = translations.get(language, translations.get("zh-CN", key))
    return text.format(*arguments) if arguments else text


def initialize(arguments=None, valueless_options=()):
    """Remove global language flags without consuming option values or text after --."""
    global language
    language = resolve(os.environ.get(ENVIRONMENT_VARIABLE))
    arguments = list(sys.argv[1:] if arguments is None else arguments)
    switches = {"--help", "-h", *valueless_options}
    remaining = []
    specified = False
    index = 0
    while index < len(arguments):
        argument = arguments[index]
        lower = argument.lower()
        if argument == "--":
            remaining.extend(arguments[index:])
            break
        inline = lower.startswith("--language=")
        if not inline and lower != "--language":
            remaining.append(argument)
            if argument.startswith("--") and "=" not in argument and argument not in switches and index + 1 < len(arguments):
                index += 1
                remaining.append(arguments[index])
            index += 1
            continue
        if specified:
            raise ValueError(t("Cli.DuplicateLanguage"))
        specified = True
        if inline:
            selected = argument[11:]
        else:
            index += 1
            if index == len(arguments) or arguments[index].startswith("--"):
                raise ValueError(t("Cli.MissingLanguage"))
            selected = arguments[index]
        normalized = normalize(selected)
        if not selected.strip() or normalized == "system" and selected.strip().lower() != "system":
            raise ValueError(t("Cli.UnsupportedLanguage", selected))
        language = resolve(normalized)
        index += 1
    return remaining


_parser_lock = RLock()


@contextmanager
def _parser_translations():
    # argparse uses module-level gettext hooks. Restore them before returning so
    # importing a numerical reference does not alter other parsers in the process.
    with _parser_lock:
        previous, previous_plural = argparse._, argparse.ngettext
        argparse._ = t
        argparse.ngettext = lambda singular, plural, count: t(singular if count == 1 else plural)
        try:
            yield
        finally:
            argparse._, argparse.ngettext = previous, previous_plural


def _localized(method):
    @wraps(method)
    def invoke(*args, **kwargs):
        with _parser_translations():
            return method(*args, **kwargs)
    return invoke


class ArgumentParser(argparse.ArgumentParser):
    @_localized
    def __init__(self, *args, **kwargs):
        kwargs.setdefault("epilog", t("Cli.LanguageHelp"))
        kwargs.setdefault("formatter_class", argparse.RawDescriptionHelpFormatter)
        super().__init__(*args, **kwargs)

    parse_args = _localized(argparse.ArgumentParser.parse_args)
    parse_known_args = _localized(argparse.ArgumentParser.parse_known_args)
    format_help = _localized(argparse.ArgumentParser.format_help)
    format_usage = _localized(argparse.ArgumentParser.format_usage)
    error = _localized(argparse.ArgumentParser.error)
    exit = _localized(argparse.ArgumentParser.exit)


def configure_plot_fonts(plt, font_manager):
    preferred = {
        "zh-CN": ("Noto Sans CJK SC", "Microsoft YaHei", "PingFang SC"),
        "zh-TW": ("Noto Sans CJK TC", "Microsoft JhengHei", "PingFang TC"),
        "ja": ("Noto Sans CJK JP", "Yu Gothic", "Hiragino Sans"),
    }
    for path in sorted((ROOT / "src/DiscImageStudio.App/Assets/Fonts").glob("HarmonyOS_Sans_SC_*.ttf")):
        font_manager.fontManager.addfont(str(path))
    available = {font.name for font in font_manager.fontManager.ttflist}
    families = [name for name in preferred.get(language, ()) if name in available]
    if language in preferred and "HarmonyOS Sans SC" in available:
        families.append("HarmonyOS Sans SC")
    families.append("DejaVu Sans")
    if "HarmonyOS Sans SC" in available and "HarmonyOS Sans SC" not in families:
        families.append("HarmonyOS Sans SC")
    plt.rcParams["font.family"] = families


def run(main):
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")
    try:
        result = main()
        return 0 if result is None else result
    except Exception as error:
        print(t("Cli.Error", str(error) or t("Python.ValidationFailed")), file=sys.stderr)
        return 1
