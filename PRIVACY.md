# 隐私说明

WorkMate Pro 的默认原则是：能在本机完成的能力不离开本机。

## 本机读取与保存

- 为判断工作状态，程序读取当前前台进程的**进程名**并映射为“开发工具、浏览器、沟通协作”等类别；不读取窗口标题或文档正文。
- 备忘、成长值、分类统计、文件暂存路径、设置和城市名保存在 `%APPDATA%\WorkMatePro\data.json`。
- 自定义宠物的参考照片、提示词与生成结果保存在 `%APPDATA%\WorkMatePro\CustomPets\`。
- 剪贴板 OCR 会先把图片副本写入 `%APPDATA%\WorkMatePro\OCR\Inbox\`，识别文本写入 `%APPDATA%\WorkMatePro\OCR\` 并尝试复制回剪贴板。
- 智能滚动截图只保存用户框选的屏幕像素，输出位置由用户选择。
- “叼住剪贴板文字 / 链接”只有在用户点击后才读取剪贴板；“识别剪贴板图片”同样需要用户主动操作。

这些数据没有应用层加密；能访问当前 Windows 用户文件的程序也可能读取它们。敏感工作环境应按需要关闭统计、定期清理 OCR/截图输出，并保护 Windows 账户。

## 网络请求

WorkMate 没有账号系统、广告、遥测或崩溃上报。更新机制默认只扫描本机 `%LOCALAPPDATA%\WorkMatePro\Updates\Inbox\`、程序所在目录和下载目录，不会在后台联网。只有用户点击“手动检查 GitHub”时，才会通过 HTTPS 获取本仓库最新 Release 中很小的 `update-catalog.json` 与签名文件；程序不会自动下载或安装 EXE。

启用天气/环境能力时，程序会访问以下 Open-Meteo HTTPS 服务：

- `geocoding-api.open-meteo.com`
- `api.open-meteo.com`
- `air-quality-api.open-meteo.com`

请求包含用户填写的城市名以及由地理编码得到的经纬度。和任何网络请求一样，服务端还会看到连接所必需的 IP/时间等元数据。关闭相关环境提醒或不触发刷新即可避免这类请求。

WorkMate 的自定义宠物功能只建立本地工作目录，不会代替用户把照片发送给图片模型。若用户自行把照片交给第三方生成服务，其隐私边界由该服务决定。

## 删除数据

1. 在 WorkMate 中关闭“开机启动”，或删除注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下的 `WorkMatePro` 值。
2. 退出 WorkMate。
3. 删除 `%APPDATA%\WorkMatePro\` 与 `%LOCALAPPDATA%\WorkMatePro\`。
4. 删除自己选择的滚动截图输出目录和下载的 EXE。

以上操作会永久删除相应本地数据，请先备份需要保留的备忘或自定义宠物素材。
