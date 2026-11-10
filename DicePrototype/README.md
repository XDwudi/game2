# 骰子构筑 P0 · v0.71

当前表现版本为 `art0.7.1`，发行身份 `p0.7.1`；玩法配置仍为 `p0.6.0`、存档 `schema4`。目标 Windows x64 与 macOS Universal，验收进度见 [STATE](../docs/STATE.md) 和 [v0.71 交付说明](../docs/DELIVERY_V071.md)。本次全量源码41aface完整Edit1018/Play182通过，双构建与Windows实际运行完成。Windows性能/独占全屏仍失败，Mac及人工待验；[GitHub测试预发布](https://github.com/XDwudi/game2/releases/tag/p0.7.1-rc.1)已提供三个成品附件，完整远端下载与新目录启动已核，准确结论与下载见交付说明。

## 运行与操作

本版构建目标为仓库根 `Builds/v071/Windows/DicePrototype.exe` 和 `Builds/v071/macOS/DicePrototype.app`。Windows 必须保留 Data 目录、UnityPlayer.dll 和全部运行文件；Mac 保留完整 `.app`。本次全量包使用 `Releases/p0.7.1/full-art-candidate/` 子目录；首批 `review-candidate/` 为历史，实际是否已生成/发布及 SHA 以交付说明为准。旧 `Builds/v06/`、`Builds/v07/` 和旧 Release 是历史产物。

主菜单“新建挑战”打开种子与初始状态确认页；种子支持 0—4294967295 的十进制整数。“继续对局”恢复最后一次成功写入的状态；已有存档时新建需经过覆盖确认。固定骰练习使用独立状态，不覆盖正式挑战。

默认键位以 `GameApp.Art07Settings.cs` 为准，玩家修改后以设置页为准：

| 动作 | 默认操作 |
| --- | --- |
| 投掷 / 重投 | R 或投掷按钮 |
| 切换当前可见骰子的待选 | 点击骰子，或 1—9、0 |
| 确认锁定 | L；确认后本手不能直接解锁 |
| 提交已锁骰 | Enter |
| 清除待选 | Backspace |
| 牌型手册 | H 或左侧手册按钮 |
| 暂停 / 关闭最上层弹窗 | Esc；有未确认显示变更时为回退 |
| 查看骰子详情 | 右键骰子；详情预览可拖动旋转 |
| 调整已锁骰顺序 | 拖动与已有栏位交换，前 n 栏保持连续 |

操作顺序为投掷 → 点选 → 确认锁定 → 提交。待选尚未处理时，提交、投掷及道具操作会被拒绝。输入框和模态窗口拦截底层快捷键；数字选骰和 Esc 保留。查看逐骰明细或“上一手”冻结快照可核对永久复制/染色的目标与实际效果。

达标后且仍有提交次数时可继续；“结束本轮”处理本关奖励，再进入商店。待选或动画忙碌时不能结束。共 24 关，第 2、5、8、11、14、17、20、23 关可选高压；最后一关结束进入整局结算。

商店上方是控制器货架，下方为道具、研习、骰子强化、局外服务、骰柜五个分区。购买详情显示规则、附魔、普通占槽、总持有与实际净价；容量不足时购买入口先打开限制说明，不绕过最终购买条件。控制器和未用道具通常出售得 1 金币，基金控制器保留按累计基金计算的售价例外。控制器可在商店/关卡预览拖拽插入排序，关内冻结。刷新只重抽控制器，基础首次 3 金币、每次 +2；免费首刷服务沿用现有规则。买骰不改变提交容量 K，C21/C22 改变 K 但不增加骰子。

手册包含 14 行牌型（含散点）及独立全红/全蓝、红组乘区，显示升级次数、倍率与可达条件。设置提供声音、画面、操作与辅助三个页签，支持演出速度、减少动态、文字缩放、颜色辅助、桌面震动与自定义快捷键。结果在表现前提交存档；跳过表现不重新抽样。

## 显示设置、调试与存档

显示设置支持显示器、分辨率、全屏/无边框/窗口和实际可用的窗口尺寸。应用后回读实际结果，15 秒内确认；超时、Esc 或未确认退出后重启恢复已确认设置。它独立保存，不进入挑战 RNG 或动作序列。当前主机原生独占是否通过必须看实际运行证据。

设置中精确输入 `XD is god` 后开启本次进程的组合试验入口。工作区克隆独立 Run，使用 `debug-run.json`，不覆盖普通挑战。可编辑最多 32 颗骰子/控制器/道具、永久六面、当前结果和提交顺序、附魔、动态状态、牌型/颜色等级、资源及指定随机结果。修改先留在草稿，应用时执行；错误输入不部分保存。可导出/导入复现 JSON。

调试列表可滚动，骰池和锁定栏可分页。既有超常 Debug32 的对局 HUD 只显示前五件道具，其余仍可在组合试验“道具与服务”库存页管理；该限制不代表丢失数据，也不是普通五槽容量的验收结论。

默认存档目录是 Unity `persistentDataPath`：Windows 通常为 `%USERPROFILE%/AppData/LocalLow/XDwudi/DiceCraft P0`；Mac 通常为 `~/Library/Application Support/XDwudi/DiceCraft P0`，实际路径可在暂停页查看。`active-run.json` 带 schema/config 和 SHA-256；成功写盘保留备份，损坏原件保留 rejected 副本。读档校验结构、冻结历史得分和规则不变量；失败回退有效备份，双损坏不覆盖原件。v0.4/schema2、v0.5/schema3 不迁移，原档和备份保留 legacy 副本。

IO 失败时保留同一候选快照，重试不重复投掷、扣费或奖励。`normal-actions.jsonl`、`debug-actions.jsonl` 分别记录操作；日志写入失败不应阻断已经保存的动作。

## 开发环境与美术样板

首次恢复源码建议 `git clone --config core.autocrlf=false https://github.com/XDwudi/game2.git`，保留已提交行尾。字节哈希依赖实际源内容，不沿用另一工作区的身份与测试绑定。

- Unity 6000.6.7f1；Windows Mono x64/D3D11、Mac Mono Universal/Metal；URP 17.6.0、uGUI/TMP 2.6.0、Input System 1.20.1。精确依赖见 `Packages/packages-lock.json`。
- Blender 5.2.3 LTS；新母模型 `../ArtSource/Art071/Dice/dice_master.blend`，可重跑源脚本 `../tools/art_v071/build_dice.py`。
- 首批大插画源在 `../ArtSource/Art071/Illustrations/`，公共表面源在 `../tools/art_v071/build_surfaces.py`；运行导出在 `Assets/Art/Art071/`。A01—A18 映射见 [素材表](../docs/specs/v071/ASSET_MAP.md)，它不是“18 张全新图片”清单。
- 只替换 C48/C29/C03/I07 四项；其余 67 项旧图标继续使用，未计为新插画交付。插画装饰点数不作规则依据，动态点数/颜色/文字/价格仍由引擎显示。
- Unity Pipeline 0.9.1-exp.1 用于开发自动化；中文字体 Noto Sans CJK SC（SIL OFL），许可证保留在 `Assets/Game/Fonts`。

用指定 Editor 打开本目录，普通开发入口为 `Assets/Game/Scenes/Bootstrap.unity`。v0.71 已接入的资源不需要每次打开时重装；需要主动重接时用 `DiceCraft/Art071/Integrate print art and style lab`。该入口会修改 Bootstrap、导入设置、材质并重建 Lab，须先保存当前场景并串行执行；不要用旧版本整套重建入口覆盖当前表现层。

`Assets/Game/Scenes/ArtStyleLab.unity` 是同资源、同组件的隔离样板。启动时设置独立 `../cache/v071/lab-saves/<UTC时间戳>/`，创建种子 71071 的测试 Run 和调试克隆，布置三控制器＋一个空位、一道具、三池骰（含一待选）和两锁骰。它使用真实命令，结束时恢复先前 SaveDirectoryOverride；**不是正式挑战、正常胜率或性能样本**。两份生产 BuildProfile 仅引用 Bootstrap，不把 Lab 当作玩家开局场景。

## 测试与构建

在 Unity Test Runner 分别执行完整 EditMode/PlayMode；`Editor/TestEvidence` 将本版报告保存到 `../evidence/v071/unity/`，并绑定实际发现清单、程序集与源哈希。先冻结源并生成本机身份，再实际运行完整测试，取得独立 PDB 核查，最后生成 gate。`--expect-*-count` 使用本轮完整发现数，不复用历史 1013/158 或最低阈值作为当前通过数。

仓库根工具入口：

```text
python tools/v071_delivery.py identity
python tools/v071_delivery.py gate --edit <实际EditMode XML> --play <实际PlayMode XML> --pdb <独立PDB报告> --expect-edit-count <本轮Edit发现数> --expect-play-count <本轮Play发现数>
```

identity 是源冻结后的派生文件；测试与构建必须绑定导入该身份后的实际程序集。修改源、资源或规则后，相关旧证据不再代表当前版本。

构建使用 `Assets/Settings/BuildProfiles/Windows_x64.asset` 或 `macOS_Universal.asset`，菜单 `DiceCraft/Build Active Verified Profile`。两个 Editor 构建进程串行，不并发写同一工程：

```text
Unity -batchmode -projectPath <绝对工程目录> -activeBuildProfile Assets/Settings/BuildProfiles/Windows_x64.asset -executeMethod DiceCraft.Editor.PrototypeBuild.BuildActiveProfile -logFile <绝对日志路径> -quit
```

Mac 替换为 `macOS_Universal.asset`。成功构建仅证明构建完成，Mac 交叉构建不代表 M5/Intel 原生运行。

Windows 实际验证用 `../tools/run_v071_windows_player.py`；默认仅打印计划，提供实际 `--build-report`、`--expected-build-guid`、新的 `--evidence-root evidence/v071/...`、`--raw-root artifacts/cache/v071/...` 并加 `--execute` 才启动。运行器保留 strict、独立 deferred/native-only 和跨进程恢复边界，局部结果不能拼成 strict 通过。

本版只读复用 `../evidence/v07/session/normal-run-trace.json` 与 `debug-24-trace.json`。正常 trace 是 seed 20261031 的 145 条合法命令，预期第 8 关失败，不能称为正常 24 关通关或胜率证据；独立 Debug24 通过真实命令覆盖完整关卡/商店路线。Mac 工具在 `../tools/macos/v071/`，实机流程见 [Mac 验证任务](../docs/tasks/P0-V071-MAC-VERIFICATION.md)。

Core/Rules、Core/Session 保持无 Unity 依赖的确定性 C#，UI 消费语义命令和冻结快照，DiceTable 只呈现结果。配置与黄金例不得通过改预期来消除测试失败。现有帧调度器的依据见 [FRAME-PACING](../docs/decisions/P0-FRAME-PACING.md)，是否满足性能预算以本轮真实采样为准。

当前开发中已有 Editor 图片与样板录制；最终全量测试、双构建、Windows 实际启动、Mac、人工风格/体验和移动端结论须分别查实际证据。旧性能、原生独占、AudioMixer 等未验/失败项不会因本次美术版本自动关闭。
