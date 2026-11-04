"""Write a CycloneDX 1.6 SBOM from the actual published .NET dependency manifest.

This inventories published project/package/runtime-pack components and bundled
fonts. It does not claim to inventory the operating system or expand every
native library inside a .NET runtime pack.
"""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import re
import uuid
import xml.etree.ElementTree as ET
import zipfile


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def package_hash(value):
    value = value.removeprefix("sha512-")
    if not value:
        return []
    try:
        data = base64.b64decode(value, validate=True)
    except ValueError as exc:
        raise ValueError("Invalid package SHA-512 in dependency manifest") from exc
    if len(data) != 64:
        raise ValueError("Package SHA-512 must contain 64 bytes")
    return [{"alg": "SHA-512", "content": data.hex()}]


def write_sbom(artifact, deps_json, assets_json, rid, source_commit, output, repository, workspace):
    if re.fullmatch(r"[0-9a-f]{40}", source_commit) is None:
        raise ValueError("source-commit must be a full lowercase Git commit SHA")
    if re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repository) is None:
        raise ValueError("repository must be owner/name")
    if rid not in {"linux-x64", "linux-arm64", "osx-x64", "osx-arm64", "win-x64"}:
        raise ValueError("Unsupported release runtime identifier")
    artifact, workspace = Path(artifact), Path(workspace)
    deps = json.loads(Path(deps_json).read_text(encoding="utf-8-sig"))
    assets = json.loads(Path(assets_json).read_text(encoding="utf-8-sig"))
    target_name = deps["runtimeTarget"]["name"]
    if not target_name.endswith("/" + rid):
        raise ValueError("Published dependency manifest does not match the release RID")
    targets = deps["targets"][target_name]
    graph = assets["libraries"]
    # A Store packaging override can differ from Directory.Build.props. Use
    # the application project actually recorded by the published manifest.
    referenced = {n + "/" + v for target in targets.values()
                  for n, v in target.get("dependencies", {}).items()}
    applications = [key for key, value in deps["libraries"].items()
                    if value["type"] == "project" and key in targets and key not in referenced]
    if len(applications) != 1:
        raise ValueError("Cannot identify one published application project")
    version = applications[0].rsplit("/", 1)[1]
    package_properties = []
    if artifact.suffix.lower() == ".msix":
        # Read only the bounded manifest; never extract archive entries. Store
        # identity versions may override the code's locked project version.
        with zipfile.ZipFile(artifact) as package:
            manifests = [info for info in package.infolist() if info.filename == "AppxManifest.xml"]
            if len(manifests) != 1 or manifests[0].file_size > 1024 * 1024:
                raise ValueError("MSIX must contain one bounded AppxManifest.xml")
            with package.open(manifests[0]) as stream:
                content = stream.read(1024 * 1024 + 1)
            if len(content) > 1024 * 1024:
                raise ValueError("MSIX manifest exceeds its size budget")
        text = content.decode("utf-8-sig")
        if "<!DOCTYPE" in text or "<!ENTITY" in text:
            raise ValueError("MSIX manifest DTDs are not permitted")
        identities = ET.fromstring(text).findall("./{*}Identity")
        if len(identities) != 1 or re.fullmatch(r"\d+\.\d+\.\d+\.\d+", identities[0].get("Version", "")) is None:
            raise ValueError("MSIX manifest must contain one versioned package Identity")
        version = identities[0].get("Version")
        package_properties = [{"name": "discimagestudio:msix-identity-name", "value": identities[0].get("Name", "")},
                              {"name": "discimagestudio:msix-identity-version", "value": version}]
    sdk = json.loads((workspace / "global.json").read_text(encoding="utf-8"))["sdk"]["version"]
    components, references = [], {}
    for key, library in sorted(deps["libraries"].items()):
        if key not in targets:
            continue
        name, component_version = key.rsplit("/", 1)
        kind = library["type"]
        reference = "dotnet:" + key
        references[key] = reference
        component = {"type": "framework" if kind == "runtimepack" else "library",
                     "bom-ref": reference, "name": name, "version": component_version,
                     "properties": [{"name": "discimagestudio:dependency-type", "value": kind}]}
        if kind in {"package", "runtimepack"}:
            nuget_name = name.removeprefix("runtimepack.")
            component["purl"] = "pkg:nuget/" + nuget_name + "@" + component_version
            metadata = graph.get(nuget_name + "/" + component_version, {})
            hashes = package_hash(library.get("sha512", "") or metadata.get("sha512", ""))
            if hashes:
                component["hashes"] = hashes
        components.append(component)
    dependencies = []
    for key, target in sorted(targets.items()):
        if key not in references:
            continue
        dependencies.append({"ref": references[key], "dependsOn": sorted(
            references[n + "/" + v] for n, v in target.get("dependencies", {}).items()
            if n + "/" + v in references)})
    # These resources are embedded by DiscImageStudio.App.csproj.
    if rid == "win-x64":
        for path in sorted((workspace / "src/DiscImageStudio.App/Assets/Fonts").glob("*.ttf")):
            components.append({"type": "file", "bom-ref": "font:" + path.name,
                               "name": path.name, "hashes": [{"alg": "SHA-256", "content": digest(path)}],
                               "licenses": [{"license": {"name": "HarmonyOS Sans Fonts License Agreement"}}]})
    root_ref = "artifact:" + artifact.name
    dependencies.append({"ref": root_ref, "dependsOn": sorted(c["bom-ref"] for c in components)})
    result = {"$schema": "https://cyclonedx.org/schema/bom-1.6.schema.json", "bomFormat": "CycloneDX",
              "specVersion": "1.6", "serialNumber": "urn:uuid:" + str(uuid.uuid4()), "version": 1,
              "metadata": {"component": {"type": "application", "bom-ref": root_ref,
                  "name": artifact.name, "version": version,
                  "hashes": [{"alg": "SHA-256", "content": digest(artifact)}],
                  "externalReferences": [{"type": "vcs", "url": "https://github.com/" + repository + "/tree/" + source_commit}]},
                  "properties": [{"name": "discimagestudio:source-commit", "value": source_commit},
                                 {"name": "discimagestudio:runtime-identifier", "value": rid},
                                 {"name": "discimagestudio:dotnet-sdk", "value": sdk},
                                 {"name": "discimagestudio:inventory-scope", "value": "Published .deps.json components and embedded fonts; native runtime subcomponents and OS excluded"}] + package_properties},
              "components": components, "dependencies": dependencies}
    Path(output).write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for option in ["artifact", "deps-json", "assets-json", "rid", "source-commit", "output", "repository"]:
        parser.add_argument("--" + option, required=True)
    parser.add_argument("--workspace", default=str(Path(__file__).resolve().parents[1]))
    args = vars(parser.parse_args())
    write_sbom(**args)


if __name__ == "__main__":
    main()
