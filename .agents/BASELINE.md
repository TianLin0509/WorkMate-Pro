# 2026-09-08 WorkMate 来源核对

## 当前规范项目

- 工作目录：`C:\AIWork\20260908-WorkMate-Pro`
- GitHub：`https://github.com/TianLin0509/WorkMate-Pro`
- 开发主干：`main`
- 产品基线：v1.26.0，`b4398a57c0876984f3d6408cae5361fe516179b3`。
- 本次从 GitHub 独立克隆，保留原始历史和 origin；仅整理开发流程与测试启动方式，不调整产品源码、素材和版本。
- project-prep 使用 v0.1.0，源码提交 `441d2c732a7c3718acd5b499dccf3e9c218b2925`，来源 `https://github.com/TianLin0509/project-prep`。安装的三个通用脚本与该发布版逐字节相同。

## 旧目录的关系

| 目录 | 核对结果 |
|---|---|
| `C:\Vibe\_scratch\WorkMate-Pro-public-20260809` | 真正对应 GitHub 的公开项目；main HEAD 与远端 v1.26.0 完全相同，但有 5 个未提交文件。 |
| `C:\Users\lintian\WorkMatePro` | 较早开发仓库，无 remote；master HEAD 为 `a721c13520eb937a5ce0e7e771fb7cf53b727e9c`，工作副本版本为 1.24.1.0，存在大量未提交改动。 |
| `C:\Users\lintian\WorkMate` | 早期单文件 C# / EXE 原型，无 Git。 |
| `C:\Users\lintian\WorkMate-Release` | 本机历史 EXE 与说明文件，已看到的最高具名 EXE 为 1.24.0，不代表 GitHub 最新版。 |

所有旧目录、分支、worktree 和运行数据均保留原状。此次没有移动旧项目，也没有关联旧开发仓库的 remote。

公开项目未提交文件为 `App.cs`、`CapabilityServices.cs`、`Models.cs`、`PetMotion.cs`、`SelfTest.cs`，共 229 行新增、15 行删除，涉及主体边界缓存、OCR 管道排空、网络超时、历史保留窗口及性能测试等。
这些变更未被视为已发布功能，本次没有顺手提交或合入规范项目。备份补丁与内容 SHA-256 位于本项目 `artifacts/20260908-workmate-original-uncommitted.patch` 和 `artifacts/20260908-workmate-original-dirty-hashes.json`，仅供后续单独审查。

## 初始化与后续

本次为建立工作流的本地初始化提交；在钩子启用前提交并将 main 快进至该准备结果，不声称经过独立 Merger 审查，也不使用合并位 bypass 环境变量。
初始化后启用 `.githooks`，后续功能任务按 Author / 独立 Merger 合同执行。未推送 GitHub，未发布新版本；origin/main 仍是上述产品基线。
