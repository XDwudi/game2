# W16 测试变更记录

## 选关与牌型手册

| 用例 | 本版核对的行为 |
| --- | --- |
| V10 章预览与选关 | 从真实高压关选择挑战，打开商店的本章情报；保留对三张关卡牌、实际奖励、目标与按钮操作的检查。六风格用例另查 24 格、窗口内实际字形和底色。 |
| V05 手册与读档 | “永久骰门槛”改查动态的“颗永久骰”，“数量受限”改为“凑不出”；读档前后牌型等级、提交容量和骰例仍需一致。 |
| V10 每章规则与首领 | 第八条标题按已批准文案检查“每章三关”。当前章首领说明取该首领配置的 `shortText`，继续逐字比较并验证首领名称。 |
| W20Z2 手册字形边距 | 小字和大字各有三行固定空间。移除旧滚动视口裁切后，改测每个字形到实际纸框的边距，并要求整段没有截断或溢出。 |

## W24 实机巡检

| 用例 | 实际核查与本次迁移 |
| --- | --- |
| `PLAY_V24_SC16_SE04_*` | 每套风格分别从启动走到菜单、设置、三档选关、手册、存盘异常、引导、提示、奖励和结果等 56 个状态。100% 与 150% 分别截取前景及滚动末页。旋转卡面在卡自身坐标系内测墨迹，四块拼成的纸面按可见边界取样，输入框的零宽光标独立处理。 |
| `PLAY_W20X_03_TableTextOnNonPlayScreens` | 用相同路由实际走六套，按屏统计取样对象、记录直接落在桌面上的字。六套合并巡检指定 600 秒执行时间；仍逐一断言取样数量大于零，且与已提交的 1080p 清单一致。 |
| `PLAY_W16B_POP05_PopupGeometryAndScrollableRows` | 两个字号分别打开九种对局弹窗，截取可滚动区的首尾，检查字形、背景与遮挡。图像遮挡同时考虑它所属的 Unity 遮罩；可见区域的阈值保留。 |

首轮 1080p 矩阵实测发现锁定骰号被骰子遮盖、大字手册名称与标题越界，以及领奖按钮的禁用原因压边。默认风格两档字号分别留下 65、71 张画面。详细坐标与截图见[巡检诊断](../../../artifacts/cache/v11/w24-screens/recheck-diagnosis.md)。

## 主菜单、新局与设置

依据为 `SCREENS_LAYOUT.md` 第 2、3、6、11 节与 `FONTS_WRAP.md` 第 12 节。以下检查使用生产 Bootstrap 与独立临时存档。

| 测试 | 按批准稿迁移的断言 | 保留或增加的检查 |
| --- | --- | --- |
| `PLAY_Bootstrap_RealReferences71IconsFontAndSingleInputSystem` | 无档时移除禁用的继续按钮 | 新局按钮可用，场景、原图、输入与字体检查保留 |
| `Art07SettlementLegacyPlayModeTests` 的旧档清理 | 普通无档菜单仅提供“开始新的一局” | 仍检查旧档清理、没有额外说明、各字号存档行不出纸面 |
| `PLAY_ART071_MenuTableRealMenuRoutesPreserveCancelAndContinueCheckpoint` | 空档、有档与覆盖说明逐字检查；种子保留在运行状态 | 主菜单版号检查移至版本页；读取简版并实际复制完整美术版本及两份 SHA，恢复测试前剪贴板；保留同一存档、动作序号及真点击检查 |
| `PLAY_ART071_MenuTableRealFramesAnd150GlyphsPreserveWorldDiceAperture` | 删除锁定提示，按存档状态测全部可见菜单按钮 | 所有原有字界、遮挡、安全区与点击尺寸门槛保留；两张正面稀有宝石、两张背面及三个游戏骰均需存在 |
| `PLAY_ART07_InvalidSeedKeepsDialogAndInputUntilCorrected` | 错误提示改为获批的十进制数值范围 | 同一输入框实例、原输入、无存档与最终正确种子都保留 |
| `PLAY_V11_ThemeSwitchFromSettingsRecoloursAndKeepsRunAndSave` | 从点击循环改为打开预览页后点瑞士，再点完成 | 检查标记、即时调色、场景重启恢复、会话不变与存档逐字节不变 |
| `PLAY_V11_PreviewThemesStayHiddenWithoutPreviewSetting` | 在真实风格页检查可见格与风格标记一致 | 保留注册表默认项与玩家过滤检查 |
| `PLAY_V16_MenuNewRunAndSettingsUseApprovedControlsAtEveryTextScale` | 新增三档字号的真实 UI 路径 | 检查纸面、种子文字、可启动状态、设置上下排列与说明滚动边界 |
| `PLAY_V16_EachThemeKeepsOneMenuPrimaryWithAndWithoutACheckpoint` | 新增六主题、有档／无档、100%／150% 组合 | 使用 W11 相同的主色判定，复查按钮位置、提示归属、真实骰子和存档 |
| `PLAY_V16_SixPreviewsUseNativeSpritesAndOneCurrentMarker` | 新增预览格交互与坐标 | 六张均须是 480×270 Sprite，各格位置与当前风格描边/勾号一一对应 |

