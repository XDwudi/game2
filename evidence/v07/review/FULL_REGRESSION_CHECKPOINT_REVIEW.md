# 86d740c 完整回归证据独立复核：REWORK

2026-11-01；复核者 `/root/v07_shop_ui`。输入 art0.7.0、玩法p0.6.0/schema4，Unity6000.6.7f1，源码检查点 `86d740cdcc3045605e41417dc0353d4cb4d76c60`。仅独立检查真实发现、NUnit、起止快照、当前物理源文件、程序集与Portable PDB；未启动Unity/Player，未修改生产、测试或门禁。

**结论：证据名称与源/程序集对应关系可信，但本检查点完整回归失败，状态REWORK，不能生成通过门禁。** EditMode 999/999；PlayMode 152项中145通过、7失败、0跳过/不确定。AudioMixer硬缺口、Player、macOS和人工验收均不由本报告关闭。

## 已完成的真实EditMode核对

`evidence/v07/unity/nunit-20261101-073920.xml`：2026-11-01 07:39:01Z至07:39:20Z，19.1582457秒；999/999，0失败、0跳过、0不确定。读取全部999个叶用例，唯一完整名称集合与 `discovery-EditMode.json` 的999项逐项相同，无缺漏、额外用例或重复名称；所有叶实际Passed，platform为EditMode。没有仅依据根计数或退出码给出结论。

报告SHA `db030a2eb8ad74546cfbef27e19c3358293e2a82d6063aedfc8a3dd198b1a656` 与实际binding一致。开始07:39:01.3629068Z、结束07:39:21.2464292Z的1358个源文件路径/SHA均与当前源逐项相同；两个时点5个实际加载MVID/DLL SHA与当前程序集及独立PDB审计相同。

## 独立PDB与物理源检查

实际执行既有只读 `artifacts/cache/v07-build-preflight/PdbAudit/bin/Debug/net10.0/PdbAudit.dll`，新输出 `artifacts/cache/v07-full-regression-review/pdb-before-play-completion.json`；未运行Unity、未编译或改写门禁。5程序集、168个项目文档、0校验差异。独立再次检查每文档SHA、程序集DLL/PDB SHA、PDB CodeView身份、各程序集文档计数；文档路径集合恰好覆盖当前 `Assets/Game` 全部168个C#文件，无遗漏/重复/范围外文件。

| 程序集 | 实际加载/物理/PDB一致的MVID |
| --- | --- |
| DiceCraft.Core | `8f690e0d-a075-4b72-98e4-2cb64ced2fa3` |
| DiceCraft.Runtime | `3fcb8c20-51f2-4fb3-b00e-d1db7ff83de7` |
| DiceCraft.EditMode | `72ee6111-518d-4daf-b551-60ed8f282366` |
| DiceCraft.PlayModeTests | `622bc020-9f0f-4248-b09a-08b4ad01de8b` |
| DiceCraft.Editor | `c5165741-6376-48d0-b120-051af7ad9916` |

**审计时点限定：** 完整报告结束后的第一次独立源/PDB核对时，HEAD与指定检查点一致，Assets/Packages/ProjectSettings生产SCOPES无未提交变动。BuildIdentity物理文件正确指向p0.7.0/art0.7.0及该commit，配置p0.6.0/schema4、配置/GoldenCases SHA及Unity版本相符。测试源快照按既有合同排除生成的BuildIdentity与测试临时场景；不只凭PDB推定Resources已载入该JSON。随后Editor正常退出保存了动态fallback字体，见下方边界；不能把此前的“当前源相同”误读为退出后仍干净。

补充已核root真实只读资源采样 `evidence/v07/unity/checkpoint-resource-identity-0743.json`：其内部UTC为 **07:42:25.6750009Z**（文件名0743只是标签），Play中frame5992，CLI `Resources.Load<TextAsset>("BuildIdentity")`返回的完整JSON与当前物理文件一致，Runtime MVID同上。命令内容只有读取，结果success且diagnostics/errors为空；这提供实际已导入/加载identity的独立关联，未据磁盘/PDB倒填。该命令由root执行，本agent只读复核。

## 完整PlayMode实际失败与对应关系

`nunit-20261101-075917.xml`：07:39:47Z至07:59:17Z、1169.7362802秒。152个唯一实际叶用例与完整PlayMode发现名称集合完全相同，无漏跑、额外或重复用例。152叶实数为145 Passed、7 Failed；root计数与叶数一致，没有跳过/不确定。报告与binding SHA相同；07:39:47.8903189Z开始与07:59:18.3294489Z结束的1358源文件、5实际加载程序集完全一致，并与首次后验的物理源/PDB对应。

本agent在完整报告后再次实际运行只读PdbAudit：`pdb-after-full-play-075917.json`仍为5程序集、168项目文档、0不符。`full-075917-independent.json`明确保留 `PlayMode: report contains failure/error details`，没有调用gate、重写结果或把失败叶移出集合。

