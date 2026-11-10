# 41aface 远端完整下载与新目录启动独立复核

复核者 art_assets，2026-11-09 UTC。结论：**三附件完整下载、封包一致性与新目录 Windows 资源/空闲诊断，在本次实际证据范围无剩余阻断。** 可以作为明确标记的诊断测试候选；未签正式性能、原生显示、Mac 或人工验收。

本代理只读实际文件与记录，并写复算结果；没有联网、启动 Player、访问 Registry、修改工具、生产或阈值。远端响应及进程由 root 实际执行，复核不是本人重跑。当前用于本报告的服务器记录为 **draft**；公开发布后响应需另行核对，不提前声称已公开。

## 三件完整下载

独立重新读取 `artifacts/cache/v071-full-release-review-download/` 下三个完整 ZIP，逐包 SHA、字节数、CRC、成员数及内嵌身份全部匹配已封包产物。

| 附件 | GitHub asset ID | 字节 / 成员 | SHA256 |
|---|---:|---:|---|
| Windows x64 | 726309465 | 197187916 / 311 | cba88bd84ce7b7b97cadf27aa544cc1953f31ef13fd10774854eb585a2bc3163 |
| macOS Universal | 726340871 | 237545640 / 306 | 83c4fc9eb1bbcd74e180412ae8cdfded6345f3f34921d7cbbdb8e0725a6a3bc6 |
| MacVerificationKit | 726344440 | 237649059 / 16 | cb182df2b29c29ca3ad901602595886cbc149020a654c334ccdc6aae6626a54b |

逐字段比较三 upload、三 download receipt 与明确指定的 `release-upload-20261109-052825-495082.json`：固定仓库 XDwudi/game2、release438762609、tag p0.7.1-rc.1、asset ID/name/state/size/digest/content-type、同一下载 URL、HTTP200 和最终 release-assets.githubusercontent.com 主机一致。最终 Kit 是修正文案后的 cb182df2…，不是旧 fadeb0f8…。

三份 receipt 的 manifest SHA 均为 `e880f6bbd42b5caf120a3f183683834f7eca6c17f6ce86c062e83c483d383d26`。实际构建 sourceCommit 保持 `41aface80bba6c1e008bd3d35344210a28003553`；releaseTagCommit/服务器 target_commitish 保持已有远端 `b11694f8a2f7d767d2113e9b467713807efa403a`。两身份未互换；新源码没有作为本次附件发布流程的 Git 对象上传。

Windows 旧 upload receipt 因草稿 alias 改变而保留为 `upload.before-draft-tag-correction.json`，现凭据 asset ID 和内容 digest 不变。已读 project_audit 的 `tag-publish-review/review.md`：两字段 PATCH 静审/55 项离线及模拟正反路线是其他审查者的结果，本报告没有把它们算作真实发布或本人重跑。

## 真实新目录运行与保护

实际路径 `artifacts/cache/v071-full-remote-41aface-01`，argv 指向其 `player/DicePrototype.exe`，入口 `--art07-resource-diagnostic`，存档重定向至 `run/diagnostic/isolated-saves`。进程 PID48256，05:35:09.234595—05:36:19.476985 UTC，exit0，无 timeout、终止或仍存活标记。顶层与底层均为 `diagnostic_completed_not_acceptance` 语义，errors 空，formalPerformanceAcceptance/wholeReleaseAccepted 始终 false。

实际 WindowsPlayer / Direct3D11 / 1920×1080 Windowed，启动显示加载被抑制，build GUID `e36a7047e1f44fe9b16387cbf61ab639`。真实 runtime MVID `7f0848c4-f0dd-43e1-ae93-09cc1d3b7253` 与同源正式 Player 参考一致；包内、runner、resource-report 的完整 BuildIdentity 相符。

独立重哈希 **14 项启动输入、311 件新解压成员、305 件原构建文件**。新目录当前文件全集与 extracted-member-verification、运行前后 build guard 及冻结 package proof 完全一致；原构建当前全集/字节与同一 proof 一致。原构建运行中未变由适配器真实前后守卫另行记录，本代理未伪造运行前观测。

