# P0-V072-ART：v0.72 高清简约美术与动画

状态：REVIEW（2026-11-11；rc.2 自动验证与交付完成；独立复核未做，风格与体验待用户判定，macOS 实机未验）
规格：[v0.72 开发方案与美术规格](../specs/v072/ART_SPEC.md)
输入版本：源 `944adf6`（分支 `codex/v0.71`），玩法 p0.6.0 / schema4，Unity 6000.6.7f1。
工作分支：`codex/v0.72`。发行身份 p0.7.2 / art0.7.2。

## 目标

按用户 2026-11-11 指令优化表现层并交付试玩版。像素方向已在同日被用户否决（“完全是糊了”），
本任务按高清、锐利、简约方向实施。

## 范围

- 新增 `tools/art_v072/`、`DicePrototype/Assets/Art/Art072/`。
- 修改表现层：`Assets/Game/UI/`、`Runtime/Shaders/Art07Felt.shader`、`Editor/`、Bootstrap / ArtStyleLab 的资源绑定。
- 版本身份、交付工具与文档适配 v072。

## 非目标

- 不改 `Core/Rules`、`Core/Session`、`P0Config.json`、`GoldenScores.json`、存档结构、布局矩形与热区。
- 不重画 71 张插画、符号与特效贴图；不改骰子模型（Blender 未调用）。
- 不引入需要下载的第三方资源；不做移动端验证。
- 不处理历史遗留的独占全屏、帧时间、AudioMixer、Mac 实机缺口（保留为已知问题）。

## 验收条件

| ID | 条件 | 方法 | 期望 | 证据 | 责任 |
| --- | --- | --- | --- | --- | --- |
| V72-A | 素材可复现 | 重跑 `tools/art_v072/build_art.py` 并比对 SHA | 输出与已提交文件逐字节一致；规格第 3 节名称齐全 | `evidence/v072/assets/verify.json` | 主 agent |
| V72-B | 实机画面锐利、风格一致 | 开发构建截图巡览，目检主菜单 / 对局 / 商店 / 结算 | 无模糊缩放件；同类组件一致 | `evidence/v072/screens/` | 主 agent；风格由用户判定 |
| V72-C | 玩法未变 | 完整 EditMode | 0 失败；Config / Golden SHA 与 `944adf6` 相同 | `evidence/v072/unity/` | 主 agent |
| V72-D | 界面可用 | 完整 PlayMode | 0 失败；因美术变更而修改的断言逐条列入“断言变更” | `evidence/v072/unity/`、本文件 | 主 agent |
| V72-E | 动画可关闭 | 巡览的减少动态运行 | 减少动态时卡牌与字标静止 | `evidence/v072/screens/reduced/` | 主 agent |
| V72-F | 可试玩构建 | 门禁后的 Windows x64 构建 + 新目录启动 | 构建成功；启动进入主菜单并能进行对局 | `evidence/v072/unity/build-*.json`、`evidence/v072/packages/`、`evidence/v072/screens/` | 主 agent |
| V72-G | 交付 | 推送 + 发行附件 + 远端回读 | 分支与标签远端可见；附件 SHA 与本地一致 | `evidence/v072/delivery/` | 主 agent |

本任务的独立复核与人工风格 / 体验判断尚未进行；交付时状态为 REVIEW，不是 ACCEPTED。

### rc.2 追加验收（用户 2026-11-11 试玩 rc.1 后的要求）

用户原话：“先做人工风格评定，反思整个游戏一下再动画和UI细节上有哪些不足：例如分数栏UI仍然较为粗劣，边缘过渡仍然不自然。同时补720P和macOS包。”

| ID | 条件 | 方法 | 期望 | 证据 | 责任 |
| --- | --- | --- | --- | --- | --- |
| V72-H | 风格自评并修订 | 逐屏审视 rc.1 截图，列出不足与修订；改后重新截图 | 规格 5A 的 10 项逐项落实；仍未处理的不足如实列出 | 规格 5A、`evidence/v072/screens/rc2/` | 主 agent 自评；风格由用户判定 |
| V72-I | 720p 回归 | 完整 PlayMode，Game 视图固定 1280×720 | 0 失败；不改阈值 | `evidence/v072/unity/` | 主 agent |
| V72-J | macOS 包 | 门禁后的 macOS Universal 构建 + 封包 | 构建成功；主程序含 x86_64 与 arm64；ZIP 写入 Unix 可执行位；如实标注未签名、未在 Mac 上运行 | `evidence/v072/unity/build-macos-*.json`、`evidence/v072/packages/` | 主 agent；Mac 实机未验 |

