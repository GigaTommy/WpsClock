"""Catalog parity, format safety and generated-source checks."""
import json
import re
import unittest
from pathlib import Path
import generate

ROOT = Path(__file__).resolve().parents[2]


class CatalogTests(unittest.TestCase):
    def test_generated_sources_are_current(self):
        for path, expected in generate.outputs().items():
            with self.subTest(path=path.name):
                self.assertEqual(path.read_bytes(), expected)

    def test_qt_arguments_and_file_filters_survive_translation(self):
        catalog = json.loads((ROOT / 'locales/en.json').read_text())
        for source, translated in catalog.items():
            with self.subTest(source=source):
                self.assertEqual(sorted(re.findall(r'%\d+', source)), sorted(re.findall(r'%\d+', translated)))
                self.assertEqual(re.findall(r'\*\.[A-Za-z*]+', source), re.findall(r'\*\.[A-Za-z*]+', translated))
                self.assertEqual(source.count('|'), translated.count('|'))

    def test_application_ui_uses_the_catalog(self):
        catalog = json.loads((ROOT / 'locales/en.json').read_text())
        cs = (ROOT / 'apps/windows/WpsClock.cs').read_text(encoding='utf-8-sig')
        sources = ((cs[cs.index('    public sealed class Audio'):], 'UiText.Get('),
                   ((ROOT / 'apps/linux/main.cpp').read_text(), 'ui('))
        for source, prefix in sources:
            for match in re.finditer(r'(?<![@\w])"(?:\\.|[^"\\])*"', source):
                text = json.loads(match.group())
                if re.search('[\u4e00-\u9fff]', text):
                    with self.subTest(text=text):
                        self.assertIn(text, catalog)
                        self.assertTrue(source[:match.start()].endswith(prefix))

    def test_linux_display_version_matches_package(self):
        version = (ROOT / 'apps/linux/VERSION').read_text().strip()
        self.assertIn('VERSION="' + version + '"', (ROOT / 'apps/linux/main.cpp').read_text())


if __name__ == '__main__':
    unittest.main()
