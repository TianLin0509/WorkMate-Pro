# WorkMate Pro v1.26.0

本版只做一件事：**完整移除智能滚动截图（AutoPageCapture）**。该功能的实现问题过多，与其继续修补，不如整体下线。

移除范围不是隐藏入口，而是从产品、二进制和源码三层一起删干净：

- 能力中心不再有“开始智能滚动截图”卡片，桌宠菜单副标题改为“OCR · 天气 · 离线更新”；
- 主 EXE 不再内嵌 `AutoPageCapture.exe`，内嵌能力工具从 2 个减为 1 个（仅本地 OCR 脚本）；
- 删除截图期间的桌宠隐藏/避让状态机，主动提醒与“今日一件事”提示不再受截图状态影响；
- 仓库删除 `tools/AutoPageCapture/` 全部 Python 源码、单测与桌面 smoke；
- 构建与 CI 不再需要 Python 3.12 及其锁定依赖，`build.ps1` 去掉 `-RebuildTools` 开关；
- 随之不再分发 Python / Tk / Pillow / PyInstaller 组件，`third_party/licenses/` 一并移除。

直接影响：EXE 体积从约 40 MB 降到约 21 MB；WorkMate 现在没有任何自动截屏能力，不会在后台抓取屏幕像素。其余功能（今日一件事、环境共感、能量模式、本地 OCR、自定义桌宠四步向导、离线增量更新、工作陪伴）均未改动。

从官方 v1.25.0 升级时，下载同一 Release 中的 `WorkMate-delta-1.25.0-to-1.26.0.workmate-update.zip`，拖到桌宠身上并确认一次即可。若当前 EXE 不是官方 v1.25.0 的精确哈希，请改用 `WorkMate-full-1.26.0.workmate-update.zip` 或完整 EXE。

发布门禁：300 张动画帧质量检查 0 问题；确定性压力（25 万次状态引擎、10 万次提醒队列、500 次原子保存、2 万次天气解析、3 万次精灵读取、自定义宠物 24 项目并发流程）0 失败；串行与并行隔离自测、损坏 JSON 恢复、40 次事件风暴全部通过。

发布 EXE 仍未做 Authenticode 商业签名，SmartScreen 可能在首次运行时提醒。请只从本仓库 Release 下载，并核对 `SHA256SUMS.txt`。