直接比较 before/after：LocalLow 完整树 **17 个文件**、游戏偏好 **46 个值**相同。17 项均有文件尺寸和 SHA，不是目录：其中 12 项游戏存档/动作/显示数据，另有 Player.log、Player-prev.log、TestResults.xml 和两项 Unity Analytics 辅助文件。不能把它称为 17 个存档槽。

**Unity Screenmanager 前后确实不同**，原报告的 `engineScreenmanagerValuesUnchanged=false` 被保留；不能声称整个 Registry 没变。诊断自身的普通偏好恢复、注册/清除诊断时钟及 app 先销毁顺序均有真实字段和列表支持。idle 前后规则状态、待选列表及隔离存档逐对象相同。

## 原始 CSV 与资源重算

idle CSV 1200 行、UnityFrame2733—3932 连续，末次 elapsed20.002525 秒；报告实际窗口20.0031986秒。由原始行重算，全部匹配报告：1200 有效 GC、0 缺失、1199 零 GC、**frame2890 一次132B**；焦点、运动、指针变化、输入和序列变化计数全部为0。20 秒观察条件完成，**并非零 GC**，也不是正式180秒性能通过。

resource CSV **3931 行**连续；三个计数器各3930有效、首行1缺失，未把空值当0。有效纹理计数固定585825814B，draws范围0—189，triangles范围0—19756。idle-warmup-and-observation 阶段1384行，draws118、triangles19726；该阶段含预热，不能把它等同于恰好1200行 idle 采样。

9 个实际快照均逐 native Texture ID 去重并重算分类：每快照221个纹理、71个被引用的插画纹理。总量及类别每次相同：

| 逐对象 native 计量 | 字节 | MiB（约） |
|---|---:|---:|
| 全部纹理 | 585825814 | 558.687 |
| 引用美术 | 484783772 | 462.326 |
| TMP 字体 atlas | 20451658 | 19.504 |
| render-target | 78905026 | 75.250 |
| unknown | 1685358 | 1.607 |

6 个 TMP font asset 的 atlas 引用和类别一致；DiceChinese/DiceChineseBold 为 Static，各1 atlas。最后两个快照10个实际活动普通 Body、合计3000三角；轮廓10件和物理面60件分开，碎片 prefab6/实例12亦分开。池实例数量不作为活动特效峰值证明。

这些是 `Profiler.GetRuntimeMemorySizeLong` 的对象 native 字节和独立 Texture Memory counter；**不是已分别测得的 CPU/GPU 驻留内存**，unknown 未减去。9 个快照稳定只说明此次有限菜单/手册/设置/商店/十骰访问期间的采样，无内存预算或长时泄漏验收结论。

首次自写复算脚本误把同帧两次独立实时钟读取假设为1微秒内相同；根据实际源码两次调用纠正，实测差值跨度79.8微秒作为信息保留。没有改项目阈值或 CSV。说明见 `remote-delivery-review/recompute-notes.md`；最终全部原有观察条件、行计数与报告重算均通过。

## 不改变的发行边界

旧实际180秒性能仍 FAILED（P95 16.8078ms，高于16.7ms；GPU覆盖91.91%低于95%）；严格原生显示确认失败、Mac M5/Metal/Intel/跨平台存档未验、AudioMixer及人工美术/听审/试玩仍待完成。此次只关闭“真实远端三包与新隔离 Windows 资源诊断”证据缺口。

独立执行的复算脚本和详细结果在 `cache/v071-full/remote-delivery-review/`：`downloads-draft.bindings.json` SHA `867f2a5c3f67c71b6ee4d6c5d73d2960dc1e6c086c5e299e569aa89d8e1ef0e1`；`run-recomputed.bindings.json` SHA `130fe0c50fb70b681b430782022fad1415b25c789e44684b5c7ad716b40e427e`。本报告同名 bindings JSON 汇总实际输入绑定，原记录及失败历史保留。
