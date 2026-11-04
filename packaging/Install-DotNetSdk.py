"""Install the reviewed SDK into a fresh directory, verifying SHA512 first.

Uses only Python's standard library. CI must set up Python before this script;
GITHUB_PATH/GITHUB_ENV select the verified SDK for subsequent steps. Updating
global.json also requires reviewing the official archive hashes in the manifest.
"""

import argparse
import hashlib
import hmac
import json
import os
from pathlib import Path, PurePosixPath
import platform
import re
import shutil
import stat
import tarfile
import tempfile
import urllib.request
import zipfile


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "eng/dependencies/dotnet-sdk.json"
RIDS = {"win-x64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"}
MAX_ARCHIVE_BYTES = 1024**3
MAX_EXTRACTED_BYTES = 4 * 1024**3
MAX_MEMBERS = 100_000


class NoRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, response, code, message, headers, new_url):
        raise ValueError("SDK download redirected away from the reviewed URL")


def open_download(request):
    return urllib.request.build_opener(NoRedirects()).open(request, timeout=120)


def host_rid():
    systems = {"Windows": "win", "Linux": "linux", "Darwin": "osx"}
    architectures = {"amd64": "x64", "x86_64": "x64", "aarch64": "arm64", "arm64": "arm64"}
    rid = systems.get(platform.system(), "unsupported") + "-" + architectures.get(platform.machine().lower(), "unsupported")
    if rid not in RIDS:
        raise ValueError(f"Unsupported SDK host: {platform.system()} / {platform.machine()}")
    return rid


def load_manifest(path=MANIFEST, global_json=ROOT / "global.json"):
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    sdk = json.loads(Path(global_json).read_text(encoding="utf-8"))["sdk"]
    version = data.get("sdk_version", "")
    if (data.get("schema_version") != 1 or not re.fullmatch(r"\d+\.\d+\.\d+", version)
            or sdk.get("version") != version or sdk.get("rollForward") != "disable"
            or sdk.get("allowPrerelease") is not False):
        raise ValueError("SDK manifest and exact global.json pin must agree")
    if set(data.get("archives", {})) != RIDS:
        raise ValueError("SDK manifest must cover all five supported hosts")
    for rid, entry in data["archives"].items():
        suffix = ".zip" if rid == "win-x64" else ".tar.gz"
        expected = f"https://builds.dotnet.microsoft.com/dotnet/Sdk/{version}/dotnet-sdk-{version}-{rid}{suffix}"
        if entry.get("url") != expected or not re.fullmatch(r"[0-9a-f]{128}", entry.get("sha512", "")):
            raise ValueError(f"Invalid official SDK URL or SHA512 for {rid}")
    return data


def download_verified(entry, destination):
    request = urllib.request.Request(entry["url"], headers={"User-Agent": "DiscImageStudio-reviewed-SDK-bootstrap"})
    digest, size = hashlib.sha512(), 0
    with open_download(request) as response:
        # No redirect to a different origin, plaintext transport, or latest alias.
        if response.geturl() != entry["url"]:
            raise ValueError("SDK download redirected away from the reviewed URL")
        length = response.headers.get("Content-Length")
        if length is not None and not 0 < int(length) <= MAX_ARCHIVE_BYTES:
            raise ValueError("SDK download exceeds the archive size limit")
        with Path(destination).open("xb") as output:
            while chunk := response.read(1024 * 1024):
                size += len(chunk)
                if size > MAX_ARCHIVE_BYTES:
                    raise ValueError("SDK download exceeds the archive size limit")
                digest.update(chunk)
                output.write(chunk)
    if size == 0 or not hmac.compare_digest(digest.hexdigest(), entry["sha512"]):
        raise ValueError("SDK archive SHA512 does not match the reviewed manifest")


class ArchiveBudget:
    def __init__(self, windows=False, case_insensitive=False):
        self.windows = windows
        self.case_insensitive = windows or case_insensitive
        self.members = 0
        self.bytes = 0
        self.seen = {}

    def validate(self, name, size, directory):
        self.members += 1
        self.bytes += size
        if size < 0 or self.members > MAX_MEMBERS or self.bytes > MAX_EXTRACTED_BYTES:
            raise ValueError("SDK archive exceeds the extraction budget")
        path = PurePosixPath(name)
        if (not name or "\\" in name or "\x00" in name or path.is_absolute()
                or ".." in path.parts or any(":" in part for part in path.parts)):
            raise ValueError(f"Unsafe SDK archive path: {name!r}")
        if not path.parts:
            if directory:
                return path
            raise ValueError("SDK archive file has an empty path")
        if self.windows:
            devices = {"con", "prn", "aux", "nul", *(f"com{i}" for i in range(1, 10)), *(f"lpt{i}" for i in range(1, 10))}
            if any(part.endswith((".", " ")) or part.split(".")[0].lower() in devices for part in path.parts):
                raise ValueError(f"Unsafe Windows SDK archive path: {name!r}")
        key = str(path).casefold() if self.case_insensitive else str(path)
        if key in self.seen and not (directory and self.seen[key]):
            raise ValueError(f"Duplicate SDK archive path: {name!r}")
        self.seen[key] = directory
        return path


