# Third-party notices

WorkMate 主程序只使用 Windows 自带的 .NET Framework/WPF 与系统 API（`Windows.Media.Ocr`、MSDelta、Win32），发布的单文件 EXE 当前**不再随包分发任何第三方可再分发组件**。

智能滚动截图移除后，原先由 Python 3.12 / Tk / Pillow / PyInstaller 构建的内嵌 `AutoPageCapture` 工具已从仓库和 EXE 中删除，对应的 `third_party/licenses/` 许可证原文也一并移除。v1.25.0 及更早版本的 GitHub Release 仍按其发布当时的 notice 文件分发这些组件的许可证。

如果后续重新引入第三方组件，请在本文件恢复索引表，并把许可证原文放回 `third_party/licenses/`。
