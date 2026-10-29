# P0 v0.4 最终独立技术评审

评审者：`/root/spec_audit`，2026-10-29 UTC（用户时区2026-10-28）。输入生产提交 **`f060098533d26e99e7eba7d0360ecfea78cbe517`**、build10、Player07。评审者没有实施生产代码、改变测试预期或验收标准，也没有操作Unity；独立执行了核心及真实Windows IO复跑、原始运行数据复算、源码/PDB绑定、测试用例核对和实际截图目视检查。Unity测试、构建及播放器由root执行，原始报告已独立核查。

**结论：Windows P0的核心规则、功能、制作和本机性能技术范围通过。没有未关闭的阻断性代码或界面缺陷。** 这是指定设备、版本、场景下的技术接受，不是商业发布、移动端通过、人工体验或美术风格签署。AC-EXPERIENCE尚未运行；最终压缩包及远端Git交接需在本报告后绑定实物哈希。因此本报告不把整个项目标为无条件ACCEPTED。

## 版本和证据绑定

- 权威输入：`docs/specs/P0_v0.4.md`（配置p0.4.0，save schema2），SHA256 `57ce15488dad390ad46f1ee57afc63482c6890502eabb0eacd30b940fab9d762`。Word阅读版SHA256 `c89c1c35b4374b425d4d29ba5185f054b83618b9e89f8badfc027629845f6f51`。冻结验收表为`docs/tasks/P0-IMPLEMENTATION.md`第25–39行，没有因实测结果改变。
- build10：`evidence/unity/build-10.json`，Succeeded，0 error / 0 warning，195,201,465 bytes。实际运行`evidence/runs/player/player-20261029-025732-31091505`，build GUID **`ee0c4c6ab89e41008cade950261da73a`**。
- Runtime MVID **`25bd34a0-8ad3-40f3-acb3-097ef4cd6fab`**，DLL SHA256 **`346b4c1ec85e75ad4f7f15d10f2968c97b1d836e235474878718fab886b43d59`**。Core DLL SHA256 **`db7798eb8ceec1febb0e020558155a24a533c23fd1229afe21ee9f82ed9c5dac`**。
- `build10-binding-independent.json`、`build10-pdb-independent.json`、`unity08-12-pdb-independent.json`：Player2程序集22项目源文档、Editor5程序集39项目源文档与当前源码全部相符，零不一致。相对build09仅`GameApp.cs`改变，其SHA由`d7644470…`变为`eb4529d315f2883544ccec23988fcf4523d7eb8b67c3b0c83dd2737d75717b71`；实际变化是Refresh完成UI树后统一`Canvas.ForceUpdateCanvases`。
- 实际EditMode08 **341/341**，`nunit-20261029-024239.xml`；实际PlayMode12 **20/20**，`nunit-20261029-025518.xml`；零失败、零跳过，逐用例与JSON一致。Core DLL、所有EditMode源、AtomicSaveStore、FramePacer均未改变，因此341项可合理复用；受GameApp影响的20项原生UI回归已重新运行。不是使用旧哈希报告自动放行新源。
- `acceptance-build09-independent.json`原附录C **70个ID**均映射到真实通过用例，相关规则、测试与资产在build10未改变。映射同时依赖此前独立规则/事务/IO复跑及本轮真实引擎行为，不能仅凭用例名称证明行为。

## 规格、制作和实际运行分列