默认风格预览取自 W22i 核验的 1920×1080 对局原图 `artifacts/cache/v11/w22i/1920x1080/review/after/default-text100-02-play-selecting.png`，整帧 Lanczos 缩至 480×270。其余预览继续使用各风格提供的资源。六套风格的玩家可用性由 `ThemeRegistry.Selectable` 决定。

三屏验收还需要对正式整合版本执行两档分辨率的 PlayMode、完整对比度与字形检查、截图及独立复核。W17 的托盘对照属于菜单底图验收。

## 暂停、读写存档、引导与提示

| 测试 | 批准的变化及核验方式 |
| --- | --- |
| `Art07GuideLayoutPlayModeTests` | 逐字核对取配置值的新手首步。三档字号下按批准的 400×144 或 400×188 检查；纸面实边、全部字符顶点、箭头、遮挡、存档与跳过检查保持原阈值 |
| `Art073PlayModeTests` 的八骰图例 | 名称“多种叠加”；原八种真实骰子、材质、旋转与不改存档检查保留 |
| `Art07LoadingUiPlayModeTests` 与 `Art07PanelsAndHintsPlayModeTests` | 读取说明默认收起；打开、关闭并测复制逐字等于原文，也核对恢复说明与不改盘。原 0.12／0.10 秒时长继续测量 |
| `Art07LoadingThreadSpikePlayModeTests` | 断言工作线程上真实回退到备份时返回新提示 |
| `AtomicSaveStoreTests`、`AtomicSaveMetadata07Tests` | 八个备份恢复场景逐字检查第 7 节已批准的“存档文件损坏，已改用自动备份（可能回到稍早的进度）。”。仍检查动作序号、原始字节损坏、备份内容、旧版本清理和存档有效性。 |
| `Art07PanelsAndHintsPlayModeTests` 的三条首次提示 | 去除人为换行后检查“提示：”与对应原因，继续验证独立已读标记、关闭区域、存档与重新启动 |
| `Art07FxIntegrationPlayModeTests`、`UiBoundaryPlayModeTests` | 真实用道具增加提交／可掷次数时的数值和玻璃破碎后的格数，迁移至批准文字 |
| `PLAY_V11_W11b_HintsBreakAtBalancedPunctuationWithoutOrphans` | 换入五条现行操作反馈，加入三条首次提示，保留全部不超过两行、无孤字、标点优先及均衡断言 |
| `PLAY_V16_PauseAndStyleLegendRespectFontScaleAndSession` | 六主题、100% 与 150% 检查暂停种子、唯一一级按钮、八格图例字号与完整说明，以及保存状态 |
| `PLAY_V16_InternalErrorToastShowsPlayerCopyAndRetainsExactDetails` | 从通知入口到真实“详情”“复制”控件，核对玩家摘要和未经裁剪的异常详情，恢复原剪贴板 |

不兼容版本在读取前由 `DeleteOtherVersionSaves` 清理或忽略，现行读取路径没有“请开始新挑战”的分支。`ui_score_breakdown` 的旧字段仅出现在编辑器生成器与资源库测试，生产效果池只实例化 `isEffect` 件，实际计分页另由 W11 绘制。W15 的整局结果摘要、W16b 的对局弹窗长文案按各自工作项接力。
