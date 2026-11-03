# WpsClock

[简体中文](README.md) | [English](README.en.md)

**A portable, installation-free presentation timer for offline enterprise use.** WpsClock detects supported WPS slideshows, displays a floating timer and applies preconfigured durations to individual PPT/PDF files. Public source is licensed under [MIT](LICENSE); retain the copyright and license notice when distributing the program internally.

Run as a regular user: no administrator elevation or sudo, installer, system service, WPS add-in, online account or network connection is required. Copy or extract the correct platform package to a writable folder. Required runtime libraries and enterprise endpoint execution policies still apply.

- Windows: **2.5.0**, version in [VERSION](VERSION).
- Kylin/Linux: **2.5.0-kylin.1-rc1**, version in [apps/linux/VERSION](apps/linux/VERSION).

## Languages

Both applications support Simplified Chinese, English and System. System mode selects Chinese for Chinese system locales and English otherwise. Open **About / Diagnostics** on Windows or **About and diagnostics** on Linux, select **Language / 语言**, and save. The timer's elapsed time is preserved and the preference survives restart.

Translations are embedded, with no language pack or download. Document names, paths, rules and custom prefixes remain unchanged. Native file pickers and system error messages may use the OS language. See [localization development](docs/localization.md).

## Prepare multiple presentation files

Add rules in **Timing** (Windows) or **Timing and files** (Linux), then save:

| File | Type | Duration | Behavior |
| --- | --- | --- | --- |
| `Speech-A.pptx` | Speech | 00:10:00 | Start a 10-minute timer |
| `Speech-B.pptx` | Speech | 00:15:00 | Switching to B starts its 15-minute timer |
| `Backdrop.pptx` | Background | Not used | Pause the last speech, hide timers and stop automatic alerts |

A → background → A resumes A's remaining time. A → B starts B's full duration. Slide navigation does not reset timing. Earlier files do not retain permanent progress across arbitrary switches. Unknown/ambiguous identities use the default duration, with a manual session-duration option. Detection requires identifiable presentation windows and file metadata.

## Platform requirements

| Platform | Runtime | Source build |
| --- | --- | --- |
| Windows 10/11 | .NET Framework 4.5+, 4.8 recommended | Existing .NET Framework C# compiler; ordinary builds need no Python or .NET SDK |
| Kylin V10 SP1 2203, x86_64, X11 | Qt 5.12.8 Core/Gui/Widgets/Multimedia, xcb plugin, X11/Xext, glibc 2.31 | C++14 compiler, CMake 3.16+, Qt/X11/Xext development files; Python 3 for packaging |
| Other Linux systems | Verify architecture, libraries and desktop compatibility separately | A newer distribution's binary may not run on the Kylin baseline |
| macOS | Python packaging/catalog tools can run | Native Windows and Linux/X11 builds require their target toolchains/environments |

Runtime libraries do not include development headers or compilers. Linux automatic detection and global shortcuts currently target X11. Page numbers, automatic per-slide timing, automatic slideshow exit and blackout are not implemented on Linux. See [validation scope](docs/linux-validation.md).

## Run a portable build

### Windows

Run `WpsClock.exe`. Right-click **Settings**, configure file rules and alerts, save and start the slideshow. F3 pauses/resumes, F4 restarts. Closing Settings leaves the tray application running; choose **Exit** to quit.

Settings normally live in `WpsClock.settings.xml` beside the EXE, with a fallback to `%LOCALAPPDATA%\WpsClockPortable`. Exit before updates and retain settings/custom audio. See the [Windows guide](docs/user-guide.en.md).

### Kylin/Linux

Extract a target-compatible portable package, then run:

```sh
./WpsClock
```

If execute permission was lost, enable it in file properties or use `chmod +x WpsClock`. Configure files, duration and alerts, then save. F3 starts/pauses/resumes; F4 restarts. Exit via the tray/context menu.

Settings normally live under `~/.config/WpsClock/WpsClock-Kylin/`, respecting `XDG_CONFIG_HOME`. Keep this directory when replacing the executable. Windows XML and drive-letter paths are not imported. See the [Linux guide](docs/linux-user-guide.en.md).

