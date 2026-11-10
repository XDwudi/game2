# AI 美术能力接入前的项目审计

日期：2026-11-08。审查者：`/root/art_audit`。输入快照 HEAD：`246646b0e4fb87595727b0fbf48559f1947230ba`，并读取当时工作树、`docs/STATE.md` 的 v0.71 检查点。任务仅研究与只读审计；未操作 Unity、Blender、生产资产或验收标准。以下结论是对现状的范围有限的判断，不签署美术标杆或整版 ACCEPTED。

## 结论与优先级

本项目已经具备正确的基础路线：固定视觉母版、分离装饰插画与规则、参数化公共表面、Blender 骰子母模型、Unity 共用组件，以及真实截图和独立复核。当前最有价值的补充是让未来 agent 能可靠地找到、引用和执行这些已有约束，并在素材进入游戏后检查风格和可读性。

不宜另建一套泛化美术评分系统或为每张资产增加人工审批。`ART_SPEC.md` §12 明确采用三个自然对照点；`P0-V071-IMPLEMENTATION.md` 已固定 V71-A～E。新技能应把这些要求变成可操作入口，普通实现自主推进，核心美术标杆与转入量产仍由用户判断。

当前剩余 67 项是未换成 v0.71 大插画的旧内容图标，不能因增加了生成工具便宣称已完成。先收敛现有样板，再沿稳定内容 ID 按原规格每批 4—6 项扩展。

## 唯一依据及已确认方向

| 事实 | 权威位置及使用方式 |
|---|---|
| 玩法和内容身份 | `DicePrototype/Assets/Game/Data/P0Config.json`，p0.6.0 / schema4；插画不可定义价格、点数、抽样或效果 |
| 视觉方向 | `docs/specs/v071/ART_SPEC.md`；复古印刷骰桌、深青毛毡、奶油厚纸、高清限色插画、温润陶瓷；覆盖 v0.7 冲突的像素媒介规定 |
| 整体参考 | `docs/specs/v071/handoff/references/battle_master_v071.png`；商店衍生图用于同风格排布示例，图中文字不是规则 |
| 插画参考 | `docs/specs/v071/handoff/assets/controller_stairs_candidate_v071.png` 与已接入四图；大轮廓、有限色块、克制网点，无卡框和动态文案 |
| 样式参数 | `docs/specs/v071/handoff/STYLE_TOKENS.json` 是起始提案，需映射到既有主题；不是另一个运行时真源，也不把主体占比建议变为硬性像素阈值 |
| 当前接入与未完成项 | `docs/specs/v071/ASSET_MAP.md` 与 `docs/STATE.md`；源交接 manifest 的历史 pending 字段不能覆盖后来接入事实 |
| 真实组件与渲染 | `DicePrototype/Assets/Game/UI/UiKit.cs`、`GameApp.Art07Shop.cs`、`GameApp.Art071Screens.cs`、`Runtime/DiceTable.cs`、`Editor/V071Setup.cs`；沿用 URP/uGUI/TMP |

## 实际资产和来源检查

本次以 Python/Pillow 只读四张源 PNG 和四张生产 PNG，重新核对尺寸、模式、alpha 极值、manifest SHA 和源/运行件字节。结果：C48/C29/C03/I07 四项均为 1254×1254 RGBA，alpha 极值 0/255，SHA 匹配，源与运行件逐字节相同。该结果只证明这些文件的完整性与透明通道存在，不证明透明边缘观感或风格获认可。

| 类别 | 实际路径与状态 | 后续使用限制 |
|---|---|---|
| 四张插画 | `ArtSource/Art071/Illustrations/` 的 `illustration_c48.png`、`illustration_c29.png`、`illustration_c03.png`、`illustration_i07.png`；`manifest.json`、`prompts.json`、`validation.json` 已存在；运行件在 `DicePrototype/Assets/Art/Art071/Illustrations/` | C29 是用户样件原字节复用，另三张记录为内建 imagegen。PNG 是栅格原图，不是具有分层绘画结构的 KRA/PSD；不能宣称模型精确版本或随机种子，除非工具实际返回 |
| 公共表面 | `tools/art_v071/build_surfaces.py` → `ArtSource/Art071/UI/` → `DicePrototype/Assets/Art/Art071/UI/`；五文件、32px 九宫格边界、PPU200 | 卡框、紧凑框、paper panel 共用图像 SHA；复用是合理工程选择，不计成三幅不同绘画；脚本是可编辑源 |
| 骰体 | `ArtSource/Art071/Dice/dice_master.blend`、`face_map.json`、`export_preset.json`，运行 FBX 在 `DicePrototype/Assets/Art/Art071/Dice/` | 现有报告为 Blender5.2.3、300tri/152顶点、26项源/回导检查；本次未重跑这些检查，不把离线图作为 Unity 外观通过 |
| 点数与状态 | `Runtime/DiceTable.cs` 与现有配置 | 点阵按真实面数据创建；插画中的骰点只是装饰。保留红蓝之外的形状/文字，保证色觉和缩小辨识 |
| 旧资源 | `ArtSource/Art07/` 与 `DicePrototype/Assets/Art/Art07/` | A01/A02/A12/A17 等仍有旧件复用；A07/A18 等是代码表达或形式差异，按 ASSET_MAP 保留真实状态 |

