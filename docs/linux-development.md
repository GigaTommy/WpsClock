# 麒麟版开发与验证

当前版本见 `apps/linux/VERSION`。根目录 `VERSION` 仍表示原 Windows 版本，不混用平台发布号。

新增模块：

- `apps/linux/model.h`：不依赖 GUI 的单调时间计时状态机、自动会话与会议背景暂停恢复。
- `apps/linux/appdata.h`：Linux 文件身份匹配、Qt JSON 配置、内置 WAV 生成。
- `apps/linux/x11probe.*`：WPS 窗口证据、全局热键、置顶及输入区域管理。
- `apps/linux/main.cpp`：Qt Widgets 悬浮界面、设置、音频与应用控制。
- `tests/linux/`：计时模型、配置文件匹配与 X11 集成检查。

已确认生产目标的 Qt 为 5.12.8，glibc 为 2.31，libstdc++ 来自 GCC 10.3，CPU 为 x86_64。必须使用兼容这些版本的构建基线，不启用构建机专属 CPU 指令。

## 原生 Linux 构建

开发机需已有 C++ 编译器、CMake、Qt 5.12 开发文件、X11 与 Xext 开发文件。这些只在开发机需要；使用者不需要安装开发工具。

```text
cmake -S apps/linux -B .local/linux-build -DCMAKE_BUILD_TYPE=Release
cmake --build .local/linux-build --parallel 2
cd .local/linux-build
ctest --output-on-failure
```

不自动安装依赖、不使用 sudo、不在使用者电脑编译。

`x11-tests` 需要 1280×800 的 X11 测试屏、窗口管理器与 xdotool，属于单独的桌面集成检查，不能在真实办公桌面上任意执行，以免测试热键影响正在使用的应用。可在隔离 Xvfb / Openbox 会话中运行。

## 本次构建方法

本次由 macOS arm64 使用 Zig 0.13.0 的 C++ 交叉编译器，目标 `x86_64-linux-gnu.2.31`、CPU `baseline`。开发头文件及链接库使用 Ubuntu focal 官方包中的 Qt 5.12.8、glibc 2.31 与 GCC 9 头文件；生成程序动态调用目标系统已有库，不附带或替换系统 Qt / libc。

生成的 ELF 随后在 QEMU 隔离的 x86_64 Linux 5.4 / glibc 2.31 / Qt 5.12.8 / Xvfb / Openbox 环境中执行。该环境没有安装真实 WPS、UKUI 或物理音频设备。

发布前检查 ELF 架构、动态依赖、符号版本和运行时搜索路径，禁止包含开发机绝对路径的 RPATH，保留二进制 SHA-256。测试与构建工具及第三方开发依赖不放入绿色运行包。

## 设计边界

界面不抢放映焦点。检测要求 WPS 身份与全屏证据；不把任意全屏窗口视作放映。对缺少文件名、同名歧义和监测间断采取保守行为，提供手动接管。

Qt 设置与样式尽量保持原 Windows 交互；Windows COM 页码接口无法照搬，本版不提供页码 / 单页计时、自动退出放映、到时黑屏。2026-11-02 用户已确认绿色版在目标电脑上可直接使用；保留候选版版本号，逐项验收边界见[验证记录](linux-validation.md)。

窗口诊断默认隐藏文档名与路径；自定义文件规则仅本地保存。不得在公开测试数据中加入实际银行文档名、个人配置或办公截图。
