# Player03 独立技术复核

spec_audit，2026-10-29 UTC。输入 `evidence/runs/player/player-20261029-011604-832cff6f`；审查者只读产物，没有启动Unity/Player。实际运行是root提供的Windows Player。本轮独立实际执行 `audit_player03.py` 22组产物一致性与原始CSV复算，证据 `player03-independent-artifact-audit.json/.log`。

## 结论

**规格符合性：本次功能修复通过。** 97个正常非Debug命令与原trace逐字段相符，磁盘和ViewState匹配记录完整；Debug压力前后正常档hash未变。31次Roll落地TRS归零且结果面法向+Y，正常/减少动态/跳过都有实际记录。RV-UI-06现有纯解释器、真实Unity57条TRACE回归与实际逐步画面形成完整证据，可关闭此缺陷。没有从画面推断未覆盖规则或人工体验通过。

**制作质量：已发现的UI/画面缺陷在build04关闭。** 独立实际查看18张图（准确清单/hash在JSON），覆盖三分辨率：layered玻璃+镀金六面详情720/1080/1440完整预览、正常Selecting720/1080/1440、Debug逐步计分720顶部/中部/底部并交叉1080中部/1440底部、正常明细1080底部/1440中部、十骰720/1080、7槽1440、商店1080、主菜单1080。标签已与骰面/下一排分开，预览不再被底层分数覆盖，暂停按钮不再与水印叠字；7骰、10控制器、5道具可读。D002明确显示小鬼当家2+3→5、玻璃5×2→10、镀金10×2→20；全局88+1500→1588、×6→9528、×1.25→11910、最后floor、+0均可滚动读到。46个接受截图及2个预热截图无黑图，原生尺寸一致。美术风格及人工体验仍由用户决定。

**实际运行：功能与画面接受于指定build；性能未完成。** 180.001327秒确实运行，原始CSV287962行含首部分帧，统计287961。UI记录器已100%有效、0/1样本/帧且无中断，GPU有效98.1209%、无异常/重复时间戳，GPU最大1.398784ms。剩余不通过原因是探针帧缓冲从60000扩至480000，共3次观察者扩容；报告正确exit2/measured_with_gaps。GC最大57,600,032B与120字节FrameSample的480000元素数组一致。不能把此数据标为完整AC-PERF通过，root正修记录缓冲与真实60fps限速后重跑。高帧率本身不证明窗口隐藏，本轮focus全记录且GPU/实图有效。

## 新Unity报告与版本绑定

- `editmode-05.json` / `nunit-20261029-011333.xml`：325/325，含52原黄金与新增57条TRACE。
- `playmode-07.json` / `nunit-20261029-011413.xml`：18/18，新增2组复杂商店UI覆盖资格双向刷新、折扣类别/旧价结算、两种升级顺序、7强化目标取消/确认、满池删后买原候选、最后骰禁删。
- 两份JSON/XML名称完全一致、没有skip；原spec/config/golden一致，失败历史保留。独立检查见 `unity05-07-independent-audit.json/.log`。
- 测试后PDB独立检查5程序集/35项目源文档全部匹配当时当前源码，见 `unity05-07-pdb-independent.json`。
- Player03 buildGuid `e92f09b5bef848bda6bc52618d8ee682`；Runtime MVID `d3894f0a-551f-4c02-84b0-c92f2241c383`与实际build04 DLL完全相符，DLL SHA `696f6f56aa7aa24cc9a8a6c6951ac1cdd8bfb9818a41efeb08cf34a1bf824c82`，Core SHA `78deceeab59142c51bfb6a5ef86a1a920e6df712104501ad916405314f145fa4`。
- Player PDB20文档中19与核对时当前源码匹配；唯一GameApp当前变更是root随后调整帧率配置，被测hash `3dd91836fde66e810288d7ba11dae44b435fb842fa91ed5d4757a2751fbcbb1c`，新源码hash `6b5878cf432b2fec099506ae92080486c19fd7dd8fabf131ab8607ea80d4f4cb`。不能用build04签新帧率实现。详见 `player03-pdb-independent.json`。

## 撤回的疑似项

首次图像工具显示/视觉解读中怀疑normal-selecting1080标题裁切与叠字；重新以original查看并直接读原PNG像素未复现，明确撤回。原图SHA `ec63af8a33aa3666b4cab3ec2079ec4441a3d8729b19818ca9818ee0bc3b1d44`；标题亮色边界(38,36)–(186,70)，row0最大通道34，五个caption和五个空槽各只有五簇像素，位置均正确。该疑似不计为游戏缺陷、不要求无意义返工。

尚需最终新Player性能及其构建绑定；人工5–8人每人至少2局未运行，不能代签体验、美术标杆或发布。手机平台仍未验证。
