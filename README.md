# WpsClock

[简体中文](README.md) | [English](README.en.md)

WpsClock 是面向企业内部会议、培训、评审和路演的**绿色免安装放映计时器**。公开源码采用 **MIT 许可证**，构建后的程序可复制、离线分发和使用，适用于 PPT 等演示文件以及受支持阅读器中的 PDF 全屏计时。

程序以普通用户身份运行，**无需管理员权限或 sudo 提权**，无需安装服务、插件、注册账号或访问网络。Windows 产物为 EXE，麒麟产物为 Linux 便携包；解压或复制后即可在满足运行依赖的电脑上启动。企业内部分发时保留随附的 MIT 版权与许可证声明。

可提前为多个文件分别设置发言时长。放映不同文件时自动切换到对应时长，翻页不重置；会议背景文件可暂停并隐藏计时器，返回原发言文件时继续。另支持提前及到时提醒、超时显示、多屏显示与背景透明度。

- Windows：**2.5.0**，版本入口为 [VERSION](VERSION)。
- 麒麟/Linux：**2.5.0-kylin.1-rc1**，版本入口为 [apps/linux/VERSION](apps/linux/VERSION)。

程序无账号、遥测或在线升级，不修改演示文档。完整功能见 [Windows 使用指南](docs/user-guide.md)和[麒麟使用指南](docs/linux-user-guide.md)。

## 多语言

Windows 和麒麟版均支持 **简体中文、English、跟随系统**。首次按系统界面语言选择：中文系统使用简体中文，其他系统使用英文。

- Windows：打开“本地设置 → 版本 / 检测 → 语言 / Language”，选择语言后保存。
- 麒麟：打开“本地设置 → 版本与检测 → 语言 / Language”，选择语言后保存。
- 保存后菜单、设置与计时状态使用所选语言，现有计时进度保留；下次启动继续使用已保存的选择。
- 文件名、文件路径、文件时长规则及自定义前缀按原值保留。系统文件选择器和操作系统错误信息可能沿用系统语言。

翻译已编入程序，使用端不需要额外语言包、Python 或联网下载。新增和维护翻译的方法见[本地化开发说明](docs/localization.md)。

## 多文件会议示例

在 Windows 的“时长设置”或麒麟的“时长与文件”中提前添加文件，保存各自时长和用途：

| 文件 | 用途 | 时长 | 放映时行为 |
| --- | --- | --- | --- |
| `发言A.pptx` | 发言计时 | 00:10:00 | 自动开始 10 分钟计时 |
| `发言B.pptx` | 发言计时 | 00:15:00 | 切换至该文件后，自动开始 15 分钟计时 |
| `会议背景.pptx` | 会议背景 | 不使用时长 | 暂停上一场发言、隐藏计时器并停止自动提醒 |

放映 A → 背景 → A 时恢复 A 的剩余时间；A → B 时使用 B 的完整时长。返回较早的发言文件通常开始新场次，不为所有文件永久保存历史进度。未设置规则的文件使用默认时长；无法确定文件或存在同名歧义时使用默认值，并提供手动选择本场时长的入口。该功能依赖 WPS/阅读器提供可识别的放映窗口和文件信息。

## 选择运行环境

| 环境 | 运行条件 | 源码构建条件 |
| --- | --- | --- |
| Windows 10 / 11 | .NET Framework 4.5+，建议 4.8 | Windows 自带的 .NET Framework C# 编译器；普通源码构建不需要 Python 或 .NET SDK |
| 银河麒麟 V10 SP1 2203 / x86_64 / X11 | Qt 5.12.8、X11、Xext、Qt xcb 平台插件；音频解码依赖系统组件 | C++14 编译器、CMake 3.16+、Qt Core/Gui/Widgets/Multimedia 和 X11/Xext 开发文件；生成便携包另需 Python 3 |
| 其他 Linux | 需另行确认架构、Qt、glibc、libstdc++ 与桌面环境兼容性 | 不能直接保证麒麟 x86_64 包适用 |
| macOS | 可使用 Python 文本打包和检查工具 | 不能原生执行 Windows 构建或 Linux/X11 应用构建；跨平台构建需要另外准备工具链 |

普通用户需要对程序、配置及演示文件所在目录具备相应读写/执行权限；企业白名单或终端执行策略仍适用。免安装与不提权不代表程序能绕过系统策略。

