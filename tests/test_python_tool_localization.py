"""Five-language tool integration using bounded searches and synthetic plot/atlas fixtures.

These fixtures test translation, numerical stability, file schemas, and figure layout.
They are not measurements of a real disc or a replacement for a full probe sweep.
Requires NumPy and matplotlib, matching the tools under test.
"""

import contextlib
import copy
import csv
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import warnings
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import localization as loc

REFERENCE = ROOT / "tools/BluRayByteProbe/reference"
BASELINES = ROOT / "tests/fixtures/python-localization-baselines.json"


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return digest(json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode())


def json_hash(path, omitted=()):
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    for name in omitted:
        value.pop(name)
    return canonical(value)


def run_script(relative, arguments, cwd, expected=0, environment="en"):
    result = subprocess.run([sys.executable, str(ROOT / relative), *map(str, arguments)], cwd=cwd,
                            env={**os.environ, loc.ENVIRONMENT_VARIABLE: environment},
                            capture_output=True, encoding="utf-8", timeout=180)
    if result.returncode != expected:
        raise AssertionError(f"{relative}: expected {expected}, got {result.returncode}: {result.stderr}")
    return result


def make_plot_fixture(directory):
    directory.mkdir()
    sys.path.insert(0, str(REFERENCE))
    from analyze import cycle

    def stats(index):
        runs = 240000 + index * 100
        histogram = [0] * 32
        histogram[3] = 4 * runs - 958264
        histogram[4] = 958264 - 3 * runs
        transitions = runs + 1
        data_transitions = transitions * 4 // 5
        return {"ChannelBits": 958272, "RunCounts": histogram, "Transitions": transitions,
                "CompletedRuns": runs, "CompletedRunBits": 958264, "Density": transitions / 958272,
                "MeanRun": 958264 / runs, "DataRegionTransitions": data_transitions,
                "ParityRegionTransitions": transitions - data_transitions,
                "DataRegionDensity": data_transitions / (958272 * .8),
                "ParityRegionDensity": (transitions - data_transitions) / (958272 * .2)}

    values = []
    reference = []
    for byte in range(256):
        exact = cycle(byte)
        cluster = stats(byte)
        values.append({"Byte": byte, "Hex": f"{byte:02X}", "Cluster": cluster,
                       "Pure": {"Density": exact["density"], "ChannelBits": 786432},
                       "ZeroDcDensity": cluster["Density"] - .002,
                       "OneDcDensity": cluster["Density"] + .001,
                       "RandomDcDensity": cluster["Density"] - .0008})
        reference.append({"q": byte, "density": exact["density"]})
    cases = []
    addresses = []
    for index, (pattern, layout, phase) in enumerate((
        ("0000101101110010", "dc-block-reset", 2),
        ("001111", "physical-17pp-input", 3),
        ("0111011110", "dc-block-reset", 4),
    )):
        item = {"Pattern": pattern, "Layout": layout, "Phase": phase, "Psn": 0x100000,
                "Greedy": stats(20 + index * 70), "VerifiedPayloadBytes": 65536,
                "ZeroDcDensity": .2, "OneDcDensity": .3, "RandomDcDensity": .25}
        cases.append(item)
        for address in range(4):
            confirmation = copy.deepcopy(item)
            confirmation["Psn"] += address * 32
            addresses.append(confirmation)
    for name, value in (("all-bytes.json", values), ("context.json", {"AllBytes": [{"Statistics": {"RunCountOutside2TTo8T": 0}}]}),
                        ("fine-patterns.json", {"PhaseSweep": cases, "AddressConfirmations": addresses})):
        (directory / name).write_text(json.dumps(value), encoding="utf-8")
    (directory / "reference").mkdir()
    (directory / "reference/independent-periodic-results.json").write_text(json.dumps({"all_bytes": reference}), encoding="utf-8")


def make_atlas_fixture(directory):
    directory.mkdir()
    # Deliberately invalid mixtures provide a stable failure-path fixture.
    # No geometric mask is copied from the verifier or C# generator.
    data = bytes((0xA5, 0x66, 0x64, 0xE4, 0xB9, 0x92)) * 6_000_000
    (directory / "atlas.q8").write_bytes(data)
    metadata = {"schema": "dvd-gray-probe-v1", "atlasWidth": 6000, "atlasHeight": 6000,
                "cellMm": .02, "canvasMm": 120, "atlasSha256": digest(data),
                "panelsClockwiseFromTwelve": [{"panel": index + 1, "darkQ": f"{value:02X}"}
                    for index, value in enumerate((0x66, 0x64, 0xE4, 0x64, 0xE4, 0x66, 0xE4, 0x66, 0x64, 0x66, 0xE4, 0x64))]}
    (directory / "probe.json").write_text(json.dumps(metadata), encoding="utf-8")


