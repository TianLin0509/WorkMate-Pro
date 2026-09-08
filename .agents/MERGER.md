# Merger：WorkMate 独立审查与本地合并

先读 `AGENTS.md`、`.agents/project.json`、`.agents/TESTING.md`、`README.md`；遵守既有 `PRIVACY.md`、`RIGHTS.md`、`SECURITY.md` 和发布边界。
Author 不能在同一 Agent 会话兼任本任务 Merger。只在亲自验证 PASS 后合并，而且只能合并亲自审过的完整 SHA。

1. 核对用户需求和 Author 候选完整 SHA、基线完整 SHA。读取 `git diff main...TASK_BRANCH`，实际检查重要行为，不凭 Author 报告批准。
2. 核对 `git rev-parse main`：主干与交付基线不一致时要求 Author 整合最新主干、重新自测，随后重新审查。不得把移动后的分支沿用旧结论。
3. 主工作目录必须干净、位于 `main`，并且没有生产进程从该目录运行。自己的验证也必须使用隔离数据，不能启动或退出用户真实桌宠。
4. 在主工作目录用真实完整 SHA 运行下列命令；两次均执行完整构建、压力、自测和四步 UI，需可用 Windows 桌面。

```text
python scripts/merge_task.py TASK_BRANCH --expected-head FULL_CANDIDATE_SHA --expected-trunk FULL_TRUNK_SHA --dry-run
python scripts/merge_task.py TASK_BRANCH --expected-head FULL_CANDIDATE_SHA --expected-trunk FULL_TRUNK_SHA
```

dry-run 会临时试合并并生成 ignored 测试产物；通过且没有未知变化时恢复 Git 跟踪状态。正式合并仍需先有独立审查 PASS。
若候选修改测试配置，本次脚本使用旧主干配置，必须额外执行新配置后才可批准。

退出码：0 为成功或候选已包含（看实际输出）；1 为验证失败且已知试合并已撤销；2 为前提失败或冲突/未知变化保留；3 为已有提交但后置检查失败。遇到 2/3 先检查现场，不能盲目 reset、abort、stash 或提交他人文件。

脚本不 fetch、push、rebase，也不抬 C# 版本、不运行签名发布。远端同步和 Release 必须另有用户授权；本地 PASS 不代替发布批准。

最终使用以下固定标签，并写实际依据：

```text
RESULT: PASS 或 REVISE
BLOCKERS: 无或具体阻塞位置与修订要求
VERIFIED: 亲验命令、结果、候选/主干完整 SHA；已合并则附合并 SHA
NEXT: 由谁做什么；完成则写本地合并完成
NOTES: 作出判断的事实依据
```
