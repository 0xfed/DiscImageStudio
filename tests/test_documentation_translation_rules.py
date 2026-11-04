"""Protect technical content while allowing explicitly reviewed documentation translations."""

from pathlib import Path
import tempfile
import unittest

from test_documentation_translations import command_blocks, documentation_sources, generated_reference_paths, parallel_language_view


SOURCE = '''```json
{
  "id": "dvd-example",
  "displayName": "实测预设",
  "description": "说明",
  "innerRadiusMm": 23.991460627731737001,
  "totalSectors": 2297888,
  "invertMapping": false,
  "contributor": "DBPolp"
}
```'''
TRANSLATED = SOURCE.replace('实测预设', 'Measured preset').replace('说明', 'Description')
TEXT_FIELDS = ("displayName", "description")


class PresetExampleRules(unittest.TestCase):
    def test_parallel_release_lists_preserve_every_distinct_change(self):
        source = '# Changelog\n\n## Unreleased\n\n### 中文\n\n- 修正 `--language`。\n- 保留 `id`。\n\n### English\n\n- Fix `--language`.\n\n- Keep `id`.\n\n## Older\n\n### Added\n\n- History.\n'
        labels = ('中文', 'English')
        expected = '# Changelog\n\n## Unreleased\n\n- 修正 `--language`。\n\n- 保留 `id`。\n\n## Older\n\n### Added\n\n- History.\n'
        self.assertEqual(parallel_language_view(source, labels, True), expected)
        self.assertEqual(parallel_language_view(expected, labels), expected)
        for invalid in (source.replace('### English', '### Missing'),
                        source.replace('- Keep `id`.\n', ''), source.replace('Fix `--language`', 'Fix `--other`'),
                        source.replace('- Keep `id`.', 'Unlisted paragraph.'), source + '\n### 中文\n- Extra.\n'):
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                parallel_language_view(invalid, labels, True)
        with self.assertRaises(ValueError):
            parallel_language_view(expected, labels, True)

    def test_only_declared_text_fields_may_change(self):
        self.assertEqual(command_blocks(SOURCE, TEXT_FIELDS), command_blocks(TRANSLATED, TEXT_FIELDS))
        self.assertNotEqual(command_blocks(SOURCE), command_blocks(TRANSLATED))
        for original, replacement in (
            ('dvd-example', 'another-id'), ('DBPolp', 'another-contributor'),
            ('23.991460627731737001', '23.991460627731737002'),
            ('2297888', '2297872'), ('false', 'true'),
            ('2297888', '"2297888"'),
        ):
            with self.subTest(original=original, replacement=replacement):
                self.assertNotEqual(command_blocks(SOURCE, TEXT_FIELDS),
                                    command_blocks(TRANSLATED.replace(original, replacement), TEXT_FIELDS))

    def test_malformed_or_empty_translations_are_rejected(self):
        for invalid in (
            TRANSLATED.replace('"Measured preset"', '"   "'),
            TRANSLATED.replace('"Description"', 'null'),
            TRANSLATED.replace('"id": "dvd-example",', '"id": "dvd-example", "id": "dvd-example",'),
            TRANSLATED.replace('2297888', 'NaN'),
        ):
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                command_blocks(invalid, TEXT_FIELDS)

    def test_commands_remain_exact(self):
        source = '```sh\n# 原始说明\ndisc-image-studio solve --sectors 32\n```'
        translated = source.replace('原始说明', 'Original description')
        self.assertEqual(command_blocks(source, TEXT_FIELDS), command_blocks(translated, TEXT_FIELDS))
        self.assertNotEqual(command_blocks(source, TEXT_FIELDS),
                            command_blocks(translated.replace('--sectors 32', '--sectors 48'), TEXT_FIELDS))

    def test_generated_reports_do_not_become_source_documents(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sources = {"README.md", "docs/guide.md", "tools/probe/README.md"}
            generated = {"artifacts/ci/tr/all-bytes.md", "tmp/report.md", "src/bin/README.md",
                         "src/obj/README.md", ".venv/lib/README.md", "README.ja.md"}
            for relative in sources | generated:
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("fixture", encoding="utf-8")
            self.assertEqual(documentation_sources(root), sources)

    def test_only_declared_generated_links_may_be_missing(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "docs/analysis.md"
            output = "../artifacts/results/plot.png"
            missing = "missing-guide.md"
            text = f"[plot]({output}) [guide]({missing})"
            self.assertEqual(generated_reference_paths(root, source, text, [output]), {output})
            self.assertEqual(generated_reference_paths(root, source, text, []), set())
            for invalid in ([missing], [output, output], ["../artifacts/unreferenced.png"],
                            ["../artifacts/../private.md"], ["https://example.com/plot.png"]):
                with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                    generated_reference_paths(root, source, text, invalid)

    def test_mermaid_words_may_change_without_changing_graph_or_digits(self):
        source = '```mermaid\nflowchart LR\n    BD --> Encoder[BD encoder / 17PP / ECC]\n```'
        translated = source.replace('BD encoder', 'BD 編碼器')
        self.assertEqual(command_blocks(source, mermaid_labels=('Encoder',)),
                         command_blocks(translated, mermaid_labels=('Encoder',)))
        self.assertNotEqual(command_blocks(source), command_blocks(translated))
        for before, after in (('17PP', '18PP'), ('BD -->', 'CD -->'), ('flowchart LR', 'flowchart RL')):
            with self.subTest(before=before):
                self.assertNotEqual(command_blocks(source, mermaid_labels=('Encoder',)),
                                    command_blocks(translated.replace(before, after), mermaid_labels=('Encoder',)))
        for invalid in (translated.replace('Encoder[', 'Changed['), translated.replace('BD 編碼器 / 17PP / ECC', '   ')):
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                command_blocks(invalid, mermaid_labels=('Encoder',))


if __name__ == "__main__":
    unittest.main()
