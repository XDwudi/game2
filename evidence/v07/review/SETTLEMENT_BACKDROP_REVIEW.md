# S09 结算桌面背景独立复核

状态：**S09 live 背景、GPU、真实保存重试及冷恢复核心行为在已列720/1080证据内通过；最新左栏避让修复已完成两个分辨率的独立实图闭环。不是整版美术或Player通过。** 审查者 `/root/v07_shop_ui`，实施者 `/root/v07_spec_audit`，集成与唯一 Unity 执行者 `/root`。输入 art0.7.0，玩法 p0.6.0 / schema4；依据 ART_SPEC S09 第234行的原桌面背景、35%压暗、同一结算收据和不重复奖励要求。

独立性范围：本审查者没有编写 S09 背景组件、冷恢复重建或 HoldPrepared 新接口。曾参与通用交易快照材质的早期修复，因此这里独立审查的是本轮 S09 集成及其实际外观，不借此独立签署自己早期通用快照实现的全部质量。

## 2026-11-01 实际复核补记

原失败、原临时目录及归档均保留。本审查没有运行Unity、修改生产或降低测试标准。下表是实际测试批次，不是本次独立审查宣称覆盖批次内全部其他模块。

| 批次/归档 | 实际结果 | 本次相关范围 |
| --- | --- | --- |
| `v07-combined-1080-065335` | 11项10过1败 | 早期S09 live、GPU三字号、Retry；冷恢复测试漏重新解锁Debug而失败 |
| `v07-settle-dice-1080-065952` | 9/9 | Skip布局修复后的live三字号、GPU、200次复用、真实Retry及cold |
| `v07-combined-720-070131` | 14/14 | 同生产源720版本；live三字号、GPU、200次复用、真实Retry及cold |
| `v07-dice-cold-720-070724` | 2/2 | 冷恢复实际持久化150%偏好并断言1.5后补录 |
| `v07-dice-cold-1080-070806` | 2/2 | 同上1080；发现左栏旧收入摘要压按钮边缘 |
| `v07-follow-cold-1080-071343` | 2/2 | Header只在真实手内阶段显示预算；cold150左栏余项关闭 |
| `v07-loading-status-720-072245` | 9/9批次 | 最新cold150的720补验，独立实图确认预算/旧摘要不压左栏按钮 |

各批archive所有记录文件的实际SHA256/bytes、NUnit报告SHA、binding.start/finish物理源清单及已加载程序集MVID/DLL SHA已逐项核对，**零归档不符、零批内源差异、零批内程序集差异**。明细见 `artifacts/cache/v07-settlement-backdrop-review/actual-evidence-audit.json`。这证明列出的源/装载程序集在采集时稳定；该binding没有单独Portable PDB文档校验，本报告不把它写成完整PDB验证或最终发行构建绑定。

实际看过的每一PNG及其SHA列于 `reviewed-images.json`；该清单同时含独立骰子交互审查。未看过的同批其他PNG不因SHA通过就计作视觉通过。065952/070131各看了live100/125/150、旧cold、Retry前后、GPU每字号source/snapshot；070724/070806/071343另看实际cold150。

### 规格符合性与制作质量

1. **Live桌面与35%背景：通过（列出配置）。** 720/1080三字号图保留10张控制器、局内道具、骰池和空提交槽。背景压暗，纸单与当前Header清楚；纸单150%使用真实滚动，不缩字。精确alpha0.35来自源检查，图片只验证实效，没有用主观目测宣称精确像素alpha。
2. **Skip后错误冻结中间pose：已修复。** 065335早期三字号图的0/8空槽仍有3D骰、池区只剩标签，实际失败不能被当时NUnit通过掩盖。root在Skip同步Refresh后及捕获前完成布局，065952/070131的live和Retry整屏已无空槽骰残影，骰身和池标签对齐；新测试保留同帧`IsTransitioning=false`断言，没有靠延时避过可操作窗口。
3. **GPU中文字与玻璃pose：通过（当前六组样本）。** 720/1080×100/125/150 source/snapshot共12PNG逐对实看，未见方块字、缺字、玻璃双影或pose改变。每组实测11个字形；最大mean error为720/100的0.155556，其余0，全部mismatch fraction=0，原阈值mean≤2、mismatch≤2%均保留。单玻璃骰ROI最大mean0、mismatch0，可见ink最少2485。不是把一个卡/骰的ROI推定全资源覆盖。
4. **生命周期与规则隔离：通过（自动断言范围）。** 两尺寸实际GPU测试均包含200次Capture/Show/Clear，材质实例与UI/world池数量不增长，源TMP停用后owned材质仍有效，销毁后owned材质释放。无Selectable/Graphic、无Collider，新背景不产生按钮；state/sequence/save在循环前后不变。这里只读检查了实际通过的断言和源，无200张周期截图；不声称逐周期肉眼观察，也不称性能验收。
5. **真实保存失败Retry：通过。** 065952/070131各四图相关组中的S09前/后两图实看：失败前13,338、金币30、10骰池/空槽，Retry后纸单基础5+余次2+利息3=现金净增10，当前金币40；背景仍是live，未误回记录模式。实际文件锁、候选不变、仅一次序列/奖励提交及重复Retry拒绝由测试保留验证。最初静态审查发现的Retry未捕获路径已由root补入真实Retry流程，下面历史表不再代表当前缺陷。
6. **冷恢复：通过（行为、最新实际150%）。** 065952/070131旧`cold-font150`实际回到100%，只能证功能恢复；本审查发现后root补PlayerPrefs150与重启1.5断言，070724/070806确为150%。背景明确“已提交记录”、控制器标“记录”，显示已保存提交当前面；没有伪装成完整原桌面。保存字节/state不变、无演出重播、离开主菜单背景隐藏均有实际断言。071343新图左栏不再夹一条被按钮截断的旧“操作结束+10金币”手内预算；072245的720实际图也无该旧摘要，纸面150%与四个左栏按钮均完整。后批另含启动loader改动，具体物理源和程序集按各批绑定，不笼统称所有批次同一MVID。

