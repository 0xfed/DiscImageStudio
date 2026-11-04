"""Standard-library checks for the shared Python catalog, parser, and source coverage."""

import argparse
import ast
from collections import Counter
import contextlib
import inspect
import io
import locale
import os
from pathlib import Path
import re
from string import Formatter
import sys
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import localization as loc

TOOLS = (
    "tools/localization.py", "tools/generate_efm_tables.py",
    "tools/BluRayByteProbe/reference/analyze.py",
    "tools/BluRayByteProbe/reference/pattern_search.py",
    "tools/BluRayByteProbe/reference/robust_pattern_search.py",
    "tools/BluRayByteProbe/plot_results.py", "tools/DvdGrayProbe/verify_mixtures.py",
)


def source_keys():
    keys = {"Duplicate localization key: {0}"}
    for relative in TOOLS:
        tree = ast.parse((ROOT / relative).read_text(encoding="utf-8"))
        parents = {child: parent for parent in ast.walk(tree) for child in ast.iter_child_nodes(parent)}
        translators = {"t"} | {name.asname or name.name for node in ast.walk(tree)
            if isinstance(node, ast.ImportFrom) and node.module == "localization" for name in node.names if name.name == "t"}
        for node in ast.walk(tree):
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id in translators:
                if node.args and isinstance(node.args[0], ast.Constant) and isinstance(node.args[0].value, str):
                    keys.add(node.args[0].value)
            if isinstance(node, ast.Constant) and isinstance(node.value, str) and any('\u4e00' <= character <= '\u9fff' for character in node.value):
                ancestor = parents.get(node)
                while ancestor is not None and not (isinstance(ancestor, ast.Call) and isinstance(ancestor.func, ast.Name) and ancestor.func.id in translators):
                    ancestor = parents.get(ancestor)
                if ancestor is None:
                    raise AssertionError(f"Unlocalized CJK text in {relative}:{node.lineno}")
            if isinstance(node, ast.Raise) and isinstance(node.exc, ast.Call):
                for argument in node.exc.args:
                    if isinstance(argument, (ast.Constant, ast.JoinedStr)) and not isinstance(getattr(argument, "value", None), (int, float)):
                        raise AssertionError(f"Unlocalized exception literal in {relative}:{node.lineno}")
            if isinstance(node, ast.Assert) and isinstance(node.msg, (ast.Constant, ast.JoinedStr)):
                raise AssertionError(f"Unlocalized assertion in {relative}:{node.lineno}")
    for node in ast.walk(ast.parse(inspect.getsource(argparse))):
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id in ("_", "ngettext"):
            for argument in node.args[:2 if node.func.id == "ngettext" else 1]:
                if isinstance(argument, ast.Constant) and isinstance(argument.value, str):
                    keys.add(argument.value)
    keys.add("optional arguments")
    return keys


