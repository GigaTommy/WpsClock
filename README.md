# WpsClock

离线演讲计时器：检测 WPS 全屏放映，显示悬浮倒计时，为不同 PPT/PDF 设置独立时长。Windows 源码版本为 **2.4.2**，版本入口见 [VERSION](VERSION)。新增麒麟原生候选版 **2.4.2-kylin.1-rc1**，见[麒麟版说明](docs/linux-user-guide.md)与[开发验证说明](docs/linux-development.md)。

主要功能包括暂停与重置、提前及到时提醒、超时显示、多屏显示、背景透明度，以及“会议背景”文件切换时暂停、返回发言文件时继续。程序无账号、遥测或在线升级，不修改演示文档。

## 平台与依赖

| 环境 | 状态 |
| --- | --- |
| Windows 10 / 11 | 当前实现目标；需要已有的 .NET Framework 4.5+，建议 4.8；WPS 实际兼容性需实机验证 |
| PowerPoint / 独立 PDF 阅读器 | 包含窗口检测逻辑，未对所有软件版本完成实机验证 |
| 银河麒麟 V10 SP1 2203 / x86_64 / X11 | C++ / Qt 5.12 原生候选版；兼容性范围见[验证说明](docs/linux-validation.md) |
| macOS | 可运行 Python 工具和文本包完整性检查，不能原生运行当前 Windows 程序 |

Windows 版使用 Windows Forms、Win32、GDI 和可选 COM 自动化；源码编译需要 .NET Framework C# 编译器。Linux 版使用 Qt 5 和 X11，构建与运行依赖见[开发说明](docs/linux-development.md)。

## 快速开始（Windows）

下载完整源码并解压到可写目录，在仓库根目录执行，或双击对应脚本：

```bat
apps\windows\Build.cmd
```

看到 `SUCCESS` 后运行 `.local\agent\apps\windows\WpsClock.exe`。关闭设置窗口后程序仍在托盘运行；退出请使用托盘菜单。编译失败查看同目录的 `build.log`，不要误用旧 EXE。

1. 右键悬浮小窗打开设置，设置默认时长和提醒。
2. 为需要独立时长的 PPT/PDF 添加文件规则；会议主背景可设为“会议背景”。
3. 开始 WPS 放映，检查计时是否自动启动；F3 暂停/继续，F4 重置。

更新前先从托盘退出旧程序。设置默认保存在 **EXE 同目录**的 `WpsClock.settings.xml`；升级或迁移程序目录时需保留该文件。目录不可写时回退至 `%LOCALAPPDATA%\WpsClockPortable`。设置和诊断可能含实际文档路径，不应上传 GitHub。

完整操作与 Windows 验收清单见 [使用指南](docs/user-guide.md)；版本变化见 [更新记录](CHANGELOG.md)。

## 仓库结构

应用模块与依赖见 [架构说明](docs/architecture.md)。

```text
apps/windows/            Windows C# 源码及构建入口
apps/linux/              Linux C++ / Qt 源码及 CMake 构建入口
tests/windows/           Windows 逻辑回归与界面布局检查
tests/linux/             Linux 计时模型、配置与 X11 集成测试
tools/text-build/        单文件文本构建生成器、PowerShell 模板和完整性检查
tools/windows/           可选的 WinRAR SFX 打包工具
tools/check_repository.py 提交前文件、文档链接及敏感信息静态检查
examples/manual-timers/  Python/Tk 与 HTML 手动计时示例
examples/legacy-windows/ PowerShell 原型
docs/                   使用指南、架构及版本说明
.local/                 本地构建与测试输出（不纳入版本控制）
```

## 验证与开发

Windows 完整逻辑回归与可选 GUI 检查：

```bat
tests\windows\RunTests.cmd
tests\windows\RunLayoutChecks.cmd
```

第二项需要交互式 Windows 桌面。输出位于 `.local\agent\tmp\windows-tests`。测试通过不等于真实 WPS、多屏、缩放、快捷键和音频兼容性全部通过。

[GitHub Actions](.github/workflows/ci.yml) 配置了 Linux 上的仓库与文本完整性检查，Linux 应用构建及隔离 X11 测试，以及 Windows 上的应用构建、逻辑回归和 PowerShell 引导检查。GUI 布局与 WPS 放映需要在交互式桌面中验证。

Python 3.9+ 工具检查，不需安装第三方 Python 包：

```sh
python3 -B -m unittest discover -s tools/text-build -p 'test_text_pack.py' -v
python3 -B tools/check_repository.py
```

生成单文件文本包：

```sh
python3 -B tools/text-build/make_text_pack.py
```

产物为 `.local/agent/packages/text-build/` 中的 `.txt`、`.cmd` 和 `manifest.json`。版本来自 `VERSION`，载荷来自 `apps/windows/WpsClock.cs`。使用方式见 [文本构建说明](docs/text-build.md)。可选真实 PowerShell 引导验证需要已安装 `pwsh`，执行 `python3 -B tools/text-build/test_bootstrap.py`；它不能替代 Windows 构建。

## 安全、贡献及授权

开发约定见 [CONTRIBUTING.md](CONTRIBUTING.md)，隐私与漏洞反馈见 [SECURITY.md](SECURITY.md)。

本项目采用 [MIT 许可证](LICENSE)。