**运行已有程序与从源码重新构建的要求不同。** 麒麟电脑即使能运行程序，也不代表已安装编译器、CMake 和 Qt 开发文件。Windows 的 `.cmd`/`.exe` 不能用于麒麟；麒麟的 ELF 可执行文件不能用于 Windows。

Linux 发布目标基线为 glibc 2.31 / Qt 5.12.8 / x86_64。较新 Ubuntu 上生成的程序不一定能在目标麒麟运行，CI 通过也不能替代目标机兼容性验证。Linux 当前仅支持 X11 自动检测与快捷键，不提供页码、自动单页计时、自动退出放映和到时黑屏。详见[验证范围](docs/linux-validation.md)。

## 快速使用

### Windows

已有程序时，双击 `WpsClock.exe`。从完整源码构建时，在仓库根目录的 **Windows 命令提示符（CMD）**执行，或双击对应文件：

```bat
apps\windows\Build.cmd
```

看到 `SUCCESS` 后运行 `.local\agent\apps\windows\WpsClock.exe`；构建日志为同目录的 `build.log`。

1. 右键悬浮小窗打开设置，设置默认时长和提醒。
2. 为需要独立时长的 PPT/PDF 添加文件规则；会议主背景可设为“会议背景”。
3. 在 WPS 中开始放映，检查计时是否自动启动；F3 暂停/继续，F4 重置。
4. 从托盘菜单退出程序；关闭设置窗口仍会在后台运行。

设置保存在 EXE 同目录的 `WpsClock.settings.xml`；目录不可写时回退到 `%LOCALAPPDATA%\WpsClockPortable`。升级前退出旧程序，保留设置和自定义音频。

### 麒麟/Linux

已有适配目标系统的便携包时，解压到可写目录，双击 `WpsClock`，或在该目录的终端执行：

```sh
./WpsClock
```

如果解压丢失可执行权限，可在文件属性中开启“允许作为程序执行”，或执行 `chmod +x WpsClock`。该操作只修改文件权限，不安装程序。

首次运行打开设置；保存默认时长、文件规则和声音选项后开始 WPS 全屏放映。F3 开始/暂停/继续，F4 重置。右键悬浮窗或托盘菜单可打开设置和退出。

配置通常位于 `~/.config/WpsClock/WpsClock-Kylin/`；设置了 `XDG_CONFIG_HOME` 时使用对应目录。升级时替换程序，保留配置目录。Linux JSON 设置不自动导入 Windows XML 设置，Windows 盘符路径需要重新配置。

## 开发者：获取代码、修改和验证

以下构建命令均从仓库根目录执行。需要提交贡献但没有原仓库写入权限时，先在 GitHub 创建 Fork，并将下面的克隆地址替换为自己的 Fork 地址；已接受协作者邀请的开发者可以直接使用原仓库。

### 1. 获取源码并创建开发分支

```sh
git clone https://github.com/GigaTommy/WpsClock.git
cd WpsClock
git switch -c feature/describe-change
```

已有工作目录时先检查 `git status`，保留未提交修改，再同步代码。不要通过重置或强制推送覆盖其他修改。

### 2. 修改对应平台的模块

| 内容 | Windows | 麒麟/Linux |
| --- | --- | --- |
| 应用源码 | `apps/windows/WpsClock.cs` | `apps/linux/main.cpp`、`model.h`、`appdata.h`、`x11probe.*` |
| 构建入口 | `apps/windows/Build.cmd` | `apps/linux/CMakeLists.txt` |
| 回归测试 | `tests/windows/` | `tests/linux/` |
| 打包工具 | `tools/text-build/`、`tools/windows/` | `tools/linux/package.py` |
| 版本更新 | 根 `VERSION` 与 C# 界面版本显示 | `apps/linux/VERSION` 与 `main.cpp` 中的版本；基础版本变化时同步 CMake 项目版本 |

行为变化应补充能复现问题的测试，并更新相关使用说明与 [CHANGELOG.md](CHANGELOG.md)。两个平台的版本号独立维护；修改一个平台不需要改动另一个平台的业务代码。

### 3. Windows 构建与检查

在 Windows CMD 执行：

```bat
apps\windows\Build.cmd
tests\windows\RunTests.cmd
tests\windows\RunLayoutChecks.cmd
```

