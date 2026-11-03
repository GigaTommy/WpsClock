# 开发与提交约定

Windows 应用位于 `apps/windows`，对应测试位于 `tests/windows`。工具脚本位于 `tools`，文档位于 `docs`。

- Windows 保持 C# 5 / .NET Framework 兼容；Linux 保持 C++14 / Qt 5.12 基线。
- Python 工具使用 Python 3.9+ 标准库。CMD 使用 CRLF，中文 C#、Markdown 和 Python 使用 UTF-8。
- 使用虚构文档名、目录及组织名作为测试数据，不提交客户资料、个人设置、真实诊断或办公文件。
- Windows 版本同步根 `VERSION` 与应用显示；Linux 版本同步 `apps/linux/VERSION` 与 `main.cpp`。同时更新中英文 README 与更新记录。生成包不进入 Git；不要只修改打包器中的版本。
- 运行 README 中适用的检查。涉及界面、全屏、音频、热键或 WPS 检测，补充目标 Windows/WPS 版本和实机结果；没有运行的项目明确标为未验证。
- PR 描述应说明问题、最终行为、验证方法和已知限制；构建输出和第三方工具留在 `.local/`。

提交前检查：

```sh
python3 -B tools/check_repository.py
python3 -B tools/i18n/generate.py --check
git status --short --untracked-files=all
git diff --check
git diff --cached --check
git diff --cached --stat
git diff --cached
```

新增 `.gitignore` 不会让已跟踪的文件自动退出版本控制。若检查发现已跟踪敏感文件，需要从索引移除，并评估历史清理和凭证轮换；不要仅补一条忽略规则。

贡献的代码与文档按本项目的 [MIT 许可证](LICENSE) 发布。引入第三方代码时保留其版权与许可证声明。

翻译维护见[本地化说明](docs/localization.md)。修改共享目录后运行生成器并提交生成源码；使用端不需要翻译开发工具。