| 失败用例（完整名称见绑定JSON） | 实际失败 | 本次责任边界 |
| --- | --- | --- |
| `PLAY_ART07_UPGRADE_ActualCeramicAndSingleQuestionFlipRenderBeforeMiddleAndAfter` | 期望进度0.175，实际0；Upgrade测试42行 | root/其它实施者调查，不在此推断产品或测试根因 |
| `PLAY_ART07_UPGRADE_FastReducedPauseAndSkipKeepCommittedStateAndResetSurfaces` | 期望0.15，实际0；115行 | 同上 |
| `PLAY_CoreLoop_ClickLockReorderAndSubmitMatchesPreview` | 锁序期望D001开头，实际D002；159行 | 同上 |
| `PLAY_PooledRefreshKeepsNodeIdentitiesAndResetsScrollInputAndSlider` | Factor0后找不到ScoreDetail；34行 | 旧导航未迁移；见下方最小候选 |
| `PLAY_RealKeyboardTenDiceMappingModalSeedFocusAndHeldKeys` | 期望第二颗D002加入，实际缺少；143行 | root/其它实施者调查 |
| `PLAY_ReloadDuringRollRestoresLocksOrderAndAcceptedFaceResults` | 期望D001开头，实际D003；240行 | root/其它实施者调查 |
| `PLAY_V06_RemoteThirtyTwoTargetsScrollAndKeepExactlyTwoWithoutOverlap` | 期望“全部 32 件控制器”，实际“全部 32 件 >”；25行 | 紧凑按钮文案陈旧；见下方最小候选 |

两处测试迁移的静态结论：S07规格要求160ms展开三列抽屉；现Factor0到 `ScoreDrawerTable`，可点击 `FullScoreDetails` 进入保留的原 `ScoreDetail`。候选增加一次原有真实pointer/raycast helper点击，不直接调用ShowBreakdown或核心规则。Debug32规格要求局部预览与表格承载；当前按钮仍准确表达全部32件、无正常10槽分母，短标题“全部 32 件 >”是该布局的实际入口。只更新精确标题预期，原滚轮、完全入视口、first-hit、恰好两目标、一次消耗、state/save/RNG断言一字不变。**这只证明旧断言与新界面不一致，候选尚未被本agent在Unity运行，不能宣称两项修后已过。**

候选在 `legacy-ui-candidates/minimal.patch`，SHA `960589ee053058efbe292abf759ab9f9d9442d5bb285aa377d8cfed5e4297822`。UiPooling原46断言行全部保留；Remote原47断言行仅修改该标题字面值，不改任何数值阈值或轮次。全PlayMode源码加两候选离线类型编译0错0警告；初次缺UnityEditor引用的2错误日志保留，补离线引用后成功，不等于Unity编译/运行。

## Editor退出后的字体源变化

root报告正常Exit后TMP动态fallback缓存落盘；独立比较 `DiceChineseFallback.asset` 与86d740c：22→26 glyph，新增U+63ED“揭”、U+300C“「”、U+300D“」”、U+8DDD“距”。旧22 glyph与字符记录无变化/删除；1024×1024 atlas的8806个变化像素全部在4个新used rect中，原used rect没有改变像素。除glyph/character/used/free/atlas动态字段外，其他字段逐字相同；AtlasPopulationMode仍1。静态证据符合正常动态追加，没有据此修改资源或禁用dynamic。

旧文件SHA `68587fe919d2abf89846f91ae611373cbc1b21c78c1f3087e23dfc010bc96c82`，退出后SHA `f4f28bb0795587f3915088f5c975d1c96d8c62b35c674d7c200b1138f7557069`。详见 `dynamic-font-audit.json`及去除巨大atlas hex行的可读diff。此项是静态差分，不是新glyph GPU验收。保留新增字体，修复测试后按root既定流程保存资产、检查二进制、建立新checkpoint并绑定新回归；旧1358源绑定只适用于该批起止及当时后验，不能套到新font或随后测试修改上。

## 保留边界

- 两份完整真实报告及其失败原件保留。修复后需真实复验；本报告不以静态候选关闭原7败。
- 当前7个未跟踪历史发行ZIP保留；只读审计未删除、移动、重打包它们。
- 此审计不是图像制作质量、完整连续动画、实时性能、设备兼容或发布检查；旧失败报告和SpriteAtlas原生崩溃现场保留，不因单批通过推定根因解决。
- 模块实施者和证据复核角色不同：此agent曾实施部分Shop/UI及测试，因此只签此次客观完整运行/源绑定一致性，不自签其历史美术实现质量。

可复现脚本 `artifacts/cache/v07-full-regression-review/audit_regression.py`；先前Edit记录保留原未审Play状态，完整记录为 `full-075917-independent.json`，含发现/报告/binding/PDB工具与输出SHA、版本检查、7ZIP路径及7个真实失败。退出后字体分析独立存放，不覆盖先前审计。最终报告/候选/证据SHA见 `final-review-bindings.json`。root负责STATE与后续源/构建冻结；本次未写Assets/Editor/index/门禁。
