# Security Policy

## Supported version

当前只维护最新 GitHub Release（现为 v1.25.x）。旧版本发现安全问题后应先升级再复现。

## Reporting a vulnerability

请不要在公开 Issue 中披露可利用细节、个人数据或未修复漏洞。优先使用本仓库 GitHub Security 页面中的 **Report a vulnerability** 私下提交，包含：

- 受影响版本和 Windows 版本；
- 最小复现步骤与影响；
- 相关日志或截图（先移除个人数据）；
- 如有，建议的修复方向。

普通功能 bug 可以提交公开 Issue。

## Release trust

发布 EXE 当前未做 Authenticode 商业签名。首次全量下载时，只从本仓库 Release 获取并核对同一 Release 中的 `SHA256SUMS.txt`，不要运行第三方重新打包版本。

应用内离线更新使用独立的 RSA-3072 发布密钥：公钥固化在客户端，私钥不进入仓库。`manifest.json` 与 `update-catalog.json` 均使用 SHA-256/RSA 分离签名；差分包还必须匹配当前 EXE 的精确 SHA-256，生成后的目标 EXE 也会再次校验。签名、路径、大小、版本或任一哈希不符时，更新会在替换前终止。

更新器不申请管理员权限，也不会替换其他程序。安装阶段先保留旧版备份；若新版本未通过启动健康检查，会恢复旧 EXE 并保留失败副本用于诊断。
