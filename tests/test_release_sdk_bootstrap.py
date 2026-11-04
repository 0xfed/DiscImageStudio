import copy
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import stat
import tarfile
import tempfile
import unittest
from unittest.mock import patch
import zipfile


ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("sdk_bootstrap", ROOT / "packaging/Install-DotNetSdk.py")
SDK = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SDK)
VERSION = "10.0.403"


def archive_bytes(rid, extra=(), host_mode=0o755):
    files = [("dotnet.exe" if rid == "win-x64" else "dotnet", b"reviewed host"),
             (f"sdk/{VERSION}/dotnet.dll", b"reviewed compiler")]
    data = io.BytesIO()
    if rid == "win-x64":
        with zipfile.ZipFile(data, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            for name, body in files + list(extra):
                entry = name if isinstance(name, zipfile.ZipInfo) else zipfile.ZipInfo(name)
                if isinstance(name, str):
                    # Preserve deliberately invalid bytes even on Windows writers.
                    entry.filename = name
                    entry.compress_type = zipfile.ZIP_DEFLATED
                archive.writestr(entry, body)
    else:
        with tarfile.open(fileobj=data, mode="w:gz") as archive:
            for name, body in files + list(extra):
                entry = name if isinstance(name, tarfile.TarInfo) else tarfile.TarInfo(name)
                entry.mode = host_mode
                entry.size = len(body) if entry.isfile() else 0
                archive.addfile(entry, io.BytesIO(body) if entry.isfile() else None)
    return data.getvalue()


def response(body, url, length=None):
    stream = io.BytesIO(body)
    stream.geturl = lambda: url
    stream.headers = {} if length is None else {"Content-Length": str(length)}
    return stream


class SdkBootstrapTests(unittest.TestCase):
    def install(self, root, rid="linux-x64", body=None, digest=None):
        body = archive_bytes(rid) if body is None else body
        entry = {"url": "https://builds.dotnet.microsoft.com/reviewed-sdk",
                 "sha512": hashlib.sha512(body).hexdigest() if digest is None else digest}
        manifest = {"sdk_version": VERSION, "archives": {rid: entry}}
        with patch.object(SDK, "open_download", return_value=response(body, entry["url"])):
            return SDK.install_sdk(manifest, rid, root / "sdk")

    def test_committed_manifest_covers_every_supported_host_and_exact_sdk(self):
        manifest = SDK.load_manifest()
        self.assertEqual(set(manifest["archives"]), SDK.RIDS)
        self.assertEqual(manifest["sdk_version"], VERSION)

    def test_manifest_rejects_version_drift_missing_hosts_and_replaced_origins(self):
        original = SDK.load_manifest()
        for change in (lambda d: d.update(sdk_version="10.0.404"),
                       lambda d: d["archives"].pop("osx-arm64"),
                       lambda d: d["archives"]["win-x64"].update(url="http://untrusted.test/sdk.zip"),
                       lambda d: d["archives"]["win-x64"].update(sha512="invalid")):
            data = copy.deepcopy(original)
            change(data)
            with tempfile.TemporaryDirectory() as temporary:
                manifest = Path(temporary) / "manifest.json"
                manifest.write_text(json.dumps(data), encoding="utf-8")
                with self.assertRaises(ValueError):
                    SDK.load_manifest(manifest)

    def test_host_detection_covers_all_five_platforms_and_rejects_other_architectures(self):
        cases = [("Windows", "AMD64", "win-x64"), ("Linux", "x86_64", "linux-x64"),
                 ("Linux", "aarch64", "linux-arm64"), ("Darwin", "x86_64", "osx-x64"),
                 ("Darwin", "arm64", "osx-arm64")]
        for system, architecture, expected in cases:
            with self.subTest(expected), patch.object(SDK.platform, "system", return_value=system), patch.object(SDK.platform, "machine", return_value=architecture):
                self.assertEqual(SDK.host_rid(), expected)
        with patch.object(SDK.platform, "machine", return_value="unreviewed-architecture"):
            with self.assertRaisesRegex(ValueError, "Unsupported SDK host"):
                SDK.host_rid()

    def test_verified_tar_and_zip_install_for_every_rid(self):
        for rid in SDK.RIDS:
            with self.subTest(rid), tempfile.TemporaryDirectory() as temporary:
                installed = self.install(Path(temporary), rid)
                self.assertEqual((installed / "sdk" / VERSION / "dotnet.dll").read_bytes(), b"reviewed compiler")
                if os.name != "nt" and rid != "win-x64":
                    self.assertTrue((installed / "dotnet").stat().st_mode & stat.S_IXUSR)

    def test_tampered_archive_rejected_before_any_archive_parser(self):
        with tempfile.TemporaryDirectory() as temporary, patch.object(SDK, "extract_archive") as extract:
            root = Path(temporary)
            with self.assertRaisesRegex(ValueError, "SHA512"):
                self.install(root, body=b"a tampered archive", digest="0" * 128)
            extract.assert_not_called()
            self.assertFalse((root / "sdk").exists())
            self.assertEqual(list(root.iterdir()), [])

    def test_existing_sdk_directory_is_never_reused(self):
        with tempfile.TemporaryDirectory() as temporary, patch.object(SDK, "download_verified") as download:
            destination = Path(temporary) / "sdk"
            destination.mkdir()
            with self.assertRaisesRegex(ValueError, "already exist"):
                SDK.install_sdk({"sdk_version": VERSION}, "linux-x64", destination)
            download.assert_not_called()
            if os.name != "nt":
                destination.rmdir()
                destination.symlink_to(Path(temporary) / "absent")
                with self.assertRaisesRegex(ValueError, "already exist"):
                    SDK.install_sdk({}, "linux-x64", destination)

    def test_traversal_absolute_backslash_and_drive_paths_rejected_in_both_formats(self):
        for rid in ("linux-x64", "win-x64"):
            for name in ("../outside", "/outside", "dir/../../outside", "dir\\outside", "C:/outside"):
                with self.subTest(rid=rid, path=name), tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    with self.assertRaisesRegex(ValueError, "Unsafe"):
                        self.install(root, rid, archive_bytes(rid, [(name, b"unapproved")]))
                    self.assertEqual(list(root.iterdir()), [])

    def test_tar_symlink_hardlink_and_special_entries_rejected(self):
        for kind in (tarfile.SYMTYPE, tarfile.LNKTYPE, tarfile.FIFOTYPE):
            entry = tarfile.TarInfo("linked")
            entry.type, entry.linkname = kind, "../../outside"
            with self.subTest(kind=kind), tempfile.TemporaryDirectory() as temporary:
                with self.assertRaisesRegex(ValueError, "link or special"):
                    self.install(Path(temporary), body=archive_bytes("linux-x64", [(entry, b"")]))
                self.assertEqual(list(Path(temporary).iterdir()), [])

    def test_zip_raw_names_survive_fixture_and_are_rejected_before_normalization(self):
        for name in ("dir\\outside", "dir\x00outside"):
            with self.subTest(name=name), patch.object(zipfile, "_sanitize_filename", side_effect=lambda value: value.split("\x00", 1)[0].replace("\\", "/")):
                body = archive_bytes("win-x64", [(name, b"unapproved")])
                with zipfile.ZipFile(io.BytesIO(body)) as archive:
                    self.assertEqual(archive.infolist()[-1].orig_filename, name)
                    self.assertNotEqual(archive.infolist()[-1].filename, name)
                with tempfile.TemporaryDirectory() as temporary:
                    with self.assertRaisesRegex(ValueError, "Unsafe"):
                        self.install(Path(temporary), "win-x64", body)

    def test_zip_symbolic_link_rejected(self):
        entry = zipfile.ZipInfo("linked")
        entry.create_system, entry.external_attr = 3, (stat.S_IFLNK | 0o777) << 16
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaisesRegex(ValueError, "link, special"):
                self.install(Path(temporary), "win-x64", archive_bytes("win-x64", [(entry, b"../../outside")]))

    def test_duplicate_and_case_alias_paths_rejected(self):
        cases = [("linux-x64", [("dotnet", b"replacement")]),
                 ("win-x64", [("DOTNET.EXE", b"replacement")]),
                 ("osx-arm64", [("DOTNET", b"replacement")]),
                 ("win-x64", [("folder/File.dll", b"one"), ("folder/file.dll", b"two")])]
        for rid, entries in cases:
            with self.subTest(rid=rid), tempfile.TemporaryDirectory() as temporary:
                with self.assertRaisesRegex(ValueError, "Duplicate"):
                    self.install(Path(temporary), rid, archive_bytes(rid, entries))

    def test_windows_device_and_alias_names_rejected(self):
        for name in ("NUL", "folder/con.txt", "folder/file.", "folder/file ", "folder/a:stream"):
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                with self.assertRaisesRegex(ValueError, "Unsafe"):
                    self.install(Path(temporary), "win-x64", archive_bytes("win-x64", [(name, b"bad")]))

    def test_extraction_size_and_entry_count_budgets(self):
        for limit in ("MAX_EXTRACTED_BYTES", "MAX_MEMBERS"):
            for rid in ("linux-x64", "win-x64"):
                with self.subTest(limit=limit, rid=rid), tempfile.TemporaryDirectory() as temporary, patch.object(SDK, limit, 1):
                    with self.assertRaisesRegex(ValueError, "extraction budget"):
                        self.install(Path(temporary), rid)

    def test_download_size_limits_and_redirect_are_rejected(self):
        entry = {"url": "https://builds.dotnet.microsoft.com/reviewed", "sha512": "0" * 128}
        cases = [response(b"body", entry["url"], SDK.MAX_ARCHIVE_BYTES + 1),
                 response(b"body", "http://unreviewed.test/archive"),
                 response(b"too large without content-length", entry["url"])]
        for stream in cases:
            with tempfile.TemporaryDirectory() as temporary, patch.object(SDK, "open_download", return_value=stream), patch.object(SDK, "MAX_ARCHIVE_BYTES", 3):
                with self.assertRaises(ValueError):
                    SDK.download_verified(entry, Path(temporary) / "archive")

    def test_redirect_is_blocked_before_following_replacement_url(self):
        with self.assertRaisesRegex(ValueError, "redirected"):
            SDK.NoRedirects().redirect_request(None, None, 302, "Found", {}, "http://unreviewed.test/sdk")

    def test_missing_expected_sdk_host_or_compiler_rejected(self):
        with tempfile.TemporaryDirectory() as temporary, patch.object(SDK, "extract_archive"):
            with self.assertRaisesRegex(ValueError, "host/compiler"):
                self.install(Path(temporary))

    @unittest.skipIf(os.name == "nt", "Unix executable permissions")
    def test_unix_host_executable_permissions_required(self):
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaisesRegex(ValueError, "executable"):
                self.install(Path(temporary), body=archive_bytes("linux-x64", host_mode=0o644))

    def test_github_exports_point_to_verified_host_for_both_shell_families(self):
        for rid in ("linux-arm64", "win-x64"):
            with self.subTest(rid), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                directory = root / "sdk with spaces_日本語"
                path, environment = root / "path", root / "environment"
                with patch.dict(os.environ, {"GITHUB_PATH": str(path), "GITHUB_ENV": str(environment)}):
                    SDK.export_github_environment(directory, rid)
                self.assertEqual(path.read_text(encoding="utf-8"), str(directory) + "\n")
                self.assertIn("DOTNET_ROOT=" + str(directory) + "\n", environment.read_text(encoding="utf-8"))
                self.assertIn("DOTNET_ROOT_" + rid.split("-")[1].upper() + "=", environment.read_text(encoding="utf-8"))
                self.assertIn("DOTNET_HOST_PATH=" + str(directory / ("dotnet.exe" if rid == "win-x64" else "dotnet")), environment.read_text(encoding="utf-8"))

    def test_environment_file_newline_injection_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            path, environment = Path(temporary) / "path", Path(temporary) / "env"
            with patch.dict(os.environ, {"GITHUB_PATH": str(path), "GITHUB_ENV": str(environment)}):
                with self.assertRaisesRegex(ValueError, "newline"):
                    SDK.export_github_environment("sdk\nUNREVIEWED=value", "win-x64")
            self.assertFalse(path.exists())
            self.assertFalse(environment.exists())

    def test_failure_does_not_export_a_partial_install(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            with patch("sys.argv", ["Install-DotNetSdk.py", "--install-dir", str(root / "sdk")]), patch.object(SDK, "install_sdk", side_effect=ValueError("tampered")), patch.object(SDK, "export_github_environment") as export:
                with self.assertRaisesRegex(ValueError, "tampered"):
                    SDK.main()
                export.assert_not_called()


if __name__ == "__main__":
    unittest.main()