class PythonLocalization(unittest.TestCase):
    def setUp(self):
        self.original = loc.language

    def tearDown(self):
        loc.language = self.original

    def test_source_catalog_and_format_parity(self):
        sources = {str(path.relative_to(ROOT)).replace('\\', '/') for path in (ROOT / "tools").rglob("*.py")
                   if not any(part in ("bin", "obj", ".venv", "__pycache__") for part in path.parts)}
        self.assertEqual(sources, set(TOOLS), "Audit new Python tools before declaring their translations complete")
        percent = re.compile(r"%(?:\([^)]+\))?[-+# 0]*\d*(?:\.\d+)?[diouxXeEfFgGcrsa%]")
        keys = source_keys()
        for key in keys:
            self.assertIn(key, loc.catalog())
            values = loc.catalog()[key]
            for language in loc.LANGUAGES:
                with self.subTest(key=key, language=language):
                    text = values[language]
                    self.assertTrue(text.strip())
                    self.assertEqual(Counter(percent.findall(values["en"])), Counter(percent.findall(text)))
                    self.assertEqual(Counter((name, spec, conversion) for _, name, spec, conversion in Formatter().parse(values["en"]) if name is not None),
                                     Counter((name, spec, conversion) for _, name, spec, conversion in Formatter().parse(text) if name is not None))
        print(f"Python localization source audit: {len(keys)} keys × {len(loc.LANGUAGES)} languages")

    def test_normalization_and_system_fallback(self):
        for tag, expected in (("zh-TW", "zh-TW"), ("zh_HK", "zh-TW"), ("zh-Hant-JP", "zh-TW"),
                              ("zh-Hans-SG", "zh-CN"), ("ja-JP", "ja"), ("tr-TR", "tr"), ("en-US", "en")):
            self.assertEqual(loc.normalize(tag), expected)
            self.assertEqual(loc.resolve("system", tag), expected)
        self.assertEqual(loc.resolve("system", "fr-FR"), "en")
        self.assertEqual(loc.resolve("system", "zh-CN"), "zh-CN")

    def test_flags_environment_values_and_numeric_locale(self):
        numeric = locale.setlocale(locale.LC_NUMERIC)
        with patch.dict(os.environ, {loc.ENVIRONMENT_VARIABLE: "ja"}):
            self.assertEqual(loc.initialize(["--help"]), ["--help"])
            self.assertEqual(loc.language, "ja")
            self.assertEqual(loc.initialize(["--output-dir=--language", "--language=tr"]), ["--output-dir=--language"])
            self.assertEqual(loc.language, "tr")
            self.assertEqual(loc.initialize(["--input", "--language", "--language", "en"]), ["--input", "--language"])
            self.assertEqual(loc.language, "en")
            self.assertEqual(loc.initialize(["--language", "tr", "--", "--language", "ja"]), ["--", "--language", "ja"])
            self.assertEqual(loc.language, "tr")
            for arguments, key in ((["--language"], "Cli.MissingLanguage"),
                                   (["--language", "es"], "Cli.UnsupportedLanguage"),
                                   (["--language", "ja", "--language", "en"], "Cli.DuplicateLanguage")):
                with self.assertRaises(ValueError) as failure:
                    loc.initialize(arguments)
                self.assertEqual(str(failure.exception), loc.t(key, "es") if key == "Cli.UnsupportedLanguage" else loc.t(key))
        self.assertEqual(locale.setlocale(locale.LC_NUMERIC), numeric)

    def test_parser_translates_without_leaking_gettext_hooks(self):
        original, plural = argparse._, argparse.ngettext
        for language in loc.LANGUAGES:
            loc.language = language
            parser = loc.ArgumentParser(prog="fixture")
            parser.add_argument("--count", required=True, type=int)
            help_text = parser.format_help()
            self.assertIn(loc.t("options"), help_text)
            self.assertIn(loc.t("show this help message and exit"), help_text)
            with contextlib.redirect_stderr(io.StringIO()) as errors, self.assertRaises(SystemExit) as failure:
                parser.parse_args(["--count", "bad"])
            self.assertEqual(failure.exception.code, 2)
            self.assertIn(loc.t("invalid %(type)s value: %(value)r") % {"type": "int", "value": "bad"}, errors.getvalue())
            self.assertIs(argparse._, original)
            self.assertIs(argparse.ngettext, plural)

    def test_error_reporting_survives_a_failed_main_catalog(self):
        for language in loc.LANGUAGES:
            loc.language = language
            message = loc.t("Duplicate localization key: {0}", "key-日本語-İ")
            expected = loc.t("Cli.Error", message)

            def fail():
                raise ValueError(message)

            with patch.object(loc, "catalog", side_effect=ValueError(message)), contextlib.redirect_stderr(io.StringIO()) as errors:
                self.assertEqual(loc.run(fail), 1)
            self.assertEqual(errors.getvalue().strip(), expected)


if __name__ == "__main__":
    unittest.main()