| 维度 | 结论 | 依据和边界 |
| --- | --- | --- |
| 规格符合性 | 技术范围通过 | 机械提取配置及原黄金预期未修改；确定性计分/概率/三路RNG、事务、38项内容、六关经济、保存恢复、UI说明及逐步计分满足已审范围；原AC-PERF条件按下文限定接受 |
| 制作质量 | 本轮原型技术通过 | 实际发现的备份破坏、异常处理、UI重复输入/旧回调/历史效果/计分说明/布局及测量完整性缺陷均修复并重验；可编辑源、隔离Core、原子保存、可追溯错误恢复、UI对象复用和构建绑定具备证据 |
| 实际运行 | Windows指定构建通过 | 非零真实341 EditMode/20 PlayMode；正常非Debug97步六关、31实际落地、三分辨率、180秒最大容量压力；独立复算原始数据并目视关键图，不把静态审查当实跑 |
| 人工体验、风格及未来平台 | 未运行 / 本轮N/A | 5–8人每人至少2局尚无真实参与者记录；美术风格由用户决定；iOS/Android不是本轮Windows首构建范围，未验收 |

## 原冻结AC逐条结论

| ID | 技术结论 | 实际证据与限制 |
| --- | --- | --- |
| AC-CONFIG | 通过 | 38唯一内容、11牌型、缺字段/非法配置拒绝；原配置与规格机械比对及实际EditMode |
| AC-SCORE | 通过 | 原52黄金的全部18字段、57解释trace；此前独立3072组合解释oracle；只最终取整、空预览/提交前效果无状态/RNG副作用，历史快照来源准确 |
| AC-PROB | 通过 | 原五向量、RNG序列、2916纹理组合、单位和总量边界；独立宿主与实际EditMode |
| AC-STATE | 通过 | 114事务用例及A/C/D边界；真实InputSystem数字/E/Space/Enter、模态屏蔽、锁定调序、损坏/复制与临时寿命 |
| AC-CONTENT | 通过于P0范围 | 20C/7I/4O/7U命中/资格/非法或重复边界；U01–U07实际目标选择、取消、重复属性禁用；38对应图标 |
| AC-ECON | 通过 | E01–E15、独立精确商店供给/RNG oracle；同店C09资格双向更新、O02旧价与后续折扣、随机/自选升级顺序、满7删后买原候选不重抽；正常六关实际记录 |
| AC-SAVE | 通过 | 独立真实Windows FileStream/Flush/File.Replace与损坏备份回归；11实际JsonUtility持久化测试；实际文件锁、Roll损坏/Submit复制多次失败重试，候选/RNG冻结且效果/日志只一次 |
| AC-UNITY | 通过于build10 | Unity6000.6.7f1，URP/uGUI/TMP/Input System锁定依赖；实际341/20、多次Play、构建和Windows启动，不存在零项测试冒充通过 |
| AC-VISUAL | 技术通过于build10 | 实际720p/1080p/1440p、分层永久预览、10骰/7栏/10控制器/5道具、可滚动逐步解释、正确点阵/朝上面；风格接受仍归用户 |
| AC-BUILD | 通过于build10 | 正常非Debug种子20261032、97合法命令、六关通关；每步显示/磁盘与trace一致；源/PDB/运行MVID对应 |
| AC-PERF | **本机Windows P0技术通过** | 原场景完整180.0145934秒；冻结LateUpdate P95≤16.7ms及≥95%完整采样满足；多原生时钟与真实工作改善支持其有效性，剩余尾部和测点限制如下完整披露 |
| AC-SOURCE | 源件和本地追溯通过；最终交接待绑定 | Unity/Blender可编辑源、实际重开导出、FBX/动画/38图标源、配置和复现方法已核；生产f060098已绑定，最终ZIP及远端推送由root完成后追加交接哈希 |
| AC-EXPERIENCE | **未运行** | 原要求5–8人每人≥2局；AI回放、搜得获胜种子和自动断言不能代替理解、误锁、购买价值、节奏与乐趣的真人证据 |

## 正常玩法与视觉回归

`audit_player07.py`独立23项检查全部通过；`player07-independent-artifact-audit.json`保存原报告、图像及配置绑定。97条命令逐字段与既有trace匹配，所有序号连续、`isDebug=false`、每步磁盘与已呈现状态一致。六关得分为208 / 424 / 528 / 884 / 1250 / 1908，最终RunWon、9金币。种子经过搜索，不作胜率或平衡证据。Debug压力使用独立存档，结束后正常存档SHA未变。

