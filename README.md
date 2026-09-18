# WorkMate Pro

WorkMate Pro 是一个面向 Windows 10/11 的本地桌面伙伴。它把桌宠、轻量任务管理、专注反馈、环境提醒和实用工具放进一个便携 EXE；默认数据留在本机，不要求账号。

当前稳定版：**v1.26.0** · [下载最新 Release](https://github.com/TianLin0509/WorkMate-Pro/releases/latest)

本文新增的显式天气/Outlook 授权、数据恢复与统计修复适用于当前 main 源码构建；现有 v1.26.0 Release 发布包尚未包含这些改进，将随下一次单独发布交付。本轮未生成或发布新 EXE 安装包。

## 能做什么

> v1.26.0 完整移除了智能滚动截图（AutoPageCapture）：功能入口、内嵌工具和源码都不再存在。从 v1.25.0 及更早版本升级后会失去该按钮；EXE 也因此从约 40 MB 降到约 21 MB。

- **今日一件事**：从备忘中固定当天唯一优先项，完成、撤销与成长值奖励可逆。
- **环境共感**：按用户配置的城市获取天气、空气质量、紫外线与降雨提醒；网络失败时保留本地工作上下文。
- **能量模式**：低能量、稳稳来、精力足三档，联动自主动作频率与主动提醒门槛。
- **本地 OCR**：调用 Windows `Windows.Media.Ocr` 识别剪贴板或图片文件，不上传图片。
- **离线增量更新**：公司电脑无需访问 GitHub；把匹配当前 EXE 哈希的签名差分 ZIP 拖到桌宠身上，验签后只需确认一次即可自动重启完成。更新过程包含备份、原子替换和启动健康检查，失败自动回滚；也保留全量包兜底。
- **自定义桌宠四步向导**：在 App 内完成身份与照片、生成指引、四姿态映射、质量校验和启用；支持草稿续办、逐张选择或拖放任意文件名 PNG、失败就地修复建议与原子导入。照片和中间文件只留在本机，WorkMate 本身不调用外部生成模型。
- **工作陪伴**：进程类别统计、会议/演示避让、久坐提醒、专注仪式、快速记录、文件暂存架和 6 个内置角色。

## 下载与运行

1. 从 [Releases](https://github.com/TianLin0509/WorkMate-Pro/releases) 下载 `WorkMate-1.26.0.exe` 和 `SHA256SUMS.txt`。
2. 在 PowerShell 中校验：

   ```powershell
   Get-FileHash .\WorkMate-1.26.0.exe -Algorithm SHA256
   ```

3. 直接双击 EXE。它是便携程序，不需要安装；如果启用“开机启动”，会写入当前用户的 Windows Run 注册表项。

发布文件目前**没有商业 Authenticode 代码签名**，Windows SmartScreen 可能在首次运行时提醒。请只从本仓库 Release 下载并核对 SHA-256。

## 公司电脑的离线增量更新

WorkMate 不会在后台访问 GitHub。公司电脑可以完全离线更新：

1. 在可联网电脑打开最新 Release，下载与公司电脑当前版本匹配的 `WorkMate-delta-旧版-to-新版.workmate-update.zip`。这个 ZIP 已内含签名清单，直接安装时不必另外下载目录文件。
2. 通过 U 盘或公司允许的文件通道把 ZIP 拷到公司电脑，然后将这一个文件拖到桌宠身上；出现“松开即可更新”后放开鼠标。
3. WorkMate 在后台验证内嵌 RSA 签名、当前 EXE 的精确 SHA-256、差分载荷哈希和生成后的目标哈希。全部通过后只显示一次“立即更新”确认。
4. 确认后独立更新器等待当前 WorkMate 正常退出，在同一目录原子替换 EXE，并保留旧版到 `%LOCALAPPDATA%\WorkMatePro\Updates\Backups\`。新版本 35 秒内未完成启动健康检查就自动恢复旧版。

不方便拖放时，仍可在“能力中心 → 离线增量更新”中导入 ZIP，或把它放入 `%LOCALAPPDATA%\WorkMatePro\Updates\Inbox\`。若希望 WorkMate 自动判断应该下载哪个文件，再同时拷入 Release 的 `update-catalog.json` 与 `update-catalog.sig`。

每次启动只扫描本地收件箱并对同一版本最多提醒一次；“手动检查 GitHub”仅在用户点击时下载签名目录，不会自动下载 EXE。若跳过多个版本且 Release 没有对应基线的差分包，界面会明确推荐全量兜底包，不会静默套用错误补丁。

v1.24.0 是第一版带内置更新器的公开基线。从更早的本地版本首次迁移时，请手动下载完整 EXE；从 v1.24.0 开始，后续 Release 才能按精确 EXE 哈希提供真正的增量包。自行重新编译或修改过的 EXE 会因哈希不同而安全回退到全量包。

## 本地数据与网络边界

- 主数据：`%APPDATA%\WorkMatePro\data.json`
- 自定义宠物与参考照片：`%APPDATA%\WorkMatePro\CustomPets\`
- OCR 输入/结果：`%APPDATA%\WorkMatePro\OCR\`
- 解压出的内嵌工具：`%LOCALAPPDATA%\WorkMatePro\Tools\`
- 离线更新收件箱、暂存与备份：`%LOCALAPPDATA%\WorkMatePro\Updates\`
- 天气/空气质量联网须先开启“设置 → 允许天气联网”，新安装城市为空，各类天气提醒及 Outlook 读取默认关闭。升级保留原有提醒配置，但需确认新增天气联网授权；手动天气刷新同样受控。
- 内置联网包括已授权的 Open-Meteo 天气/空气质量查询（发送城市名及查询坐标），以及用户主动点击“手动检查 GitHub”时获取签名更新目录。无后台 GitHub 检查。
- 没有账号、广告 SDK、遥测或自动上传。OCR 在本机处理。

完整说明见 [PRIVACY.md](PRIVACY.md)。

## 并行开发入口

本项目使用 project-prep v0.1.0 的本地工作流。实现位读取 `.agents/AUTHOR.md`，独立合并位读取 `.agents/MERGER.md`，项目配置为 `.agents/project.json`。首次克隆后执行 `git config core.hooksPath .githooks`；主目录保留给审查合并，开发在独立 worktree 进行。

完整本地闸门：`python -X utf8 scripts/run_checks.py`。除下述构建环境，还需 Python 3.10+、Git 2.38+ 和可用 Windows 桌面。它运行全部现有测试入口，详见 `.agents/TESTING.md`。CI 的缩小档不替代此闸门；普通本地合并不抬产品版本或发布。

数据保存使用同卷原子替换；损坏或缺失的主文件优先读取有效 `.bak`，恢复后的第一次保存保留原备份。恢复及保存失败会在界面提示，保存失败不会退回覆盖写。

## 从源码构建

要求：64 位 Windows 10/11、Windows PowerShell 5.1+，以及系统自带的 .NET Framework C# 编译器。构建全程离线，不需要 Python 或其他外部工具链。

```powershell
git clone https://github.com/TianLin0509/WorkMate-Pro.git
Set-Location .\WorkMate-Pro
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Clean
```

输出：`dist\WorkMate.exe`。构建脚本会先检查 300 张逐帧素材，再计算内嵌资源哈希并编译单文件 WorkMate。

## 测试

```powershell
# WorkMate：确定性压力、串并行隔离自测、损坏数据恢复与事件风暴
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-v124-stress.ps1

# 自定义宠物：真实四步 WPF 页面、AutomationId 与高 DPI 截图
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-custom-pet-e2e.ps1
```

发布维护者可用 `scripts\New-UpdatePackage.ps1` 为精确旧版 EXE 生成 MSDelta 差分包、全量兜底包和签名目录。发布私钥只保存在发布机 `C:\VibeData\WorkMatePro\Signing\`，不得提交仓库，并应另做加密离线备份；私钥丢失后，已安装客户端不会信任新密钥签出的增量包，只能由用户手动全量换版建立新的信任根。

本地发布门禁结果记录在 [CHANGELOG.md](CHANGELOG.md)。GitHub Actions 会从干净检出重新构建并执行自动化门禁。

## 权利与第三方组件

本仓库公开源码用于透明审查和可复建发布，**当前未授予开源再使用许可**；详情见 [RIGHTS.md](RIGHTS.md)。当前发布不再随包分发第三方可再分发组件，见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

安全问题请按 [SECURITY.md](SECURITY.md) 私下报告。
