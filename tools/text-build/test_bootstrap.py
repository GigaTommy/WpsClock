"""Optional real PowerShell validation; no Windows compilation or GUI execution."""
from pathlib import Path
from unittest import mock
import os
import re
import shutil
import subprocess
import tempfile

import make_text_pack


def main():
    pwsh = os.environ.get('WPSCLOCK_TEST_PWSH', 'pwsh')
    if not shutil.which(pwsh):
        print('PowerShell validation unavailable: install/use an approved pwsh separately, or run this check on Windows CI.')
        return 2
    with tempfile.TemporaryDirectory(prefix='wpsclock-pstest-') as directory:
        staging = Path(directory)
        with mock.patch.object(make_text_pack, 'OUTPUT', staging):
            make_text_pack.make_pack()
        pack = staging / ('WpsClock-' + make_text_pack.VERSION + '-单文件构建.txt')
        text = pack.read_text()
        boot = next(line for line in text.splitlines() if line.startswith('"%WPSCLOCK_PS%"')).split(' -Command "', 1)[1][:-1]
        script = re.search(r'^:WPSCLOCK_SCRIPT_BEGIN\n(.*?)^:WPSCLOCK_SCRIPT_END$', text, re.M | re.S).group(1)
        script_path = staging / 'loader.ps1'
        script_path.write_text(script)
        parser = "$tokens=$null;$errors=$null;[System.Management.Automation.Language.Parser]::ParseFile($env:WPSCLOCK_TEST_SCRIPT,[ref]$tokens,[ref]$errors)|Out-Null;if($errors.Count){$errors|Format-List;exit 1};Write-Host 'PowerShell syntax OK'"
        environment = dict(os.environ, WPSCLOCK_TEST_SCRIPT=str(script_path),
                           WPSCLOCK_PACK=str(pack), WINDIR=str(staging / 'missing-compiler'))

        def run(command):
            return subprocess.run([pwsh, '-NoLogo', '-NoProfile', '-Command', command],
                                  env=environment, text=True, capture_output=True, timeout=60)

        parsed = run(parser)
        if parsed.returncode != 0:
            raise RuntimeError('PowerShell parser rejected the generated script: ' + parsed.stdout + parsed.stderr)
        result = run(boot)
        if not (result.returncode == 1 and
                'script, compressed payload and source checksums verified' in result.stdout and
                'compiler not found' in result.stdout):
            raise RuntimeError('Unexpected bootstrap result: ' + result.stdout + result.stderr)
        print('PASS: PowerShell syntax, source restoration and missing-compiler failure path.')
        for name, data, expected in [
            ('changed-script', text.replace("$version = '" + make_text_pack.VERSION + "'", "$version = '0.0.0'"), 'Build script checksum mismatch'),
            ('truncated', text[:-160], 'Payload missing'),
            ('duplicate', text + text, 'Build script missing or duplicated'),
        ]:
            damaged = staging / (name + '.txt')
            damaged.write_text(data)
            environment['WPSCLOCK_PACK'] = str(damaged)
            result = run(boot)
            if result.returncode != 1 or expected not in result.stdout:
                raise RuntimeError(name + ': ' + result.stdout + result.stderr)
            print('PASS: ' + name + ' rejected by actual PowerShell bootstrap')
    print('PowerShell checks passed; Windows native build still requires Windows.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