## Developer workflow

Without write permission, create a GitHub Fork first and substitute its clone URL below. Accepted collaborators may use the upstream repository directly.

```sh
git clone https://github.com/GigaTommy/wpsbell.git
cd wpsbell
git switch -c feature/describe-change
```

Keep existing local changes safe before updating. Edit Windows code under `apps/windows` and tests under `tests/windows`; Linux code/tests are under `apps/linux` and `tests/linux`. Windows stays compatible with C# 5; Linux with C++14 and Qt 5.12. Update the appropriate platform version, application display, documentation and changelog.

### Build Windows

From the repository root, using Windows CMD:

```bat
apps\windows\Build.cmd
tests\windows\RunTests.cmd
tests\windows\RunLayoutChecks.cmd
```

Application output: `.local\agent\apps\windows\WpsClock.exe`; log: the adjacent `build.log`. Layout tests need a Windows desktop. Validate real slideshows, scaling, menus, displays and sound separately.

Optional ordinary ZIP, using Windows PowerShell:

```powershell
New-Item -ItemType Directory -Force .local\agent\packages | Out-Null
Compress-Archive -Path ".local\agent\apps\windows\WpsClock.exe","LICENSE","docs/user-guide.en.md" -DestinationPath ".local\agent\packages\WpsClock-Windows.zip" -Force
```

Do not include private configuration in an update package. Optional WinRAR SFX tooling is under `tools/windows`.

### Build Kylin/Linux

Use a Linux development environment compatible with the target baseline and with dependencies already prepared:

```sh
cmake -S apps/linux -B .local/linux-build -DCMAKE_BUILD_TYPE=Release
cmake --build .local/linux-build --parallel 2
(cd .local/linux-build && ctest --output-on-failure)
python3 -B tools/linux/package.py --binary .local/linux-build/WpsClock --out .local/agent/packages/linux
```

The executable is `.local/linux-build/WpsClock`; ZIP and tar.gz output is under `.local/agent/packages/linux/`. Packages contain the executable, Chinese/English instructions, validation notes, MIT license and checksums; system libraries are not bundled.

`package.py` archives an existing ELF executable; it does not compile C++. Rebuild after source changes before packaging. Default CTest covers the model/configuration. For isolated X11 testing, configure `-DWPSCLOCK_X11_TESTS=ON` and use Xvfb/Openbox and xdotool as shown in [CI](.github/workflows/ci.yml). Do not run synthetic global-key tests on a working desktop.

### Verify and contribute

Python tooling uses Python 3.9+ and its standard library. Use `python` instead of `python3` on Windows if appropriate.

```sh
python3 -B tools/check_repository.py
python3 -B tools/i18n/generate.py --check
python3 -B -m unittest discover -s tools/i18n -p 'test_*.py' -v
python3 -B -m unittest discover -s tools/text-build -p 'test_text_pack.py' -v
git diff --check
git status --short
```

Stage only the relevant files, commit and push the development branch. For a README-only change:

```sh
git add README.md README.en.md
git commit -m "docs: describe the change"
git push -u origin HEAD
```

Open a PR targeting `GigaTommy/wpsbell:main`. Fork users push to their own Fork. Main is protected; required checks must pass before merge. Also verify Linux CI for Linux changes. Do not push directly to or force-push main. Translation changes require regeneration; see [localization](docs/localization.md).

## Transfer source as text and rebuild internally

Use the organization's permitted text-transfer and execution methods. No internal access to GitHub is needed. Compilers, headers and runtime libraries must already be available; these scripts do not install dependencies or alter execution policies.

Save complete plaintext files with exact names and directory structure, without message labels or Markdown fences. CMD uses CRLF; source and documents use UTF-8. Linux paths are case-sensitive. Translations are embedded in the C# file and Linux `i18n.h`; recompilation does not need `locales/` or the generator.

### Windows: one text file

On the source side:

```sh
python3 -B tools/text-build/make_text_pack.py
```

Use `python` on Windows when appropriate. Output under `.local/agent/packages/text-build/` includes identical `WpsClock-2.5.0-单文件构建.txt`/`.cmd` files and a checksum manifest; names follow `VERSION`.

