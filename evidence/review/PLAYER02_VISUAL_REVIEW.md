# Player02 独立画面与运行证据复核

审查者：spec_audit，2026-10-29 UTC。只读生产与标准；审查者未启动 Unity/Player。输入为 `evidence/runs/player/player-20261029-005142-873bffd3` 的真实 Player 产物。15组独立产物检查实际运行，结果及逐图SHA见 `player02-independent-artifact-audit.json`，命令 `python evidence/review/audit_player02.py`。这15组是证据一致性检查，不是15项新增游戏实跑。

## 分列结论

- **规格符合性：有缺陷。** RV-UI-06计分解释不完整，见下；核心97命令记录与原trace逐字段一致，未发现新规则得分错误。原始黄金的此前独立核验仍有效于对应源码，不能代替新解释器回归。
- **制作质量：有缺陷。** 骰子caption遮挡、详情3D预览被底层ScorePanel遮住、水印覆盖暂停按钮。商店商品在已检查720p/1440p可读；分数详情顶部与底部滚动均有真实画面。不能因39张PNG存在而判三分辨率全通过。
- **实际运行：部分完成，性能未通过。** WindowsPlayer/Unity6000.6.7f1，buildGuid `4f8ffc07be074280b4d4f2e40a53a767`，探针MVID `9b536b29-2f3d-4743-a392-2a4a66ab6246`；97步正常非Debug六关、31次落地TRS/+Y、首Roll0.320/0.450秒不同姿态的记录一致性已核对。正常档在Debug压力前后hash相同。180秒已运行，但UI只有29.789%覆盖且GPU有不可能值，报告正确保持 `measured_with_gaps / exitCode2`。不能签AC-PERF、整体验收或人工试玩。

## 实际看图结果

亲自通过图像工具查看16张（逐图清单记录JSON `visuallyInspected`，其余23张仅尺寸、像素范围及hash检查）：

| 场景 | 实际检查图片 | 观察 |
|---|---|---|
| 正常投掷 | normal-selecting 720p、1080p | 720p基本布局可用，caption叠在骰面；1080p全黑，不能验收 |
| 商店 | normal-shop 720p、1440p | 3张卡的名称/完整描述/价格/购买入口均可读，无卡片裁切；只覆盖控制器Tab，不能推断其它Tab交互 |
| 六面详情 | normal-die-detail 720p、debug-die-detail 1440p | 永久六面/当前/有效点数及概率可读；3D预览只剩下部碎片，Debug时底层11910分/按钮直接侵入弹窗（root已在修） |
| 正常分数 | normal-score-preview 720p、normal-score-preview-bottom 1440p | 顶部公式、逐骰基础信息和滚到底部文字可到达；步骤解释不完整 |
| 满7槽分数 | debug-score-preview 720p、bottom 720p及1440p | 7骰明细可滚到底部，无证据表明尾部被截断；步骤解释不完整 |
| 十骰全未锁 | debug-ten-dice-unlocked-pool 720p、1440p | 10骰两排、7空槽、10控制器及5道具可见；第一排标签压第二排骰面，第二排标签压自身前面（root已在修） |
| 最大容量 | debug-stress-capacity 720p、1080p | 3未锁+7已锁、10控制器/5道具均能辨认；锁区标签与面/分隔线及按钮上缘挤压 |
| 通关 | normal-run-won 1080p | 第6关1908分/9金币/#97与trace末状态一致，胜利文字和两个主按钮完整 |

**RV-UI-06：计分步骤不完整（P2，原规格缺口）。** `docs/specs/P0_v0.4.md:615`要求逐步前值/操作/后值及加点→玻璃→镀金的顺序；583/590要求C10/C17单列新增贡献。Player02的 `debug-score-preview-1280x720.png` 中D002有效2、贡献20仅显示玻璃×2/镀金×2，缺失C17加3来源及2→5→10→20；全局只给(88+1500)×6×1.25×1，缺逐乘前后值。对应旧 `GameApp.Modals.cs` ShowBreakdown仅Formula/DieLine/Triggered。root已追加ScoreExplanation，尚需本轮独立静态、真实测试、新截图复验，不因源码存在关闭。

**RV-VIS-04：水印覆盖暂停入口（P2，制作问题）。** `normal-shop-1280x720.png`右下“设置/暂停”与Unity trial version/Development Build水印叠字，正常/Debug其它720图同样；1440擦碰更轻。root已移动按钮x1720→1540，但Player02仍是旧布局，需新版实际图关闭。此意见仅要求布局让位，不移除引擎许可水印。

其余已知缺陷（初始黑图、caption重叠、详情预览层级）已实际确认；不重复包装成新增发现。`main-menu-1920x1080.png`与`normal-selecting-1920x1080.png`全部RGB极值为0，PNG尺寸正确仍不代表有效截图。

## 原始采样核查与待验范围

CSV实际286833行，其中首行是开始采样的部分帧，报告按源码明确跳过首行，统计286832；不是漏帧。连续frameID、180.005秒最后原始时间与180.008秒报告窗口相容。有效UI85445/286832=29.789%，GPU异常原始帧133248、elapsed42.9594秒、13163700653408.648ms，报告浮点序列化为13163700653408.6ms。两值在报告精度内一致。约1593.44fps的后台运行不能代替前台真实呈现60fps预算验收；新采样保持16.7ms/180秒/95%完整性判据。

另已核对EditMode04 JSON/XML268/268、PlayMode06 JSON/XML16/16，测试名称一致、无skip/失败，非零实际报告。新增R03遥控/R05真实键盘/R06文件锁保存失败回归在PlayMode06中；未来18项商店或ScoreExplanation测试尚未见新运行报告，不提前判通过。最新源码补丁与这些报告不同，必须重验受影响项。

审查脚本最初将CSV行数直接等于报告帧数、将JSON浮点精确等于CSV值，因上述明确统计口径/15位序列化差异失败；修正审查脚本读取口径后15组通过。旧审查日志保留，未修改游戏采样、标准或原证据。

后续需新Player证明：黑图消失；3分辨率caption/详情预览/暂停按钮无遮挡；新增逐步解释长文可滚动且历史快照不变；商店各Tab与资格/价格真实回归；前台180秒原始CPU/GPU/GC/UI证据完整。5–8人每人至少2局仍未运行，独立技术审查不代签。
