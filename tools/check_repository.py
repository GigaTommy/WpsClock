"""Read-only preflight over tracked and non-ignored files; no network access.

This is a heuristic check, not a substitute for a dedicated secret scanner or
review of Git history. It reports file/line/category without printing secrets.
"""
import re
import subprocess
import sys
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[1]
SECRETS = {
    'private key': re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----'),
    'GitHub token': re.compile(r'\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})\b'),
    'AWS access key': re.compile(r'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'),
    'OpenAI-style API key': re.compile(r'\bsk-(?:proj-|svcacct-)?[A-Za-z0-9_-]{32,}\b'),
    'Slack token': re.compile(r'\bxox[baprs]-[A-Za-z0-9-]{20,}\b'),
    'credential assignment': re.compile(r'''(?i)\b(?:password|passwd|api_key|access_token|client_secret)\s*[:=]\s*["'][^"'\s]{12,}["']'''),
    'personal macOS path': re.compile(r'/' + r'Users/[^/\s]+/'),
}
FORBIDDEN_SUFFIXES = {'.exe', '.dll', '.pdb', '.zip', '.7z', '.rar', '.deb', '.rpm',
                      '.ppt', '.pptx', '.pps', '.ppsx', '.pdf', '.mp3', '.wav', '.pem', '.pfx', '.p12', '.key'}
REQUIRED = ('README.md', '.gitignore', '.gitattributes', 'VERSION', 'CONTRIBUTING.md',
            'SECURITY.md', 'LICENSE', 'CHANGELOG.md', 'apps/windows/WpsClock.cs',
            'apps/windows/Build.cmd', 'tests/windows/RunTests.cmd',
            'docs/user-guide.md', 'docs/architecture.md')


def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args])


def main():
    errors = []
    try:
        raw = git('ls-files', '--cached', '--others', '--exclude-standard', '-z')
        paths = sorted({p.decode('utf-8') for p in raw.split(b'\0') if p})
    except (subprocess.CalledProcessError, FileNotFoundError, UnicodeError):
        print('FAIL: run from a Git working tree with UTF-8 file names (git init -b main for a new repository).')
        return 1
    for name in REQUIRED:
        if name not in paths:
            errors.append(name + ': missing or ignored required file')
    ignored = subprocess.run(
        ['git', '-C', str(ROOT), 'check-ignore', '--no-index', '--stdin', '-z'],
        input=b'\0'.join(name.encode('utf-8') for name in paths) + b'\0',
        stdout=subprocess.PIPE, check=False)
    if ignored.returncode not in (0, 1):
        errors.append('git check-ignore: unable to check included files')
    for name in ignored.stdout.split(b'\0'):
        if name:
            errors.append(name.decode('utf-8') + ': ignored material is already tracked or forcibly added')
    for name in paths:
        p = ROOT / name
        if p.is_symlink():
            errors.append(name + ': symbolic link requires explicit review')
            continue
        if not p.is_file():
            errors.append(name + ': missing tracked file; review staged deletions')
            continue
        if p.suffix.lower() in FORBIDDEN_SUFFIXES or '.local' in p.relative_to(ROOT).parts:
            errors.append(name + ': artifact, private material or local-only file included')
        if p.stat().st_size > 1024 * 1024:
            errors.append(name + ': exceeds 1 MiB; review generated/binary content')
            continue
        try:
            content = p.read_text(encoding='utf-8-sig')
        except UnicodeError:
            errors.append(name + ': non-UTF-8 content requires explicit review')
            continue
        for number, line in enumerate(content.splitlines(), 1):
            for category, pattern in SECRETS.items():
                if pattern.search(line):
                    errors.append('{}:{}: potential {}'.format(name, number, category))
        if p.suffix == '.md':
            for target in re.findall(r'\[[^\]\n]*\]\(([^)\n]+)\)', content):
                if re.match(r'[a-z][a-z0-9+.-]*:', target, re.I) or target.startswith('#'):
                    continue
                target = unquote(target.split('#', 1)[0])
                linked = (p.parent / target).resolve()
                if not linked.is_relative_to(ROOT) or not linked.exists():
                    errors.append(name + ': missing or external local Markdown link: ' + target)
    if (ROOT / 'VERSION').exists() and (ROOT / 'apps/windows/WpsClock.cs').exists():
        version = (ROOT / 'VERSION').read_text().strip()
        source = (ROOT / 'apps/windows/WpsClock.cs').read_text(encoding='utf-8-sig')
        if not re.fullmatch(r'\d+\.\d+\.\d+', version) or 'WpsClock ' + version + ' ' not in source:
            errors.append('VERSION: does not match application display or expected x.y.z format')
    probes = ('.local/agent/backups/check.txt', '.env', '.env.production', '.npmrc', '.netrc', 'credentials.json',
              'client_secret_test.json', 'private.pem', 'WpsClock.settings.xml',
              'apps/windows/WpsClock.settings.xml.tmp', 'apps/linux/settings.json', 'instance.lock', 'build.log', 'example.pptx',
              'release.zip', 'apps/windows/bin/app.exe', 'examples/__pycache__/x.pyc')
    for probe in probes:
        result = subprocess.run(['git', '-C', str(ROOT), 'check-ignore', '-q', '--no-index', probe])
        if result.returncode != 0:
            errors.append(probe + ': expected ignore rule is absent')
    for error in errors:
        print('FAIL: ' + error)
    if errors:
        return 1
    print('PASS: {} source/document files; required files, version, local links, ignore probes and heuristic secret scan.'.format(len(paths)))
    print('LIMIT: no history/remote scan or Windows/WPS runtime validation; manually review staged diff and licensing.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
