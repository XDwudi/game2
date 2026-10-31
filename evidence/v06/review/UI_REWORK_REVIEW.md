# v0.6 Player 返工独立复核

本轮基线是 `6f9e285` 之后的定向返工，旧通过和失败证据均保留。审查者不启动Unity，不操作原生输入。新的实际运行结果尚待协调者提供；本文件先记录静态复核与需要关闭的证据。

- **UI06-13 分页**：骰池按钮从骰面区域移入上方独立标题带；提交分页移入提交标题带，栏号保持其下方。静态坐标不重叠；PlayMode加入实际屏幕矩形与骰子命中区/栏号不重叠断言。右侧基础/控制器超长数采用紧凑文本，完整数值仍在动态高度的滚动明细；上一手按钮不再嵌入无限长得分。需新Player截图确认。
- **UI06-14 目标列表**：遥控与商店永久骰均为8列滚动列表，32项完整保留；重绘保存滚动位置与既有选择，遥控最多两个目标。新增PlayMode覆盖实际滚轮、完整viewport包含、首位raycast、最后两颗的点数覆写/一次消费、末尾骰强化、取消纯净和准确删除。实际首轮 `nunit-20261031-134849.xml` 为30/27/3，两个新例在新控件首次创建后同帧raycast失败，另一个旧例是Debug容量标签预期变化；不得用静态布局关闭这些失败。实施者正增加真实渲染帧等待与depth/cull/hit诊断，首位raycast断言保留。
- **Escape探针**：原Player02/03的队列投到真实Keyboard，未取得已聚焦证据。新探针使用自身派生Keyboard布局，明确允许其后台运行，真实Dynamic输入更新触发edge，随后由生产GameApp.Update处理。onAfterUpdate仅选中自身设备，不直接调用Cancel或Revert。finally移除探针设备与布局，恢复原current；全局backgroundBehavior/updateMode不变并记录验证。报告明确是合成InputSystem路径，不等于物理键盘或原生聚焦通过。锁定包InputManager源码确认新增设备在后台也会受设备后台策略影响，采用单独布局不改生产全局策略是合理的测试隔离。
- **分辨率探针**：先等待退出全屏的两帧，再设置窗口尺寸，仍使用原总12秒上限及三帧精确尺寸/Windowed读回；没有缩放截图替代真实窗口，也没有减少压力采样。
- **Player32目标覆盖**：要求新增 `remote-target32-ui`、`shop-target32-ui` 真正滚动和点击末尾目标；因原Press仅检查活跃/viewport后直接分派事件，审查要求进一步检查首位实际raycast，避免绕过上面真实遮挡/渲染深度问题。需最终源与实际运行结果确认。

交付验证器已把两个新ID纳入必需集合，并为任一缺失在Windows/macOS均拒绝加入负例；独立实际 **68/68**，前后源码稳定，见 `delivery-tools-final02.json/.log`。这只是离线工具通过，非Mac运行。

独立读取修复后的真实定向报告 `nunit-20261031-135300.xml`：两项新目标测试 **2/2**。诊断证明 `Face_F01` 首次创建时 Graphic.depth=-1、首位命中Dialog，经过实际渲染帧后depth=130且首位命中Face_F01；ConfirmPurchase同样从-1变为97/162并正确命中。保留首位raycast断言并在交互间等待真实帧是修正测试时序，未移除遮挡检查。遥控末尾目标、点数、确认的首位命中也均通过。此定向通过尚未替代完整套件和最终Player绑定。

随后已静态复核Player目标流程改为 `PressTargetReady`：真实帧等待、全部角点位于screen与启用mask中、首位raycast目标Button一致、记录depth/cull/hit后才分派点击，全部末尾目标/点数/面/取消/确认路径使用该方法。新的实际PNG与 `v06-target32-raycasts.json` 仍待最终Player运行。

最终返工后的完整Unity报告已真实执行并独立绑定：EditMode `nunit-20261031-135754.xml` **776/776**、PlayMode `nunit-20261031-135725.xml` **30/30**。`unity-evidence-review05.json` 校验完整发现名册、五个实际加载模块起止MVID/DLL SHA和Editor退出后603源文件均一致；独立 `unity-final-pdb04.json` 覆盖五程序集61个C#文件、0 mismatch。旧776/28仅保留历史，本轮依据新的776/30；Windows Player与截图仍待新构建实际运行。

Windows03 / Player04（`player-20261031-135926-f1439a98`）实际失败：探针自有设备 `enabled=true` 但 `canRunInBackground=false`，诊断 `focusedBefore=true`，不能归因为未聚焦。审查者直接核对锁定包 `InputSystem/Runtime/Controls/InputControlLayout.cs:1636`，发现JSON字段实际为 **`runInBackground`，字符串值 `enabled`/`disabled`**；C#属性名 `canRunInBackground` 不能直接当JSON键。当前探针错误键被忽略，失败断言正确拦截。已通知root/rules改自有layout JSON并保留全部断言，未建议改变全局焦点策略或直接调用生产取消。受影响源修改后，a641924的Unity/PDB证据仅属该历史候选，必须重新绑定。

关闭条件沿原P21–P28执行：新的完整Unity发现清单/报告、实际加载模块和PDB/源快照一致；实际Windows Player正常轨迹/Debug24/32目标/输入路径/显示/180秒压力和截图通过；失败均保留。Mac、物理输入/热插拔与人工体验状态仍需分别记录，不能从合成输入推定。