On the receiving Windows computer:

1. Transfer the complete TXT, from `@echo off` through its final line.
2. Save as `Build-WpsClock.cmd`, using ANSI or UTF-8 without BOM; avoid `.cmd.txt`.
3. Exit the old tray application. Put its XML settings beside this CMD to import them; retain custom audio.
4. Run CMD. Existing Windows PowerShell verifies/restores the source and invokes the .NET Framework compiler.
5. After `SUCCESS`, run `WpsClock-<version>.exe` from the adjacent `WpsClock-<version>-<source hash>/` folder.
6. Verify the displayed version and rules; preserve the generated `LICENSE.txt` when redistributing.

No Python, .NET SDK or WinRAR is needed on this receiving computer. Existing destination settings are preserved; see `BUILD-INFO.txt` and `build.log`. For future daily use, run the EXE rather than rebuilding each time.

### Windows: separate plaintext files

Transfer `LICENSE`, `apps/windows/WpsClock.cs` and `apps/windows/Build.cmd` with their directory structure. Run `apps\windows\Build.cmd` in CMD to produce `.local\agent\apps\windows\WpsClock.exe`. This method needs neither Python nor PowerShell. Transfer `tests/windows` as well if regression tests are needed.

### Kylin/Linux: multiple plaintext files

There is currently no Linux single-text bootstrap. Transfer complete source files, including:

```text
LICENSE
apps/linux/CMakeLists.txt
apps/linux/VERSION
apps/linux/main.cpp
apps/linux/model.h
apps/linux/appdata.h
apps/linux/i18n.h
apps/linux/x11probe.cpp
apps/linux/x11probe.h
tests/linux/model_tests.cpp
tests/linux/appdata_tests.cpp
tests/linux/x11_tests.cpp
tools/linux/package.py
docs/linux-development.md
docs/linux-user-guide.md
docs/linux-user-guide.en.md
docs/linux-validation.md
```

Tests are required because the current CMake build compiles their targets too. Packaging needs the license, guides and platform version; copying only `main.cpp` or `package.py` is insufficient.

Optionally generate a plaintext source checksum list from the source root:

```sh
mkdir -p .local/agent/packages
sha256sum apps/linux/* tests/linux/* tools/linux/package.py docs/linux-*.md LICENSE > .local/agent/packages/linux-source-SHA256SUMS.txt
```

On macOS use `shasum -a 256` in place of `sha256sum`. Transfer that list too and preserve file encoding/line endings. On the internal Linux build machine, from the restored source root:

```sh
sha256sum -c .local/agent/packages/linux-source-SHA256SUMS.txt
cmake -S apps/linux -B .local/linux-build -DCMAKE_BUILD_TYPE=Release
cmake --build .local/linux-build --parallel 2
(cd .local/linux-build && ctest --output-on-failure)
python3 -B tools/linux/package.py --binary .local/linux-build/WpsClock --out .local/agent/packages/linux
```

Run the first command only if a checksum list was supplied. The build machine needs the development dependencies; a runtime-only workstation cannot rebuild. Distribute the resulting target-compatible ZIP/tar.gz to runtime workstations. In the extracted version folder verify `sha256sum -c SHA256SUMS.txt` and run `./WpsClock`.

Repeat compilation, tests and packaging after code changes. Keep personal settings outside the source transfer. A GLIBC/GLIBCXX mismatch requires a compatible build environment, not simply a new archive. Other architectures and Wayland need separate support.

## Repository and license

Application code is under `apps/`, tests under `tests/`, translation catalogs under `locales/`, packaging/catalog/preflight tooling under `tools/`, and guides under `docs/`. Local outputs stay under ignored `.local/`.

[CI](.github/workflows/ci.yml) checks source, embedded catalogs, text packages, Windows compilation/regression/layout/bootstrap, and Linux compilation/model/configuration/X11/packaging. Physical WPS/audio/display acceptance remains separate.

See [CONTRIBUTING](CONTRIBUTING.md), [SECURITY](SECURITY.md) and [MIT LICENSE](LICENSE). SHA-256 detects corruption; it does not authenticate the publisher. Redact actual document names and paths before sharing diagnostics.
