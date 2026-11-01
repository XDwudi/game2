# S01–S22 正文要素完整性补查

日期：2026-11-01。审查者：`/root/v07_art`。状态：**REWORK / 部分新补项待实图**。本次只读生产源码与既有真实图，不启动 Unity，不修改标准。目标是找正文已确定而遗漏的功能或视觉要素，不重做全动画验收，也不把个人风格偏好当门槛。

输入为 `docs/specs/v07/ART_SPEC.md` 正文 S01–S22（164–352 行），玩法 p0.6.0 / 表现 art0.7.0 / schema4。源码正在由 root 与 shop 串行集成，**不是发布冻结版**。实际读取的源副本及 SHA、所看图和原始 metadata 绑定见同目录 `S01_S22_SCOPE_SWEEP.bindings.json`；副本保存在 `artifacts/cache/v07-final-scope-sweep/source-snapshot/`。后续修改需重验受影响项。

## 可操作的明确差距

下列行号为本轮定位锚点；root在审查过程中持续集成，个别后续行号会位移，方法名与绑定副本可用于准确追溯。未附本轮运行场景的项目以源码定位为结论，不冒充实机失败。

| ID | 正文要求 | 已有行为与明确差距 | 证据与后续验证 |
|---|---|---|---|
| SW01 | S04/192、S06/210：投掷骰杯、提交票据；零投掷暗蓝底空圈；非法道具锁角标与原因 | **已通知后由root补源码，待实图**。原图Header只有预算文字、DrawItems07无窗口角标；归档时已读到 `DrawBudgetCounters07` 骰杯/票据/零掷空圈，以及按 `Art07ItemUnavailable` 画 `UnavailableLock`。详情原因原本已有，不是没有使用规则。 | `UI/GameApp.Art07.cs:73–98,267–271`、`UI/GameApp.Shop.cs:95–130`。补前实图 `004-before-first-roll.png`、`030-after-real-submit.png`。新图需覆盖0掷、HandReady合法/非法道具、Selecting以及待锁。 |
| SW02 | S05/200：超剩余 K 时“可再锁 n 颗”及超额骰细斜纹角标 | Core 允许待选超过 K；Confirm 已整次禁用，未出现部分锁定。当前仍显示“确认锁定 n”，所有待选都用同一虚线/空心勾，缺容量文字和超额角标。 | `Core/Session/GameSession.cs:33–41`、`UI/GameApp.Art07.cs:288,326–332`。本轮未新建超额实图，源码确定缺分支；回归保持 Core 不变，检查先选合法再超额、取消后恢复。 |
| SW03 | S07/220：x496 y400 / 1280×520 来源、计算式、结果三列抽屉，160ms入/100ms出 | `ShowBreakdown` 是通用大模态加单列纵向源记录，不是指定三列抽屉。精确公式与复制、逐骰来源、全局计算和只读语义已有，不应删掉。 | `UI/GameApp.Modals.cs:305–340`；实看 `026-score-preview-detail.png`。改后需三字号两尺寸、长整数、历史与预览、关闭后待选/锁定/RNG保持。 |
| SW04 | S08/228：左栏展开现在结束的基础/剩次/利息，并独列继续提交的 C01 收入 | 左栏只画 `QualifiedSummary07()` 的总金额；完整信息放在 EndStage 悬停层。**信息存在但正文要求的位置与常驻展开未实现**。Debug32实图还显示该简述被组合试验按钮区域遮挡。 | `UI/GameApp.Art07.cs:62`、`UI/GameApp.Art07Interactions.cs:54–87`；实看 `030-after-real-submit.png`（左下预算下方）。不能以删除明细或缩字解决；普通/调试需避导航与预算。 |
| SW05 | S09/234–236：桌面压暗35%，结算单180ms展开，前三现金行各100ms，≤5象征硬币，空白/跳过完成 | 结算正文信息、已入账标记、基金“卖价”与临时清理、后继按钮已有。当前 `Settlement()` 同步创建整个纸单，前三行合一 TMP，无35%背景层、专用展开/逐行组件或跳过入口。非 Submit 的表现支路只有通用现金纸签，不能代替本段演出。 | `UI/GameApp.Art07.cs:467–498`、`UI/GameApp.Art07Presentation.cs:90–95`、`UI/GameApp.Art07Effects.cs:333–375`。实看 `032-stage-settlement.png`确认静态纸单/未压暗；动画缺项判断来自全调用支路而非静图。重验不允许再次入账。 |
| SW06 | S02/176：继续快照、180ms桌布过渡，已完成提交直接显示最终数字/“已恢复上一手结果”/只读收据 | 菜单有保存关卡/金币摘要，恢复不重掷；上一手明细入口存在。Continue 只设置通用恢复 notice 和 Refresh。冷启动未从 lastSubmission 初始化 `receipt07` / `stableResultRequested07`，而稳定结果视图要求它们存在，故上一手最终展示不能靠此分支恢复。读取是 Awake 同步完成，无180ms Continue 过渡；>200ms才显示进度的机制也未见。 | `Runtime/GameApp.cs:61–68`、`UI/GameApp.Art07.cs` Menu/Continue、`UI/GameApp.Art07StableResult.cs:12–22`。本轮未冷启动重放，属源码缺项；需用真实已有提交存档启动，保持三RNG/金币/已售/序号不变。 |
| SW07 | S01/168、S22/350：旧 schema 明示版本，说明及“保留备份并开始 v0.6 规则的新局” | 存储已经复制旧版本到 legacy 路径，失败保留原档；无需改该存储策略。UI错误只有通用“不兼容”，未包含实际旧 schema/config；ShowLoadIssue07仅复制和返回菜单，菜单也未专门显示旧版本状态/指定新局入口。 | `Runtime/AtomicSaveStore.cs:95–104`、`UI/GameApp.Modals.cs:90–99`、Menu。需旧档实测，验证明确显示旧/新版本、入口只打开新局确认、不静默覆盖或迁移。 |
| SW08 | S17/308：720×360独立显示确认层、当前模式/分辨率与15秒倒计时 | 回退和确认功能已有；当前确认仍复用1600×790通用 OpenModal，未建立正文720×360层。此项是明确布局差距，**不是声称自动回退失效**。 | `UI/GameApp.Display.cs:55–65`、`UI/GameApp.Modals.cs` OpenModal。需真窗口切换后实测安全区、15秒、Esc、失焦，不能仅 Editor 截图签平台全屏。 |
| SW09 | S15/288：悬停300ms后80ms淡入、离开60ms消失 | 300ms等待与60ms离开计时存在；show回调创建满alpha浮层，未见80ms淡入。完整960×720详情和实例状态已有。 | `UI/GameApp.Art07Shop.cs:249–287`。本轮未重录hover动画；源码差距，修后只需对应开始/结束帧及无重复动画/不挡点击回归。 |

