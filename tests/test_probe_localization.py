"""Exercise the actual Blu-ray probe against independent Python 17PP vectors."""

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile


ROOT = Path(__file__).resolve().parents[1]
LANGUAGES = ("zh-CN", "zh-TW", "en", "ja", "tr")


def check(executable: Path) -> None:
    spec = importlib.util.spec_from_file_location(
        "independent_17pp", ROOT / "tools/BluRayByteProbe/reference/analyze.py")
    reference = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(reference)
    catalogs = {key: value for path in (ROOT / "src/DiscImageStudio.Localization/Catalogs").glob("*.json")
                for key, value in json.loads(path.read_text(encoding="utf-8")).items()}
    with tempfile.TemporaryDirectory(prefix="disc-probe-languages-") as temporary:
        work = Path(temporary)
        vectors = {f"{value:02X}": reference.finite(f"{value:08b}" * 4) for value in range(256)}
        data = json.dumps(vectors, ensure_ascii=False).encode("utf-8")
        (work / "reference-日本語-İ.json").write_bytes(data)
        (work / "--language").write_bytes(data)
        patterns = {f"pattern-İ-日本語-{index}-{previous}": {
            "data_bits": bits,
            "previous_modulation_bit": previous,
            "modulation_bits": reference.finite(bits, previous),
        } for index, bits in enumerate(("00", "01", "0001" * 8, "00111101" * 7, "010110001101" * 5))
            for previous in (0, 1)}
        (work / "patterns.json").write_text(json.dumps(patterns, ensure_ascii=False), encoding="utf-8")
        (work / "mismatch.json").write_text('{"AA": "invalid"}', encoding="utf-8")
        bad_name = "case-İ-日本語"
        (work / "bad-pattern.json").write_text(json.dumps({bad_name: {
            "data_bits": "00", "previous_modulation_bit": 0, "modulation_bits": "invalid",
        }}, ensure_ascii=False), encoding="utf-8")

        def run(*arguments: str, expected: int = 0, environment=None):
            command = ["dotnet", str(executable)] if executable.suffix == ".dll" else [str(executable)]
            result = subprocess.run(command + list(arguments), cwd=work, capture_output=True,
                                    encoding="utf-8", env=environment, timeout=30)
            if result.returncode != expected:
                raise ValueError(f"{arguments}: expected {expected}, got {result.returncode}: {result.stderr}")
            return result.stdout, result.stderr

        for language in LANGUAGES:
            output, _ = run("--language", language, "--help")
            if catalogs["ByteProbe.Help"][language] not in output or catalogs["Cli.LanguageHelp"][language] not in output:
                raise ValueError(f"Untranslated probe help: {language}")
            output, _ = run("--verify-reference", "reference-日本語-İ.json", "--language", language)
            if output.strip() != catalogs["Independent Figure 41 reference: {0} finite vectors matched bit for bit."][language].format(256):
                raise ValueError(f"Reference result not translated: {language}")
            output, _ = run("--language", language, "--verify-patterns", "patterns.json")
            if output.strip() != catalogs["Independent periodic-pattern reference: {0} finite vectors matched bit for bit."][language].format(len(patterns)):
                raise ValueError(f"Pattern result not translated: {language}")
            _, error = run("--language", language, "--verify-reference", expected=1)
            if error.strip() != catalogs["Missing reference file for {0}."][language].format("--verify-reference"):
                raise ValueError(f"Missing reference diagnostic not translated: {language}")
            _, error = run("--language", language, "--verify-reference", "mismatch.json", expected=1)
            if error.strip() != catalogs["Independent reference mismatch: {0}."][language].format("AA"):
                raise ValueError(f"Reference mismatch diagnostic not translated: {language}")
            _, error = run("--language", language, "--verify-patterns", "bad-pattern.json", expected=1)
            if error.strip() != catalogs["Independent pattern reference mismatch: {0}."][language].format(bad_name):
                raise ValueError(f"Pattern mismatch lost language or user case name: {language}")
        run("--verify-reference", "--language", "--language", "tr")
        if (work / "reference-日本語-İ.json").read_bytes() != data:
            raise ValueError("Probe changed its reference input")
        environment = {**os.environ, "DISC_IMAGE_STUDIO_LANGUAGE": "ja"}
        output, _ = run("--help", environment=environment)
        if catalogs["ByteProbe.Help"]["ja"] not in output:
            raise ValueError("Probe environment preference was ignored")
        output, _ = run("--help", "--language=tr", environment=environment)
        if catalogs["ByteProbe.Help"]["tr"] not in output:
            raise ValueError("Probe explicit language did not override the environment")
    print("probe localization: passed (five languages, 256 independent byte vectors, finite patterns, localized errors and Unicode names)")


if __name__ == "__main__":
    check(Path(sys.argv[1]).resolve(strict=True))
