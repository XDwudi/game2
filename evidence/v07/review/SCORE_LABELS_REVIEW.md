# 计分文字、fallback与大倍率独立复核

2026-11-01 UTC；art0.7.0 / p0.6.0 schema4；reviewer v07_spec_audit；生产/测试只读，未运行Unity。结论：**720p三字体的缺字与大倍率按钮已限域修复；分数明细行重叠及1080p/150%分数越过倍率按钮边界仍为REWORK。**

## 实际证据与源边界

| 批次 | 实际结果 | 本轮实际打开PNG |
|---|---|---:|
| 720p 031040 | 3项2过1败；Hand全角斜线GPU墨迹0 | 1（fallback150） |
| 720p 032026 | 3项2过1败；fallback探针在无存档时直接读debug-run.json失败，预览字形与overflow通过 | 6（MaximumTable三字体、Zero/SharedHalf/overflow150） |
| 720p 032743 | **3/3，23.656s** | **全部21张**，7场景×100/125/150% |
| 1080p 032835 | **3项2过1败，20.638s**；FinalScore进入倍率按钮区域 | 5（MaximumTable125、SharedHalf100、Debug32/精确公式100、overflow150） |

合计实际看 **33张**，不是56张库存全看。四份XML SHA与绑定一致；各批运行起止1257源文件快照稳定。720新批runtime MVID `cf4f28d9-e3b8-4b10-88fe-3bc3d2616409`；旧图不代表后续源。56张PNG另算SHA，55张具有自身元数据且尺寸/MVID核对；031040失败截图没有配套元数据，不冒充完整绑定。详细清单在`score-labels/bindings.json`。

032743有51份真实GPU差分记录、2415个字形观测，最少4个变化像素、最大隐藏背景差0，state与存档存在性均不变；9份fallback探针观测开始/结束都无存档。检查器分别捕获两张隐藏背景帧和一张真实可见帧，逐字形采样限制在自身advance内，避免邻字替失踪字供墨。它能证明本场景有实际字形墨迹，不能单独证明布局、美术或其他场景全部通过。

## 本轮确认修复

- **Hand缺“＋／混”**：MaximumTable三字体完整显示“计分 两对＋顺子”“预计得分 · 混色”；无空行，精确13,338及六因子可读。ASCII主字库＋真实中文fallback的独立探针连续复用3次，三字体“H＋混 8”实图完整，证明未仅依赖预热主字库掩盖fallback生命周期。
- **Debug32入口压卡**：正常10卡保留；32卡时只展示9张，第10格专用“全部32件>”按钮，与邻卡无覆盖。720三字体及1080/150代表图可读；实际测试保留打开/关闭只读state/save检查。
- **超长控制器倍率**：032026仍把16位数挤入两行；032743三字体统一为单行“控制器 ×≈1.85e15”。点击后Formula保留精确`1853020188851841/1`，完整最终分数`1,081,570,823,829,042,554,880`可见；近似只用于按钮摘要。三字体公式都位于滚动视口内，150%允许公式换行。
- **零分与随机预览**：0分显示原因“没有有效5点”；SharedHalf显示94—24,192及“查看9种结果”，没有空白字符行。720三字体已实看；这不是随机规则重新验收，也未通过截图推断隐藏q。
- **无档探针修法未降低只读要求**：当前GPU辅助先记录File.Exists，结束严格断言存在性一致；有档时仍逐字节比对。没有为让无档场景通过而创建临时普通存档或跳过已有存档验证。

## 保留缺陷

**SL01 / P1 / 分数明细正文重叠。** 032743的`score-exact-controller-factor-font150.png`中相邻DieLine的第二行与下一骰编号行互压，125%亦明显拥挤，100%正常。定位于当时`GameApp.Modals.cs:323`固定高度70、cursor+=72（每骰额外14），未根据150%文字实际preferredHeight调整；GlobalTitle固定48/58同样存在风险但此图未滚到该位置。Formula自适应高度已修不能代替正文通过。修法应按每段实际高度推进并覆盖第一屏、中段、底部/全局行；不得只缩字号或减少骰详情。root已委派返工，尚无本轮修后实图。

