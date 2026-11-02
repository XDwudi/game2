# 结算 Draw Calls 214：只读根因分析与最小试验建议

范围：冻结游戏源 `dca7a2610f39d65088b9541549d3465c388fd3e7`、Windows GUID `388c2eee37244ba48bb656114b6320bf`，实际 `evidence/v07/resources/v07-resource-player-dca7a26-0243`。未启动 Editor/Player、未写生产、未修改 §31 起始目标 180。此研究不阻止准确标记的现有诊断候选包。

**最有证据支持的候选是结算冻结 UI 的“每图形一份材质”导致批次拆分。代码与持续计数支持该方向，但现有原始数据没有逐批次归属，尚不能证明 214 的完整分解、精确可省数量或优化后 ≤180。**

## 实际数据说明问题是持续开销

独立复算 `resource-frames.csv`，完整数字见 numeric.json。1920×1080 Windowed、D3D11、真实非 Editor Player，普通速度/非减少动态。ProfilerRecorder 读最近完成帧，因此命令边界存在前阶段计数，不强行把同一行 phase 标签当作该渲染帧的全部因果。

| 区段 | 实际计数 | 含义 |
|---|---|---|
| Submit 后 HandReady | 205 行为 59 | EndStage 前桌面基准，不是与结算同内容的 A/B |
| EndStage，223 行 | 2×59、1×202、198×212、4×213、18×214 | 221 行超过 180；两个 59 是边界旧帧 |
| EndStage 开始一秒后 | 165 行全部 212 | 金币等短演出结束后仍持续超出起始目标 |
| Continue | 首两行 212，随后收敛到 33 | 两行旧计数不能当 Shop 持续超标 |
| 同一路线全部阶段 | 223 行 >180 | 221 结算标签行 + 2 Continue 边界行 |
| 最后普通十骰稳定桌面 | 96 | 场景内容不同，不能从 212−96 推断结算背景的独立 Draw Calls |

现有图表为总 Draw Calls，不是 SetPass 数或 UI-only batch 数。峰值 214 与稳定 212 的差额仅 2，单独削减金币动画即使奏效，也不足以闭合稳定段 32 次差额。没有证据把这项归咎于字体图集数量、十骰数量或最高 Combo 演出。

## 真实结算内容已能从存档收据恢复

对原始隔离 `debug-run.json` 的 payload SHA 验证通过。最终 Apply MaximumTable 后，原先五条 processedReceipts 仍保留：

1. seq1 ApplyDebugScene：`art0.7.0 Qualified`，五颗普通骰、容量 5，C02/C30/C31/C32 四控制器且全部无附魔，I01–I05 五个道具。
2. seq2 Submit：实际五骰提交，无破碎/销毁/道具消耗 effects；返回 HandReady。
3. seq3 EndStage：真实 StageSettlement，score1512/target1，金币 30→40，奖励 5+剩余提交2+利息3；无基金/临时骰清理 effects。
4. seq4 Continue：Shop。
5. seq5 ApplyDebugScene：后续十骰、十控制器 MaximumTable，不能拿它当 214 出现时的 inventory。

这比仅按诊断源码猜测场景更强，但收据不含 CanvasRenderer/material inventory；不能据其重建实际批次数。

## 源代码中的可证链条

