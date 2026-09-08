# WorkMate Pro 项目约定

本仓库是 Windows WPF / .NET Framework 桌面伙伴，主干为 `main`。

- 项目合同：实现位先读 `.agents/AUTHOR.md`；独立合并位先读 `.agents/MERGER.md`。
- 机器配置：`.agents/project.json`；完整验证：`python -X utf8 scripts/run_checks.py`。
- 项目知识：`README.md`、`PRIVACY.md`、`RIGHTS.md`、`SECURITY.md`、`.agents/TESTING.md`。
- 主工作目录只用于验证与已审查合并；任务在独立 worktree 开发。只暂存本任务文件。
- 其他 Agent 可能同时工作，不覆盖或撤销他人修改，不清理已有分支或 worktree。
- 测试必须使用本 worktree 构建的 EXE、独立 `WORKMATE_TEST_DIR`，不操作用户正在运行的桌宠或真实数据。
- 不关闭或重启生产 AI Hub。不得从主目录启动长期服务后再执行试合并。
- 私钥、个人数据和更新备份不得入库；保留既有隐私、权利及发布规则。
- 产品版本只在单独的发布任务中调整；整理工作流不抬版本、不生成签名包、不推送远端。
- 仓库内报告写 `artifacts/`，文件名带日期和任务标识；工作树不共享 `dist/` 或 `artifacts/`。
- 本文件与 `CLAUDE.md`、`GEMINI.md` 正文保持一致，修改时三份同步。
