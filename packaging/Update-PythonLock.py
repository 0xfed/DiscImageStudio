"""Refresh or verify the CI wheel lock without executing downloaded packages.

Run with Python 3.14 and pip. The resolver uses only official PyPI wheels,
then checks dependency markers for each target OS (pip's --platform alone
does not change the marker environment). --check verifies the committed lock
without changing it. Lock refreshes fail if OSV reports known vulnerabilities.
"""

import argparse
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import urllib.request

from pip._vendor.packaging.requirements import Requirement
from pip._vendor.packaging.utils import canonicalize_name

ROOT = Path(__file__).resolve().parents[1]
PYTHON_VERSION = (ROOT / ".python-version").read_text(encoding="utf-8").strip()
if not re.fullmatch(r"3\.14\.\d+", PYTHON_VERSION):
    raise ValueError(".python-version must pin a supported CPython 3.14 patch release")
INPUT = ROOT / "requirements-ci.in"
OUTPUT = ROOT / "requirements-ci.lock"
TARGETS = {
    "linux-x64": ("linux", "Linux", "x86_64", [
        "manylinux_2_28_x86_64", "manylinux_2_27_x86_64",
        "manylinux_2_17_x86_64", "manylinux2014_x86_64"]),
    "linux-arm64": ("linux", "Linux", "aarch64", [
        "manylinux_2_28_aarch64", "manylinux_2_27_aarch64",
        "manylinux_2_17_aarch64", "manylinux2014_aarch64"]),
    "osx-x64": ("darwin", "Darwin", "x86_64", ["macosx_15_0_x86_64"]),
    "osx-arm64": ("darwin", "Darwin", "arm64", ["macosx_15_0_arm64"]),
    "win-x64": ("win32", "Windows", "AMD64", ["win_amd64"]),
}