**SL02 / P1 / 1080p 150%预览FinalScore侵入倍率区。** 032835真实严格断言失败：实际glyph yMin=701.431，所需≥711。现有overflow150图可见大分数字体贴近因子按钮上沿；没有相应失败后完整截图可证明当前修复。root随后报告已在原字号下改MidlineRight对齐，但重跑03:32发生Editor原生内存分配崩溃，**没有新通过结果**。崩溃原因不能凭同时其他进程OOM确定；本报告不把它归因外部，也不据此关闭失败。

当前状态：上述三个目标缺陷（缺字、入口压卡、720大倍率按钮）可在指定场景关闭；整张计分/明细界面继续REWORK。没有代签人工美术验收、Mac、全回归、发布或性能。


## 后续 SL02 复验（033545 / 033650）

重建GameView后的1080p033545真实3/3，独立打开MaximumTable、Debug32、SharedHalf三个150% PNG：完整数字、22位缩写及随机范围都移回顶栏，和倍率按钮之间有清楚间距，没有缩小原字号。逐字形严格几何断言保留，**SL02在本批覆盖场景关闭**。同源720p033650真实3/3已核对XML；本轮尚未打开该批新PNG，不能冒充新720视觉全验。此前恢复失败033436为0/3（未建立GameView、错误尺寸/EndFrame超时），原失败保留，不改写成功。新增证据绑定`score-labels/sl02-retest.json`；新增实际看3张，总计36张。SL01分数明细正文仍待修后复验，整体REWORK。


## SL01 limited retest — 720p 034631

2026-11-01 independent follow-up: **SL01 closed for the inspected 720p source-list layout**, not whole UI/art acceptance. Actual 150% screenshots viewed: MaximumTable pages00/10/30/48 and Debug32 pages00/51/95 (7 PNG). The two-line dice identity/contribution rows now have clear separation; intermediate source rows and wrapped large-number global multipliers do not overlap. Formula and final result remain exact and legible. Text clipped at the top/bottom of intermediate wheel pages is normal viewport masking; subsequent pages expose it.

The 034631 XML is **5 total / 3 passed / 2 failed**, not an all-pass batch. The three 100/125/150% source-scroll cases pass (62.96/71.76/86.24s). Both unrelated Burst cases fail and remain failures. All369 capture PNG hashes match their metadata. Runtime MVID96763855-f259-4ec0-992e-018902c29414 and source manifestfb4b77b9dd370a68cce067230da4453959c12ebd605db91967b25752a7e2ded3. Exact viewed image/XML hashes: score-labels/sl01-retest.json. 1080 source-row visual regression and the older Historical14 case were not reviewed here. Original034058 timeout remains history; this limited closure relies on the newer three completed source cases plus actual images, not on deleting/ignoring that failure.


## SL01 limited retest — 1080p 035307

2026-11-01 independent follow-up: **SL01 is also closed for the inspected 1080p source-list layout.** XML035307 is **4/4 passed, 272.929s**: the three font-scale traversals plus Historical14. Actually viewed 8 PNG at 150%: MaximumTable pages00/16/48, Debug32 pages00/62/97, and historical permanent effects pages00/24. Dice identity/contribution rows are separated, source steps and the long global multiplier do not overlap, and the exact fraction1853020188851841/1 and result1,081,570,823,829,042,554,880 remain legible. Historical C36D006/D007 faceF06 changes3→4, red/blue changes, income and self-destruction remain separate from the present hand. Intermediate viewport masking is expected; traversal exposes the complete content.

All397 capture hashes match their own report entries; every recorded state/save pair is unchanged. This verifies bindings, **not visual inspection of397 images**. Source runtime MVID5bbc7f8e-1239-4f3a-88ca-2da361a52959, core MVID8f690e0d-a075-4b72-98e4-2cb64ced2fa3, manifest005757713df2104938c5de2056bd7644426a997de5a3230288f3bf5e63052ca6. Exact XML, report and viewed-image hashes are in `score-labels/sl01-1080-retest.json`. Total actual images viewed in this score-review chain is now51 (33 original +3 SL02 +7 SL01/720 +8 SL01/1080). This closes the reported row-overlap defect at the two tested resolutions; it does not approve the entire UI, later clipboard/button additions, platform builds, native display behavior or performance.