31次实际投掷落地的RollPivot位置/旋转/缩放误差为0，已提交物理面的法线朝上；首普通翻滚有运动采样，第二次真实减少动态效果为零时长，另有跳过动画路径。无需把截图冒充动画连续性证据。

播放器接受46张截图；目录共48张PNG含启动预热图。自动核对实际屏幕尺寸、非黑内容、无拒绝截图后，评审者按原分辨率实际目视以下10张：

- `normal-selecting-1920x1080.png`、`normal-shop-1920x1080.png`、`normal-run-won.png`。
- `debug-die-detail-permanent-preview-1280x720.png`。
- `debug-score-preview-1280x720.png`、`debug-score-preview-middle-1280x720.png`、`debug-score-preview-bottom-1280x720.png`。
- `debug-ten-dice-unlocked-pool-2560x1440.png`、`debug-stress-capacity-1280x720.png`、`debug-stress-final.png`。

未发现黑图、旧标签重影、底层面板遮挡预览、按钮/水印重叠、容量丢失或逐骰/全局乘法步骤无法阅读的回归。历史分层状态、永久/current/effective区别与滚动明细由真实交互测试及这些画面共同支持。Development Build / trial水印仍可见，是当前原型构建属性；不声称发布版外观完成。

## 性能原始结果和接受理由

设备为Windows10 19045 x64、AMD Ryzen9 5900X、约64GB RAM、RTX3080 10GB、D3D11。1920×1080 Windowed / PC画质，Unity6000.6.7f1。自定义生产60Hz节拍安装有效，原生vSync0 / target=-1由节拍器接管并在停止时恢复。不是提高目标吞吐来获得更短帧。完整场景保持10骰、7槽、10控制器、5道具及多层外观，190轮操作含10秒预热，采样180.0145934秒、10,750完整帧（10,751原始行）。真实预览、UI刷新、原子自动保存与日志持续工作。

| 全量原始指标 | P95 | 最大值 | 有效覆盖 |
| --- | ---: | ---: | ---: |
| 原LateUpdate间隔 | **16.6807ms** | 24.5388ms | 100% |
| Main Thread | 16.9783ms | 24.7554ms | 100% |
| FTM FrameStart间隔 | 16.9825ms | 24.7582ms | 100% |
| FTM PresentCalled间隔 | 16.9369ms | 24.7138ms | 100% |
| FTM FrameComplete间隔 | 16.9452ms | 24.7165ms | 100% |
| GPU帧时间 | 0.23552ms | 0.305152ms | 98.5209% |
| 每帧GC分配 | 56,280B | 3,519,296B | 100% |
| Canvas.BuildBatch | 0.0066ms | 0.0326ms | 100% |

独立重算10项原指标的分位数/均值/最大值/覆盖、UI流式采样及GPU分类与报告一致；观察缓冲扩容0，采样无不可能GPU值、无时间戳警告。`audit_player07_clocks.py`重新构造全部原始时钟链、gap/重复/倒退分类，零不一致。原CSV SHA256 **`ba38ffbdf63be2e8195f15d13dfa150132495a291e2a56524e1325fcb5b93178`**。原生console保留被裁剪/不支持的DOF/Panini后处理shader提示，当前场景不使用这些效果；没有发现应用异常、崩溃或swapchain坏时间戳记录。构建0warning不等于启动日志完全无提示。

原规格第30节为1080p、60FPS目标、约16.7ms及至少3分钟完整场景记录；冻结工程探针继续以原LateUpdate P95≤16.7ms判帧预算，并要求关键指标≥95%有效覆盖。**该判据未修改，三个后加FTM诊断轴也没有被事后添加为三个新16.7ms硬门槛。** 本次接受不是仅看exit0：Player06虽原gate也通过，但存在明显的调度后UI工作摆动，已明确拒绝并保留`PLAYER06_REVIEW.md`。随后仅在Refresh后真实完成已排队Canvas/TMP工作，pacer位置、所有探针时点、阈值和操作日程完全不动；相同完整场景重跑后，Main/FrameStart/Present/Complete P95从约18.75ms同步降到16.94–16.98ms。

