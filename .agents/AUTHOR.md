# Author：WorkMate 实现与自测

先读 `AGENTS.md`、`.agents/project.json`、`.agents/TESTING.md`、`README.md`；涉及数据、网络或发布时再读 `PRIVACY.md`、`RIGHTS.md`、`SECURITY.md`。

1. 确认用户本次任务与验收范围。其他 Agent 可能同时工作，不覆盖或撤销他人修改。
2. 在主库核对 `git status --short`、`git worktree list --porcelain`、`git rev-parse main`。主干更新后以新的完整 SHA 为基线。
3. 从主库执行 `git worktree add ../20260908-WorkMate-Pro-worktrees/YYYYMMDD-TASK-SEAT -b task/YYYYMMDD-TASK-SEAT main`；将 TASK、SEAT 替换为实际任务和席位。全部实现与提交在新 worktree 进行。
4. 无需安装依赖。每个工作树单独编译自己的 `dist/WorkMate.exe`；不共享构建目录，不指向旧安装包。Python/Git 只用于工作流，WPF 编译器来自系统 .NET Framework。
5. 自测必须执行 `python -X utf8 scripts/run_checks.py`，包含完整默认 12 次串行、6 次并行以及真实四步 UI；不能拿较小的 CI 档替代。UI 变化另提供自己的视觉检查证据。
6. 普通功能任务不修改产品版本；版本、签名更新与发布属于单独发布任务。不得读取签名私钥或操作用户的真实桌宠数据。
7. 只暂存本任务文件并提交，报告候选完整 SHA、所基于主干完整 SHA、命令/耗时/结果、风险。若主干已前进，保留现场并在自己的任务分支完成必要整合与重测，再交独立合并位。
8. 默认不 push、不创建 PR；需要远端同步时另按用户授权执行。Author 不在同一会话自审、自合并，也不设置合并专用 bypass 变量。

最终固定四行，长证据放 `artifacts/` 的日期任务文件：

```text
PROGRESS: 完成内容、任务分支、候选完整 SHA 和主干完整 SHA
VERIFIED: 实际验证命令、覆盖、耗时与结果
RISK: 已知风险、未验证项或无已知阻塞
REPORT: 可打开的报告绝对路径；无文件则写本消息
```