## 已发现且 root 正在补的项目

- **S01/166–170**：此前四背卡/版本/菜单进入呼吸项已有明确制作工作。新 `artifacts/cache/v07-menu-preview-0543/menu-1080.png` 已实际打开：四纸背卡、三实体3D骰、五按钮、右下三版本可见，骰卡没有互盖。相邻 metadata 报三骰真实视图、state=null、drift1.8927px、breath0.00946。这个单图只能确认要素与该时刻布局，**不能签240/35/120ms、8秒完整周期、六配置或最终冻结版本**。
- **S02/174**：0521批缺初始五骰视觉且采用通用大面板（SIX01）。shop agent 正在 cache 制作1040×640专用纸单和五3D预览；本次未收到新运行图，不标已完成。
- **S03/182–186**：本轮先发现完成勾、当前方框、风险顶签斜边、固定顺序提示缺失并通知 root。后续读到生产 `StagePreview/ChallengeCard07` 已增加 `StageTick` 完成✓、`CurrentStageOutline`、`RiskHeader/RiskStripe`、`ControllerOrderWarning`。**源码已补、实图待验**；旧 `003-stage-preview.png`保留为补前证据。160ms进入/220ms退出未在本轮重录，不据静态补项宣布时序完成。

## S01–S22 覆盖与边界

“已定位”仅表示主要要求能找到实际生产路径，不是整屏或全版本通过。实施者自己的资产/Guide/骰标签/Toast实现不在本报告自签制作质量。