构建输出为 `.local\agent\apps\windows\WpsClock.exe`，测试程序位于 `.local\agent\tmp\windows-tests\`。布局检查需要交互式 Windows 桌面；还应按[使用指南](docs/user-guide.md)验证真实 WPS 放映、缩放、菜单、音频和多屏。

需要制作普通便携 ZIP 时，在仓库根目录的 **Windows PowerShell** 执行：

```powershell
New-Item -ItemType Directory -Force .local\agent\packages | Out-Null
Compress-Archive -Path ".local\agent\apps\windows\WpsClock.exe","LICENSE","docs/user-guide.en.md" -DestinationPath ".local\agent\packages\WpsClock-Windows.zip" -Force
```

自定义声音按需另外附带；不要把个人配置混入更新包。`tools/windows/MakeSfx.cmd` 是可选 WinRAR 自解压打包方式，普通 ZIP 无需使用它。

### 4. 麒麟/Linux 构建、检查与打包

在已备齐开发依赖、且兼容目标系统基线的 Linux 环境执行：

```sh
cmake -S apps/linux -B .local/linux-build -DCMAKE_BUILD_TYPE=Release
cmake --build .local/linux-build --parallel 2
(cd .local/linux-build && ctest --output-on-failure)
python3 -B tools/linux/package.py --binary .local/linux-build/WpsClock --out .local/agent/packages/linux
```

- 应用：`.local/linux-build/WpsClock`。
- 便携包：`.local/agent/packages/linux/WpsClock-Kylin-x86_64-<版本>.zip` 和同名 `.tar.gz`。
- 包内包含程序、使用说明、验证说明、MIT 许可证和 `SHA256SUMS.txt`，不附带系统 Qt/glibc 运行库。

**`package.py` 只封装已经编译好的程序，不负责将 C++ 源码编译为可执行文件。** 源码修改后必须重新执行 CMake 构建，不能只重新运行打包脚本。

默认 CTest 执行计时模型和配置测试。X11 集成测试使用 `-DWPSCLOCK_X11_TESTS=ON` 注册，需要隔离的 Xvfb/Openbox、xdotool 与 1280×800 测试屏；完整运行命令见 [CI 配置](.github/workflows/ci.yml)。不要在日常办公桌面运行会模拟全局按键的集成测试。其他开发细节见[Linux 开发说明](docs/linux-development.md)。

### 5. 通用检查与提交 PR

Python 工具使用 Python 3.9+ 标准库，无需第三方 Python 包。在 Linux/macOS 执行：

```sh
python3 -B tools/check_repository.py
python3 -B tools/i18n/generate.py --check
python3 -B -m unittest discover -s tools/i18n -p 'test_*.py' -v
python3 -B -m unittest discover -s tools/text-build -p 'test_text_pack.py' -v
git diff --check
git status --short
```

Windows 上若 Python 命令为 `python`，将上述 `python3` 替换为 `python`。文本包生成与完整性测试可跨平台执行，实际 Windows 程序构建仍须在 Windows 完成。

只暂存本次修改的源码、测试和文档，然后提交、推送开发分支：

```sh
git add README.md
git commit -m "docs: describe the change"
git push -u origin HEAD
```

这里以 README 修改为例，代码修改应列出实际修改文件。向 `GigaTommy/WpsClock` 的 `main` 创建 PR；Fork 贡献者从自己的 Fork 发起 PR。`main` 受保护，修改须经 PR 合并，`repository` 和 `windows` 检查为必需项；涉及 Linux 的修改还应确认 `linux` 检查通过。不要直接推送或强制推送 `main`。

## 通过文本传回内部并重新构建

文本传输的对象是**源码与构建/打包脚本**，接收端保存成文件后再执行。整个过程不需要内部电脑访问 GitHub；编译器、开发头文件及运行库需要预先具备。传输与执行使用所在环境允许的方式，构建脚本不会安装依赖或调整执行策略。

文件均应完整保存，保留扩展名和目录层级；不要混入聊天时间、序号、Markdown 围栏或自动替换的引号。Windows CMD 文件使用 CRLF，C#/C++/Python 和文档使用 UTF-8，Linux 文件名区分大小写。翻译已嵌入 C# 源码和 Linux 的 `i18n.h`，只重新构建时不需要传输 `locales/` 或运行翻译生成器。

### Windows 方案 A：单个文本文件（接收端无需 Python）

**生成端**在完整源码仓库执行：

```sh
python3 -B tools/text-build/make_text_pack.py
```

Windows 生成端可使用 `python -B tools\text-build\make_text_pack.py`。输出位于 `.local/agent/packages/text-build/`：

```text
WpsClock-2.5.0-单文件构建.txt
WpsClock-2.5.0-单文件构建.cmd
manifest.json
```

文件名随根 `VERSION` 变化。TXT 与 CMD 内容相同，内含源码与构建脚本；`manifest.json` 是校验记录，不是接收端运行的必需文件。

**Windows 接收端：**

1. 将 TXT 的全部内容传入内部，从第一行 `@echo off` 到最后一行完整保留。
2. 用纯文本编辑器保存为 `Build-WpsClock.cmd`。使用 ANSI 或 UTF-8 无 BOM 编码，确认不是 `Build-WpsClock.cmd.txt`。
3. 从托盘退出旧 WpsClock。若要迁移设置，将旧 `WpsClock.settings.xml` 放在此 CMD 旁，原音频文件保留。
4. 双击 CMD。它调用 Windows PowerShell 校验并还原源码，再调用 .NET Framework C# 编译器生成程序。
5. 看到 `SUCCESS` 后，打开 CMD 旁的 `WpsClock-<版本>-<源码摘要>/`，运行 `WpsClock-<版本>.exe`。
6. 在“版本／检测”确认版本与文件时长规则。后续日常使用只运行 EXE。

接收端不需要 Python、.NET SDK 或 WinRAR；需要已有 Windows PowerShell 与 .NET Framework 编译器。生成目录附带 `LICENSE.txt`，分发 EXE 时一并保留。新目录已有配置时不会覆盖，设置导入情况见 `BUILD-INFO.txt`；构建失败查看该目录的 `build.log`。校验失败需重新传输完整文本，不要手改载荷。详见[文本构建说明](docs/text-build.md)。

### Windows 方案 B：分别传输明文源码与构建文件

需要逐个审阅源码时，可传输以下文件的完整文本，在接收端还原目录：

```text
wpsclock/
├── LICENSE
└── apps/windows/
    ├── WpsClock.cs
    └── Build.cmd
