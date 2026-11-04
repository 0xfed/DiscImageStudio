"""Smoke-test a published CLI from a working directory outside the checkout."""

import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path


def check_localization(executable: Path) -> None:
    catalog_path = Path(__file__).resolve().parents[1] / "src/DiscImageStudio.Localization/Catalogs/Cli.json"
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    descriptions = [
        "Without files, writes WAV/CUE or RAW audio; with --data-dir, writes a Mode 1 ISO with inner/outer-ring files.",
        "Projects generated geometry onto measured disc geometry.",
        "Renders WAV/RAW audio or a Mode 1 ISO with measured geometry.",
        "Generates a DVD image with the fixed fast dispersion mapping in CW direction.",
        "Encodes one DVD ECC block into packed NRZI channel levels.",
        "Renders generated geometry through measured disc radii.",
        "Renders a generated ISO's payload sectors under measured disc radii.",
        "Runs the DVD engine's deterministic regression suite.",
        "Describe nominal BD-R geometry; optionally export JSON.",
        "Export raw user sectors for an explicit scrambled-byte probe (no filesystem).",
        "Encode exactly 65536 payload bytes into 76880 ECC bytes; not a burn image.",
        "Draw the full user-data area; selecting a files directory adds a UDF 2.50 filesystem at the inner or outer ring.",
    ]

    def run(*arguments: str, environment: dict | None = None) -> subprocess.CompletedProcess:
        return subprocess.run([executable, *arguments], capture_output=True, encoding="utf-8", env=environment)

    for language in ("zh-CN", "zh-TW", "en", "ja", "tr"):
        result = run("--language", language, "--help")
        if result.returncode != 0:
            raise ValueError(f"{language}: help failed: {result.stderr}")
        for key in ("Cli.Title", "Cli.Usage", "Cli.LanguageHelp", *descriptions):
            if catalog[key][language] not in result.stdout:
                raise ValueError(f"{language}: missing translated help text for {key}")
        for command in ("cd-generate", "cd-preview-warp", "cd-preview-track", "solve", "encode",
                        "calibrate", "simulate", "selftest", "bd-info", "bd-probe", "bd-encode", "bd-generate"):
            if sum(line.lstrip().startswith(command + " ") for line in result.stdout.splitlines()) != 1:
                raise ValueError(f"{language}: command identifier changed or duplicated: {command}")

        # Repetition must report the error in the language selected by the first flag.
        duplicate = run("--language", language, "--help", "--language=en")
        if duplicate.returncode != 1 or catalog["Cli.DuplicateLanguage"][language] not in duplicate.stderr:
            raise ValueError(f"{language}: duplicate language diagnostic is missing: {duplicate.stderr}")

        environment = dict(os.environ, DISC_IMAGE_STUDIO_LANGUAGE=language)
        default = run("--help", environment=environment)
        if default.returncode != 0 or catalog["Cli.Usage"][language] not in default.stdout:
            raise ValueError(f"{language}: environment preference was ignored")
        missing = run("--language", environment=environment)
        if missing.returncode != 1 or catalog["Cli.MissingLanguage"][language] not in missing.stderr:
            raise ValueError(f"{language}: missing language diagnostic is missing: {missing.stderr}")
        override = run("--help", "--language=en", environment=environment)
        if override.returncode != 0 or catalog["Cli.Usage"]["en"] not in override.stdout:
            raise ValueError("Explicit language did not override the environment")
        unknown = run("--language", language, "unknown-test-command")
        expected = catalog["Unknown optical-disc command '{0}'."][language].format("unknown-test-command")
        if unknown.returncode != 1 or expected not in unknown.stderr:
            raise ValueError(f"{language}: command routing diagnostic was not translated: {unknown.stderr}")

        module_catalog = json.loads((catalog_path.parent / "ModuleMessages.json").read_text(encoding="utf-8"))
        missing_option = run("--language", language, "cd-generate")
        expected = module_catalog["Missing required option --{0}."][language].format("output")
        if missing_option.returncode != 1 or expected not in missing_option.stderr:
            raise ValueError(f"{language}: CD option diagnostic was not translated: {missing_option.stderr}")

    invalid = run("--language", "es", "--help", environment=dict(os.environ, DISC_IMAGE_STUDIO_LANGUAGE="en"))
    if invalid.returncode != 1 or "Unsupported language 'es'" not in invalid.stderr:
        raise ValueError("An incomplete, unsupported language was accepted")


