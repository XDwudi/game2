# Player05 / UI池化独立复核

spec_audit，2026-10-29 UTC。输入 `evidence/runs/player/player-20261029-015104-31d8ee23` / build06。审查者未运行Unity；真实引擎/Player由root运行。独立实际执行 `audit_acceptance_pool.py` 与 `audit_player05.py`，相应JSON/log保存在本目录。

## 分列结论

**规格符合性：本轮池化与显示缓存回归通过；性能仍失败。** 新版保持97个非Debug合法命令逐字段匹配原trace，磁盘/渲染状态及正常档隔离完整；31次投掷落地位置/旋转/缩放及物理结果面+Y检查通过。Core仅加只读ActionSequence入口，事务与外部隔离保留；UI缓存由Session实例+提交序号共同失效，Busy/Pending仍实时读取，日志使用Refresh后的已提交ViewState。新Session同序号不会错误复用，失败保存不显示未提交候选。

**制作质量：本轮未发现剩余池化实质缺陷。** 实际20项PlayMode全部通过，其中新两组验证稳定对象身份/节点集合、重复事件清除、换目标一次提交、只读面卡不残留回调、真实TMP focus/active caret、重复Pause音量填充45%、滚动复位。三项池化审查发现已关闭，详见 `UI_POOL_REVIEW.md` 追加实测段。独立以原尺寸查看9图：normal-selecting1080、Debug永久详情720、十骰1440、逐骰明细720顶部/中部/底部、商店1080、180秒结束1080、正常通关1080。文字、模态孔、三维骰面、7槽/10C/5I及滚动逐步公式无回归；准确图名和SHA见JSON。没有据此代签美术风格或真人体验。

**实际运行：正式180秒完整采样有改善，但不能放行AC-PERF。** 原始CSV10,725行，首部分帧后10,724有效帧，180.0143142秒。十项指标samples/coverage/mean/P95/max独立重算与报告完全一致，22组产物一致性检查通过。报告`budget_exceeded / exit3`正确。

| 指标 | Player05 | Player04 |
|---|---:|---:|
| 全量LateUpdate间隔P95 | **18.3322ms** |23.5467ms|
| 间隔均值 / 最大 |16.78460 /40.2854ms|16.98349 /48.3832ms|
| MainThread P95 |17.0318ms|17.0496ms|
| GPU P95 / 有效覆盖 |0.236544ms /97.47296%|0.232448ms /96.62200%|
| UI BuildBatch P95 /覆盖 |0.0066ms /100%|0.0065ms /100%|
| GC allocated P95 |56,280B|1,265,626B|
| GC allocated最大 |3,704,946B|8,946,498B（含旧首边界污染）|
| 采样中帧记录扩容 |0|0|

pool减少分配与尖峰有真实证据，未改变60FPS/16.7ms/180秒/95%覆盖。GPU无无效值、无重复时间戳；UI无中断或累计旧批次。不能以改善百分比代替硬阈值通过。

## NUnit与版本绑定

- EditMode06 `nunit-20261029-015006.xml`：325/325；PlayMode09 `nunit-20261029-014917.xml`：20/20。两个JSON/XML用例全名集合、汇总数完全一致，零失败/skip/inconclusive。
- 70个附录C ID映射、38内容/图标/可编辑源与模型源hash仍成立，原始黄金未变；失败历史保留。
- `unity06-09-pdb-independent.json`：5程序集/36项目源文档全部匹配；`player05-pdb-independent.json`：2程序集/20源全部匹配。
- buildGuid `885b8596070142b0a7609c4512ab4502`；Runtime MVID `9d9dbf0f-78d8-4842-99d8-94f348c35626`。
- Runtime DLL SHA `2f43e7aedf0878dbf93f49516ec56377e5bbbf648291c0e84ba048e46792e995`；Core DLL SHA `db7798eb8ceec1febb0e020558155a24a533c23fd1229afe21ee9f82ed9c5dac`。
- UiKit SHA `2f88743afc30c1db5066986aab080d7d4151588dba2fe86f585ef4945b7ec862`；GameApp `094840aaf38ce2df90b1252bab8356f22e805353e576a3f798992c67a5b4a6a7`；probe `75fe1bfb97a279224b1ab8357ced9936e534ce652e4ce7cea06a55ea56a53287`。

## 节奏试验方法边界

原LateUpdate-to-LateUpdate指标是每帧脚本回调的墙钟间隔，包含前后两帧工作位置差，**不能严格代表屏幕呈现间隔**；这不取消冻结指标或本次失败。待40秒两模式试验提供所有原始时点后，应同时观察frameStart、PresentCalled、FrameComplete，不能挑最好的一个。PresentCalled表示CPU调用Present，FrameComplete表示GPU完成并通知CPU，都不单独证明物理屏幕扫描显示；排队、DWM、VSync与可变刷新会进一步影响可见时点。FTM有延迟，必须以原始帧时间戳识别重复/缺失，不能按取样行号强当当前帧。

该短试验只帮助定位，不能代替正式180秒，也不自动允许改变采样时点或门槛。后续生产节奏策略与未测窗口/独占模式需单独绑定新构建。参考依据已记录于 `FRAME_PACING_RESEARCH.md`；本报告不预先放行任何候选。
