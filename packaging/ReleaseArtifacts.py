"""Validate release attachments and publish approved ZIP entries without extracting them."""
import hashlib
import http.client
import json
import os
from pathlib import Path
import re
import stat
import tempfile
import urllib.error
import urllib.parse
import urllib.request
import zipfile


def expected_names(tag):
    if re.fullmatch(r"v(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-(?:alpha|beta|rc)\.[1-9][0-9]*)?", tag) is None:
        raise ValueError("Invalid release tag")
    binaries = [f"DiscImageStudio-{tag}-win-x64.exe"] + [f"DiscImageStudio-CLI-{tag}-{rid}.tar.gz" for rid in ("linux-x64", "linux-arm64", "osx-x64", "osx-arm64")]
    return binaries, set(binaries + [n + ".cdx.json" for n in binaries] + ["SHA256SUMS.txt", "release-notes.md"])


def check_manifest(manifest, tag, commit):
    _, expected = expected_names(tag)
    if re.fullmatch(r"[0-9a-f]{40}", commit) is None or manifest.get("source_commit") != commit:
        raise ValueError("Release source commit mismatch")
    files = manifest.get("files", [])
    if not isinstance(files, list) or {f.get("name") for f in files} != expected or len(files) != len(expected):
        raise ValueError("Unexpected or duplicate release attachments")
    for item in files:
        if type(item.get("size")) is not int or not 0 < item["size"] <= 1024 * 1024 * 1024:
            raise ValueError("Invalid attachment size")
        if re.fullmatch(r"[0-9a-f]{64}", item.get("sha256", "")) is None:
            raise ValueError("Invalid attachment digest")
        limit = 16 * 1024 * 1024 if item["name"].endswith(".cdx.json") else 1024 * 1024
        if item["name"] in {"release-notes.md", "SHA256SUMS.txt"} or item["name"].endswith(".cdx.json"):
            if item["size"] > limit:
                raise ValueError("Release metadata exceeds its size budget")
    if sum(f["size"] for f in files) > 2 * 1024 * 1024 * 1024:
        raise ValueError("Release exceeds the publication budget")
    return {f["name"]: f for f in files}


def inspect_directory(directory, tag, commit):
    directory = Path(directory)
    binaries, expected = expected_names(tag)
    paths = list(directory.iterdir())
    if {p.name for p in paths} != expected or any(p.is_symlink() or not p.is_file() for p in paths):
        raise ValueError("Unexpected release attachment or filesystem link")
    manifest = {"source_commit": commit, "files": []}
    for path in sorted(paths):
        with path.open("rb") as stream:
            sha256 = hashlib.file_digest(stream, "sha256").hexdigest()
        manifest["files"].append({"name": path.name, "size": path.stat().st_size, "sha256": sha256})
    entries = check_manifest(manifest, tag, commit)
    checksums = {}
    for line in (directory / "SHA256SUMS.txt").read_text(encoding="utf-8-sig").splitlines():
        match = re.fullmatch(r"([0-9a-f]{64})  (.+)", line)
        if match is None or match[2] in checksums:
            raise ValueError("Invalid or duplicate checksum entry")
        checksums[match[2]] = match[1]
    expected_checksums = expected - {"SHA256SUMS.txt", "release-notes.md"}
    if set(checksums) != expected_checksums or any(entries[n]["sha256"] != value for n, value in checksums.items()):
        raise ValueError("Release checksum mismatch")
    for name in binaries:
        sbom = json.loads((directory / (name + ".cdx.json")).read_text(encoding="utf-8"))
        props = {p["name"]: p["value"] for p in sbom["metadata"]["properties"]}
        component = sbom["metadata"]["component"]
        hashes = {h["alg"]: h["content"] for h in component["hashes"]}
        if props.get("discimagestudio:source-commit") != commit or component["name"] != name or hashes.get("SHA-256") != entries[name]["sha256"]:
            raise ValueError("SBOM does not describe the approved source and artifact")
    return manifest


def validate_archive(archive, manifest, tag, commit):
    entries = check_manifest(manifest, tag, commit)
    infos = archive.infolist()
    if {i.filename for i in infos} != set(entries) or len(infos) != len(entries):
        raise ValueError("Unexpected or duplicate archive entry")
    for info in infos:
        item = entries[info.filename]
        mode = info.external_attr >> 16
        if info.is_dir() or stat.S_ISLNK(mode) or info.file_size != item["size"]:
            raise ValueError("Archive entry type or size mismatch")
        with archive.open(info) as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        if digest != item["sha256"]:
            raise ValueError("Archive content differs from the inspected release")
    return entries


