# b005e00 rc.2 技术美术独立复核

结论：本次已查看范围内，**未发现由新菜单/木沿毛毡桌面引入的阻断性美术问题**。当前源的技术美术整合复核完成，可形成 `full_art_review_complete` 证据；`humanArtAccepted=false`。这不把未审画面、作者自审、性能或整版发行验收改成通过。

## 当前运行与实际看图

源 `b005e0088b227b26e2f8fc44f1c59f68df0c0a35`，p0.7.1 / art0.7.1 / 规则p0.6.0 / schema4。Windows 实际构建 GUID `75e7f6b0f1c34075a6527abbc6829e8b`；Player Runtime MVID `5c19060c-301d-4a32-8ed5-6d4ce85722a2`。已直接比较当前 BuildIdentity、build report、完整 Player report、serial referenceReport 和 gate 的身份，不以旧41aface运行替代。本次图像来自 WindowsPlayer / Unity6000.6.7f1 / D3D11 / RTX3080，实际分辨率和每图 SHA 在同名 bindings。

实际打开新 Player 10 张 PNG：菜单1080、普通待选720/1080、商店720、普通骰详情720/1080、8锁压力容量720/1080、骰库物理面概率图、达标继续/结束1080。图审结论逐图记录如下：

- 菜单标题、操作列、卡片与真实骰子构图有明确层次；流程行和卡边有余量，版本显示 b005e008，未采用概念图虚构日期。
- 对局新木沿、深青毛毡与旧纸卡/陶瓷搭配一致，中央背景不夺文字注意。ItemsTitle 与 ActionReason 被动底色分开缝线和文字；已看720/1080未见新增遮挡。
- 商店720的三件商品、右侧纸面详情、下部骰柜和操作可见，没有由背景引入的新裁切。这里仅签实际所见，不声称已重新遍历全部商品/五页。
- 压力容量720图中8颗锁骰已在槽内、2颗临时骰在池中；标题、序号、状态与操作可辨。1080同名图仍处在骰子位置过渡，只用于HUD/背景范围，不当作最终落槽图。
- 当前 Player 的 `v07-dice-sheet-physical-probability.png` 已实际看见开孔内陶瓷骰、物理六面与概率列表。它是骰库视图，不能偷换成下面两张普通详情图的同一采样。
- 达标1080图保留真实移动中的骰子与未完全揭示的章条，仅用于桌面和操作布局；不签静止终态或动画节奏。

## 详情采集限制与当前稳定补充

两张 `normal-die-detail-permanent-preview-*` 实际是暗入场面板和不透明预览遮罩，**不能签“这两张图证明预览可见”**。原图完整保留，自动 accepted 只是尺寸/非黑等采图条件，不能替代肉眼结论。

已实际对照41aface旧同名1080图，同样出现暗面板和不透明遮罩；`PlayerVerification.cs`、`GameApp.Modals.cs`、`Art07PanelMotion.cs` 三件字节均未变。采集在 ShowDie 后只等一次 end-of-frame，而面板/预览有0.16秒淡入及0.06/0.10秒揭示。由同代码与实际图推断，这是既有入场采图时机限制；未记录精确采图 elapsed，不虚称测出了具体毫秒，也不把它列成本轮新产品缺陷。

另已实际看本轮完整185中的 `004-table-dice-detail-font150.png`：1920×1080、fontScale1.5、WindowsEditor，真实骰子完全露出，纸面、正文、按钮可见。其 Runtime MVID `5d15c310-0a76-4911-aa88-b75336a00bff`、sidecar、extra-report、源码与原 NUnit 用例 Passed 一并绑定。它是当前源 Editor 稳定补充，**不是上述 Player 图或新增 Player150%证据**。

## 全71与资源来历

127条 FullSourceManifest 完整保持：71活跃内容插画、43公共资源、12表面、1旧毛毡；源与运行PNG均同声明SHA，127 PNG/meta与旧合格gate同字节。68 full原稿加3项明确复用（C03/C48/I07），不是用同图改名填数。原132 PNG TextureImporter实际快照包括另5张复用纸面，132 PNG/meta也同字节，因此复用其真实旧采集记录，不冒称本轮又操作了Unity。

原全71跨作者复核及闭环按相同字节继承；本人曾绘制部分插画和初始附魔组件，不在本报告重新签自己的制作质量。新 MenuTable 源图/提示词、运行背景、材质、Shader、菜单及Setup由单独 menuTable绑定核查；不更改127历史目录，也不把旧 `legacyPngDependencies=[]` 当成新Bootstrap扫描。

## 运行证据与结论边界

完整Editor自动结果为1018 Edit /185 Play；采用现有独立完整报告和源/PDB/构建审查，未在此重复跑Unity。实际 Player有50项通过的运动/落定记录（1次初始运动，49次 completed / reduced-zero / skipped 落定检查）。这里只核真实记录，不把静态截图或数字检查称为已观看新录像、已人工认可全部动效节奏。

本次参考报告完整结束但为 `budget_exceeded` / exit3，serial明确 `diagnostic_performance_failed_strict_native_automated`：180.0108082秒、10138帧、frame p95 16.8469999999843ms，大于16.7ms；GPU覆盖97.2183862694812%。性能失败保留，本报告未重算CSV。当前自动原生显示模式切换通过，不沿用旧版本失败说成本次结果；物理拔屏/失焦、超工作区显示模式、Mac实际运行、AudioMixer及人工风格/体验验收仍按各自原证据限制保留。

没有新艺术阻断，不等于所有限制关闭。本 JSON 可作为rc.2本次技术美术审查输入。后续下载版稳定帧如需追加，另写补充文件，不能静默修改已用于封包的最终JSON。早期DRAFT、running快照、旧失败及原始PNG均保留。
