# UI 复用改造独立审查（进行中）

spec_audit，2026-10-29 UTC。只读审查 `UiKit.cs`、`GameApp.cs`、`GameApp.Modals.cs`、`GameApp.Shop.cs`，并查当前已安装 UGUI/TMP 源码；未操作 Unity，**静态审查不等于原生生命周期运行通过**。这是 Player04 性能失败后新改造，旧 PlayMode08 / Player04 不能放行它。

## 已反馈的实质风险

1. **骰面目标旧回调/重复组件。** 原 `FaceCards` 对可复用 Box 每次 `AddComponent<Button>`，并追加旧 choose 回调；下一次只读面卡也没有清理。root 已改 GetOrAdd，先清 listener、禁用与不可交互，只在本次 choose 非空时启用并绑定一个最新目标；Box 默认关闭 raycast。静态修复方向正确，需实际换骰、预览只读与取消/再次选择回归。
2. **复用音量条填充显示失真。** 相同 Slider.value 与 fillRect 引用赋值都不会重新 UpdateVisuals；原 Pause 在它们之间强制把 Fill anchors 恢复 0..1，会把原 45% 显为全条。当前 UGUI `Slider.cs:86` 与 `:544` 提供实现依据。root 已加 `slider.fillRect=null` 后重设，避免相同引用短路；待实际重复 Pause/切 Fast 后值与 fill.anchorMax.x 匹配。
3. **种子输入光标生命周期。** `Rent(Viewport)` 调 `Clear` 停用所有子物体，含 TMP 自动创建的 Caret，Input 只恢复 Text/Placeholder。当前 TMP `TMP_InputField.cs:1179–1195` 在 cached renderer 为 null 时创建 Caret，`:3858–3863` 仅更新 mesh，未自动恢复 activeSelf；首次 AddComponent 的 OnEnable 又早于 textComponent 引用赋值。已向 root/core_rules 反馈，需要布置引用后正确激活 TMP、恢复或保留其 Caret，并实际测试首次及多次菜单刷新后的光标与选择高亮。此项仍待修复/原生验证，不能声称已运行复现。

## 已检查且暂未发现新实质问题的部分

池在实际父节点上，以 name+role 分开 Rect/Text/Box/Button/Scroll，避免 Dialog 预览孔与普通实底面板混用组件。每次 Rent 重设 RectTransform、顺序和显示状态，重建 Button/DiePointer 回调，保留的旧兄弟已停用。Scroll 停止惯性并回顶；新 Run/练习 Release 清索引并停用后销毁子树，避免跨局累积；它不改规则、存档或 RNG。普通按钮没有持久化 Inspector listener。Player probe 寻找滚动组件显式 activeInHierarchy，未把旧隐藏组件当当前面板。

需由新的实际 PlayMode 与 Windows Player 确认组件生命周期、事件一次性、模态状态、输入焦点、画面和180秒性能。不能为了池化通过改原规则或预算。

## 后续实测关闭（build06 / Player05）

真实 `playmode-09.json` / `nunit-20261029-014917.xml` 为20/20，包含2组新池化回归；EditMode06为325/325，所有名称与JSON/XML一致，零失败/跳过。`unity06-09-pdb-independent.json`5程序集36源文档、`player05-pdb-independent.json`2程序集20源文档在本次检查时全部与当前源匹配；不依赖旧报告hash推定。

新增实际测试反复14轮Gameplay→永久详情→滚动明细→Pause→Menu→Continue，检查对象身份/所有层级节点集合稳定、没有多余交互组件，滚动回顶且惯性清零，Slider重复45%填充正确且旧listener不调用，TMP种子输入真实focus并有active caret。另一组6轮旧光滑→新镀金目标切换，直接Invoke只读Face事件证明旧listener已清，最终只改D002/F03并只扣款/记日志一次，再换控制器购买只执行当前回调。

Input实现现先停用field、布置引用，再启用并恢复TMP_SelectionCaret。结合上述真实原生回归，三项已反馈风险在指定版本**关闭**。显示快照缓存同时以Session对象与已提交ActionSequence为键，失败保存仍旧快照，成功重试和新Session使其失效；现有真实IO失败回归也已在20项中重新运行。

Player05 97正常命令/31落地检查通过，9张新实际图未见遮挡或残留。**只接受池化功能与画面回归；性能仍失败**（P95 18.3322ms）。完整结论见 `PLAYER05_REVIEW.md`。