V72-A 至 V72-G 对 rc.2 的冻结源重新执行。

## 断言变更

| 测试 | 原断言 | 新断言 | 依据 |
| --- | --- | --- | --- |
| `PLAY_ART071_FullArtEveryContentAndPrefabUsesPrintAssets` | 预制体内所有图片路径以 `Assets/Art/Art071/` 开头 | 以 `Assets/Art/Art071/` 或 `Assets/Art/Art072/` 开头 | 规格第 3 节：共用表面件由 v0.72 按名称替换，插画 / 符号 / 特效仍在 Art071 |
| `PLAY_ART071_CompactCardHoverKeepsBottomHitAndClickThenRestoresPooledVisuals` | 断言本身未改（悬停结束后各子节点回到原位，误差 <0.05） | 用例开头关闭卡牌呼吸（`Art07ButtonMotion.IdleSway072=false`，TearDown 恢复），只测悬停 | 规格 M1 新增常驻呼吸；该用例验证的是悬停复位，不是常驻动画 |

没有删除用例，没有调整阈值。`PLAY_ART071_TableWorldGpu…` 首次因新桌面受光范围 0.0296 低于既有 0.03 下限而失败，
处理方式是加深毛毡受光池（美术改动），阈值未动。

rc.2 没有修改任何测试文件。三类失败都是改实现解决的：

| 失败（报告） | 原因 | 处理 |
| --- | --- | --- |
| `PLAY_ART07_ShopWalletExactDigitsInterestAndGpuAtThreeFonts`（`nunit-20261112-010815`，1080p） | 商店金币改粗体后，十位数在 150% 字号下超出文本框 | 商店金币恢复常规字重 |
| `PLAY_ART07_TwoHundredFullRendererReplaysKeepNodesMaterialsAndCommittedStateStable`（同上） | 分数栏数字改粗体后引入第二张字体图集，其回退材质每次回放都被创建和释放 | 分数栏数字恢复常规字重；左栏读数的粗体不受影响 |
| 720p 下 5 项（`nunit-20261112-015908`）：`…S11RefreshAndExpand…`、`…S13UpgradeTargets…`、`…ShopWalletExactDigits…`、`PLAY_PooledRefreshKeepsNodeIdentities…`、`PLAY_PreviousSubmissionRetainsOriginalScore…` | 同一条规则：商店标签页与宽松布局下的分数因子按钮高 52 单位，720p 下只有 34.7 像素，低于既有 40 像素下限。高度来自 v0.71；rc.1 没跑过 720p 全量，所以此前没暴露 | 两处热区加高到 60 单位（标签页 460–520，因子按钮 44–104），绘制的色片仍收在计分板内 |

热区加高是对“布局矩形与热区不变”这一非目标的一处有意例外，原因是 720p 下不满足项目自己的最小点击尺寸。

## 记录

- 2026-11-11：建立任务。环境：Unity 6000.6.7f1 在 `E:\unity\6000.6.7f1`，Blender 5.2 在 `E:\blender`，
  git 2.56（Codex 运行时自带，不在 PATH），Python 3 + Pillow 9.4 / numpy 1.24。无图像生成工具。
- 2026-11-11：按初始指令做了像素方向（DC-32 色板、64×64 插画转换、1/3 分辨率世界渲染），
  合成原型图与一轮实机截图后，用户否决该方向。相关素材与渲染代码已全部删除。
- 2026-11-11：改为高清简约方向；首轮实机截图（主菜单 / 对局 / 商店）已发给用户。
  据截图取消桌面缝线（压到实时文字），并把骰面预览改为圆角骰面。
- 2026-11-11：无界面批处理下 PlayMode 的 `WaitForEndOfFrame` 不触发，用例逐个超时（该次运行已终止，不计结果）；
  改用带窗口的编辑器。首轮 Game 视图为自由比例，167/185，16 项为“必须在真实 1080p/720p 下运行”的环境失败；
  固定 1920×1080 后 184/185，修复后受影响 7 项 7/7。失败报告保留在 `evidence/v072/unity/`。
- 2026-11-11：冻结源 `b9c45f1`。完整 EditMode `nunit-20261111-170034` 1018/1018，PlayMode `nunit-20261111-172336` 185/185；
  PDB 核对 5 程序集 / 203 源文件 0 差异；门禁绑定 1935 文件。Windows 构建 `build-windows-20261111-172553`，GUID `d7f5f8d368e54749a7d098b026714f27`。
