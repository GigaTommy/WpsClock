"""Transport integrity checks; does not claim Windows CMD/GUI execution."""
import base64
import gzip
import hashlib
import re
import unittest
import tempfile
from unittest import mock
import make_text_pack
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACK = None

def verify(data):
    text = data.decode('ascii')
    script_blocks = re.findall(r'^:WPSCLOCK_SCRIPT_BEGIN\r?\n(.*?)^:WPSCLOCK_SCRIPT_END\r?$', text, re.M | re.S)
    if len(script_blocks) != 1:
        raise ValueError('script block')
    script = script_blocks[0].replace('\r', '')
    expected = re.search(r"if\(\$v -ne '([A-F0-9]{64})'\)", text).group(1)
    if hashlib.sha256(script.encode()).hexdigest().upper() != expected:
        raise ValueError('script hash')
    payload_blocks = re.findall(r'^:WPSCLOCK_PAYLOAD_BEGIN\r?\n(.*?)^:WPSCLOCK_PAYLOAD_END\r?$', text, re.M | re.S)
    if len(payload_blocks) != 1:
        raise ValueError('payload block')
    payload = base64.b64decode(re.sub(r'\s', '', payload_blocks[0]), validate=True)
    ph = re.search(r"\$payloadHash = '([A-F0-9]{64})'", script).group(1)
    if hashlib.sha256(payload).hexdigest().upper() != ph:
        raise ValueError('payload hash')
    source = gzip.decompress(payload)
    sh = re.search(r"\$sourceHash = '([A-F0-9]{64})'", script).group(1)
    length = int(re.search(r'\$sourceLength = (\d+)', script).group(1))
    if len(source) != length or hashlib.sha256(source).hexdigest().upper() != sh:
        raise ValueError('source hash')
    return source

class TextPackTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        global PACK
        cls.directory = tempfile.TemporaryDirectory(prefix='wpsclock-text-tests-')
        output = Path(cls.directory.name)
        try:
            with mock.patch.object(make_text_pack, 'OUTPUT', output):
                make_text_pack.make_pack()
            PACK = output / ('WpsClock-' + make_text_pack.VERSION + '-单文件构建.txt')
        except BaseException:
            cls.directory.cleanup()
            raise

    @classmethod
    def tearDownClass(cls):
        cls.directory.cleanup()

    def test_exact_source_roundtrip(self):
        self.assertEqual(verify(PACK.read_bytes()), (ROOT/'apps/windows/WpsClock.cs').read_bytes())
    def test_txt_and_cmd_identical(self):
        self.assertEqual(PACK.read_bytes(), PACK.with_suffix('.cmd').read_bytes())
    def test_lf_copy_accepted(self):
        self.assertEqual(verify(PACK.read_bytes().replace(b'\r\n', b'\n')), verify(PACK.read_bytes()))
    def test_wrapped_payload_accepted(self):
        data = PACK.read_bytes()
        start = data.index(b':WPSCLOCK_PAYLOAD_BEGIN\r\n') + len(b':WPSCLOCK_PAYLOAD_BEGIN\r\n')
        self.assertEqual(verify(data[:start+40] + b'\r\n  ' + data[start+40:]), verify(data))
    def test_truncated_payload_rejected(self):
        with self.assertRaises(ValueError):
            verify(PACK.read_bytes()[:-160])
    def test_changed_script_rejected(self):
        with self.assertRaises(ValueError):
            verify(PACK.read_bytes().replace(("$version = '" + make_text_pack.VERSION + "'").encode(), b"$version = '0.0.0'"))
    def test_changed_payload_rejected(self):
        data = PACK.read_bytes()
        start = data.index(b':WPSCLOCK_PAYLOAD_BEGIN\r\n') + len(b':WPSCLOCK_PAYLOAD_BEGIN\r\n')
        data = data[:start+12] + (b'A' if data[start+12:start+13] != b'A' else b'B') + data[start+13:]
        with self.assertRaises(ValueError):
            verify(data)
    def test_duplicate_package_rejected(self):
        with self.assertRaises(ValueError):
            verify(PACK.read_bytes()*2)
    def test_boot_line_below_cmd_limit(self):
        self.assertLess(max(map(len, PACK.read_bytes().splitlines())), 8191)

if __name__ == '__main__':
    unittest.main()
