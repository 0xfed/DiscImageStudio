import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import re
import stat
import tempfile
import unittest
from unittest.mock import Mock, patch
import zipfile


ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("release_artifacts", ROOT / "packaging/ReleaseArtifacts.py")
RELEASE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RELEASE)
TAG, COMMIT = "v1.2.3", "a" * 40


class ReleaseSecurityTests(unittest.TestCase):
    def fixture(self, root):
        binaries, _ = RELEASE.expected_names(TAG)
        for name in binaries:
            path = root / name
            path.write_bytes(b"published binary")
            sbom = {"metadata": {"properties": [{"name": "discimagestudio:source-commit", "value": COMMIT}],
                                 "component": {"name": name, "hashes": [{"alg": "SHA-256", "content": hashlib.sha256(path.read_bytes()).hexdigest()}]}}}
            (root / (name + ".cdx.json")).write_text(json.dumps(sbom))
        lines = [hashlib.sha256(p.read_bytes()).hexdigest() + "  " + p.name for p in sorted(root.iterdir())]
        (root / "SHA256SUMS.txt").write_text("\n".join(lines) + "\n")
        (root / "release-notes.md").write_text("Reviewed release notes")
        return RELEASE.inspect_directory(root, TAG, COMMIT)

    def archive(self, root, mutate=None):
        data = io.BytesIO()
        with zipfile.ZipFile(data, "w") as archive:
            for p in root.iterdir():
                archive.writestr(p.name, p.read_bytes())
            if mutate:
                mutate(archive)
        data.seek(0)
        return zipfile.ZipFile(data)

    def test_approved_archive_is_verified_without_filesystem_extraction(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = self.fixture(root)
            with self.archive(root) as archive:
                entries = RELEASE.validate_archive(archive, manifest, TAG, COMMIT)
                self.assertEqual(set(entries), RELEASE.expected_names(TAG)[1])

    def test_archive_traversal_and_duplicate_entries_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = self.fixture(root)
            for name in ("../../outside", "/etc/profile", "release-notes.md"):
                with self.archive(root, lambda archive: archive.writestr(name, b"unapproved")) as archive:
                    with self.assertRaisesRegex(ValueError, "Unexpected or duplicate"):
                        RELEASE.validate_archive(archive, manifest, TAG, COMMIT)

    def test_content_and_manifest_identity_cannot_be_replaced(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = self.fixture(root)
            (root / "release-notes.md").write_text("changed content")
            with self.archive(root) as archive:
                with self.assertRaises(ValueError):
                    RELEASE.validate_archive(archive, manifest, TAG, COMMIT)
            for name, value in (("size", -1), ("size", "20"), ("sha256", "bad"), ("name", "../unapproved")):
                changed = copy.deepcopy(manifest)
                changed["files"][0][name] = value
                with self.assertRaises(ValueError):
                    RELEASE.check_manifest(changed, TAG, COMMIT)
            with self.assertRaisesRegex(ValueError, "source commit"):
                RELEASE.check_manifest(manifest, TAG, "b" * 40)

    def test_inspector_rejects_sbom_for_different_source(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.fixture(root)
            with self.assertRaisesRegex(ValueError, "SBOM"):
                RELEASE.inspect_directory(root, TAG, "b" * 40)

    def test_symbolic_link_archive_entries_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = self.fixture(root)
            data = io.BytesIO()
            with zipfile.ZipFile(data, "w") as archive:
                for p in root.iterdir():
                    info = zipfile.ZipInfo(p.name)
                    if p.name == "release-notes.md":
                        info.create_system = 3
                        info.external_attr = (stat.S_IFLNK | 0o777) << 16
                    archive.writestr(info, p.read_bytes())
            data.seek(0)
            with zipfile.ZipFile(data) as archive:
                with self.assertRaisesRegex(ValueError, "type or size"):
                    RELEASE.validate_archive(archive, manifest, TAG, COMMIT)

    def test_workflows_pin_actions_and_keep_actions_out_of_the_write_job(self):
        for path in (ROOT / ".github/workflows").glob("*.yml"):
            text = path.read_text()
            for match in re.finditer(r"uses:\s+(\S+)", text):
                reference = match[1]
                if not reference.startswith("./"):
                    self.assertRegex(reference, r"^[^@]+@[a-f0-9]{40}$", str(path))
            self.assertNotIn("pip install numpy matplotlib", text)
            for options in re.findall(r"uses: actions/attest@[^\n]+\n        with:\n((?:          .*\n)+)", text):
                self.assertIn("          push-to-registry: false\n", options)
        workflow = (ROOT / ".github/workflows/release.yml").read_text()
        publisher = workflow.split("  draft:\n", 1)[1]
        self.assertNotIn("uses:", publisher)
        self.assertNotIn("GH_TOKEN:", workflow.split("  draft:\n", 1)[0])
        self.assertIn("RELEASE_MANIFEST: ${{ needs.inspect.outputs.manifest }}", publisher)
        self.assertIn("actions: read", publisher)

    def publisher_scenario(self, *, published=False, tag_sha=None, different_run=False, failure=None):
        """All API, storage and upload transports are mocked; never use the network."""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = self.fixture(root)
            files = {p.name: p.read_bytes() for p in root.iterdir()}
            with self.archive(root) as archive:
                zipped = archive.fp.getvalue()
            calls, uploads, storage = [], [], []
            blob_url = "https://artifact-storage.example/signed-archive"
            token = "offline-test-token"

            def urlopen(request, timeout):
                if isinstance(request, str):
                    # A plain URL carries no GitHub Request/Authorization headers.
                    self.assertEqual(request, blob_url)
                    storage.append(request)
                    return io.BytesIO(zipped)
                self.assertEqual(request.get_header("Authorization"), "Bearer " + token)
                path = RELEASE.urllib.parse.urlsplit(request.full_url).path.removeprefix("/repos/owner/repo")
                method = request.get_method()
                body = json.loads(request.data) if request.data else None
                calls.append((method, path, body))
                if path == "/actions/artifacts/123":
                    result = {"name": "release-" + TAG, "expired": False,
                              "workflow_run": {"id": 999 if different_run else 456}}
                elif path == "/releases" and method == "GET":
                    result = [{"id": 7, "tag_name": TAG, "draft": False}] if published else []
                elif path == "/git/ref/tags/" + TAG:
                    if tag_sha is None:
                        raise RELEASE.urllib.error.HTTPError(request.full_url, 404, "No tag", {}, io.BytesIO())
                    result = {"object": {"type": "commit", "sha": tag_sha}}
                elif path == "/git/refs" and method == "POST":
                    self.assertEqual(body, {"ref": "refs/tags/" + TAG, "sha": COMMIT})
                    result = {"object": {"type": "commit", "sha": COMMIT}}
                elif path == "/releases" and method == "POST":
                    self.assertTrue(body["draft"])
                    self.assertEqual(body["body"], "Reviewed release notes")
                    self.assertEqual(body["target_commitish"], COMMIT)
                    result = {"id": 7, "assets": [], "upload_url": "https://uploads.github.com/repos/owner/repo/releases/7/assets{?name,label}"}
                else:
                    self.fail("Unexpected API request: " + method + " " + path)
                return io.BytesIO(json.dumps(result).encode())

            def storage_redirect(request, timeout):
                self.assertEqual(request.get_header("Authorization"), "Bearer " + token)
                raise RELEASE.urllib.error.HTTPError(request.full_url, 302, "Storage", {"Location": blob_url}, io.BytesIO())

            def connection(host, timeout):
                self.assertEqual(host, "uploads.github.com")
                upload = {"headers": {}, "body": bytearray()}
                uploads.append(upload)
                client = Mock()
                client.putrequest.side_effect = lambda method, path: upload.update(method=method, path=path)
                client.putheader.side_effect = lambda name, value: upload["headers"].update({name: value})
                client.send.side_effect = upload["body"].extend
                client.getresponse.return_value.status = 201
                client.getresponse.return_value.read.return_value = b"{}"
                return client

            opener = Mock()
            opener.open.side_effect = storage_redirect
            environment = {"GH_TOKEN": token, "GH_REPO": "owner/repo", "RELEASE_TAG": TAG,
                           "RELEASE_COMMIT": COMMIT, "RELEASE_MANIFEST": json.dumps(manifest),
                           "RELEASE_ARTIFACT_ID": "123", "GITHUB_RUN_ID": "456", "IS_PRERELEASE": "false"}
            with patch.dict(RELEASE.os.environ, environment), \
                    patch.object(RELEASE.urllib.request, "urlopen", side_effect=urlopen), \
                    patch.object(RELEASE.urllib.request, "build_opener", return_value=opener), \
                    patch.object(RELEASE.http.client, "HTTPSConnection", side_effect=connection), \
                    patch("sys.stdout", new=io.StringIO()):
                if failure:
                    with self.assertRaisesRegex(ValueError, failure):
                        RELEASE.publish()
                else:
                    RELEASE.publish()
            return calls, uploads, storage, files

    def test_publisher_creates_draft_and_streams_only_approved_entries(self):
        calls, uploads, storage, files = self.publisher_scenario()
        self.assertEqual(len(storage), 1)
        self.assertIn(("POST", "/git/refs", {"ref": "refs/tags/" + TAG, "sha": COMMIT}), calls)
        names = set()
        for upload in uploads:
            name = RELEASE.urllib.parse.parse_qs(RELEASE.urllib.parse.urlsplit(upload["path"]).query)["name"][0]
            names.add(name)
            self.assertEqual(upload["body"], files[name])
            self.assertEqual(upload["headers"]["Content-Length"], str(len(files[name])))
            self.assertEqual(upload["headers"]["Authorization"], "Bearer offline-test-token")
        self.assertEqual(names, RELEASE.expected_names(TAG)[1] - {"release-notes.md"})

    def test_publisher_refuses_public_release_wrong_tag_or_cross_run_artifact(self):
        cases = [("already published", {"published": True}),
                 ("different commit", {"tag_sha": "b" * 40}),
                 ("workflow run", {"different_run": True})]
        for message, options in cases:
            with self.subTest(message=message):
                calls, uploads, storage, _ = self.publisher_scenario(failure=message, **options)
                self.assertFalse(uploads)
                self.assertFalse(any(method != "GET" for method, _, _ in calls))
                if options.get("different_run"):
                    self.assertFalse(storage)


if __name__ == "__main__":
    unittest.main()
