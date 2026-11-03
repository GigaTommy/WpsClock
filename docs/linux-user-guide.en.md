# WpsClock portable for Kylin/Linux

Version: 2.5.0-kylin.1-rc1. [中文使用指南](linux-user-guide.md)

An MIT-licensed portable timer for offline enterprise PPT/PDF presentations. Extract the package into a writable folder and run `./WpsClock`, or double-click the executable. No installer, sudo, administrator elevation, service, account or network connection is required. Retain the MIT license when redistributing the program. Endpoint execution policies still apply.

Target: Kylin Desktop V10 SP1 2203, x86_64, X11, glibc 2.31 and Qt 5.12.8. Qt Core, Gui, Widgets, Multimedia, the xcb platform plugin, X11 and Xext must be available. The package does not bundle these system libraries. Native Wayland and other CPU architectures need separate adaptation. If extraction removed execute permission, enable it in file properties or use `chmod +x WpsClock`.

## Configure a meeting

1. Start the application. Open **Settings → Timing and files**.
2. Set a default duration and add each speech file with its own duration.
3. Mark the meeting backdrop as **Background**, then save.
4. Start the WPS slideshow. F3 starts/pauses/resumes, F4 restarts.
5. Use the timer's context menu or tray menu for settings and **Exit**. Closing Settings does not quit.

Switching A → B starts B's duration. Slide navigation does not reset it. A → background → A pauses, hides and resumes A; background time is excluded. Earlier files do not retain permanent progress across arbitrary switches. Unknown or ambiguous names use the default duration. Paths are case-sensitive; Windows XML settings and drive-letter paths are not imported automatically.

## Language and appearance

In **About and diagnostics → Language / 语言**, choose System, 简体中文 or English and save. This preserves elapsed time. Translation is embedded, so no extra language file or download is needed. Document names and user-defined prefixes remain unchanged.

Drag digits to move the timer, or the bottom-right corner to resize. Each display keeps its position. Unlock click-through using the tray menu. Compositing is needed for transparency; otherwise the background is opaque.

## Audio, settings and updates

Blank sound paths use built-in WAV alerts. Test output before the meeting; MP3 needs system codecs. When time is up, the slideshow stays open. Page numbers, automatic per-slide timing, automatic fullscreen exit and blackout are unavailable.

Settings, language, positions and rules are stored in `settings.json`, normally under `~/.config/WpsClock/WpsClock-Kylin/`, or the corresponding `XDG_CONFIG_HOME` location. Exit before replacing the executable and preserve that directory. Stop the application and remove its extracted folder to remove the program; delete the configuration separately if desired.

The diagnostics copy button hides document names and paths. Test actual Kylin/WPS, projector displays, hotplug, shortcuts and audio devices before deployment. Source build and offline text-transfer instructions are available in the project's English README.