| 位置 | 已确认事实 | 对 Draw Calls 的解释边界 |
|---|---|---|
| `GameApp.Art07SettlementBackdrop.cs:27–45` | EndStage 前，抓取未排除的可见 main 根及实际骰子姿态；排除 Stats07、ScorePanel、导航等 | 来源含控制器、道具、骰池/提交区等；本次未保存逐项捕获清单 |
| `GameApp.cs:73–96` | Refresh 清空/停用 live main 与原骰，画 Header、Settlement、导航；不会另画完整 Gameplay | 不能凭冻图出现就声称 live 游戏 UI 也重复绘制；代码没有这个重复路径 |
| `GameApp.Art07Settlement.cs:46–94` | Show 冻结背景，再绘遮罩、结算纸张、奖励/状态/继续、最多五金币 | 必须保留用户可读背景与现金说明；隐藏整背景不是等价优化 |
| `Art07SettlementBackdrop.cs:36–44,82–95` | roots 按原 256 Graphic 上限分组，每组使用 Art07TransactionMotion | 新增的是 CanvasGroup/CanvasRenderer，没有为每图形另建 Canvas；组数本次未量测 |
| `Art07TransactionMotion.cs:45–71` | 每可绘 Graphic 拷贝 mesh；从 TMP 主材质/子网格材质取正确 atlas；每 mesh slot 自建并复用一份 owned Material；逐 renderer SetMaterial/SetTexture | 即使原先多个 Graphic 共用同一个材料，捕获后也成为不同 Material 实例；这是直接可见的批次破坏候选 |
| `Art07TransactionMotion.cs:76–80` 与 `Art07SettlementBackdrop.cs:101–115` | HoldPrepared 持续显示捕获图，不再按时间淡出；世界骰体按原姿态、材质、property block 复制 | 符合稳定段持续212的时间形态；世界骰子是否贡献额外批次仍需逐批次确认 |
| `Art07TransactionMotion.cs:67–71` | 每 renderer 保留 tint/inherited alpha 与 Mask/RectMask2D 交集裁剪 | 共享材质也不能消除所有裁剪/纹理/顺序导致的拆批 |

