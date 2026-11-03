"""Build portable archives from an already built Linux ELF. No network/install steps."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import tarfile
import zipfile

ROOT = Path(__file__).resolve().parents[2]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--binary', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    data = args.binary.read_bytes()
    if data[:6] != b'\x7fELF\x02\x01' or data[18:20] != b'\x3e\x00':
        raise SystemExit('Expected a little-endian x86_64 ELF executable')
    if b'/Users/' in data:
        raise SystemExit('Development machine paths remain in executable')
    version = (ROOT / 'apps/linux/VERSION').read_text().strip()
    name = 'WpsClock-Kylin-x86_64-' + version
    args.out.mkdir(parents=True, exist_ok=True)
    files = {
        'WpsClock': (data, 0o755),
        '使用说明.txt': ((ROOT / 'docs/linux-user-guide.md').read_bytes(), 0o644),
        'README.en.txt': ((ROOT / 'docs/linux-user-guide.en.md').read_bytes(), 0o644),
        '验证记录.txt': ((ROOT / 'docs/linux-validation.md').read_bytes(), 0o644),
        'LICENSE.txt': ((ROOT / 'LICENSE').read_bytes(), 0o644),
        '版本.txt': ((version + '\nLinux x86_64; Kylin V10 SP1 2203; X11; Qt 5.12.8; glibc 2.31\n').encode(), 0o644),
    }
    checksums = ''.join(hashlib.sha256(content).hexdigest() + '  ' + path + '\n' for path, (content, _) in files.items())
    files['SHA256SUMS.txt'] = (checksums.encode(), 0o644)
    with zipfile.ZipFile(args.out / (name + '.zip'), 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path, (content, mode) in files.items():
            info = zipfile.ZipInfo(name + '/' + path, date_time=(2026, 10, 30, 0, 0, 0))
            info.create_system = 3
            info.external_attr = (0o100000 | mode) << 16
            info.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(info, content)
    with tarfile.open(args.out / (name + '.tar.gz'), 'w:gz') as archive:
        for path, (content, mode) in files.items():
            info = tarfile.TarInfo(name + '/' + path)
            info.size = len(content)
            info.mode = mode
            info.mtime = 0
            archive.addfile(info, io.BytesIO(content))
    with zipfile.ZipFile(args.out / (name + '.zip')) as archive:
        assert archive.testzip() is None
        assert (archive.getinfo(name + '/WpsClock').external_attr >> 16) & 0o111
        assert archive.read(name + '/WpsClock') == data
        assert not any(p.endswith(('.sh', '.cmd', '.ps1')) for p in archive.namelist())
    with tarfile.open(args.out / (name + '.tar.gz')) as archive:
        assert archive.extractfile(name + '/WpsClock').read() == data
        assert archive.getmember(name + '/WpsClock').mode == 0o755
    print(json.dumps({'version': version, 'binary_bytes': len(data), 'binary_sha256': hashlib.sha256(data).hexdigest(), 'zip_bytes': (args.out / (name + '.zip')).stat().st_size}, indent=2))

if __name__ == '__main__':
    main()
