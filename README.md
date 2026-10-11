# Crate Garden · 箱庭推箱子

基于 Unity 2022.3 的桌面推箱子游戏，包含完整关卡流程和内置关卡编辑器。原创 Blender 渲染美术与合成音效，无需外部素材下载。

## 快速运行

1. 在 Unity Hub 中打开 `My project` 文件夹。
2. 使用 **Unity 2022.3.62f3c1**（当前实际验证版本）。题目推荐的 2022.3.51f1 属于同一 LTS 系列，但未在该精确版本上重新测试。
3. 打开 `Assets/Scenes/CrateGarden.unity`，点击 Play。
4. 本机已生成 `Builds/Crate Garden.app`，可直接启动。构建文件不纳入 Git。

![游戏主菜单](Docs/Screenshots/home.png)

## 功能

- 12 个原创关卡，逐步引入转向、双箱、多箱及障碍；全部通过求解和动作回放。
- 主菜单 → 选关/继续 → 游玩 → 通关结算 → 下一关；所有关卡均可自由选择。
- WASD / 方向键移动；Z 撤销，Y 重做，R 重开，Esc 暂停。
- 步数、推箱次数、目标完成数、最佳成绩与完成进度保存。
- 平滑移动、推箱音效、音效开关、角落死锁提示、基于当前局面的下一步提示。
- 完整运行时编辑器：墙、地板、目标、箱子、玩家；绘制撤销、结构检查和有预算的可解性检查。
- 自制关卡命名、保存、加载、试玩、返回编辑、JSON 导入/导出与剪贴板分享。
- 存档原子替换与备份恢复，保存失败有可见提示。
- 固定设计画布按窗口等比缩放，宽屏保留边距。

## 编辑器使用

主菜单选择 **Level workshop**。左键按所选笔刷绘制，右键清空格子；边框保持为墙。目标格上再放箱子或玩家，会保留底层目标。新画布为 10×8，导入格式支持宽 3–20、高 3–16。

- **Validate** 检查结构并尝试求解。求解超过 30,000 个状态时，明确提示需手动试玩，不会宣称可解。
- **Playtest** 从当前草稿进入独立试玩，返回时保留编辑内容；试玩不计入正式成绩。
- **Save to collection** 保存有效关卡，在选关页 **Your creations** 中游玩。
- **Export / Import** 交换 JSON。导入会生成新 ID，不覆盖已有收藏。
- 新建或离开未保存草稿时有确认；加载和导入界面也明确提示会替换草稿。

JSON 行字符：`#` 墙，空格地板，`.` 目标，`$` 箱子，`@` 玩家，`*` 目标上的箱子，`+` 目标上的玩家。每行等宽、边框封闭、恰好一个玩家、箱子与目标数量相等且至少一对。

## 验证与构建

Unity 顶部 **Crate Garden** 菜单：

- **Prepare Main Scene**：重建游戏入口场景并配置构建列表（会替换当前打开场景，请先保存自己的修改）。
- **Run Acceptance Tests**：运行规则、全关卡求解回放、存档及 JSON 测试，结果写入 `Docs/TEST_RESULTS.md`。
- **Smoke Test Play Mode**：进入 Play 后调用，实际完成第一关并检查胜利状态。
- **Build macOS**：生成本机桌面应用，结果写入 `Docs/BUILD_RESULT.md`。

已验证：202 项断言通过；macOS 构建 0 错误、0 警告；Play 模式和独立应用启动成功。详见 [测试报告](Docs/TEST_RESULTS.md)、[人工验收记录](Docs/ACCEPTANCE.md)。

## 源码结构

| 文件 | 职责 |
| --- | --- |
| `Runtime/Puzzle.cs` | 无 Unity 依赖的确定性棋盘规则、历史栈、死角检测、BFS 求解、内置关卡 |
| `Runtime/CrateGardenGame.cs` | 游戏状态流程、输入、IMGUI 界面、绘制、音效、关卡编辑器 |
| `Runtime/SaveStore.cs` | JSON 数据、最佳成绩、用户关卡、原子保存及备份恢复 |
| `Editor/ProjectTools.cs` | 入口场景生成、自动验收、构建、Play 模式冒烟检查 |
| `Editor/McpStartup.cs` | 编辑器本地 Unity MCP 桥接启动，不参与 Player 构建 |

以上路径均位于 `My project/Assets/Sokoban/`。仓库含 `Assets`、`Packages`、`ProjectSettings` 及全部 `.meta`。`Library`、`Temp`、日志、用户设置及构建产物均已忽略。

## 存档与已知边界

存档位于 `Application.persistentDataPath`；本机通常为 `~/Library/Application Support/Garden Studio/Crate Garden/`，包含 `progress.json`、`levels.json` 及 `.bak`。编辑器和独立版本共用该产品目录。退出/重开关卡会重新开始该局，但完成进度和最佳成绩保留。

当前界面为英文，文档为中文。目标平台为桌面键鼠，未实现触屏/手柄、本地化切换或移动平台构建。提示求解和死角提示不是完整死锁检测器；预算耗尽时提供通用建议。当前已实测 macOS，其他桌面目标可通过 Unity Build Settings 构建，但未在其他系统实机验收。

## GitHub 仓库

https://github.com/jifeng-2025/crate-garden

源码包括 Unity 工程、Blender 原始模型、渲染脚本和验收记录。`Library`、构建产物和本地存档不纳入仓库。首次拉取后 Unity 会重建缓存；Unity Registry 包由 Package Manager 按锁文件还原。

## Blender 美术源文件

`ArtSource/GardenAssets.blend` 包含独立的箱子、已送达箱子、石墙及机器人模型集合。Unity 使用 `Assets/Sokoban/Resources/GardenArt` 下四张透明 PNG，运行或导入工程均不需要安装 Blender。

复现渲染（脚本会从自身路径定位仓库，也可设置 `CRATE_GARDEN_ROOT`）：

```sh
blender --background --python Tools/build_garden_art.py
```

脚本创建独立场景，不删除已有场景；模型采用倒角、材质与柔光，输出 256×256 RGBA 图像。Unity 导入器关闭有损压缩和 mipmap，减少小尺寸透明边缘杂色。若图像资源缺失，绘制代码仍保留基础图形回退。

Unity MCP 为随工程保留的 MIT 授权编辑器插件，详见 [第三方说明](Docs/THIRD_PARTY.md)。游戏运行和构建产物不要求 MCP 服务在线。