def extract_archive(archive_path, directory, rid):
    budget = ArchiveBudget(windows=rid == "win-x64", case_insensitive=rid.startswith("osx-"))
    if rid == "win-x64":
        with zipfile.ZipFile(archive_path) as archive:
            for entry in archive.infolist():
                mode = entry.external_attr >> 16
                if (stat.S_IFMT(mode) not in (0, stat.S_IFREG, stat.S_IFDIR)
                        or entry.flag_bits & 1):
                    raise ValueError("SDK ZIP contains a link, special or encrypted entry")
                # filename can already have backslashes normalized or NULs truncated.
                relative = budget.validate(entry.orig_filename, entry.file_size, entry.is_dir())
                target = directory.joinpath(*relative.parts)
                if entry.is_dir():
                    target.mkdir(parents=True, exist_ok=True)
                else:
                    target.parent.mkdir(parents=True, exist_ok=True)
                    with archive.open(entry) as source, target.open("xb") as output:
                        shutil.copyfileobj(source, output, length=1024 * 1024)
    else:
        def safe_member(member, destination):
            if not (member.isfile() or member.isdir()):
                raise ValueError("SDK TAR contains a link or special entry")
            budget.validate(member.name, member.size, member.isdir())
            return tarfile.data_filter(member, destination)

        with tarfile.open(archive_path, "r|gz") as archive:
            archive.extractall(directory, filter=safe_member)


def install_sdk(manifest, rid, destination):
    """Never reuse an existing SDK directory, including empty or linked paths."""
    if rid not in RIDS:
        raise ValueError(f"Unsupported SDK RID: {rid}")
    destination = Path(os.path.abspath(destination))
    if any(character in str(destination) for character in "\r\n"):
        raise ValueError("SDK directory cannot contain a newline")
    if os.path.lexists(destination):
        raise ValueError("SDK destination must not already exist")
    destination.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".verified-sdk-", dir=destination.parent) as temporary:
        workspace = Path(temporary)
        archive = workspace / "sdk.archive"
        download_verified(manifest["archives"][rid], archive)
        staging = workspace / "sdk"
        staging.mkdir()
        extract_archive(archive, staging, rid)
        version = manifest["sdk_version"]
        host = staging / ("dotnet.exe" if rid == "win-x64" else "dotnet")
        sdk = staging / "sdk" / version
        if not host.is_file() or not (sdk / "dotnet.dll").is_file():
            raise ValueError("Verified archive lacks the expected SDK host/compiler")
        if os.name != "nt" and rid != "win-x64" and not host.stat().st_mode & stat.S_IXUSR:
            raise ValueError("SDK archive did not preserve executable host permissions")
        if os.path.lexists(destination):
            raise ValueError("SDK destination appeared during installation")
        staging.rename(destination)
    return destination


def export_github_environment(directory, rid):
    directory = str(directory)
    if any(character in directory for character in "\r\n"):
        raise ValueError("SDK directory cannot contain a newline")
    host = str(Path(directory) / ("dotnet.exe" if rid == "win-x64" else "dotnet"))
    variables = {"DOTNET_ROOT": directory,
                 "DOTNET_ROOT_" + rid.split("-")[1].upper(): directory,
                 "DOTNET_HOST_PATH": host,
                 "DOTNET_MULTILEVEL_LOOKUP": "0"}
    if path := os.environ.get("GITHUB_PATH"):
        with Path(path).open("a", encoding="utf-8", newline="\n") as output:
            output.write(directory + "\n")
    if path := os.environ.get("GITHUB_ENV"):
        with Path(path).open("a", encoding="utf-8", newline="\n") as output:
            output.writelines(f"{key}={value}\n" for key, value in variables.items())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--install-dir", type=Path, help="new SDK directory; defaults to a unique directory under RUNNER_TEMP")
    args = parser.parse_args()
    manifest, rid = load_manifest(), host_rid()
    destination = args.install_dir
    if destination is None:
        destination = Path(tempfile.mkdtemp(prefix="discimagestudio-dotnet-", dir=os.environ.get("RUNNER_TEMP"))) / "sdk"
    installed = install_sdk(manifest, rid, destination)
    export_github_environment(installed, rid)
    print(f"SHA512-verified SDK {manifest['sdk_version']} ({rid}) installed at {installed}", flush=True)


if __name__ == "__main__":
    main()
