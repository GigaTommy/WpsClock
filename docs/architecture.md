# 架构

麒麟原生实现独立位于 `apps/linux/`，见[麒麟版开发说明](linux-development.md)。以下描述保留原 Windows 实现。

Windows 应用源码位于 `apps/windows/WpsClock.cs`，使用 C# 5 和 .NET Framework。单文件源码可通过系统附带的编译器完成离线构建。

## 应用模块

| 模块 | 职责 |
| --- | --- |
| `Settings` / `SettingsStore` | XML 配置、持久化与兼容迁移 |
| `ClockState` / `TimingSession` / `BackgroundHold` | 计时、文件切换、会议背景暂停恢复 |
| `FileTiming` / `WindowFiles` / `FullscreenPolicy` | 标题清洗、规则匹配、窗口关联及放映判断 |
| `Native` / `OfficeProbe` | Windows 原生窗口检测与可选 COM 元数据 |
| `ClockWindow` / `SettingsDialog` / `Controller` | 悬浮界面、设置与控制 |

## 构建与验证

`tests/windows` 编译应用源码并执行逻辑回归或界面布局检查。`tools/text-build` 将源码封装为单文件文本构建包；`tools/windows` 提供可选的 WinRAR SFX 打包配置。

`examples` 包含 Python/Tk、HTML 手动计时器和 PowerShell 原型，功能与正式应用不同。

## 依赖

应用不依赖第三方 NuGet 包。界面使用 Windows Forms，窗口与透明绘制使用 Win32/GDI，文档元数据通过可选的 COM 接口读取，音频使用 Windows 系统组件。

文本构建工具使用 Python 3.9+ 标准库。PowerShell 引导检查需要 `pwsh`；SFX 打包需要 WinRAR。

## 参考

交互设计参考：[FlyClock 使用介绍及设置截图](https://blog.csdn.net/qq_45634338/article/details/141186362)。
