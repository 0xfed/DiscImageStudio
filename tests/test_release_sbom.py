import base64
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile


ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("release_sbom", ROOT / "packaging/Write-ReleaseSbom.py")
SBOM = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SBOM)


class ReleaseSbomTests(unittest.TestCase):
    def test_published_dependencies_and_source_are_bound_to_artifact(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "Directory.Build.props").write_text("<Project><PropertyGroup><VersionPrefix>1.2.3</VersionPrefix></PropertyGroup></Project>")
            (root / "global.json").write_text('{"sdk":{"version":"10.0.403"}}')
            artifact = root / "release.tar.gz"
            artifact.write_bytes(b"published artifact")
            deps = {"runtimeTarget": {"name": ".NETCoreApp,Version=v10.0/linux-x64"},
                    "targets": {".NETCoreApp,Version=v10.0/linux-x64": {"App/1.2.3": {"dependencies": {"Codec": "2.0.0"}}, "Codec/2.0.0": {}, "runtimepack.Microsoft.NETCore.App.Runtime.linux-x64/10.0.14": {}}},
                    "libraries": {"App/1.2.3": {"type": "project"}, "Codec/2.0.0": {"type": "package", "sha512": "sha512-" + base64.b64encode(bytes(64)).decode()}, "BuildOnly/1.0.0": {"type": "package"}, "runtimepack.Microsoft.NETCore.App.Runtime.linux-x64/10.0.14": {"type": "runtimepack"}}}
            (root / "app.deps.json").write_text(json.dumps(deps))
            (root / "project.assets.json").write_text('{"libraries":{}}')
            result = SBOM.write_sbom(artifact, root / "app.deps.json", root / "project.assets.json", "linux-x64", "a" * 40, root / "app.cdx.json", "owner/repo", root)
            self.assertEqual(result["metadata"]["component"]["hashes"][0]["content"], SBOM.digest(artifact))
            self.assertEqual({c["name"] for c in result["components"]}, {"App", "Codec", "runtimepack.Microsoft.NETCore.App.Runtime.linux-x64"})
            self.assertIn({"ref": "dotnet:App/1.2.3", "dependsOn": ["dotnet:Codec/2.0.0"]}, result["dependencies"])
            self.assertTrue(result["metadata"]["component"]["externalReferences"][0]["url"].endswith("a" * 40))
            with self.assertRaisesRegex(ValueError, "does not match"):
                SBOM.write_sbom(artifact, root / "app.deps.json", root / "project.assets.json", "osx-x64", "a" * 40, root / "bad.json", "owner/repo", root)

    def test_untrusted_source_identity_and_corrupt_hash_are_rejected(self):
        with self.assertRaisesRegex(ValueError, "full lowercase"):
            SBOM.write_sbom("a", "b", "c", "linux-x64", "main", "out", "owner/repo", ROOT)
        with self.assertRaisesRegex(ValueError, "owner/name"):
            SBOM.write_sbom("a", "b", "c", "linux-x64", "a" * 40, "out", "owner/repo/../../escape", ROOT)
        with self.assertRaises(ValueError):
            SBOM.package_hash("not a SHA-512")

    def test_msix_override_uses_actual_manifest_identity_without_relabeling_code(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "global.json").write_text('{"sdk":{"version":"10.0.403"}}')
            artifact = root / "release.msix"
            with zipfile.ZipFile(artifact, "w") as package:
                package.writestr("AppxManifest.xml", '<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"><Identity Name="Test.Identity" Version="2.3.4.0" /></Package>')
            deps = {"runtimeTarget": {"name": ".NETCoreApp,Version=v10.0/win-x64"},
                    "targets": {".NETCoreApp,Version=v10.0/win-x64": {"App/1.2.3": {}}},
                    "libraries": {"App/1.2.3": {"type": "project"}}}
            (root / "app.deps.json").write_text(json.dumps(deps))
            (root / "project.assets.json").write_text('{"libraries":{}}')
            result = SBOM.write_sbom(artifact, root / "app.deps.json", root / "project.assets.json", "win-x64", "a" * 40, root / "app.cdx.json", "owner/repo", root)
            self.assertEqual(result["metadata"]["component"]["version"], "2.3.4.0")
            self.assertEqual(result["components"][0]["version"], "1.2.3")
            properties = {p["name"]: p["value"] for p in result["metadata"]["properties"]}
            self.assertEqual(properties["discimagestudio:msix-identity-name"], "Test.Identity")
            with zipfile.ZipFile(artifact, "w") as package:
                package.writestr("AppxManifest.xml", b" " * (1024 * 1024 + 1))
            with self.assertRaisesRegex(ValueError, "bounded"):
                SBOM.write_sbom(artifact, root / "app.deps.json", root / "project.assets.json", "win-x64", "a" * 40, root / "bad.cdx.json", "owner/repo", root)


if __name__ == "__main__":
    unittest.main()