已保存导入与场景引用由现有 `cache/v071/review-final-assets.md` 独立检查记录：Bootstrap 与 ArtStyleLab 各有 71 唯一内容 ID、4 新插画和67旧图标，新 PNG 为 Bilinear/Clamp/sRGB/FullRect，UI border32，运行时不烘焙文字。本次读了该报告及 `V071Setup.cs`，没有把它当作新执行的 Unity 导入或再签同一证据。

## 本次实际查看的画面

通过本地图像查看工具打开以下九张文件；没有声称观看所有48页或完整录像。录像只取两帧用于静态观察。

| 图像 | 观察与适用范围 |
|---|---|
| `docs/specs/v071/handoff/references/battle_master_v071.png` | 视觉参照含厚纸、大主题图、柔和陶瓷高光和明确主动作；属于概念图，不能据其布局文字推导玩法 |
| `ArtSource/Art071/Illustrations/illustration_c03.png` | 旗帜和骰列轮廓完整，深墨轮廓、珊瑚红和纸色与样件方向相符；细网点和透明边缘需在真实缩放下判断 |
| `ArtSource/Art071/Dice/Review/source_ceramic.png` | 有圆角体块和柔和明暗；只是 Blender 离线源预览 |
| `cache/v071/recording-final/frames/00000.png` | 三张新控制器图与纸卡共同显示，已有大主题插画；骰池、锁序和分数信息分层清楚。与母版相比光泽、材质细节和装饰密度仍较克制，属于后续人工风格讨论，不能擅自宣布偏差合格或加大效果 |
| `cache/v071/recording-final/frames/00087.png` | 五骰回池后仍使用同一母模型和材质，三张卡画法相邻可比；该静帧不证明投掷动效和交互完整 |
| `cache/v071/screens-final-1080/screens/006-pending-die.png` | 普通空装备对局中五骰、待锁、颜色文字和动作能辨认；空槽保持低对比，不能用空装备状态代替核心样板 |
| `cache/v071/final-special/shop-three-hd.png` | 纸框和动作表面与对局一致，但所选商品仍主要是旧像素符号，印刷插画与旧图标混用的阶段事实直接可见 |
| `cache/v071/screens-final-1080/screens/015-handbook-hands.png` | 规则点阵是简洁程序示例，适合保持确定性；不能让生成插画替换精确牌型图 |
| `cache/v071/final-special/shop-target-readable-720-150.png` | 修后未选目标ID/永久属性可读。适用于该720p/150%静态弹窗，不代替所有状态、设备和生命周期运行 |

查看时曾尝试不存在的 `006-pending-lock.png`，工具明确报缺失；随后以目录实际存在的 `006-pending-die.png` 完成检查，没有以失败调用计通过。

用于复核的代表图 SHA256：

- 录像帧00000：`edacda95eca900e2a523a2360768db7ca1c3ddc3ba4b09e57eb8d2f3ebdf7021`。
- 待选对局006：`e18d0e2db8e51ade044d6907291ddb9f27bbff347f4c17b7f350d59c29730f5b`。
- 三商品商店：`a8335b4a9c178a7ab60f14ca29a40b39492ac618fbf7e882e022fe5b5d376bfb`。
- 手册015：`7499600c6633852ee0a49af5d72aa5fe5638ea8109e3c17fa4ffb3dd39491f32`。
- 目标弹窗修后：`10aeaa88fae248e5fe6678a1ab74a6c5cc9d7532fb91b26462a70b232601ef1a`。

## 可以复用的检查与不该误用的工具

