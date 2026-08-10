# WorkMate Pro

WorkMate Pro 是一个面向 Windows 10/11 的本地桌面伙伴。它把桌宠、轻量任务管理、专注反馈、环境提醒和实用工具放进一个便携 EXE；默认数据留在本机，不要求账号。

当前稳定版：**v1.24.0** · [下载最新 Release](https://github.com/TianLin0509/WorkMate-Pro/releases/latest)

## v1.24 能做什么

- **今日一件事**：从备忘中固定当天唯一优先项，完成、撤销与成长值奖励可逆。
- **环境共感**：按用户配置的城市获取天气、空气质量、紫外线与降雨提醒；网络失败时保留本地工作上下文。
- **能量模式**：低能量、稳稳来、精力足三档，联动自主动作频率与主动提醒门槛。
- **智能滚动截图**：先测量真实滚动位移，再用视觉内容对齐相邻画面；流式去重并输出多张接近一屏高度的独立图片，不生成超长图。也支持固定 `@0.30～@0.90` 推进比例。
- **本地 OCR**：调用 Windows `Windows.Media.Ocr` 识别剪贴板或图片文件，不上传图片。
- **离线增量更新**：公司电脑无需访问 GitHub；导入匹配当前 EXE 哈希的签名差分 ZIP，自动校验、备份、原子替换和启动健康检查，失败自动回滚。也保留全量包兜底。
- **自定义桌宠**：在本机创建照片参考项目，校验并导入 `idle / typing / happy / sleep` 四张透明 PNG。WorkMate 本身不调用外部生成模型。
- **工作陪伴**：进程类别统计、会议/演示避让、久坐提醒、专注仪式、快速记录、文件暂存架和 6 个内置角色。

## 下载与运行

1. 从 [Releases](https://github.com/TianLin0509/WorkMate-Pro/releases) 下载 `WorkMate-1.24.0.exe` 和 `SHA256SUMS.txt`。
2. 在 PowerShell 中校验：

   ```powershell
   Get-FileHash .\WorkMate-1.24.0.exe -Algorithm SHA256
   ```

3. 直接双击 EXE。它是便携程序，不需要安装；如果启用“开机启动”，会写入当前用户的 Windows Run 注册表项。

发布文件目前**没有商业 Authenticode 代码签名**，Windows SmartScreen 可能在首次运行时提醒。请只从本仓库 Release 下载并核对 SHA-256。

## 公司电脑的离线增量更新

WorkMate 不会在后台访问 GitHub。公司电脑可以完全离线更新：

1. 在可联网电脑打开最新 Release，先下载很小的 `update-catalog.json`、`update-catalog.sig`，以及目录为当前版本推荐的 `WorkMate-delta-旧版-to-新版.workmate-update.zip`。
2. 通过 U 盘或公司允许的文件通道，把这些文件放到公司电脑的 `%LOCALAPPDATA%\WorkMatePro\Updates\Inbox\`；也可把 ZIP 放在任意位置后，在“能力中心 → 离线增量更新”中选择“导入更新包”。
3. WorkMate 验证内嵌 RSA 签名、当前 EXE 的精确 SHA-256、差分载荷哈希和生成后的目标哈希。全部通过后才允许安装。
4. 独立更新器等待当前 WorkMate 正常退出，在同一目录原子替换 EXE，并保留旧版到 `%LOCALAPPDATA%\WorkMatePro\Updates\Backups\`。新版本 35 秒内未完成启动健康检查就自动恢复旧版。

每次启动只扫描本地收件箱并对同一版本最多提醒一次；“手动检查 GitHub”仅在用户点击时下载签名目录，不会自动下载 EXE。若跳过多个版本且 Release 没有对应基线的差分包，界面会明确推荐全量兜底包，不会静默套用错误补丁。

v1.24.0 是第一版带内置更新器的公开基线。从更早的本地版本首次迁移时，请手动下载并替换为 `WorkMate-1.24.0.exe`；从 v1.24.0 开始，后续 Release 才能按精确 EXE 哈希提供真正的增量包。自行重新编译或修改过的 EXE 会因哈希不同而安全回退到全量包。

## 本地数据与网络边界

- 主数据：`%APPDATA%\WorkMatePro\data.json`
- 自定义宠物与参考照片：`%APPDATA%\WorkMatePro\CustomPets\`
- OCR 输入/结果：`%APPDATA%\WorkMatePro\OCR\`
- 解压出的内嵌工具：`%LOCALAPPDATA%\WorkMatePro\Tools\`
- 离线更新收件箱、暂存与备份：`%LOCALAPPDATA%\WorkMatePro\Updates\`
- 唯一内置联网能力是 Open-Meteo 天气/空气质量查询；会向其 HTTPS API 发送用户填写的城市名及查询坐标。
- 没有账号、广告 SDK、遥测或自动上传。滚动截图与 OCR 均在本机处理。

完整说明见 [PRIVACY.md](PRIVACY.md)。

## 从源码构建

要求：64 位 Windows 10/11、Windows PowerShell 5.1+、Python 3.12，以及系统自带的 .NET Framework C# 编译器。首次重建截图工具需要联网安装已锁定版本的 Python 构建依赖。

```powershell
git clone https://github.com/TianLin0509/WorkMate-Pro.git
Set-Location .\WorkMate-Pro
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Clean -RebuildTools
```

输出：`dist\WorkMate.exe`。构建脚本会先检查 300 张逐帧素材，再从 `tools\AutoPageCapture\` 构建截图工具、计算内嵌资源哈希并编译单文件 WorkMate。

## 测试

```powershell
# WorkMate：确定性压力、串并行隔离自测、损坏数据恢复与事件风暴
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-v124-stress.ps1

# 智能滚动截图：算法与核心单测
python -m pip install -r .\tools\AutoPageCapture\requirements.txt
python -m unittest discover -s .\tools\AutoPageCapture\tests -v

# 可选：真实 Windows 截屏 + 滚轮输入 smoke（会短暂打开并关闭自己的测试窗口）
python .\tools\AutoPageCapture\tests\desktop_smoke.py
```

发布维护者可用 `scripts\New-UpdatePackage.ps1` 为精确旧版 EXE 生成 MSDelta 差分包、全量兜底包和签名目录。发布私钥只保存在发布机 `C:\VibeData\WorkMatePro\Signing\`，不得提交仓库，并应另做加密离线备份；私钥丢失后，已安装客户端不会信任新密钥签出的增量包，只能由用户手动全量换版建立新的信任根。

本地发布门禁结果记录在 [CHANGELOG.md](CHANGELOG.md)。GitHub Actions 会从干净检出重新构建并执行自动化门禁。

## 权利与第三方组件

本仓库公开源码用于透明审查和可复建发布，**当前未授予开源再使用许可**；详情见 [RIGHTS.md](RIGHTS.md)。内嵌 Python/Tk/Pillow/PyInstaller 等组件按各自许可证分发，见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

安全问题请按 [SECURITY.md](SECURITY.md) 私下报告。
