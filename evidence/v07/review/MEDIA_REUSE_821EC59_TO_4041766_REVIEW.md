# 821ec59 → 4041766：精确源差异与媒体有限复用

结论：**原已审视觉条件可有限续用，保留原采集身份。** 本次只读Git提交、原媒体报告与源文件；没有操作Unity或执行新测试，不把旧媒体改标为4041766。

旧提交 `821ec5992c34b59eddfe2a0ae984fbaca48f41f8`；新本地提交 `404176661240a5d9834895416ed3719d6493e253`。比较Assets、Packages、ProjectSettings全部非`~` Git源：两边均 **1360文件**，零新增/删除，**1358个blob完全相同**，仅下列两文件变化：

| 文件 | 完整delta | 原断言 |
|---|---|---|
| Runtime/Verification/PlayerVerification.V06.cs | 首次固定货架positions取样前新增6行：2注释、唯一活跃Screen的Art07ShopEntrance、3秒deadline、自然等待IsPlaying结束、超时Require | 删除这6行后与821原字节完全相同。原150个Require调用文本全部保留，仅新增1个超时断言；固定位置、五类商品、完整StateHash/磁盘、容量仍原样。 |
| Runtime/Verification/PlayerVerification.cs | ProbeRevision 18→19 | 反向替换后与821原字节完全相同；95个Require全部不变。10秒预热、180秒采样、16.7ms预算与native/display检查不变。 |

产品UI、美术资产、规则、配置、录制器、测试源与项目设置没有变化。探针只观察真实商店时钟，不调用Complete/Refresh/Skip，不改变生产Busy。详细失败因果和集成复核沿用 `PLAYER_SHOP_ENTRANCE_PRECONDITION_REVIEW.md`；本次对已提交版本再次核对，不依赖工作区diff猜测。

## 可续用的原媒体

- **0f04六配置×48屏、9段录像**：沿 `MEDIA_REUSE_0F04E51_TO_821EC59_REVIEW.md` 已核链路继续复用到404；只有各独立报告真正观看、明确签署的原视觉条件续用。布局/抓手范围见 `FINAL_0F04E51_SCREEN_ART_INCREMENTAL_REVIEW.md`、`FINAL_0F04E51_GRIP_SCREEN_REVIEW.md`；影片范围见 `FINAL_0F04E51_CLIPS_REVIEW.md` 与独立POST报告 `FINAL_0F04E51_POST_INCREMENTAL_REVIEW.md`。继续保留0f04源码、Runtime MVID `e858ca97-2062-4977-801f-579d213d9ff4`、尺寸、字号、fixture及静音/固定时钟限制；不能把未看图补称实看。
- **821 ART14真实43屏/567实例**：`artifacts/cache/v07-art14-gpu-1103/capture-report.json` 保留821、Runtime MVID `dea6b318-5db4-4f86-9b94-c1dad2e85dca`、11:03:07–11:03:11UTC、1920×1080原身份。`ART14_SLICES_REVIEW.md` 独立看000–015；`ART14_TEXTURES_REVIEW.md` 独立看016–042。可续用三底透明边缘、九宫格三宽和角标三尺寸的已审结论；3份线性mask仍只作诊断，柔光同心色阶观察Q-ART14-FX01仍保留，不扩大成最终柔光质感或整版艺术通过。

上述媒体均不经PlayerVerification入口，本次两probe delta不影响其图像生成路径。1360是Git源文件数；历史媒体1361项另含其当时派生BuildIdentity，不把不同口径相加或重标新身份。

## 不可由复用推定的结果

当前派生 `Assets/Resources/BuildIdentity.json` 已只读核为4041766，版本仍art0.7.0/p0.6.0/schema4；本任务收到的root实际加载记录为11:16:48UTC、Runtime MVID `b37c1374-4c4e-44b5-9ff1-85e239f457fc`。这些身份信息不证明新999 Edit/154 Play、双平台构建或修后的实际Windows Player通过。新源回归/Player、商店基线前置、跨进程恢复、显示/性能、Mac及人工体验仍由对应真实证据裁定。821商店失败和mode0失败原件保留。

精确两边1360项Git blob表、原字节SHA、反向还原和Require计数见 `artifacts/cache/v07-media-reuse-4041766-review/delta-bindings.json`；本报告同名bindings绑定上述原媒体报告。没有重录、修改旧媒体manifest或解除AudioMixer/声音/人工验收边界。
