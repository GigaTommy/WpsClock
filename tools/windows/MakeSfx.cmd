@echo off
setlocal
set "WPSCLOCK_APP=%~dp0..\..\.local\agent\apps\windows\WpsClock.exe"
set "WPSCLOCK_OUT=%~dp0..\..\.local\agent\packages\sfx"
title Package WpsClock with WinRAR
set "WPSCLOCK_RAR=%ProgramFiles%\WinRAR\WinRAR.exe"
if not exist "%WPSCLOCK_RAR%" set "WPSCLOCK_RAR=%ProgramFiles(x86)%\WinRAR\WinRAR.exe"
if not exist "%WPSCLOCK_RAR%" goto missing
if not exist "%WPSCLOCK_APP%" goto nobuild
if not exist "%~dp0SFX-Options.txt" goto nooptions
echo Close WpsClock before packaging or running the SFX archive.
echo Packaging the generated EXE with WinRAR...
if not exist "%WPSCLOCK_OUT%" mkdir "%WPSCLOCK_OUT%"
if not exist "%WPSCLOCK_OUT%" goto failed_no_push
pushd "%WPSCLOCK_OUT%"
if errorlevel 1 goto failed_no_push
"%WPSCLOCK_RAR%" a -cfg- -sfx -ep -y -z"%~dp0SFX-Options.txt" "WpsClock-Portable.exe" "%WPSCLOCK_APP%"
if errorlevel 1 goto failed
popd
echo SUCCESS: %WPSCLOCK_OUT%\WpsClock-Portable.exe
echo This SFX archive extracts to LocalAppData\WpsClockPortable and runs WpsClock.exe.
echo It does not include your personal settings or external sound files.
pause
exit /b 0
:failed
popd
:failed_no_push
echo Packaging failed. Please use the manual WinRAR steps in the README.
pause
exit /b 1
:missing
echo WinRAR was not found in its usual installation folder.
echo Use the manual WinRAR steps in the README, or edit WPSCLOCK_RAR in this script.
pause
exit /b 1
:nobuild
echo First run apps\windows\Build.cmd to generate WpsClock.exe.
pause
exit /b 1
:nooptions
echo SFX-Options.txt is missing. Keep all supplied text files in the same folder.
pause
exit /b 1