| 屏幕 | 本轮要素结果 | 生产入口与已看证据范围 |
|---|---|---|
| S01 | 新实体要素已见；进入/呼吸新实现待完整实测；旧档状态SW07 | Art07.Menu / Art07MenuReveal；新0543整屏 |
| S02 | 五骰面板正在补；继续恢复SW06 | ShowNewRun07 / Menu.Continue；旧047在六配置报告已实看 |
| S03 | 四项标志源码已补，待图；规则选择仍仅局部预览到开始才提交 | StagePreview、ChallengeCard07、StartPreviewStage07；旧003本轮实看 |
| S04 | 待投掷签、空提交、首掷前非分数、动作禁用已定位；SW01新源码待图 | Gameplay/ScorePanel07；004本轮实看。机械点阵/180ms复位未重录 |
| S05 | 待锁虚线/勾/顺序、不可逆规则与禁用已有；SW02 | DrawDie、Confirm/ActionReason；原Affordance独立报告有效范围，非本次自签 |
| S06 | 抓手合法窗口、真实拖动/插线/取消、满栏仍可重掷已有；零掷图形SW01源码待图 | DrawLockedGrip07、TryDieInsertion07、Gameplay；旧maximum/锁槽图见六配置报告 |
| S07 | 顺序乘区/结构计分区分、零值/C53范围/精确复制已定位；SW03 | ScorePanel07、ShowRandomPreview07、ShowBreakdown；026本轮实看 |
| S08 | 收据表现/一次命令/首次达标章及稳定结果已有；奖励常驻区域SW04 | Art07Presentation/PostEffects/Interactions；030本轮实看；不重签旧所有演出 |
| S09 | 现金、已入账、基金卖价、清理与正确后继信息已有；SW05 | Settlement；032本轮实看，数据底部可滚动，不因首屏裁窗误判缺项 |
| S10 | 五分区、商品状态/钱包/容量/已装备列已有 | Art07Shop；033–037六配置此前实看，G05–07由shop独立专审，本次不重复代签 |
| S11 | 替换/卖出/刷新/扩容合法候选与收据效果已有路径 | GameApp.Shop / Art07Transactions / Art07Effects；该事务全段由专项报告覆盖，本轮无新现场 |
| S12 | 稳定骰ID、三分区、固定F02十字、当前/永久/有效/概率、可旋转预览已有 | OpenDiceSheet/DrawDiceSheet；017–023中此前023六配置实看；自身相关实现不自签 |
| S13 | 确定强化预览、U07范围/确认后结果、两不同遥控目标及六按钮/风险说明已有 | DrawDiceSheet、RemoteTargets375–427、UpgradePreview；新U05/U07已有专项录像，本次不重录 |
| S14 | 14固定排序行、散点末行、独立颜色红组页、结构金线与计分文字分开已有 | ShowHandbook16–80；015/016既有实图，普通可达性/升级原因保留 |
| S15 | 完整详情/C41 owner、C45/C51/C52/C53状态与交易规则已有；SW09 | ShowController、ShowOwned、Art07AttachTooltip；028/029此前六配置实看 |
| S16 | 640×640暂停、冻结表现、显示最终/保存问题分支已有 | Art07Settings.Pause、TickInput/Presentation；008此前图，未用静图证明暂停时序 |
| S17 | 三页设置、四音量、Normal/Fast/Instant、辅助/缩放/快捷键与实际显示回退已有；SW08 | Art07Settings、Display.cs；009–013既有截图；平台独占全屏仍不外推 |
| S18 | 胜/败原因、最终骰、统计/种子/普通调试、印章/跳过/只读构筑已有源码 | Art07Results / Art07ScreenReveal；本次未新开终局图，制作质量未验证 |
| S19 | 五步骤、400×144纸签、目标箭头、真实对局、首遇边界提示已有 | Art07Guide；引用独立GUIDE_INSTANT_REWORK_REVIEW，不由本实施者自签 |
| S20 | Ordinal口令、三入口、独立文件/原checkpoint、调试常驻标识已有 | Art07Settings / GameApp.Debug；013/014既有图；本轮未重测错误口令 |
| S21 | 七导航、32表格/局部预览、专用字段、严格数值、重建提示/三RNG/一次强制已有 | GameApp.Debug（106–442）；038–045既有六配置关键图，非所有字段本轮逐个操作 |
| S22 | 普通短/长Toast、主动详情、保存重试同候选、放弃checkpoint、ID占位已有；SW07 | Art07Toast、SaveFailure、ShowLoadIssue07、Shop.Icon71–92；Toast为本实施者故引用独立评审，不自签 |

## 证据解释与后续

本轮实际重新打开6张正常1080画面：003、004、026、030、032和新0543菜单。前两张在本任务较早读取；另四张在后续补查打开。旧六配置报告的102张实看范围仍按原报告列明，不因本表列了22屏就变成22屏完整实测。0521图绑定该批源/Runtime MVID，不能覆盖新菜单、S03及shop待合入源。

应先把明确差距补齐，再由 root 串行编译与针对场景实测，并让未参与相关实现的审查者复核受影响实图。规则、存储策略、容量、概率和验收阈值均不因此更改；本报告没有人工风格批准、发布批准、性能或 macOS 结论。