`audit_player06_07_comparison.py`独立从两份全量CSV核对五轴和尾部计数、GC总量。全序列Present>18ms后紧接<15.5ms的对数 **901→1**；按既有动作推断匹配的子集是899→0，三类纯刷新540个>18ms消失。全量Present>18ms **901→242**。这支持生产UI尾段扰动已实质减少，非仅移动测点、跳过UI、过滤坏帧或缩小压力范围。

同时保留以下限制：

- 平均周期16.7440ms，约**59.72FPS**，不是每帧精确60；最大原生间隔24.7582ms，Present>20ms为119帧。原生三轴P95仍高于16.7ms，不能写成所有计时器都低于预算。
- 真实事务尾段仍较贵：最后30秒的Roll/Reorder匹配Present均值约21.7ms，单次分配约3.3MB；总分配约767MB且与Player06基本不变。最慢帧和平均周期没有随着刷新摆动一起改善，不把ForceUpdate宣传成消除GC或所有卡顿。原规格没有另定零分配/最大帧硬预算，本次技术接受不删除这些优化余地，也不外推更长会话或低端设备。
- LateUpdate表示该脚本阶段间隔；FTM PresentCalled为CPU发起Present，Complete为GPU完成通知CPU的时点，均非显示器物理scanout。异步`GetLatestTimings(1)`相邻观察不严格证明每个原生记录无遗漏；三轴覆盖100%只是其观察链的完整性。未以最优时钟替换原gate。
- 节拍器最多1.5ms尾段自旋有CPU/功耗代价；本次不声称功耗、移动端或所有驱动适配通过。实际销毁/重装及native cap恢复有同构建spike04证据；focus/pause是对真实组件注入回调，不是OS切窗测试，不称真实Alt-Tab已验。

上述数据满足本次原型已冻结的目标及记录要求。保留有限尾部波动与设备限制，是如实限定接受范围，不是降低或重写阈值。

## 缺陷关闭与历史保留

- 存档：损坏active覆盖有效backup、错误异常过滤均有独立失败复现和修复复验；后续validator复用不缓存有效性、不跳语义检查。见`REVIEW.md`、`ATOMIC_VALIDATOR_REUSE_REVIEW.md`及本轮真实11项持久化/保存失败UI回归。
- 交互：首次NewRun失败、readonly日志、商店资格、Enter双消费者、历史分数/效果匹配、C07/C15提交前目标及逐步加乘解释均已真实回归。见`UI_REVIEW.md`、`UNITY_RUNTIME_REVIEW.md`、`SCORE_TRACE_REVIEW.md`、`UI_HIGH_RISK_CHECKLIST.md`。
- 池化：骰面旧listener、Slider填充、TMP输入光标生命周期风险经修复和真实反复面板/目标切换关闭；20项PlayMode在最后GameApp更改后再次运行。见`UI_POOL_REVIEW.md`及PlayMode12。
- 视觉/探针：黑帧、遮挡、caption、水印布局、录制容量、无效GPU样本及观察者扩容均保留原失败，再由新构建证据关闭。Player01–05失败/缺测、Player06单轴pass但未接受的原始结果继续保留，后续成功不改写历史。

## 交接边界

截至本报告，生产f060098的技术评审已完成；没有要求继续修复的本轮阻断缺陷。最终可玩ZIP、包内文件哈希与远端Git提交尚待root提供，AC-SOURCE的最终交接部分需核对后补充，不能使用本地提交存在推定已经推送。用户真人试玩及美术风格判断保持未签；它们不由自动测试或本审查代填。移动端有理由标记本轮N/A。新代码、资产或构建变化使受影响结论重新进入验证。