项目已装 uGUI **2.6.0**。其本地 `Documentation~/ProfilerUI.md` 明确：不同材质实例、裁剪、纹理、CanvasGroup injection 等会造成分批；该版本 UI(Canvas)/Details 数据 **仅限 Editor**。官方 [uGUI Profiler 文档](https://docs.unity.cn/Packages/com.unity.ugui@2.0/manual/ProfilerUI.html) 也列出这些拆批原因。这里把规则用于提出推断，没有把规则本身当作本次214的实际逐批次证据。

### 文本与现有证据

本次真实资源快照均记载五个已加载字体/atlas：DiceChinese 静态、DiceChineseFallback 动态、font_numbers_tmp 静态及 LiberationSans 主/回退。这个清单是加载状态，不能证明它们全部同时在结算帧绘制。

现有九个资源 snapshot 只有菜单、手册、设置、商店与十骰桌面，**没有 EndStage snapshot**，更没有 CanvasRenderer 的实际材质ID、Canvas层级、rect clip或batch reason。先前同源 Editor 的三个 settlement GPU报告各捕获单张紧凑卡14个 mesh、通过对应字形比较；它们支持维护字体/生命周期回归的重要性，不是本次五骰/四完整卡结算的批次清单。此次未新看媒体，不作视觉通过声明。

## 最小可演进试验：先量到原因，再限域改两个文件

### A. 诊断基线（不改正式180秒流程）

在下一次获准启动的独立、隔离存档场景，复现上列真实 Qualified→Submit→EndStage 路线，不换成更轻的空场景。正常1080/100%先做一例即可。

- 在 Editor Play 的结算稳定帧，用 **UI(Canvas) Details / Frame Debugger** 留实际批次列表与 `Different Material Instance` 等原因。列出 `SettlementBackdrop07/FrozenUiGroup*/CommittedOldMeshes/TransactionMesh*` 所属批次、纸张/live HUD、FrozenDice、剩余场景的分账。不能只看全局214。
- 在捕获前记录可见来源 Graphic 的路径、实际材料实例、shader、keywords、render queue、实际 texture（TMP主/回退分别取对应atlas）、Canvas/父组与绘制次序、tint/alpha和裁剪。在稳定捕获后记录 active snapshot mesh/独立owned材料数、世界骰mesh数。**CanvasRenderer.material.mainTexture不一定等于SetTexture覆盖后的图片纹理**，没有来源映射的项保留未知，不擅自归类。
- 不宣称 Player 能提供 Editor-only UI Details。真实 Player 部分仍用独立现有资源路线计数；若想在 Player 读batch详情，先验证 Frame Debugger连接能力，或另加仅diagnostic对象快照，未接通前不作为现成能力。
- 现有 public `SettlementBackdropGraphicCount07`/`SettlementBackdropDiceMeshCount07` 可支持总量观察；逐材质/源图形映射需额外只读采集，不应混进高频20秒idle采样。

### B. 候选接口和边界（建议，未实现）

若 A 确认该来源明显拆批，优先仅修改：

1. `Runtime/Presentation/Art07TransactionMotion.cs`：保留原 `Prepare(IEnumerable<RectTransform> roots)` 行为；新增等价的可选接口，例如 `Prepare(roots, bool shareOwnedMaterials)`。true 分支在**同一次捕获、同一个 snapshot group 内**，按实际源 Material 实例 + 实际 Texture 引用分组，给同组 renderer 共享一个**组件自有**克隆。材料内容相同但实例不同者先不尝试深度合并。
2. `UI/Art07SettlementBackdrop.cs:94`：仅此结算 CaptureGroup 传 `shareOwnedMaterials:true`。商店事务与入关卡片原调用保持原路径，降低首个试验影响范围。不要先修改 `Art07PanelMotion` 或美术资源。

自有材质池每次 Prepare 清空 source→slot 映射、复用已有slot，按本次实际唯一组合更新内容；避免持久按 TMP 临时 material ID 缓存导致200轮增长。捕获前旧 renderer 已清空，不在仍显示的其他快照之间共享可变材质。统一销毁池内owned克隆一次，不能让多个mesh重复拥有/销毁同一材料。

**保持不变的行为：**TMP主/回退/多atlas分别绑定，源TMP停用/释放后仍可见；完整拷贝shader属性、keywords、renderQueue；每renderer的SetTexture、mesh UV、颜色/继承alpha、rect clip、CanvasGroup alpha、次序与世界骰姿态保持；256上限与已确认规则/存档/现金一次性合同保持。不把快照改成低分辨率整屏截图，不通过去背景/缩中文/删来源图形来降计数。

key不应只用shader名称或atlas数量。同源Material的完整渲染属性才支持保守共享；不同clip/alpha/order仍由独立CanvasRenderer保留，由Unity真实批次决定能否合并。未来若要进一步合并几何，应另立证据，当前不提前扩大。

## 受影响验证范围与判定

- **先红绿代表例：**同一Qualified结算稳定帧及短演出，实际Material实例/批次原因前后对照，独立 Player 相同路线总Draw Calls和上下文。保留原始212/214基线，观察是否达原180起始目标；未达到就如实继续保留缺口，不增加采样快帧稀释它。
- **正确性优先：**现有 `PLAY_ART07_SETTLE_BACKDROP_GpuThreeFontsAnd200RenderCyclesPreserveInkPoseAndOwnedMaterials`，三字号真实字形/骰子GPU像素、源材质停用存活、200轮池稳定/销毁释放；追加有意义的共享组测试（同源材料复用，主/回退或不同texture不混组）及实际来源图形/文字裁剪alpha顺序对照，而非复写分组算法。
- **交界：**CommitAndColdRecovery、SaveRetry单次奖励、结算普通/Fast/Reduced/Instant、暂停/刷新/跳过/继续和跨分辨率重投影；冷恢复走重建背景也应覆盖。还应检查100/125/150字体、最大普通容量、32调试容量，因为来源与分组数可能变。
- **公共类保护：**默认false的商店交易/刷新/替换与StageCardExit测试保持绿；确认shared路径关闭时原材料池释放不变。若实现影响公共生命周期，扩大这些检查而不是以opt-in为由省略。
- **成功后才扩大：**符合画面及性能证据再纳入生产，由root重新冻结、运行受影响完整回归及同源构建/Player。当前候选不是修复，现有 dca 三包不因这份研究改变身份。

剩余未知：214中各来源精确DrawCalls、实际冻结Graphic/owned Material数、batch破坏分布、共享后的实际节省量/CPU收益与所有规格最高场景。没有通过“对象数≈绘制数”补造这些结果。

文件：numeric.json 为只读复算，bindings.json 为输入/源码/文档/分析SHA。根因结论为**代码上已确认的拆批风险 + 实际持续计数吻合，需逐批次最小试验确认归属**，不是“已修复”或“完整根因已证明”。
