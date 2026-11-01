# 0f04e51 → 821ec59 媒体有限复用审查

2026-11-01，独立审查者 `/root/v07_spec_audit`；生产/Unity/Git只读，仅写cache/review。

**结论：可有限复用0f04e51已完成的视觉证据；必须保留其原源码身份与配置范围。** 本次差异没有改变产品UI、资产、计分表现、录制器或规则。不能把旧媒体重标为821ec59新采集，不能据此推定新Player或新完整回归通过。依据为 `QUALITY_GATES.md` 的影响分析与未受影响历史证据复用要求，没有新增门槛或放宽原标准。

## 两提交的精确差异

- 旧：`0f04e51a2add69ca69f44c2d0c7b8425367e7a89`
- 新：`821ec5992c34b59eddfe2a0ae984fbaca48f41f8`
- 比较范围：`DicePrototype/Assets`、`Packages`、`ProjectSettings` 的全部非 `~` Git源。

两边均1360个文件，零新增、零删除；**1358个Git blob完全相同**。只有两处源码改变，完整patch和两版原字节副本在 `artifacts/cache/v07-media-reuse-821ec59-review`：

| 文件 | 全部变化 | 对既有媒体的影响 |
| --- | --- | --- |
| Runtime/Verification/PlayerVerification.cs | ProbeRevision17→18；`VerifyHandbookPureReads` 首轮读表之前增加两行注释及 `yield return PressTargetReady("HandbookHands")` | 仅显式Player验证流程主动切真实牌型页签；旧录制器不经该探针。新探针行为需在新Player实际跑。 |
| Tests/PlayMode/V05InterfacePlayModeTests.cs | 原20次只读用例先打开Colors、确认all_red条目、关闭；每轮打开后切Hands | 为原失败增加真实跨页签前置；不改变产品或媒体录制逻辑。 |

独立执行精确反向替换后，两文件各自完整文本恢复为旧Git原文，未做换行归一化隐藏差异。Player原**95条Require逐项相同**；20轮、牌型14行、等级、pending、完整state/磁盘字节与键盘断言均保留；测试新增all_red断言。10秒预热、180秒采样、16.7ms预算和原生显示检查未改。新增页签操作经过现有 `PressTargetReady` 的真实按钮准备/事件路径，没有反射改 `handbookColors` 或伪造牌型行。

产品页签仍由 `GameApp.V05.cs` 保存实例内UI状态，原样不变；关闭Colors后再次打开仍是Colors。Bootstrap也原样不变，无显式验证参数时不会创建PlayerVerification对象。本次不是为验证器修改产品默认页签。已有独立 `PLAYER_HANDBOOK_PRECONDITION_REVIEW.md` 的102406两项专项2/2作为针对性背景证据保留，不替代新完整测试或Player。

## 可以复用什么

0f04e51媒体before/after的1361项清单一致，集合恰为上述1360个旧Git源加派生BuildIdentity；两处旧源码的SHA也与旧Git blob相同。旧媒体/独立报告绑定到0f04e51的关系保持完整。

- 已完成六配置屏幕和九段静音计分录像中，**此前真正完成独立观察的视觉条件**，可作为821ec59相同UI/资产/演出代码的有限延续证据。包括各报告明确覆盖的布局、抓手修复、最高档、Instant、Qualified、POST等；不把仅做SHA检查的图扩称实看。
- 原样保留每张图/视频的0f04e51、Runtime MVID `e858ca97-2062-4977-801f-579d213d9ff4`、采集时间、尺寸、字号与fixture。以本差异审查建立复用关系，不改旧manifest中的sourceCommit，不重录后冒充原片，也不把旧截图中的版本/构建信息当新构建身份。
- 九段仍是1920×1080/font150、静音固定1/60表现时钟。ContinuousHighest仍为一次Submit加两次只读回放；历史未覆盖家族/平台/状态不因复用得到通过。POST最终独立制作质量沿美术代理的 `FINAL_0F04E51_POST_INCREMENTAL_REVIEW.md`，不由作者重新自签。

这是对**未受这两项delta影响的已审条件**复用，不是让旧gate放行新源码。所有失败历史继续保留。

## 必须重新取得的证据

新完整999 Edit/154 Play及新PDB/起止源绑定等待真实结果，不能借旧报告认定完成。新的双平台构建身份、实际Player入口与完整回放、修正后的Hands前置、显示/性能/跨进程恢复及最终Player版本截图均须绑定821ec59重新执行；原0f04 Player手册失败、mode0失败不因静态修复变成通过。Mac实机和人工体验仍独立。

`checkpoint-resource-identity-821ec59.json` 已实际在10:34:52.4299741Z成功执行idle guard、同步导入及Resources读取。返回提交821ec59、Runtime MVID `dea6b318-5db4-4f86-9b94-c1dad2e85dca`；与旧identity相比只有sourceCommit字段改变，art0.7.0、p0.6.0、schema4、Unity6000.6.7f1及配置/黄金用例SHA相同。这只证明当时真实加载的新身份，不证明尚未提供的全量/Player结果。

AudioMixer缺口、静音媒体不验声音、离线媒体不验实时性能等边界不变。玩家说明/发行文案是否披露是独立文档事实，不代替Mixer实现或实际验收。本轮未调用Unity、未轮询正在执行的测试，也未修改HEAD或生产。

精确Git blob/原字节SHA、反向替换检查、95条Require核对及实际身份证据见同名bindings和cache `delta-bindings.json`。后续若出现UI/资产/表现共享代码的新变化，应再次按影响审查；本结论只覆盖上述两提交之间的已核delta。