def publish():
    token, repo, tag, commit = (os.environ[n] for n in ("GH_TOKEN", "GH_REPO", "RELEASE_TAG", "RELEASE_COMMIT"))
    if re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repo) is None:
        raise ValueError("Invalid repository")
    manifest = json.loads(os.environ["RELEASE_MANIFEST"])
    entries = check_manifest(manifest, tag, commit)
    artifact_id, run_id = os.environ["RELEASE_ARTIFACT_ID"], os.environ["GITHUB_RUN_ID"]
    if not artifact_id.isdecimal() or not run_id.isdecimal():
        raise ValueError("Invalid artifact or workflow run ID")
    base = "https://api.github.com/repos/" + repo
    headers = {"Authorization": "Bearer " + token, "Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28", "User-Agent": "DiscImageStudio-release"}

    def api(path, data=None, method=None):
        request = urllib.request.Request(base + path, data=None if data is None else json.dumps(data).encode(), headers={**headers, "Content-Type": "application/json"}, method=method)
        with urllib.request.urlopen(request, timeout=60) as response:
            body = response.read()
            return json.loads(body) if body else None

    artifact = api("/actions/artifacts/" + artifact_id)
    if artifact.get("expired") or str(artifact.get("workflow_run", {}).get("id")) != run_id or artifact["name"] != "release-" + tag:
        raise ValueError("Artifact is not the approved attachment from this workflow run")
    # Do not forward the GitHub credential to the blob-storage redirect.
    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, request, fp, code, msg, hdrs, newurl):
            return None
    try:
        urllib.request.build_opener(NoRedirect).open(urllib.request.Request(base + "/actions/artifacts/" + artifact_id + "/zip", headers=headers), timeout=60)
        raise ValueError("Expected a signed artifact-storage redirect")
    except urllib.error.HTTPError as error:
        if error.code != 302:
            error.close()
            raise
        location = error.headers["Location"]
        error.close()
    if urllib.parse.urlsplit(location).scheme != "https":
        raise ValueError("Artifact download must use HTTPS")
    with tempfile.TemporaryFile() as downloaded:
        with urllib.request.urlopen(location, timeout=60) as response:
            total = 0
            while block := response.read(1024 * 1024):
                total += len(block)
                if total > 2 * 1024 * 1024 * 1024:
                    raise ValueError("Artifact archive exceeds its size budget")
                downloaded.write(block)
        downloaded.seek(0)
        with zipfile.ZipFile(downloaded) as archive:
            validate_archive(archive, manifest, tag, commit)
            notes = archive.read("release-notes.md").decode("utf-8-sig")
            if len(notes) > 1024 * 1024:
                raise ValueError("Release notes exceed their size budget")
            existing = []
            page = 1
            while True:
                batch = api(f"/releases?per_page=100&page={page}")
                existing += [r for r in batch if r["tag_name"] == tag]
                if len(batch) < 100:
                    break
                page += 1
            if len(existing) > 1 or (existing and not existing[0]["draft"]):
                raise ValueError("Refusing to replace an already published or ambiguous release")
            try:
                reference = api("/git/ref/tags/" + urllib.parse.quote(tag, safe=""))["object"]
            except urllib.error.HTTPError as error:
                if error.code != 404:
                    error.close()
                    raise
                error.close()
                reference = api("/git/refs", {"ref": "refs/tags/" + tag, "sha": commit})["object"]
            for _ in range(20):
                if reference["type"] != "tag":
                    break
                reference = api("/git/tags/" + reference["sha"])["object"]
            if reference["type"] != "commit" or reference["sha"] != commit:
                raise ValueError("Release tag points to a different commit")
            options = {"tag_name": tag, "target_commitish": commit, "name": "Disc Image Studio " + tag, "body": notes, "draft": True, "prerelease": os.environ["IS_PRERELEASE"] == "true"}
            release = api("/releases/" + str(existing[0]["id"]), options, "PATCH") if existing else api("/releases", options)
            # No artifact filename is used as a filesystem destination. Only
            # entries approved by the read-only inspection job are uploaded.
            upload = urllib.parse.urlsplit(release["upload_url"].split("{")[0])
            if upload.scheme != "https" or upload.hostname != "uploads.github.com":
                raise ValueError("Unexpected GitHub release upload endpoint")
            for name, item in sorted(entries.items()):
                if name == "release-notes.md":
                    continue
                for asset in release.get("assets", []):
                    if asset["name"] == name:
                        api("/releases/assets/" + str(asset["id"]), method="DELETE")
                connection = http.client.HTTPSConnection(upload.hostname, timeout=120)
                try:
                    connection.putrequest("POST", upload.path + "?name=" + urllib.parse.quote(name, safe=""))
                    for key, value in {**headers, "Content-Type": "application/octet-stream", "Content-Length": str(item["size"])}.items():
                        connection.putheader(key, value)
                    connection.endheaders()
                    with archive.open(name) as stream:
                        while block := stream.read(1024 * 1024):
                            connection.send(block)
                    response = connection.getresponse()
                    if response.status != 201:
                        raise RuntimeError("GitHub release attachment upload failed with status " + str(response.status))
                    response.read()
                finally:
                    connection.close()
    print("Draft ready: https://github.com/" + repo + "/releases")


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["inspect", "publish"])
    parser.add_argument("--directory")
    parser.add_argument("--tag")
    parser.add_argument("--commit")
    parser.add_argument("--output")
    arguments = parser.parse_args()
    if arguments.command == "publish":
        publish()
    else:
        Path(arguments.output).write_text(json.dumps(inspect_directory(arguments.directory, arguments.tag, arguments.commit), separators=(",", ":")) + "\n", encoding="utf-8")