```

在 `wpsclock` 目录的 Windows CMD 执行 `apps\windows\Build.cmd`，生成 `.local\agent\apps\windows\WpsClock.exe`。这一路径不使用单文件文本构建器，也不需要 PowerShell 或 Python。

若还需执行回归测试，另外传输 `tests/windows/` 下的测试源码与 CMD 文件。更新时替换源码并重新构建；运行新 EXE 前退出旧程序，并将原设置保留在新 EXE 同目录。

### 麒麟/Linux：按文件还原源码、构建并打包

当前没有麒麟版单文件文本构建器。将下面文件逐个以文本方式传入，并按原路径保存；也可以传输完整源码仓库的文本文件。

```text
wpsclock/
├── LICENSE
├── apps/linux/
│   ├── CMakeLists.txt
│   ├── VERSION
│   ├── main.cpp
│   ├── model.h
│   ├── appdata.h
│   ├── i18n.h
│   ├── x11probe.cpp
│   └── x11probe.h
├── tests/linux/
│   ├── model_tests.cpp
│   ├── appdata_tests.cpp
│   └── x11_tests.cpp
├── tools/linux/
│   └── package.py
└── docs/
    ├── linux-development.md
    ├── linux-user-guide.md
    ├── linux-user-guide.en.md
    └── linux-validation.md
