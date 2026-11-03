# 单文件文本构建

将源码和构建脚本封装为一个 ASCII 文件，便于文本传输。生成器使用 Python 3.9+ 标准库；接收端使用 Windows 已有的 Windows PowerShell 和 .NET Framework 编译器，不下载安装依赖。

在仓库根目录运行 `python3 -B tools/text-build/make_text_pack.py`。输出到 `.local/agent/packages/text-build/`，版本来自根目录 `VERSION`。多语言翻译已包含在 C# 源码中，无需向接收端传输独立语言文件。生成的 `.cmd` 与 `.txt` 内容相同，`manifest.json` 记录源码、压缩载荷、脚本及完整包的校验值。

将完整 TXT 从 `@echo off` 至最后一行复制到接收端，保存为 `.cmd`；不要带 Markdown 围栏或消息标签。文件全部为 ASCII，可使用 ANSI 或 UTF-8。

退出旧程序后运行 CMD。显示 `SUCCESS` 后，在文本包旁的 `WpsClock-<版本>-<源码摘要>` 目录运行生成的 EXE。构建失败时检查该目录的 `build.log`。生成目录同时包含 `LICENSE.txt`；分发程序时保留该许可证。日常只运行 EXE，不重复运行构建脚本。

文本包旁有 `WpsClock.settings.xml` 时，会尝试导入至新目录；已有目标设置不会被覆盖。相对声音路径会转为基于原目录的绝对路径，原声音文件需保留。运行后从“版本／检测”确认版本。

完整性检查：`python3 -B -m unittest discover -s tools/text-build -p 'test_text_pack.py' -v`。可选 `test_bootstrap.py` 需要系统已有 `pwsh`，检查实际 PowerShell 解析、损坏拦截和缺少编译器时的失败路径；不代表 Windows CMD 或 GUI 已验证。

校验值用于发现文本损坏，不认证来源。
