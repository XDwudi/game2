# 4041766 → 69d2f4d 精确差异与媒体有限续用补记

结论：**旧媒体已完成的限域视觉结论可以继续引用；采集源身份保持原值。** 新检查点不增加旧媒体签署范围，也不能用它替代新 Player、版本截图或完整回归。此次只读核对 Git/磁盘和实际资源读取记录，未运行 Unity、重看/重录图片、修改标准或外传。

比较 `404176661240a5d9834895416ed3719d6493e253` 与 `69d2f4dc3afa6387d443e827eb0b96892e9f2cfb`：Assets/Packages/ProjectSettings 的1360项 tracked physical source 路径集合相同，无增删；1359个 Git blob 原字节不变。唯一变化是 `Runtime/Verification/PlayerVerification.cs`，旧 SHA `1aa09930202f68d61a4fd9b8944cafcc8f0728a4735db30484e307c11c9b68f6`，新 SHA `7e76293d6e9be83eb642c68bcbc2cba0245357fb7fef7852f56077c703cf6787`。ArtSource 与 tools/art_v07 另作路径比较，也无差异。

新提交文件原字节与 `PLAYER_REDUCED_ROLL_COVERAGE_REVIEW.md` 已复核的 revision20 候选完全相同：仅 revision19→20、独立已接受 normalRolls 计数、第二次 Roll 的 Reduced 选样及原立即检查移出 IsBusy。原95条 Require完整保留；游戏 UI、资源、计分、演出、录制器、输入/显示和存储实现没有改动。本次没有重复修改或重签此前静态审查。

11:53:37.9679673Z 的 `checkpoint-resource-identity-69d2f4d.json` 实际成功执行 idle guard、同步导入及 Resources.Load；读取 sourceCommit 为新提交，Runtime MVID 为 `40bf476e-46fc-401d-99ac-cf636e7371cf`。与404前身份相比，仅 sourceCommit 字段变化，art0.7.0 / p0.6.0 / schema4、Unity6000.6.7f1、配置和黄金用例 SHA 不变。新 MVID只记录实际读取，不在全量未提供时预填新 PDB/测试通过。

因此，0f04e51 的六配置/九段媒体、821ec59 的 ART14 补证，继续按此前精确差异链与独立报告引用未受影响的既有结论。PNG/MP4/report/source-binding 不改标为69d2f4d；未实看的范围、ART14 柔光观察项、声音/性能/平台边界均不改变。这一结论不要求机械重录未改动的艺术素材，也不扩大为整版艺术接受。

旧404 Player覆盖失败保持失败；独立显示专项的原限域事实也保持原源身份，不被当作新严格流程通过。新全量999/154、构建、Player/性能/恢复及新身份版本截图，等待root提供真实结果再复核；此次不轮询、不新增门禁。AudioMixer、Mac实机、人工体验/美术与外传授权边界继续保留。

逐项源比较、原/新文件快照、实际身份和 SHA 见同名 bindings 及 `artifacts/cache/v07-media-reuse-69d2f4d-review/audit.json`。