```

`tests/linux/` 不能省略：当前 CMake 在构建应用时也会编译测试程序。`package.py` 需要 `LICENSE`、中英文使用说明、验证说明及 `apps/linux/VERSION`；仅复制 `main.cpp` 或 `package.py` 无法完成构建和打包。

**生成端可附带源码校验清单。** 在上述目录结构的源码根目录执行：

```sh
mkdir -p .local/agent/packages
sha256sum apps/linux/* tests/linux/* tools/linux/package.py docs/linux-*.md LICENSE > .local/agent/packages/linux-source-SHA256SUMS.txt
```

macOS 生成端将 `sha256sum` 替换为 `shasum -a 256`。将清单文本也保存到接收端相同位置，确保文本文件采用相同换行符。

**麒麟/Linux 接收端：**

1. 在可写目录建立上述目录结构，逐个保存 UTF-8 文本文件。不要保存为额外带 `.txt` 的文件。
2. 在源码根目录核对传输内容；提供了校验清单时执行：

   ```sh
   sha256sum -c .local/agent/packages/linux-source-SHA256SUMS.txt
   ```

3. 确认内部构建机已具备 C++14、CMake、Qt 和 X11/Xext 开发文件，以及打包所需 Python 3。只有运行库的电脑无法执行下一步；需在兼容目标系统的内部开发机完成构建，再把便携包交给使用端。
4. 在源码根目录执行：

   ```sh
   cmake -S apps/linux -B .local/linux-build -DCMAKE_BUILD_TYPE=Release
   cmake --build .local/linux-build --parallel 2
   (cd .local/linux-build && ctest --output-on-failure)
   python3 -B tools/linux/package.py --binary .local/linux-build/WpsClock --out .local/agent/packages/linux
   ```

5. 构建和测试均成功后，将 `.local/agent/packages/linux/` 中的 ZIP 或 tar.gz 解压到使用端；在解压后的版本目录执行 `sha256sum -c SHA256SUMS.txt` 核对包内文件，再执行 `./WpsClock`。
6. 按[麒麟使用指南](docs/linux-user-guide.md)确认放映识别、快捷键、会议背景、声音及多屏。升级保留原用户配置目录。

重新开发后，应重新传输修改文件并执行编译、测试、打包，不能复用旧可执行文件只更新版本文本。其他架构或 Wayland 桌面需要另外适配。

### 常见问题

| 现象 | 检查方法 |
| --- | --- |
| Windows 看不到编译器或报告被禁用 | 确认 .NET Framework 编译器可用，或在允许编译的 Windows 开发机构建 |
| Windows 仍显示旧版本 | 从托盘退出旧进程，核对实际运行的 EXE 路径；重新构建并查看 `SUCCESS` 与版本显示 |
| 麒麟 CMake 找不到 Qt、X11 或编译器 | 缺少开发工具/开发文件；运行库可用不代表具备构建环境 |
| 麒麟启动提示 `GLIBC` / `GLIBCXX` 版本不存在 | 使用兼容目标系统基线的构建环境重新编译，不能只重新压缩程序 |
| 提示无法连接显示或缺少 xcb 插件 | 在 X11 图形桌面启动，核对 Qt 平台插件与其运行依赖 |
| 校验清单不一致 | 检查缺失文件、文本截断、编码和换行变化，重新传输对应文件 |
| 有计时器但未自动识别 | 先手动计时，再按平台使用指南查看诊断；反馈前移除真实文件名与路径 |

## 仓库结构

```text
apps/windows/             Windows C# 源码及构建入口
apps/linux/               Linux C++ / Qt 源码及 CMake 构建入口
tests/windows/            Windows 逻辑回归与界面布局检查
tests/linux/              Linux 计时模型、配置与 X11 集成测试
tools/text-build/         Windows 单文件文本构建工具
tools/windows/            可选 WinRAR SFX 打包工具
tools/linux/              Linux 便携包打包工具
tools/i18n/               翻译生成器与完整性检查
locales/                  共享翻译目录（运行时无需携带）
tools/check_repository.py 提交前文件、文档链接及敏感信息静态检查
examples/                 Python/Tk、HTML 手动计时示例和 PowerShell 原型
docs/                     使用指南、架构及版本说明
.local/                   本地构建与测试输出（不纳入版本控制）
```

应用结构见[架构说明](docs/architecture.md)。[GitHub Actions](.github/workflows/ci.yml) 包含仓库预检、文本包、Windows 构建/回归/PowerShell 引导，以及 Linux 构建/模型/配置/X11/便携包检查。自动化结果不能替代物理设备和真实 WPS 的功能验收。

## 安全、贡献及授权

开发约定见 [CONTRIBUTING.md](CONTRIBUTING.md)，隐私与漏洞反馈见 [SECURITY.md](SECURITY.md)。设置、诊断、真实文档和个人音频可能包含私有信息，不属于源码传输清单。SHA-256 用于发现文件损坏，不认证发布者身份。

本项目采用 [MIT 许可证](LICENSE)。
