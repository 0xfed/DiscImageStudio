"""Windows integration checks for the actual dithering CLI and off-screen window."""

import json
from pathlib import Path
import struct
import subprocess
import sys
import tempfile


ROOT = Path(__file__).resolve().parents[1]
LANGUAGES = ("zh-CN", "zh-TW", "en", "ja", "tr")


def check(executable: Path, snapshots: Path) -> None:
    catalogs = {key: value for path in (ROOT / "src/DiscImageStudio.Localization/Catalogs").glob("*.json")
                for key, value in json.loads(path.read_text(encoding="utf-8")).items()}
    snapshots.mkdir(parents=True, exist_ok=True)
    original = (ROOT / "src/DiscImageStudio.App/Assets/DiscImageStudio-256.png").read_bytes()
    with tempfile.TemporaryDirectory(prefix="disc-dither-languages-") as temporary:
        work = Path(temporary)
        source = work / "原图-İ-日本語.png"
        source.write_bytes(original)
        (work / "--language").write_bytes(original)
        expected_bytes = {}

        def run(*arguments: str, expected: int = 0):
            command = ["dotnet", str(executable)] if executable.suffix == ".dll" else [str(executable)]
            result = subprocess.run(command + list(arguments), cwd=work, capture_output=True,
                                    encoding="utf-8", timeout=60)
            if result.returncode != expected:
                raise ValueError(f"{arguments}: expected {expected}, got {result.returncode}: {result.stderr}")
            return result.stdout, result.stderr

        for language in LANGUAGES:
            output, _ = run("--language", language, "--help")
            if catalogs["Dither.Help"][language] not in output or catalogs["Cli.LanguageHelp"][language] not in output:
                raise ValueError(f"Dithering help not translated: {language}")
            for gamma, mode in (("none", "cd"), ("srgb", "dvd")):
                name = f"结果-İ-日本語-{language}-{gamma}.png"
                output, _ = run("--input", source.name, "--output", name, "--mode", mode,
                                "--gamma", gamma, "--language", language)
                if name not in output or catalogs["CD 二阶" if mode == "cd" else "DVD 二阶"][language] not in output:
                    raise ValueError(f"Result lost translated mode or Unicode filename: {language}")
                data = (work / name).read_bytes()
                if data[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">IIBB", data[16:26]) != (256, 256, 8, 0):
                    raise ValueError("Dithering output must retain dimensions and be Gray8 PNG")
                if gamma in expected_bytes and data != expected_bytes[gamma]:
                    raise ValueError(f"Selecting {language} changed {gamma} PNG bytes")
                expected_bytes.setdefault(gamma, data)
            _, error = run("--language", language, "--output", "missing-input.png", expected=1)
            if error.strip() != catalogs["Missing {0}."][language].format("--input"):
                raise ValueError(f"Missing-input diagnostic not translated: {language}")
            _, error = run("--language", language, "--input", source.name, "--output", source.name, expected=1)
            if error.strip() != catalogs["请另存为新文件，不能覆盖原图。"][language]:
                raise ValueError(f"Source-overwrite diagnostic not translated: {language}")
            snapshot = snapshots / f"dither-{language}.png"
            run("--language", language, "--input", source.name, "--snapshot", str(snapshot))
            if not snapshot.is_file() or snapshot.stat().st_size == 0:
                raise ValueError(f"Window snapshot missing: {language}")
        run("--input", "--language", "--output", "flag-value.png", "--language", "tr")
        if (work / "flag-value.png").read_bytes() != expected_bytes["none"]:
            raise ValueError("An input filename resembling a language flag changed the image")
        if source.read_bytes() != original:
            raise ValueError("Dithering tool changed the source image")
    print("dithering integration: passed (five-language CLI, identical PNG bytes, Unicode names, errors and five window snapshots)")


if __name__ == "__main__":
    check(Path(sys.argv[1]).resolve(strict=True),
          (ROOT / "artifacts/ci/dither-languages").resolve())
