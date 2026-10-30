@echo off
setlocal
set "WPSCLOCK_OUT=%~dp0..\..\.local\agent\tmp\windows-tests"
if not exist "%WPSCLOCK_OUT%" mkdir "%WPSCLOCK_OUT%"
if not exist "%WPSCLOCK_OUT%" exit /b 1
set "WPSCLOCK_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%WPSCLOCK_CSC%" set "WPSCLOCK_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%WPSCLOCK_CSC%" exit /b 1
"%WPSCLOCK_CSC%" /nologo /langversion:5 /target:exe /main:LayoutSmokeTests /codepage:65001 /out:"%WPSCLOCK_OUT%\LayoutSmokeTests.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:Microsoft.CSharp.dll "%~dp0..\..\apps\windows\WpsClock.cs" "%~dp0LayoutSmokeTests.cs"
if errorlevel 1 exit /b 1
"%WPSCLOCK_OUT%\LayoutSmokeTests.exe"
exit /b %errorlevel%
