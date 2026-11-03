# WpsClock for Windows

Version: 2.5.0. [中文使用指南](user-guide.md)

WpsClock is an MIT-licensed portable presentation timer for offline enterprise meetings, training and reviews. Copy or extract the application into a writable folder and run the EXE as a regular user. No installer, administrator elevation, service, WPS add-in, account or network connection is required. Keep the MIT license when redistributing it. The computer must provide .NET Framework 4.5+ (4.8 recommended) and permit the application to run under its endpoint policies.

## Start and configure

1. Start `WpsClock.exe`. The timer enters standby and remains in the system tray.
2. Right-click the timer and open **Settings**.
3. In **Timing**, set a default duration and add PPT/PDF rules with individual durations.
4. Mark a conference backdrop as **Background**. Speech files use **Speech**.
5. Set early/end alerts under **Controls**, then test sound playback.
6. Save, and start a supported WPS slideshow. F3 pauses/resumes; F4 restarts.

Switching from speech A to speech B starts B's configured duration. Slide navigation does not reset timing. Switching A → background → A pauses, hides and resumes A with its remaining time. Earlier speech files do not retain permanent progress across arbitrary switches. Unknown or ambiguous file names use the default duration; the menu offers manual session duration selection.

## Language

Open **About / Diagnostics → Language / 语言**, select System, 简体中文 or English, and save. The UI updates without resetting elapsed time. Native system dialogs may retain the operating-system language. Translations are embedded; no language pack is required.

## Move, exit and update

Drag the digits to move the timer; drag edges/corners to resize. Unlock a click-through timer from the tray. Closing Settings keeps the application running; use **Exit** from the tray or context menu to quit.

Settings are normally stored in `WpsClock.settings.xml` beside the EXE, with a fallback to `%LOCALAPPDATA%\WpsClockPortable` if that folder is read-only. Exit before replacing the EXE, and keep settings and custom audio. Diagnostics may contain document paths; redact them before sharing.

Build and offline text-transfer instructions are in the [English README](../README.en.md). The default end action leaves the slideshow running; optional fullscreen exit/blackout behavior depends on the presentation application. Verify real WPS versions, displays, scaling, shortcuts and sound before a meeting.
