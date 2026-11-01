# art0.7.0 表现接口

本目录仅消费已保存的 p0.6.0 收据，不持有 `GameSession`、存储器或随机数源。没有规则实现或 schema 变更。原样规格见 `docs/specs/v07/ART_SPEC.md` 第18、20–23、29–30章，验收FX01–FX11。

## 集成顺序

1. UI保留执行前的独立 `RunState`，执行后取得真实最终快照。
2. 仅新成功保存结果调用 `PresentationReceiptAdapter.BuildCommitted(result, before, after, cfg)`。duplicate、pendingSave、noChange均拒绝启动奖励演出。
3. 捕获旧/缺损收据的解释异常时，使用 `Fallback` + Instant；已提交事务不得因展示失败被阻塞或重执行。
4. `PresentationSequence.Create(receipt, options)`；`PresentationPlayer.Play(sequence)`。
5. 每帧传 `Time.unscaledDeltaTime` 给 `Tick`。暂停使用 `Clock.Paused`；显示确认15秒必须继续由原显示逻辑真实时钟管理，不能使用PresentationClock。
6. `BeatStarted` 只更新视图，`FinalStateRequested` 直接显示已经保存的after状态，`Completed`解除表现输入门禁。不得在这些回调中提交或补发规则命令。
7. `Skip`、`Cancel`、减少动态切换直接结束视觉；显示切换/失焦也可使用Cancel。对象清理仅针对当前receipt。
8. 恢复调用 `Restore`，直接呈现最终状态并标记收据事件完成；可用 `Play(..., readOnlyReplay:true)` 查看只读记录，界面必须标“记录”。

`PresentationReceipt`、`PresentationEvent`、`PresentationBeat`为 detached 只读数据。数值保留十进制字符串和约分后的有理数，不用浮点数判档。before/operand/after中的有理数为`n`或`n/d`；UI可把`1/2`格式化成`0.5`，不能改数据。

## 冻结来源归属

既有ScoreExplanation逐骰及倍率分项已经有emitter/stateOwner；直接使用。既有全局C27/C44/C48/C50基础分仅记录技能聚合，本适配器按已冻结的同技能命中来源等分原精确总额，校验相加与收据一致，不重算得分。

既有规则没有C03/C12/C24/C25/C29的逐来源展示记录，按下列纯展示合同补充：

- C12：冻结当前点数与有效点数存在变化的目标；同技能幂等来源都保留归属。
- C03：首颗确实由蓝改有效红，且没有红潮覆盖；本来红色不制造数值变化。
- C24：实际`one_as_four`分组中有效红1目标；被C25覆盖时不归为实际分组变更。
- C25：实际`all_red`分组且有效红点数不全相同；相同点数组不制造合并增益。
- C29：实际成立顺子，但冻结有效点数不能成立原始5连顺子；完整自然5连不虚构缩短增益。

此归属只影响连锁N与只读说明，不控制技能条件、附魔或任何游戏状态。基础/倍率附魔与本技能按emitter去重。纯金币、永久改造、等级成长、次数和基金不增加N；非恒等×0/×0.5照常计入。条件命中仍以triggeredSourceIds为准，不拿N当触发列表。

后置EffectRecord只有技能ID时，事件保留全部冻结`SourceEmitterIds`组，`EmitterId=null`，不伪造单一最后写入者。每个原始事件及顺序保留；UI按同一物理面聚合，在480ms内一次抵达最后值，记录面板仍可查全部中间步骤。ActiveColorChanged与FundAccrued的真实所有者单独识别。C53每stateOwner只揭示一次，时间轴放在所有原卡/复制卡倍率之前。

## 时间、焦点与池

Normal全部非空阶段≤3200ms。前4个控制器主焦点依冻结来源顺序，其余分组显示但明细不丢。Fast每phase合并、≤800ms；Instant无瞬间多次闪光而直接最终画面。减少动态保留阶段与数字，不移动/缩放/抖动/粒子。

同一emitter在基础阶段已完整抬卡时，倍率阶段只亮角标；UI为当前receipt维护已抬卡集合。`PresentationEffectBudget`强制全局1主3辅助，池上限8标签/3线/3光环/12碎片/512粒子（低档128、减少动态0）。同源组可有多个静态细项，但不能再创建独立焦点。`PresentationObjectPool<T>`有界并在借出与归还时reset；reset回调必须重置颜色、透明度、比例、目标和完成状态。

## 音频

16逻辑cue经`AudioCueMap`路由25个WAV（另有原创90BPM音乐）。`PresentationAudioPlayer.Configure(clips, mixer, groups)`，暴露 `SetVolumes(master,music,sfx,ui)`、`PlayCue(...)`、`StopReceipt(receiptId)`、`PlayMusic()`。没有资源时静音返回false，不阻塞事务。

Unity集成者创建Master→Music、SFX、UI，SFX→Dice、Score、Ambience。四个暴露dB参数必须命名`MasterVolume`、`MusicVolume`、`SFXVolume`、`UIVolume`。Clip名字必须与WAV assetId一致。UI组路由hover/press/denied/coin/refresh；基金估值没有coin事件。收据重复由PresentationPlayer过滤，音频额外同源120ms合并、hover全局200ms、denied全局300ms；12预分配AudioSource、最多8路有声。滚动≤2路、锁定≤3、破碎≤2。爆分只播最终L3/4/5一条；音阶最高12半音。sourceKey建议`receiptId/emitterId`，同目标聚合后再调用一次。

源和许可：`ArtSource/Art07/Audio/recipe.json`，可复现脚本`tools/art_v07/audio/generate_audio.py`。当前为原创合成音，已做PCM格式/峰值与确定性字节检查；主混音峰值、实际听感和Unity导入由引擎验证，不由脚本峰值代替。

## 验证边界

`Validation~`下.NET/NUnit runner是预检，不是Unity。真实EditMode与Player由root串行运行。所有失败JSON保留。测试覆盖160条冻结黄金、精确阈值、G147、来源去重/幂等、共享随机、后置来源组、32来源预算、暂停/跳过/恢复/三速、焦点/池、音频映射和并发。实际动画外观、可读性、声音听感与性能仍需最终Player证据。
