# Linux 开发

Linux 版本见 `apps/linux/VERSION`；根目录 `VERSION` 表示 Windows 版本。

## 模块

- `apps/linux/model.h`：单调时钟计时、自动会话与会议背景暂停恢复。
- `apps/linux/appdata.h`：Linux 文件匹配、JSON 配置与内置 WAV。
- `apps/linux/x11probe.*`：WPS 窗口检测、全局热键、置顶与点击穿透。
- `apps/linux/main.cpp`：Qt Widgets 界面、设置、音频与控制。
- `tests/linux/`：计时模型、配置及 X11 集成测试。

## 构建

需要 C++14 编译器、CMake 3.16+、Qt 5.12+ Core/Gui/Widgets/Multimedia、X11 与 Xext 开发文件。

```sh
cmake -S apps/linux -B .local/linux-build -DCMAKE_BUILD_TYPE=Release
cmake --build .local/linux-build --parallel 2
(cd .local/linux-build && ctest --output-on-failure)
```

`model` 不依赖 GUI；`appdata` 使用 Qt Core/Gui，但不需要显示服务。X11 集成测试需要独立的 1280×800 显示会话、窗口管理器与 xdotool。使用 `-DWPSCLOCK_X11_TESTS=ON` 将其加入 CTest。CI 通过 Xvfb/Openbox 隔离运行，避免影响日常桌面的窗口与快捷键。

## 发布与依赖

目标基线为银河麒麟 V10 SP1 2203 / x86_64 / X11，Qt 5.12.8、glibc 2.31。发布包应在兼容此基线的环境构建，不启用构建机专属 CPU 指令。较新 Ubuntu 上的 CI 构建用于回归检查，不能作为目标系统二进制兼容性的证明。

```sh
python3 -B tools/linux/package.py --binary .local/linux-build/WpsClock --out .local/agent/packages/linux
```

打包器生成 ZIP、tar.gz、许可证、说明和校验清单；运行库由目标系统提供。发布前另需核对 ELF 架构、GLIBC/GLIBCXX 符号版本、动态依赖、RPATH，以及目标系统的 Qt xcb 平台插件。

## 平台边界

仅支持 X11 桌面的自动检测和全局热键。Wayland 原生桌面、页码、自动单页计时、自动退出放映和到时黑屏未实现。配置格式独立于 Windows XML，不自动迁移 Windows 盘符路径。

诊断默认隐藏文档名与路径；文件规则仅保存在本地配置中。验证范围见[验证说明](linux-validation.md)。
