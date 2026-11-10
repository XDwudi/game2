# v0.71 主菜单与桌面更新：最终限域复核

**结论：本轮受影响工程检查通过；发现的 MenuRules 与卡牌斜边贴碰已修复，三画幅实际图复核关闭该缺陷。** 未发现剩余的本轮阻断项。此结论不升级旧全量回归、整版体验、美术人工批准、平台或性能验收。

复核人 `/root/project_audit`；只读生产及实际证据，没有操作 Unity 或改生产/标准。本人是三个新增测试候选作者，`dice_sample` 独立静审了其指针/生命周期实现；本报告独立复核 root 的生产改动、root 真实运行及原 PNG，不自签全项目质量。

首轮矩阵为 1080 的 8 项、720 的 3 项、1920×1200 的 3 项，即 **14 次执行、8 个独立用例**，全部非零逐叶 Passed，无失败/跳过。原报告、GPU/字形/存档比对、1781 行源与五程序集绑定检查，以及已发现的问题图，完整保留在 `TARGETED_MATRIX_PRE_RULES_FIX.md` 和 `FINAL_TARGETED_REVIEW.bindings.json`。桌图在关闭颗粒/Reduced 下仍渲染、16:10 暗房外延、临时阴影强弱、真实菜单入口/取消/继续、五骰/最大锁标题/详情孔洞的实际范围见该报告；没有把其中 1080 专项扩大到其他画幅。

root 随后仅将 `MenuRules` y402 改为 y382，字号和框尺寸不变。新三轮各运行同一个 `PLAY_ART071_MenuTableRealFramesAnd150GlyphsPreserveWorldDiceAperture`：

| 画幅 | 新 NUnit | 实际结果 |
|---|---|---|
| 1920×1080 | `nunit-20261110-084835.xml` | 1/1 Passed，0 Failed/Skipped |
| 1280×720 | `nunit-20261110-084902.xml` | 1/1 Passed，0 Failed/Skipped |
| 1920×1200 | `nunit-20261110-084928.xml` | 1/1 Passed，0 Failed/Skipped |

新旧证据明确分开：**14 次原受影响执行 + 3 次修后画面验证**，不是最终源重新跑了整套 14 项。新三轮源快照首尾及跨轮相同，1781 行全部与检查时当前文件 SHA 相符，五个 DLL 与运行绑定相符。相对旧矩阵只有 `GameApp.Art071Screens.cs` 的 SHA 变化；将当前文件唯一的 `166,382,924,48,21` 反向改为 `166,402,924,48,21` 后，字节 SHA 精确恢复旧值，证明仅这一位置变更。原 GPU/阴影/菜单路由实现和相关测试均未变。

新三轮 18 张原图 SHA/画幅均复核，截图 state/save/pending 前后相同；只读动作维持状态、存档及 action sequence。9 份字形报告共 108 个标签实例仍完整、可见，无 overflow/truncation。`P0Config.json`、`GoldenScores.json` 在旧矩阵已逐字节核为 `41aface` 原件，且新矩阵相对旧快照未变。

独立实际打开三张新 `005-table-saved-menu-font150.png`：1080 SHA `57d15a428075599c55093e60ac259d6cdd8173269313a14b9d7226da085cb868`；720 SHA `a996ad7e78a4818bb4cfed74bea0a734a72986f1c08f18aa33c6ec330394ad5f`；1200 SHA `7af77429ce16bcaeb3ca2bb7a43d20c16810a8ef96a12382b2f4c2491bdb9973`。三图流程文字下方均可见连续桌布空隙，已离开 C48 倾斜卡顶；副标题、右侧菜单、底部锁定提示无新增遮挡。桌面木沿、三颗真实骰子和 16:10 上下暗房保持可见。仅这一间距缺陷作关闭结论，未给未打开的新图补签视觉通过。

`ItemsTitleUnderlay` / `ActionReasonUnderlay` 是先于文字绘制的被动深青底色，`UiKit.Box` 的默认及实际设置均为 `raycastTarget=false`，未新增 Button/Selectable/事件；先前原图及真实指针检查无动作输入阻断。这些代码在最后三轮没有变化。

精确路径、SHA、逐轮程序集及图像索引在 `SPACING_REPAIR.bindings.json`，SHA256 `5c61d1076e353a797b18461ab33f018b2b28727f72c59bf3a9ed8c993cb8bb25`。原报告和旧失败现场不覆盖；本报告不声称新增 PDB、构建、上传或 Player 测试。源码提交状态由主代理据实际 Git 结果记录。
