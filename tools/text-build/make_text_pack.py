"""Maintainer tool. Recipients need only the generated TXT, not Python or this file."""
from pathlib import Path
import base64
import gzip
import hashlib
import json
import textwrap

ROOT = Path(__file__).resolve().parents[2]
VERSION = (ROOT / 'VERSION').read_text(encoding='ascii').strip()
SOURCE = ROOT / 'apps' / 'windows' / 'WpsClock.cs'
OUTPUT = ROOT / '.local' / 'agent' / 'packages' / 'text-build'

def sha(data):
    return hashlib.sha256(data).hexdigest().upper()

def make_pack():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    source = SOURCE.read_bytes()
    if ('WpsClock ' + VERSION + ' ').encode() not in source:
        raise ValueError('VERSION does not match the application version display')
    packed = gzip.compress(source, mtime=0)
    script = (ROOT / 'tools' / 'text-build' / 'build-loader.ps1').read_text(encoding='ascii')
    values = {'VERSION': VERSION, 'SOURCE_HASH': sha(source), 'PAYLOAD_HASH': sha(packed), 'SOURCE_LENGTH': str(len(source)), 'LICENSE': (ROOT / 'LICENSE').read_text(encoding='ascii').rstrip()}
    for key, value in values.items():
        script = script.replace('@@' + key + '@@', value)
    if '@@' in script:
        raise ValueError('Unresolved loader template placeholder')
    script = script.rstrip() + '\n'
    boot = "$ErrorActionPreference='Stop';try{$t=[IO.File]::ReadAllText($env:WPSCLOCK_PACK);$m=[regex]::Matches($t,'(?ms)^:WPSCLOCK_SCRIPT_BEGIN\\r?\\n(.*?)^:WPSCLOCK_SCRIPT_END\\r?$');if($m.Count -ne 1){throw 'Build script missing or duplicated. Copy all text again.'};$s=$m[0].Groups[1].Value.Replace([string][char]13,'');$h=[Security.Cryptography.SHA256]::Create();try{$v=([BitConverter]::ToString($h.ComputeHash([Text.Encoding]::ASCII.GetBytes($s)))).Replace('-','')}finally{$h.Dispose()};if($v -ne '" + sha(script.encode('ascii')) + "'){throw 'Build script checksum mismatch. Copy all text again.'};& ([scriptblock]::Create($s));exit 0}catch{Write-Host ('FAILED: '+$_.Exception.Message);exit 1}"
    lines = [
        '@echo off', 'setlocal DisableDelayedExpansion',
        'rem WpsClock ' + VERSION + ' - SINGLE TEXT BUILD - copy this entire file.',
        'rem Save as .cmd using UTF-8 or ANSI. Do not copy Markdown code fences.',
        'rem Requires existing Windows PowerShell and .NET Framework; no downloads.',
        'title WpsClock ' + VERSION + ' - Single text build',
        'set "WPSCLOCK_PACK=%~f0"',
        'set "WPSCLOCK_PS=%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe"',
        'if not exist "%WPSCLOCK_PS%" goto missing',
        '"%WPSCLOCK_PS%" -NoLogo -NoProfile -Command "' + boot + '"',
        'if errorlevel 1 goto failed',
        'echo.', 'echo Build completed. The output folder contains the versioned EXE.',
        'pause', 'exit /b 0', ':missing',
        'echo FAILED: Windows PowerShell was not found. Contact your IT team.',
        ':failed', 'echo No new build was confirmed. Do not mistake an older EXE for this build.',
        'pause', 'exit /b 1',
        ':WPSCLOCK_SCRIPT_BEGIN', script.rstrip(), ':WPSCLOCK_SCRIPT_END',
        ':WPSCLOCK_PAYLOAD_BEGIN', textwrap.fill(base64.b64encode(packed).decode('ascii'), 100),
        ':WPSCLOCK_PAYLOAD_END', 'rem END OF WPSCLOCK ' + VERSION + ' SINGLE TEXT BUILD', ''
    ]
    content = '\r\n'.join(lines).replace('\r\n', '\n').replace('\n', '\r\n').encode('ascii')
    for extension in ['txt', 'cmd']:
        (OUTPUT / ('WpsClock-' + VERSION + '-单文件构建.' + extension)).write_bytes(content)
    manifest = dict(version=VERSION, source_sha256=sha(source), payload_sha256=sha(packed), script_sha256=sha(script.encode('ascii')), pack_sha256=sha(content), bytes=len(content))
    (OUTPUT / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps(manifest, indent=2))

if __name__ == '__main__':
    make_pack()