结论只关闭以上限定缺陷。最终完整回归、Player/native平台、连续交互体验和人工美术决定不在此报告内。旧历史检查及其发现如下保留，已由补记明确更新。

## 初次静态源绑定（历史）

候选 `artifacts/cache/v07-settlement-backdrop-rework/integration.patch` SHA `de1f0ef6b833cca089cf81efe8daa44bc6351757ca4037c6f35d443bbe04af70`；manifest SHA `6dbfd8565a752b365aeb3329602c0412943da81d91fd5ff7e8b3d8b21fd92fd6`。两个新增生产源字节 SHA 与候选 manifest 一致。静态检查涉及的完整源 SHA 见 `artifacts/cache/v07-settlement-backdrop-review/static-bindings.json`。这些是检查时源绑定，尚不是实际测试报告的 PDB/MVID 或 Player 构建证明。

## 静态结论

| 检查 | 静态所见 | 实际核验状态 |
| --- | --- | --- |
| 原桌面捕获 | Send 执行 Submit/EndStage 前同步捕获；只在同 commandId 成功进入 StageSettlement 后绑定结算 identity。随后 Rebuild 先同步背景状态、再隐藏原桌面与原骰。没有后台线程或异步推迟核心事务。 | 待看 live 整屏与源/冻结 ROI |
| 快照与规则分离 | 新组件只复制 UI renderer mesh、材质及 3D MeshRenderer pose/PropertyBlock。无规则命令、RNG、Save 或金额写入。展示 DTO 对当前面覆盖只作用于 detached die。 | 待核 state/save/sequence 的真实记录 |
| 材质和对象生命周期 | 每 UI 分组沿用 256 Graphic 上限；各组专用 Motion 实例，HoldPrepared 不推进时间、不调用回调。UI Mesh/Material 自有并销毁；3D 引用现有不可变 mesh/material、不销毁共享资源；退出 Clear 隐藏并清空引用，池节点保留复用。 | 待看真实 200 次复用/销毁结果与退出画面 |
| 输入隔离 | 背景 UI 只有 CanvasRenderer，父 CanvasGroup 关闭 interactable/blocksRaycasts；3D 副本只有 MeshFilter/MeshRenderer/Transform，无 Collider。真实结算单和可跳过 scrim 在 main 上层。 | 待看实际 raycast/按钮与遮罩层级 |
| 35% 压暗 | `SettlementScrim07` 明确 alpha 0.35，位于背景之后、纸单之前。新 Header 显示真实结算后数值；旧 Stats/ScorePanel/导航不纳入背景，避免旧金额或分数充当终态。 | 待实际看压暗、文字和纸单层级；代码颜色不等于画面通过 |
| 冷恢复 | 只取 stageId 匹配的 lastSubmission 快照，标“已提交记录”；没有匹配记录时标“已保存关末 · 静态桌面”。最多显示前 8 提交槽，超限明确总数。未提交池只取现存永久上下文。没有重抽或重计分。 | 待看冷恢复文字、当前面及无演出 |
| 重试保存边界 | 保存失败会清除准备快照。现 RetrySave 路径只重新准备交易表现，未重新准备 S09 live 背景；重试成功预计走已保存记录重建，不保留失败前完整 live 桌面。 | 已通知 root；需实际覆盖并明确这是记录回退，不能声称重试保留逐像素原桌面 |

当前静态检查没有发现本轮新增重复奖励或规则采样路径。以上不是引擎、GPU、制作质量或整版通过结论。

## 待运行证据

root 计划在 720 与 1080 运行 `PLAY_ART07_SETTLE_BACKDROP_` 两项及原 SETTLE 回归。本审查将核对原 NUnit、独立存档目录、实际源/冻结 PNG、逐 glyph / 骰子 ROI 指标、200 次复用、冷恢复和 source/PDB/MVID 绑定；失败必须保留。

实际画面优先看：100/125/150% 卡名与附魔文字是否完整、玻璃骰是否双影或改变材质、纸单可读性、背景确实保留且压暗、冷恢复“记录”说明的含义，以及 Continue/返回主菜单是否遗留骰子或 renderer。不同分辨率/模式切换中的旧 3D 投影仍需真实检查，单次静态图不能证明所有显示模式。未将离线编译用作任何通过依据。
