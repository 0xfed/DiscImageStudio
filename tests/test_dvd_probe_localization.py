"""Windows integration checks for DVD probe translations and invariant experiment data."""

import copy
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import zlib


ROOT = Path(__file__).resolve().parents[1]
LANGUAGES = ("zh-CN", "zh-TW", "en", "ja", "tr")


def write_fixture(path: Path) -> None:
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))

    row = b"\0" + b"".join(bytes([level]) * 1500 for level in (0, 85, 170, 255))
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 6000, 6000, 8, 0, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(row * 6000)) + chunk(b"IEND", b""))


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def check(executable: Path) -> None:
    catalogs = {key: value for path in (ROOT / "src/DiscImageStudio.Localization/Catalogs").glob("*.json")
                for key, value in json.loads(path.read_text(encoding="utf-8")).items()}
    with tempfile.TemporaryDirectory(prefix="disc-dvd-probe-languages-") as temporary:
        # Windows temp paths can contain an 8.3 alias that .NET expands. Compare
        # the canonical path in both console output and generated metadata.
        work = Path(temporary).resolve(strict=True)
        source = work / "原图-İ-日本語.png"
        write_fixture(source)
        original = digest(source)
        (work / "--language").write_bytes(source.read_bytes())
        expected_hashes = None
        expected_metadata = None

        def run(*arguments: str, expected: int = 0, environment: str = "en"):
            command = ["dotnet", str(executable)] if executable.suffix == ".dll" else [str(executable)]
            result = subprocess.run(command + list(arguments), cwd=work, capture_output=True,
                                    encoding="utf-8", timeout=180,
                                    env={**os.environ, "DISC_IMAGE_STUDIO_LANGUAGE": environment})
            if result.returncode != expected:
                raise ValueError(f"{arguments}: expected {expected}, got {result.returncode}: {result.stderr}")
            return result.stdout, result.stderr

        def error(language, key, *arguments, values=()):
            _, diagnostic = run("--language", language, *arguments, expected=1)
            if catalogs[key][language].format(*values) not in diagnostic:
                raise ValueError(f"Untranslated diagnostic in {language}: {key}: {diagnostic}")

        text_fields = ("sourceKind", "coordinateSystem", "sampling", "previewMeaning", "mixtureMeaning")
        for language in LANGUAGES:
            output, _ = run("--language", language, "--help")
            if catalogs["Verbatim 43533 calibrated geometry; complete raw DVD sector image; no burn command."][language] not in output:
                raise ValueError(f"Untranslated DVD probe help: {language}")
            if catalogs["Cli.LanguageHelp"][language] not in output or "DvdGrayProbe verify --prepared DIR --iso TEST.iso" not in output:
                raise ValueError("Help lost the shared language option or invariant command syntax")
            error(language, "Missing {0}.", "prepare", values=("--source",))
            error(language, "Expected unique --name value options.", "prepare", "--source")
            error(language, "Unknown command.", "unknown")
            error(language, "Parallelism must be 1..64.", "generate", "--prepared", "missing",
                  "--output", "unused.iso", "--parallelism", "0")
            prepared = work / f"实验-İ-{language}"
            source_name = "--language" if language == "tr" else source.name
            output, _ = run("prepare", "--source", source_name, "--output-dir", str(prepared), "--language", language)
            expected_output = catalogs["Prepared: {0}"][language].format(str(prepared))
            if expected_output not in output:
                raise ValueError(f"Preparation output mismatch in {language}: expected {expected_output!r}, got {output!r}")
            hashes = {name: digest(prepared / name) for name in ("atlas.q8", "code-map.png", "nominal-preview.png")}
            if expected_hashes is not None and hashes != expected_hashes:
                raise ValueError(f"Selecting {language} changed the direct-Q atlas or preview PNG bytes")
            expected_hashes = hashes
            metadata = json.loads((prepared / "probe.json").read_text(encoding="utf-8"))
            if metadata["source"] != str(work / source_name) or metadata["sourceSha256"] != original:
                raise ValueError("Preparation changed the source path or source hash")
            normalized = copy.deepcopy(metadata)
            normalized.pop("createdUtc")
            normalized["source"] = str(source)
            for name in text_fields:
                key = next(key for key, values in catalogs.items() if values.get(language) == metadata[name])
                normalized[name] = catalogs[key]["en"]
            normalized["geometry"]["preset"] = "Verbatim DVD-R AZO 43533 calibrated"
            if metadata["geometry"]["preset"] != catalogs[normalized["geometry"]["preset"]][language]:
                raise ValueError(f"Untranslated preset explanation: {language}")
            normalized["layout"] = [next(key for key, values in catalogs.items() if values.get(language) == text)
                                    for text in metadata["layout"]]
            if expected_metadata is not None and normalized != expected_metadata:
                raise ValueError(f"Selecting {language} changed JSON schema or technical values")
            expected_metadata = normalized
            error(language, "Preparation directory must be empty to avoid replacing an existing experiment.",
                  "prepare", "--source", source.name, "--output-dir", str(prepared))
            if digest(prepared / "atlas.q8") != hashes["atlas.q8"]:
                raise ValueError("Refused preparation changed the existing atlas")
        output, _ = run("--help", environment="ja")
        if catalogs["Cli.LanguageHelp"]["ja"] not in output:
            raise ValueError("Environment language selection failed")
        if digest(source) != original or digest(work / "--language") != original or (work / "unused.iso").exists():
            raise ValueError("Probe changed its source or generated an image for invalid options")
    print("DVD probe integration: passed (five-language help/errors/JSON, identical direct-Q atlas and preview PNG bytes, Unicode paths and source protection)")


if __name__ == "__main__":
    check(Path(sys.argv[1]).resolve(strict=True))
