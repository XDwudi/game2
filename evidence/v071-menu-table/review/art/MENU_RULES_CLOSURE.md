# MenuRules 间距缺陷修后限域复核

结论：**通过，局部缺陷关闭**。实际查看修后 6 张原始 PNG，流程行与 C48 倾斜上边线之间均有清楚、连续的深青空隙；上方标题纸边也留有余量。空存档与有存档状态结果一致，未见本次上移引入的新接触或裁切。720 的空间最紧，但字形与卡边已可清楚分辨；16:10 上下深色余边保留，菜单整体完整。

| 画幅记录 | 实际像素 | 实看场景 | 独立读取 NUnit |
|---|---|---|---|
| 1080 | 1920 × 1080 | 150% 空存档 + 有存档，各 1 张 | 1/1，0 失败、0 跳过 |
| 720 | 1280 × 720 | 150% 空存档 + 有存档，各 1 张 | 1/1，0 失败、0 跳过 |
| 1200 | 1920 × 1200 | 150% 空存档 + 有存档，各 1 张 | 1/1，0 失败、0 跳过 |

本轮只复核 `MenuRules y402 → y382` 的菜单间距修复。此前 project_audit 第二审发现此问题，我在复看旧图后确认；这是我此前图审漏指的局部问题。`MENU_RULES_INITIAL_FINDING.md`、上一阶段 `FINAL_REVIEW.md` 及其绑定均原样保留，不用旧通过覆盖新发现，也不重复签署无关 HUD。

证据核对：6 张 PNG 已实际打开查看并解码，实际尺寸、SHA-256 与各自 sidecar / extra-report 一致；均为 fontScale 1.5，截图前后状态/存档/待选存档摘要一致。6 份 glyph 报告显示 MenuRules 的 y 为 382，无 overflow/truncated/errors。3 份 NUnit 各 1/1 通过；对应 report SHA 与 binding 相同，测试前后 1781 项源文件列表及程序集列表不变。以上是对原始报告的只读复核，没有重跑 Unity，也没有修改阈值。

3 组共同 Runtime MVID：`5d15c310-0a76-4911-aa88-b75336a00bff`；Runtime DLL SHA-256：`abcf44d9b4f1554e61691ebebf66b1f197c42a382c31e8bafdd623cf972879f0`；截图记录 sourceManifest SHA-256：`de1f052956e69a191c7e74cfa2deb2e668528839b584dcd9a3acba33f79371ca`。环境为 Unity 6000.6.7f1 / WindowsEditor / D3D11。此处绑定工作树快照与加载程序集；截图内旧 BuildIdentity 的 41aface 不等于本次新源提交证明。

完整原图绝对路径、逐件 SHA、sidecar、glyph、3 份运行入口/NUnit/binding/extra-report，以及旧发现记录哈希，见 `MENU_RULES_CLOSURE.bindings.json`。原图与报告未改写。

本结论仅覆盖此次技术美术间距缺陷；人工风格体验、性能和发行验收不在本轮范围。