| 已有能力 | 复用方式 | 注意 |
|---|---|---|
| `tools/quality_check.py` | 对明确列出的来源、快照、NUnit报告作一致性检查 | 不产生真实运行、不判断审美、不自动批准任务 |
| `tools/art_v071/verify_dice.py` | Blender真实重开和FBX回导，检查尺寸、面序、层级和三角数 | 会重写源审查报告，须由已获骰子文件所有权的制作者在对应任务运行；本次未执行 |
| `tools/art_v07/validate_art_samples.py` | 复查原v0.7源/导出关系 | 含64px逻辑像素网格约束，不能直接施加给v0.71高清印刷插画；会写报告，不是只读审计命令 |
| `tools/art_v07/roundtrip_krita.py` | 在需要KRA源交付时验证原生打开/导出 | STATE仍有公共Krita62项原生补证，不可用ZIP结构或mergedimage比对冒充已原生验过 |
| `DicePrototype/Assets/Game/Tests/PlayMode/Art071StylePlayModeTests.cs` | 五骰/玻璃/临时属性/实际面的命中与卡片hover复用检查 | 受影响新资源需重跑适用项，旧报告不证明新版本 |
| `Art07CardReadabilityPlayModeTests.cs`、`Art07PanelGlyphPlayModeTests.cs`、`Art07AssetLibraryPlayModeTests.cs` | 字体、可读性、组件引用和复用等现有测试入口 | 应根据影响面选择，不能将测试数量当美术质量 |
| `tools/art_v071/encode_recording.py` | 带真实时间戳的录制编码及解码一致性检查 | RGBA母片与RGB预览各有边界，不随意重标版本 |
| `cache/v071/screens-final-1080`、`screens-720-150`及`final-special` | 实际尺寸、字体放大、跨页面/容量比较 | 截图绑定不同MVID；历史失败图保留，修复图只覆盖相应问题 |

## 给未来技能的具体建议

以下是工作方法建议，沿用既有 V71 条件，不新设美术门槛或替代用户决策。

1. **先选正确生产路径。** 概念/主题插画使用参考图生成或局部编辑；九宫格、图标几何、规则点阵用确定性源；骰体继续用Blender母模型。不能为展示调用AI能力而重做已有稳定组件。
2. **固定参考包，改变主题。** 每次引用母版、对应样件、稳定内容ID和当前组件裁切；提示词说明各输入图角色，保留画法、配色和轮廓，只改变主体。后续卡片不能以各自最后一次生成结果无限串联为新风格依据。
3. **保留原图和操作来源。** 保存实际提示词、参考文件SHA、工具返回的可用身份、原图和派生文件关系。工具未提供的模型快照/seed写未知。源交接状态和集成验收状态分开记录，不覆盖历史。
4. **透明不等于质量合格。** 除RGBA/alpha存在外，检查浅/深底、实际卡面大小、紧凑卡、详情放大；看黑白边、半透明脏边、主体被裁、细节糊成噪声。自动检查只指出尺寸/边界等可判定问题，视觉判断仍看图。
5. **统一靠共享资源落实。** 同类页面调用UiKit、同一材质、字体和母模型；检索局部硬编码颜色/替代卡框。已有“强化商品丢纸框”“浅字落纸底”案例说明单张图片合格仍可能集成失败。
6. **沿三个对照点形成证据。** 母版与组件样件 → Unity对局样板截图及10—15秒操作 → 对局/三商品和满货架商店/手册并排。按规格先处理最大三处差距，修复后重验实际影响面。
7. **小批扩展并检验回退。** 内容ID逐项映射，明确新插画/旧图标/缺失三种状态；测试资源缺失及旧图标回退，批量前先验证一项完整的生成、接入和跨页面显示。

## 未完成与禁止扩大的结论

- 规格符合性：已检查的四图身份、文件尺寸/透明存在与源运行件关系通过；资源形式差异和67项替换未完成，按ASSET_MAP保留。
- 制作质量：静态抽样可观察到同一纸卡、配色和新插画语言；仍需用户对正式样板作风格判断，未宣称最终通过。
- 实际运行：本次未启动Unity/Player、未操作Blender、未重跑测试、未完整观看视频；现有运行结论仍引用原证据及其范围。
- 整版：最终同源全量回归、双平台构建/Windows启动、Mac原生、历史性能/独占/Mixer/Krita补证和人工体验按STATE继续保留，本报告不关闭。

主要已读来源：README、AGENTS、STATE顶部v0.71、QUALITY_GATES、TOOLCHAIN、P0-V071-IMPLEMENTATION、ART_SPEC、ASSET_MAP、STYLE_TOKENS、四图manifest/prompts/validation/README、UI manifest、骰子HANDOFF/export_preset/roundtrip、review-final-assets/pages/root、QUALITY_CHECK说明及上表对应检查源。外部工具与模型能力研究由主任务另行记录，本报告不以在线产品宣传推断本地接通。