def fake_pdf():
    def page(number, numbers):
        row = ["\n".join(map(str, numbers))]
        for state in range(4):
            row.extend(("\n".join(f"{(byte * 37 + state):016b}" for byte in numbers),
                        "\n".join(str((byte + state) % 4 + 1) for byte in numbers)))
        return SimpleNamespace(page_number=number, extract_tables=lambda: [[[], [], row]])

    pages = [None] * 74
    for index in range(6):
        pages[66 + index] = page(67 + index, list(range(index * 256 // 6, (index + 1) * 256 // 6)))
    for index in range(2):
        pages[72 + index] = page(73 + index, list(range(index * 44, (index + 1) * 44)))
    return contextlib.nullcontext(SimpleNamespace(pages=pages))


def plot_hashes(directory):
    fine = json.loads((directory / "fine-validation.json").read_text(encoding="utf-8"))
    for row in fine["SelectedPatterns"]:
        row.pop("name")
    with (directory / "recommended-patterns.csv").open(encoding="utf-8-sig", newline="") as file:
        reader = csv.DictReader(file)
        fields = reader.fieldnames
        rows = list(reader)
    for row in rows:
        row.pop("name")
    numeric_markdown = [line for line in (directory / "all-bytes.md").read_text(encoding="utf-8").splitlines()
                        if len(line.split("|")) > 2 and line.split("|")[1].strip() in {f"{value:02X}" for value in range(256)}]
    return {"validation": json_hash(directory / "validation.json"), "fine-validation": canonical(fine),
            "recommended-header": canonical(fields), "recommended-values": canonical(rows),
            "fine-csv": digest((directory / "fine-patterns.csv").read_bytes()), "markdown-values": canonical(numeric_markdown)}


def check(figure_output):
    baseline = json.loads(BASELINES.read_text(encoding="utf-8"))
    scripts = (("tools/BluRayByteProbe/reference/analyze.py", "Python.ReferenceDescription"),
               ("tools/BluRayByteProbe/reference/pattern_search.py", "Exhaustive 17PP periodic-input search; both previous-bit states are checked."),
               ("tools/BluRayByteProbe/reference/robust_pattern_search.py", "Search short input patterns robust to either bit-pair phase."),
               ("tools/BluRayByteProbe/plot_results.py", "Python.PlotDescription"),
               ("tools/DvdGrayProbe/verify_mixtures.py", "Python.MixtureHelp"),
               ("tools/generate_efm_tables.py", "Python.EfmHelp"))
    figure_output.mkdir(parents=True, exist_ok=True)
    (figure_output / "FIXTURES.txt").write_text(__doc__, encoding="utf-8")
    with tempfile.TemporaryDirectory(prefix="disc-python-languages-") as temporary:
        work = Path(temporary)
        plot_source, atlas = work / "plot-source", work / "原图-İ-日本語"
        make_plot_fixture(plot_source)
        make_atlas_fixture(atlas)
        atlas_hash = digest((atlas / "atlas.q8").read_bytes())
        generator = load_module("efm_generator_languages", ROOT / "tools/generate_efm_tables.py")
        for language in loc.LANGUAGES:
            loc.language = language
            for script, description in scripts:
                result = run_script(script, ["--language", language, "--help"], work)
                normalized = " ".join(result.stdout.split())
                if " ".join(loc.t(description).split()) not in normalized or " ".join(loc.t("Cli.LanguageHelp").split()) not in normalized:
                    raise AssertionError(f"Untranslated help: {script}/{language}")
            for script, _ in scripts[:5]:
                result = run_script(script, ["--language", language, "--unknown"], work, expected=2)
                expected = loc.t("the following arguments are required: %s") % "--prepared" if script == "tools/DvdGrayProbe/verify_mixtures.py" else loc.t("unrecognized arguments: %s") % "--unknown"
                if expected not in result.stderr:
                    raise AssertionError(f"Untranslated parser error: {script}/{language}")
            for flags, key in ((["--language"], "Cli.MissingLanguage"),
                               (["--language", "es"], "Cli.UnsupportedLanguage"),
                               (["--language", language, "--language", "en"], "Cli.DuplicateLanguage")):
                failure = run_script(scripts[0][0], flags, work, expected=1, environment=language)
                detail = loc.t(key, "es") if key == "Cli.UnsupportedLanguage" else loc.t(key)
                if loc.t("Cli.Error", detail) not in failure.stderr:
                    raise AssertionError("Untranslated global flag error")
            environment_help = run_script(scripts[0][0], ["--help"], work, environment=language)
            if " ".join(loc.t("Python.ReferenceDescription").split()) not in " ".join(environment_help.stdout.split()):
                raise AssertionError("Environment language selection failed")
            directory = work / ("--language" if language == "tr" else f"结果-İ-{language}")
            directory.mkdir()
            for script, _ in scripts[:3]:
                arguments = ["--language", language, "--output-dir=" + str(directory)]
                if not script.endswith("analyze.py"):
                    arguments.extend(("--max-period", "8"))
                run_script(script, arguments, work)
            for name, expected in baseline["reference"].items():
                path = directory / name
                if name.endswith(".json"):
                    if name == "independent-periodic-results.json":
                        data = json.loads(path.read_text(encoding="utf-8"))
                        if data.pop("domain") != loc.t("repeated post-scramble Q, no DC/ECC/BIS/sync"):
                            raise AssertionError("Reference explanation bypassed localization")
                        actual = canonical(data)
                    else:
                        actual = json_hash(path)
                else:
                    actual = digest(path.read_bytes())
                if actual != expected:
                    raise AssertionError(f"Changed original reference data/schema: {name}/{language}")
            result = run_script("tools/DvdGrayProbe/verify_mixtures.py", ["--prepared", atlas, "--language", language], work, expected=1)
            if result.stderr:
                raise AssertionError("Expected a completed failing measurement, not an execution error: " + result.stderr)
            summary = json.loads((atlas / "mixture-measurements.json").read_text(encoding="utf-8"))
            if summary["measurement"] != loc.t("actual atlas pixel occupancy; excludes panel margins and column gutters; not optical brightness or ISO channel-sample occupancy"):
                raise AssertionError("Mixture explanation bypassed localization")
            if json_hash(atlas / "mixture-measurements.json", ("measurement", "measured_utc")) != baseline["mixture-json"] or digest((atlas / "mixture-measurements.csv").read_bytes()) != baseline["mixture-csv"]:
                raise AssertionError(f"Changed original occupancy results/schema: {language}")
            if json.loads(result.stdout)["all_passed"] or digest((atlas / "atlas.q8").read_bytes()) != atlas_hash:
                raise AssertionError("Invalid fixture passed or the verifier changed the atlas")
            with patch.dict(sys.modules, {"pdfplumber": SimpleNamespace(open=lambda _: fake_pdf())}):
                if digest(generator.generate(Path("原始-İ.pdf")).encode()) != baseline["efm-source"]:
                    raise AssertionError("Language changed generated EFMPlus C# source")
            malformed = SimpleNamespace(page_number=67, extract_tables=lambda: [])
            try:
                generator.extract_page_rows(malformed)
                raise AssertionError("Malformed table accepted")
            except RuntimeError as error:
                if str(error) != loc.t("unexpected table layout on page {0}", 67):
                    raise AssertionError("Untranslated EFM extraction error")
            print(f"Python reference, mixture and table-generator data: {language} passed", flush=True)

        from matplotlib.figure import Figure
        from matplotlib.text import Text
        plot = load_module("plot_languages", ROOT / "tools/BluRayByteProbe/plot_results.py")
        savefig = Figure.savefig
        for language in loc.LANGUAGES:
            target = figure_output / language
            shutil.copytree(plot_source, target, dirs_exist_ok=True)
            original_inputs = {name: digest((target / name).read_bytes()) for name in ("all-bytes.json", "context.json", "fine-patterns.json")}
            labels = []

            def inspect_figure(figure, filename, *args, **kwargs):
                drawn = []
                text_draw = Text.draw

                def record_draw(text, renderer):
                    if text.get_visible() and text.get_text().strip():
                        drawn.append(text)
                    return text_draw(text, renderer)

                with patch.object(Text, "draw", record_draw):
                    figure.canvas.draw()
                renderer = figure.canvas.get_renderer()
                # Axis locators retain extra off-screen ticks, which are not drawn.
                for text in dict.fromkeys(drawn):
                    labels.append(text.get_text())
                    bounds = text.get_window_extent(renderer)
                    if bounds.x0 < -2 or bounds.y0 < -2 or bounds.x1 > figure.bbox.width + 2 or bounds.y1 > figure.bbox.height + 2:
                        raise AssertionError(f"Clipped {language} text in {Path(filename).name}: {text.get_text()!r}, {bounds.bounds}")
                return savefig(figure, filename, *args, **kwargs)

            with warnings.catch_warnings(), patch.object(Figure, "savefig", inspect_figure), patch.object(sys, "argv", ["plot_results.py", str(target), "--language", language]):
                warnings.filterwarnings("error", message=r"Glyph .* missing from font")
                # Exercise the CLI startup wrapper, including UTF-8 console
                # configuration on Windows, while retaining figure inspection.
                if plot.run(plot.main) != 0:
                    raise AssertionError(f"Plotting failed: {language}")
            if plot_hashes(target) != baseline["plot"]:
                raise AssertionError(f"Changed original plot numbers/schema: {language}")
            if any(digest((target / name).read_bytes()) != value for name, value in original_inputs.items()):
                raise AssertionError("Plotting modified its input JSON")
            if not all((target / name).stat().st_size > 1000 for name in ("all-bytes-density.png", "byte-candidates.png", "fine-pattern-comparison.png")):
                raise AssertionError("Missing translated figures")
            if loc.t("位级控制扩大密度差：19.41 → 27.42 个百分点") not in labels:
                raise AssertionError("Figure title bypassed localization")
            print(f"Python plot labels, fonts and layout: {language} passed", flush=True)
    print("Python tool integration passed: five languages, original numerical/schema baselines, unchanged inputs, translated errors and rendered synthetic fixtures.")


if __name__ == "__main__":
    output = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else ROOT / "artifacts/ci/python-localization-fixtures"
    check(output)
