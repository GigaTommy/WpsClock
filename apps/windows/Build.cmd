@echo off
setlocal
set "WPSCLOCK_OUT=%~dp0..\..\.local\agent\apps\windows"
if not exist "%WPSCLOCK_OUT%" mkdir "%WPSCLOCK_OUT%"
if not exist "%WPSCLOCK_OUT%" exit /b 1
title Build WpsClock
set "WPSCLOCK_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%WPSCLOCK_CSC%" set "WPSCLOCK_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%WPSCLOCK_CSC%" goto missing
echo Building WpsClock.exe using the Windows .NET Framework compiler...
echo No PowerShell, Python, SDK download or Internet connection is used.
"%WPSCLOCK_CSC%" /nologo /langversion:5 /target:winexe /platform:anycpu /optimize+ /codepage:65001 /out:"%WPSCLOCK_OUT%\WpsClock.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:Microsoft.CSharp.dll "%~dp0WpsClock.cs" > "%WPSCLOCK_OUT%\build.log" 2>&1
if errorlevel 1 goto failed
echo.
echo SUCCESS: %WPSCLOCK_OUT%\WpsClock.exe
echo Double-click WpsClock.exe to run. There will be no console window.
echo Keep WpsClock.exe in a writable folder to save portable settings.
echo.
if not defined CI pause
exit /b 0
:missing
echo The Windows .NET Framework C# compiler was not found on this PC.
echo Ask your IT team for an approved build computer with .NET Framework.
echo This script does not install software or change system policies.
if not defined CI pause
exit /b 1
:failed
echo.
echo Build failed. Close any running WpsClock.exe and try again.
echo If it still fails, read build.log in this folder.
type "%WPSCLOCK_OUT%\build.log"
if not defined CI pause
exit /b 1