def check_package(executable: Path) -> None:
    check_localization(executable)
    fixture = Path(__file__).resolve().parents[1] / "src/DiscImageStudio.App/Assets/DiscImageStudio-256.png"
    with tempfile.TemporaryDirectory() as directory:
        work = Path(directory)
        shutil.copyfile(fixture, work / "input image.png")

        def run(*args: str) -> None:
            subprocess.run([executable, *args], cwd=work, check=True)

        def check_size(name: str, expected: int) -> None:
            actual = (work / name).stat().st_size
            if actual != expected:
                raise ValueError(f"{name}: expected {expected} bytes, found {actual}")

        run("--help")
        run("selftest")
        run("cd-generate", "--input", "input image.png", "--output", "track.wav", "--sectors", "2")
        check_size("track.wav", 44 + 2 * 2352)
        if 'FILE "track.wav" WAVE' not in (work / "track.cue").read_text(encoding="utf-8-sig"):
            raise ValueError("CUE does not reference the generated WAV")
        check_translated_generation(executable, work)

        run("solve", "--image", "input image.png", "--iso-output", "dvd.iso",
            "--total-sectors", "160", "--fill-sectors", "160")
        check_size("dvd.iso", 160 * 2048)

        run("bd-generate", "--input", "input image.png", "--output", "bd.iso", "--total-sectors", "1024")
        check_size("bd.iso", 1024 * 2048)


def check_translated_generation(executable: Path, work: Path) -> None:
    catalog = json.loads((Path(__file__).resolve().parents[1]
                          / "src/DiscImageStudio.Localization/Catalogs/ModuleMessages.json").read_text(encoding="utf-8"))
    outputs = []
    for language in ("zh-CN", "zh-TW", "en", "ja", "tr"):
        name = f"光盘-{language}.wav"
        result = subprocess.run(
            [executable, "--language", language, "cd-generate", "--input", "input image.png",
             "--output", name, "--sectors", "2", "--r0", "24.5", "--r1", "56.8"],
            cwd=work, capture_output=True, encoding="utf-8", check=True)
        prefix = catalog["CD track complete: {0} sectors, {1} bytes, interleave={2}, elapsed={3:F1}s, output={4}"][language].split("{0}")[0]
        if prefix not in result.stdout or name not in result.stdout:
            raise ValueError(f"{language}: generated track result did not preserve translated text and filename")
        outputs.append((work / name).read_bytes())
        if f'FILE "{name}" WAVE' not in (work / Path(name).with_suffix(".cue")).read_text(encoding="utf-8-sig"):
            raise ValueError(f"{language}: CUE filename was changed by translation")
    if any(output != outputs[0] for output in outputs[1:]) or len(outputs[0]) != 44 + 2 * 2352:
        raise ValueError("Selecting a display language changed the generated audio data or numeric parameter parsing")
    for index, input_name in enumerate(("--language", "--language=ja")):
        shutil.copyfile(work / "input image.png", work / input_name)
        output_name = f"flag-name-{index}.wav"
        subprocess.run([executable, "--language", "en", "cd-generate", "--input", input_name,
                        "--output", output_name, "--sectors", "2"],
                       cwd=work, capture_output=True, encoding="utf-8", check=True)
        if (work / output_name).read_bytes() != outputs[0]:
            raise ValueError("An option value resembling a language flag was reinterpreted")


if __name__ == "__main__":
    check_package(Path(sys.argv[1]).resolve(strict=True))
