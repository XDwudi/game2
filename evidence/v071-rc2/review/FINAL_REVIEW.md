# rc.2 完整 Play 现场与源码绑定独立复核

**通过本次实际完整 Play 结果与源码/归档绑定范围的复核。** 本代理只读实际报告、文件与输入身份，没有运行 Unity、修改源/测试/阈值或操作 Git。未对全部归档图片补签视觉通过；实际制作质量仍由具名图审/人工负责。

源为 `b005e0088b227b26e2f8fc44f1c59f68df0c0a35`。实际报告 `evidence/v071/unity/nunit-20261110-103648.xml`，起止 `2026-11-10 10:13:51Z` — `2026-11-10 10:36:48Z`，duration `1377.0017875` 秒。逐叶 **185/185 Passed**，0 失败/跳过/不确定，名称唯一，完整等于 PlayMode discovery；不是只读取总计字段。

三个新增用例真实执行并 Passed：菜单入口/取消/继续与存档守卫、100/150% 菜单/对局/详情实际帧，以及桌面 GPU 方向/比例/颗粒/Reduced。其精确名称、时长及原 NUnit 属性全部绑定在机器报告。没有把旧 17 次专项当作这次运行。

## 源与运行身份

- 本次开始快照先于结束保存，精确 UTC 为 `2026-11-10T10:13:51.5439522Z`；最终报告 binding 的 started 与该快照完整相等。
- 开始/结束 **1781 个文件**与 **5 个程序集**逐项一致；报告字节 SHA 匹配 binding。
- PDB 审计的 **198 项源 checksum** 与本次冻结快照相符，5 个 DLL SHA/MVID 与实际运行一致。原 Portable PDB 解析由 project_audit 执行；本代理独立比较其输出与真实运行绑定，没有冒称再次解析二进制。
- 现场中 **27 份 extra-report** 的 Runtime/Core/Test MVID 全匹配；**27 份 source-snapshot** 每份 199 项全部匹配本次开始快照。
- 归档时捕获到唯一退出缓存差异：`DiceChineseFallback.asset`。root 保留原件/patch 后恢复已测字节；本代理随后只重哈希该字体，SHA `ad4bd727b9dbbb1cdbe302c41404c2682632a2d93f0c83e7eeaa18ecfa4cffc0` 与测试开始/结束完全相同。原 archive 的差异记录未覆盖，由本闭环记录解释。
- root 恢复后生成的 **1789 项 gate** 内，全部 1781 个 Play 冻结源条目逐项一致。本代理没有在平台构建期间重新扫描 ProjectSettings 或把 Library 现状当作先前 Editor DLL；临时 preloadedAssets 写入不被错误归为永久源码变化。

## 精确现场归档

仅从该次已完成 NUnit 的 test-case 输出取 `DiceCraftTests_*` UUID；不按 LastWriteTime、是否有 PNG 或临时目录的全局列表猜选。原早期专项和其他用户临时文件没有被扫描或复制。

归档位于 `artifacts/cache/v071-rc2/full-play-archive/`：**176 个目录（17 个空目录）、3157 个文件、992 张 PNG、1,615,209,047 B**。逐件源前 SHA → 副本 SHA → 源后 SHA 相等，源大小/mtime不变；每个目录复制前后成员集合相同。空目录显式记录，失败历史原件未删除。

`archive.json` SHA256：`6d64d4aaae1a93a3d00e3b95e0b989beb7bb6ad4c0b53b306cda95e3ea4b906d`。完整目录→用例对应、每文件路径/大小/SHA及5程序集身份均保存其中。重 IO 已结束；本归档不应整目录自动上传 Git。

最终机器绑定：`FINAL_REVIEW.bindings.json`，SHA256 `3f1bc3c9f812816cff80ef12f10bcf91155e77465945974f10c240ec9907c769`。原 archive 在字体恢复前的 `technicalSourceBindingConsistent=false` 是真实历史观察；本报告仅在精确字体恢复与 gate 对照后关闭差异，没有重写原件或将实际失败/未验变成通过。

本结论不替代后续双平台构建、Windows Player/180 秒性能、原生显示、Mac 实机、AudioMixer 或人工风格/体验验收。