def resolve(name, source, directory, check):
    report = directory / (name + ".json")
    command = [sys.executable, "-m", "pip", "--isolated", "install",
               "--dry-run", "--ignore-installed", "--only-binary=:all:",
               "--index-url", "https://pypi.org/simple", "--python-version", PYTHON_VERSION,
               "--implementation", "cp", "--abi", "cp314", "--report", str(report),
               "--requirement", str(source)]
    for platform in TARGETS[name][3]:
        command += ["--platform", platform]
    if check:
        command += ["--require-hashes"]
    result = subprocess.run(command, capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(f"{name} wheel resolution failed:\n{result.stdout}\n{result.stderr}")
    data = json.loads(report.read_text(encoding="utf-8"))
    validate_dependencies(name, data)
    return name, data


def validate_dependencies(name, report):
    """Catch platform-conditional requirements hidden by cross-platform pip."""
    platform, system, machine, _ = TARGETS[name]
    environment = {**report["environment"], "sys_platform": platform,
                   "platform_system": system, "platform_machine": machine,
                   "os_name": "nt" if platform == "win32" else "posix",
                   "python_version": "3.14", "python_full_version": PYTHON_VERSION, "extra": ""}
    versions = {canonicalize_name(i["metadata"]["name"]): i["metadata"]["version"]
                for i in report["install"]}
    for item in report["install"]:
        if item.get("is_yanked") or not item["download_info"]["url"].endswith(".whl"):
            raise ValueError(f"{name}: only non-yanked wheels are allowed")
        for text in item["metadata"].get("requires_dist", []):
            requirement = Requirement(text)
            if requirement.marker and not requirement.marker.evaluate(environment):
                continue
            key = canonicalize_name(requirement.name)
            if key not in versions or versions[key] not in requirement.specifier:
                raise ValueError(f"{name}: unresolved target dependency {text}")


def get_json(url, payload=None):
    headers = {"User-Agent": "DiscImageStudio-CI-lock-review", "Accept": "application/json"}
    if payload is not None:
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(url, headers=headers,
        data=None if payload is None else json.dumps(payload).encode("utf-8"))
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.load(response)


def collect(reports):
    packages = {}
    for target, report in reports:
        for item in report["install"]:
            metadata = item["metadata"]
            name = canonicalize_name(metadata["name"])
            entry = packages.setdefault(name, {"version": metadata["version"], "wheels": {}})
            if entry["version"] != metadata["version"]:
                raise ValueError(f"{name}: target versions differ; constrain a common version in requirements-ci.in")
            info = item["download_info"]
            if not info["url"].startswith("https://files.pythonhosted.org/"):
                raise ValueError(f"{target}: wheel is not hosted by official PyPI")
            filename = info["url"].rsplit("/", 1)[-1]
            digest = info["archive_info"]["hashes"]["sha256"]
            if not re.fullmatch(r"[a-f0-9]{64}", digest):
                raise ValueError(f"{target}: invalid SHA-256 for {filename}")
            entry["wheels"][filename] = digest
    return packages


def verify_origins_and_advisories(packages):
    """Compare wheel hashes to release metadata and fail closed on OSV errors."""
    for name, entry in packages.items():
        release = get_json(f"https://pypi.org/pypi/{name}/{entry['version']}/json")
        official = {i["filename"]: i["digests"]["sha256"] for i in release["urls"]
                    if not i.get("yanked") and i["packagetype"] == "bdist_wheel"}
        for filename, digest in entry["wheels"].items():
            if official.get(filename) != digest:
                raise ValueError(f"PyPI release hash mismatch for {filename}")
    queries = [{"package": {"ecosystem": "PyPI", "name": name}, "version": entry["version"]}
               for name, entry in sorted(packages.items())]
    response = get_json("https://api.osv.dev/v1/querybatch", {"queries": queries})
    results = response.get("results", [])
    if len(results) != len(queries):
        raise ValueError("OSV returned incomplete results")
    findings = [(q["package"]["name"], v["id"]) for q, r in zip(queries, results)
                for v in r.get("vulns", [])]
    if findings:
        raise ValueError(f"Review known vulnerabilities before updating the CI lock: {findings}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify the existing lock without changing it")
    args = parser.parse_args()
    if sys.version_info[:2] != (3, 14):
        parser.error("Use Python 3.14 to match CI's interpreter and dependency markers")
    source = OUTPUT if args.check else INPUT
    if not args.check:
        for line in INPUT.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if line and not line.startswith("#") and not re.fullmatch(r"[A-Za-z0-9_.-]+==[A-Za-z0-9_.+!-]+", line):
                raise ValueError("requirements-ci.in must contain exact package==version pins only")
    with tempfile.TemporaryDirectory(prefix="disc-image-studio-python-lock-") as temporary:
        directory = Path(temporary)
        with ThreadPoolExecutor(max_workers=len(TARGETS)) as pool:
            reports = list(pool.map(lambda target: resolve(target, source, directory, args.check), TARGETS))
        packages = collect(reports)
        verify_origins_and_advisories(packages)
        if not args.check:
            lines = ["# Generated by python packaging/Update-PythonLock.py; review version/hash changes.",
                     f"# CPython {PYTHON_VERSION}: Linux x64/arm64, macOS 15 x64/arm64, Windows x64.",
                     "# Contains only wheels selected for these targets; no source distributions.",
                     "# Install using pip --isolated install --require-hashes --only-binary=:all:",
                     "#   --index-url https://pypi.org/simple -r requirements-ci.lock", ""]
            for name, entry in sorted(packages.items()):
                digests = sorted(set(entry["wheels"].values()))
                lines.append(f"{name}=={entry['version']} \\")
                for index, digest in enumerate(digests):
                    lines.append("    --hash=sha256:" + digest + (" \\" if index < len(digests) - 1 else ""))
                lines.append("")
            OUTPUT.write_text("\n".join(lines), encoding="utf-8")
    verb = "Verified" if args.check else "Wrote"
    print(f"{verb} requirements-ci.lock: {len(packages)} packages, five platforms; no OSV matches")


if __name__ == "__main__":
    main()
