# WorkMate 验证与运行边界

## 环境与命令

Windows 10/11 x64、Windows PowerShell 5.1、系统 .NET Framework C# 编译器及交互式桌面。
项目准备和本地合并另需 Python 3.10+、Git 2.38+ 和 Git for Windows 自带的 sh。
不需要 npm、pip 安装或虚拟环境；不要引入共享依赖 junction。

完整命令：`python -X utf8 scripts/run_checks.py`。命令解析自身所在仓库为根，顺序执行：

1. `scripts/build.ps1`：300 帧素材质量检查、全部根目录 C# 源码和内嵌 OCR 资源编译。
2. `scripts/verify-v124-stress.ps1` 默认参数：确定性压力测试；12 次串行和 6 次并行隔离 SelfTest；损坏 JSON 恢复；40 条运行时事件风暴。
3. `scripts/verify-custom-pet-e2e.ps1`：真实四步 WPF 页面、AutomationId、下一步动作、原子导入及截图非空检查。
4. `scripts/verify-workbench-e2e.ps1`：真实工作台草稿输入、文本选区、长期选项、筛选/跨页切换、保存与 FileVersion；980×700 和 640×460 DIP 可用区下关键按钮可达并截图。DPI 来自本机实际窗口，测试可用区为隔离模拟，不修改用户显示配置。

没有独立 tests/ 测试目录；实际测试代码为 `SelfTest.cs`、`StressTest.cs`、`CustomPetE2E.cs`，另有上述 PowerShell 验证脚本。运行器每次分配新的 artifacts 子目录，避免重复 UI 测试复用数据。
各套件在独立 PowerShell 进程执行，失败返回非零，运行器立即停止。构建和测试产物均应被 Git 忽略。
旧 .NET 文件 API 仍受路径长度约束，完整运行器在用户可写临时目录分配唯一短数据根；可用 `WORKMATE_TEST_BASE` 指定另一个可写短根。每次运行的 `artifacts/.../test-roots.json` 和各套件 `data-root.txt` 记录数据根与 checkout 映射。源码、EXE、截图和报告仍属于当前 worktree，绝不共享 dist/artifacts，也不自动删除失败现场。标准长 Author worktree 必须运行完整闸门。
新增可靠性用例由每轮 SelfTest 调用 `ReliabilityChecks`：主备恢复、锁定/只读保存、统计暂停/清空/奖励、缺省授权及升级配置、日历过滤与时钟回绕。工作台交互由 `scripts/verify-workbench-e2e.ps1` 验证草稿、筛选、跨页、保存和小可用区。
CI 当前只做构建与 2 次串行/2 次并行自测，不等于完整本地闸门。

2026-09-08 初始化实测：完整命令通过，128.14 秒，其中压力与自测 88.95 秒、UI 36.12 秒。每轮 SelfTest 有 224 条 PASS；300 帧素材 0 问题，四步导航/导入/错误恢复均通过。耗时是本机该次测量，不是固定预算保证。
恢复用例曾错误地把桌宠窗口当工作台；验证脚本现按测试进程及 `custom-pet-next` AutomationId 定位窗口，保持 20 秒截止时间和原失败断言。

## 隔离

必须先在本 checkout 构建 `dist/WorkMate.exe`，不要指向旧安装 EXE。
`WORKMATE_TEST_DIR` 同时隔离 Store、事件通道、实例互斥和更新根。仅关闭验证脚本自己启动的进程。
UI 测试需要可用 Windows 桌面；截图非空与 UI 自动化通过不等于全部视觉效果已人工验收。
普通运行会读写用户 APPDATA，测试不可省略隔离变量。

## 版本与发布

`AssemblyInfo.cs`、`app.manifest` 和发布文档由单独发布任务保持一致；普通任务不抬版本。
设置页从当前 EXE 的 FileVersion 动态读取；构建门禁检查 AssemblyVersion、AssemblyFileVersion、manifest identity 与 README 当前稳定版一致。本轮将遗留 manifest 1.24 同步到已经发布的 1.26，不改变产品版本。
不自动调用 `New-UpdatePackage.ps1`、`New-UpdateSigningKey.ps1`，也不访问发布私钥。
本地 dry-run 暂时试合并并执行上述完整测试，留下 ignored artifacts，成功后恢复 Git 跟踪状态。
发布和 GitHub 同步保持原有独立授权流程；本地 PASS 不代表授权发布。
