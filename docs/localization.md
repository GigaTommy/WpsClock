# 本地化 / Localization

应用提供简体中文、英文和跟随系统。显式选择优先于系统设置；自动模式在中文系统使用简体中文，其余使用英文。未知或缺失的配置值回退到自动模式，不影响旧文件规则。

## 维护翻译

共享目录为 `locales/en.json`，以中文源文本为键、英文译文为值。添加或修改界面文字时，先补充目录，再更新调用：Windows 使用 `UiText.Get(...)`，Linux 使用 `ui(...)`。

```sh
python3 -B tools/i18n/generate.py
python3 -B tools/i18n/generate.py --check
python3 -B -m unittest discover -s tools/i18n -p 'test_*.py' -v
```

生成器更新 C# 源码中带标记的翻译段和 `apps/linux/i18n.h`；这两项随源码提交。不要直接编辑生成段。接收端只重新编译时不需要 Python、JSON 目录或生成器；Windows 单文本构建继续只封装一个 C# 文件。

- 保留 Qt `%1`、`%2` 等参数、文件过滤器通配符及分隔符。
- 不对用户文件名、路径或自定义前缀做字符串替换；在输出边界翻译状态和标签。
- WPS 中文/英文窗口标题识别独立于界面语言。
- 英文通常比中文长，应验证 Windows 布局检查与 Qt 设置页，保留必要换行。
- 操作系统文件选择器及底层错误信息可能沿用系统语言。

## 扩展语言

当前内置目录为简体中文源文本和英文译文。新增语言需要增加对应 JSON 目录，并同步生成器、两端语言解析与选择器及回退测试；仅添加文件不会自动启用新语言。扩展后继续将翻译编入程序，保持便携和离线运行。

## Runtime behavior

Both applications embed translations. Select **System**, **简体中文** or **English** in the About/Diagnostics settings page and save. Language changes preserve the timer's elapsed time. Configuration stores `Language` in Windows XML and `language` in Linux JSON. Missing or unsupported values use the system default; non-Chinese system locales use English.

Document names, paths and duration rules are never translated. Native operating-system dialogs may keep the system language. Adding another language requires catalog, generator, selector and fallback-test updates on both platforms.
