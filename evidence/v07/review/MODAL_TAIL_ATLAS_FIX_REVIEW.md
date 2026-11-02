# Modal 尾影字体 atlas 修复独立复核

**结论：本次修复的规格符合性与针对性制作质量通过；P07-MODAL-TMP-01 在已验证的 Editor 真实关闭路径范围关闭。** 可冻结修复源继续完整回归、新构建及未仪器化 Player 验收。旧 4efd2b7 Player 坏图、失败测试和旧包仍保留，不能改称已修复通过。

复核者 `/root/v07_run_review` 未启动 Unity/Player，未改生产、测试、门槛或旧证据。独立静审候选、校验实际 NUnit/source binding/归档/偏好/PDB，实际打开 **14 张图**，并从 PNG 独立复算 **74 字形、44868 像素**。root 是实际 Unity 运行及生产集成者。

## 修复范围和身份

唯一生产变更为 `DicePrototype/Assets/Game/UI/Art07PanelMotion.cs`：TMP_Text 取 fontSharedMaterial，TMP_SubMeshUI 取 sharedMaterial；两者取对应材质 mainTexture 作为 atlas，其余 Graphic 路由保持。每个尾影槽拥有一个复用的材料副本，拷贝 shader/properties/keywords/renderQueue，避免关闭原 TMP 后 fallback 材料释放影响尾影；组件销毁时只释放自有 material/mesh。

UV0.w、四通道 UV、顶点、颜色、裁剪、alpha 公式、原 .12 秒/减少动态 .08 秒关闭、同步逻辑关闭、存档与射线行为均未变。CancelTail 清 renderer 并隐藏槽，保留材料供同槽复用，不逐次关闭无界创建材质。未泛化重构或改其它快照路径。

| 输入 | SHA-256 |
| --- | --- |
| 修复前 PanelMotion | b0fa31244a97cdae3be0fd8601d2229a62e2bd61ad3f7a7253b2cb98f5ed9263 |
| 修复后 PanelMotion | da1bdd0e6ab6d85f7ebe28fe94620663fbfb30c2b705acaab4496f1a099715ea |
| 两项新增测试 | 077cadb542812a5584ba7a4179c051eb3288749adf01fc4a87bc6426b1da11f4 |
| 测试 meta | 1ea92729b85bffe0d93321877cacf3843c85aa6f7f3ac45cb6e14884ec09bf47 |

红、绿及后续 Guide 三批起止源/程序集快照各自相同，每份 1366 源路径无重复。红→绿源集合中唯一 SHA 变化是 PanelMotion，新增测试及 meta 始终不变，未为变绿修改测试。绿与 Guide 两批的全部源及加载程序集完全相同。实际绿 Runtime MVID `c8a3b94c-3cb7-4453-bb95-88902288db3d`，Test MVID `bd7a4e02-5fda-4bb0-9bfa-7f522fae8f66`，与四组 GPU 报告一致。

## 真实红绿与依赖检查

| NUnit | 结果 | 实际范围 |
| --- | --- | --- |
| 013059 | 0/2，2.8393668 秒 | 旧生产同两新测试均在真实 GPU 字形比较失败，首字形 mean error 109.833336 / 120.247864，门槛≤2 |
| 013206 | 4/4，6.9322737 秒 | 两新测试 + 原 ModalRedraw/Close/Input 与 DiceSheetFade/Aperture 同步恢复用例 |
| 013247 | 3/3，16.3329966 秒 | 原 GuideLayout 三项补充，未复用此前 filter 未匹配的空范围 |

均无跳过；红测试失败原样保留。各 XML 的真实 SHA 与 binding、归档副本一致。归档及原 Temp 逐文件 SHA/大小核对：红 **2 目录/16文件/6PNG**，绿 **4/40/16**，Guide **3/45/18**；目录集合精确对应各 NUnit output。没有把 Guide 的18图算作实际视觉查看。

新测试经过真实 Debug32 工作区与 RunDebugScene→CloseModal→CaptureExit，读取源 GPU 画面；逻辑面板关闭后冻结实际 tail.Update 取得 alpha=1 字形比较，源 UI 已关闭两个渲染帧。没有制作替代渲染器或仅测试材质选择 helper。

每个字形的原门槛保持 source ink>0、RGB最大通道差的平均值≤2、差异>8的像素比例≤2%。从原 PNG 按 Unity 矩形/Y方向独立复算，与 JSON 逐项相同：

| 绿组 | 字形 / 比较像素 | mean error / mismatch | 中间帧 alpha / elapsed / duration |
| --- | ---: | ---: | --- |
| Debug32 normal | 32 / 19449 | 0 / 0 | .858184 / .017018 / .12 秒 |
| Debug32 reduced | 32 / 19449 | 0 / 0 | .787424 / .017006 / .08 秒 |
| 两 atlas 第一次关闭 | 5 / 2985 | 0 / 0 | .857811 / .017063 / .12 秒 |
| 两 atlas 第二次关闭 | 5 / 2985 | 0 / 0 | .861156 / .016661 / .12 秒 |

fallback 两轮确有两个不同字体资产及 materialReferenceIndex>0，文本 `H＋混 78` 的主/备用 atlas 都被比较。测试还实际执行并通过：373/374 个尾影 graphic 槽的材料与原材料分离、每槽独有、第二次关闭材料 ID 完全复用、销毁真实 tail 后所持材料全部为 Unity null；状态 hash/动作序号/debug 保存和普通保存不变，尾影无可交互 Graphic/Selectable 且 blocksRaycasts=false，结束后 inactive。

## 实图与时序边界

复核者实际打开红两组的 tail.png；两图大面积文字都是实心矩形，确实重现原 Player 视觉缺陷。实际打开绿四组各 source.png、tail.png、tail-in-motion.png，共12图：标题、说明、按钮和备用字体文字在原位置正确显示，alpha=1 尾影与源一致，半透明退出中间帧中也无矩形字块。

中间帧来自**测试仪器化暂停后恢复原 Update**：冻结时先完成截图/比较工作，再保持 disabled 跨两帧，避免测试的 PNG 编码耗时进入首次动画 delta；随后恢复原 Update，在 0<alpha<1 时真实 EndOfFrame 捕捉。四份 JSON 的 alpha 与 `1-elapsed/duration` 相符，未改 duration 或放慢生产公式。此证据验证了原渲染和恢复推进；不宣称整段退出未被暂停，也不替代最终 standalone 的连续观感验收。

实看清单与哈希在 bindings/facts 中逐个列明。本次没有把稳定终态或 tail 消失当作中间帧正确的证据，也没有仅改截图等待来掩盖产品字形问题。

## 恢复与后续

01:34:06.0473335Z 实际 idle guard/SaveAssets 成功，场景为空、dirty=false。六整数、五按键及音量的值和存在标记，与先前 v07-post-full-4efd2b7 十二键逐字段一致。`pdb-after-green.json` 五程序集、172 个唯一 C# 文档、零差异；源 checksum 恰好覆盖绿快照的 C# 集合，加载 MVID/DLL SHA 与绿和 Guide 绑定一致。

当前修复已解决此具体尾影图集错误，未发现新增阻断。新完整回归、新 Windows/Mac 构建与实际 Player 仍应绑定修复后源；旧显示未稳定、P95 超预算、AudioMixer、Mac M5 与人工体验等未完项不会被本修复关闭。实际 Player 后续若出现差异，应重开受影响范围。报告与细节位于 `artifacts/cache/v07-panel-exit-atlas-review/`，由 root 维护 STATE 与新检查点。
