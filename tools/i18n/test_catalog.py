"""Catalog parity, format safety and generated-source checks."""
import json
import io
import re
import unittest
from unittest import mock
from pathlib import Path
import generate

ROOT = Path(__file__).resolve().parents[2]


class CatalogTests(unittest.TestCase):
    def test_generated_sources_are_current(self):
        for path, expected in generate.outputs().items():
            with self.subTest(path=path.name):
                self.assertEqual(path.read_bytes(), expected)

    def test_qt_arguments_and_file_filters_survive_translation(self):
        catalog = json.loads((ROOT / 'locales/en.json').read_text(encoding='utf-8'))
        for source, translated in catalog.items():
            with self.subTest(source=source):
                self.assertEqual(sorted(re.findall(r'%\d+', source)), sorted(re.findall(r'%\d+', translated)))
                self.assertEqual(re.findall(r'\*\.[A-Za-z*]+', source), re.findall(r'\*\.[A-Za-z*]+', translated))
                self.assertEqual(source.count('|'), translated.count('|'))

    def test_application_ui_uses_the_catalog(self):
        catalog = json.loads((ROOT / 'locales/en.json').read_text(encoding='utf-8'))
        cs = (ROOT / 'apps/windows/WpsClock.cs').read_text(encoding='utf-8-sig')
        sources = ((cs[cs.index('    public sealed class Audio'):], 'UiText.Get('),
                   ((ROOT / 'apps/linux/main.cpp').read_text(encoding='utf-8'), 'ui('))
        for source, prefix in sources:
            for match in re.finditer(r'(?<![@\w])"(?:\\.|[^"\\])*"', source):
                text = json.loads(match.group())
                if re.search('[\u4e00-\u9fff]', text):
                    with self.subTest(text=text):
                        self.assertIn(text, catalog)
                        self.assertTrue(source[:match.start()].endswith(prefix))

    def test_linux_display_version_matches_package(self):
        version = (ROOT / 'apps/linux/VERSION').read_text(encoding='utf-8').strip()
        self.assertIn('VERSION="' + version + '"', (ROOT / 'apps/linux/main.cpp').read_text(encoding='utf-8'))

    def test_checks_with_gbk_platform_default(self):
        original_open = io.open

        def gbk_open(file, mode='r', buffering=-1, encoding=None, *args, **kwargs):
            if 'b' not in mode and encoding is None:
                encoding = 'gbk'
            return original_open(file, mode, buffering, encoding, *args, **kwargs)

        with mock.patch('io.open', side_effect=gbk_open):
            self.test_generated_sources_are_current()
            self.test_qt_arguments_and_file_filters_survive_translation()
            self.test_application_ui_uses_the_catalog()
            self.test_linux_display_version_matches_package()


if __name__ == '__main__':
    unittest.main()