- 2026-11-11：封包 `DicePrototype-p0.7.2-win-x64.zip`（198,082,248 B，306 成员，SHA-256 `074997398ed8dd4f9a94a9218d19e06b38532983d300ffd274cbea20825ed391`）。
  新目录解压后巡览：普通 60 步 / 减少动态 14 步均完成，Player 日志 0 异常；减少动态两帧最大像素差 2，普通模式有可见变化。
- 编辑器启动时的“管理员权限”提示由界面自动化点击 Ignore Warning 通过（“Restart as Standard User”在本机会循环回到同一提示）；未改任何系统设置。
- 未做（rc.1 当时）：1280×720 完整 PlayMode、Windows 帧时间 / 独占全屏专项、macOS 构建、独立复核、人工风格与体验评定。

### rc.2（2026-11-11，文件时间戳为 UTC 的 2026-11-12）

- 用户试玩 rc.1 后要求先做风格评定并反思动画与界面细节（点名分数栏粗劣、边缘过渡不自然），同时补 720p 和 macOS 包。
  自评与 10 项修订、3 项新动画、仍未处理的不足写在规格 5A。自评由实施者完成，不是独立复核。
- 新增 4 个表面件、重画凹槽与桌面，素材共 27 个 PNG，`verify_assets.py` 逐字节复现通过（`evidence/v072/assets/verify.json`）。
- 冻结前预跑（源未提交，报告保留）：EditMode `nunit-20261112-004651` 1018/1018；PlayMode 1080p `nunit-20261112-010815` 183/185，
  两项失败见“断言变更”下方表格，修复后定向复测 `nunit-20261112-011238` 3/3。
- 第一次冻结 `1c3d786`：EditMode `…011439` 1018/1018，PlayMode 1080p `…013802` 185/185，PlayMode 720p `…015908` **180/185**（5 项同一规则，见上表）。
  定向复测 720p `…020306` 7/7。
- 第二次冻结 `ef5fd5f`（热区加高）：EditMode `…020651` 1018/1018，PlayMode 1080p `…023013` **184/185**：
  `PLAY_ART07_ShopTabRedrawSettles…` 断言选中标记的横坐标为 696，我在加高标签页时多改了它的横向位置（400→404）。恢复原横向位置。
- 最终冻结 `cf9f790f…`：EditMode `nunit-20261112-023145` **1018/1018**；PlayMode 1920×1080 `nunit-20261112-025451` **185/185**；
  PlayMode 1280×720 `nunit-20261112-031719` **185/185**。PDB 核对 5 程序集 / 205 源文件 0 差异（`pdb-cf9f790.json`）；门禁绑定 1963 文件
  （门禁按既有工具只绑定 EditMode 与 1080p PlayMode 两份报告；720p 报告及其 binding 单独归档）。rc.1 的门禁另存为 `build-gate-rc1-b9c45f1.json`。
- 构建：Windows `build-windows-20261112-031908`（GUID `f385dd26486a4a4ea34f4f8fb4962c26`）；macOS Universal `build-macos-20261112-032018`。两者由各自独立的编辑器进程在同一门禁下构建。
- 封包：`DicePrototype-p0.7.2-win-x64.zip` 198,186,265 B / 306 成员 / SHA-256 `34c34499e081e32ce37b8fff860d96aaf0ce647cb67797b6e885e3b76ba5f2db`；
  `DicePrototype-p0.7.2-macos-universal.zip` 238,545,236 B / 301 成员 / SHA-256 `af371e9a3b4ee63a35134ac94d5976f799ef5df0581a7d6f2ee0308a69487ce4`。
  macOS 包内 6 个原生组件（主程序 `DiceCraft P0`、UnityPlayer、3 个 Mono 库、Burst 包）均为 x86_64 + arm64；ZIP 写入了 Unix 权限位。
  第一次 Windows 封包后发现随包说明里 Mac 主程序名写错（应为 `DiceCraft P0`），改正后重封；被替换的那份从未发布。
  rc.1 的包与证明移到 `Releases/p0.7.2/rc1/`、`evidence/v072/packages/rc1/`。
- Windows 包新目录解压后巡览：普通 60 步、减少动态 14 步均完成，Player 日志 0 异常；减少动态两帧最大像素差 2，普通模式约 0.5% 像素变化。
  截图在 `evidence/v072/screens/rc2/`。
- macOS 包没有在任何 Mac 上解压或启动；没有开发者签名与公证。包内 `_CodeSignature` 目录是 Unity 构建写出的，没有用 `codesign` 校验过。
- 未做：macOS 实机、Windows 帧时间 / 独占全屏专项、独立复核。风格与体验仍由用户判定。