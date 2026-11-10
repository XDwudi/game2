# v0.71 最新界面试玩包

状态 REVIEW（诊断试玩候选已发布，非整版ACCEPTED）；2026-11-10。用户明确要求“按要求进行打包，给我可试玩版本”，延续已授权GitHub发行附件渠道。基于已上传 `b005e0088b227b26e2f8fc44f1c59f68df0c0a35`（游戏变更bd57633）的新Image2菜单/桌面；沿既有Windows x64、macOS Universal与Mac验证包范围。不改玩法/图风，不覆盖旧p0.7.1-rc.1；新p0.7.1-rc.2测试发行保留原验收阈值和已知平台/性能限制。

准备：实际工具Unity6000.6.7f1/CLI beta.15，当前源码已上传；旧专项结果不冒充完整构建gate。root独占Unity/Assets/构建输出/文档与发布；dice_sample独占4份packager/manifest/publisher/test文本，project_audit只读构建验证，art_assets只写cache玩家说明草稿及只读实际图审。所有旧失败、旧包和无关修改保留。

| ID | 验收方法/期望 | 证据/责任 |
|---|---|---|
| RC2-01 | 当前源完整发现/运行Edit+Play，无空集/跳过/失败，前后SHA与PDB/实际程序集匹配 | NUnit、discovery、gate；root执行/独立复核 |
| RC2-02 | Windows与Mac profile实际严格构建成功，身份绑定当前源；Mac架构arm64+x86_64 | BuildReport、成员身份/PDB/架构；Mac实际运行仍未测 |
| RC2-03 | Windows普通/debug回放、UI/动画/显示恢复与既有180秒样本真实执行；存档隔离，限制如实记录 | Player原始数据、截图、守卫；不把性能/native失败写通过 |
| RC2-04 | 三完整ZIP内容/CRC/SHA、身份、说明与Mac kit一致，独立复核通过测试包范围 | package proof/manifest、非作者复核；人工体验待用户 |
| RC2-05 | 新GitHub发行附件上传发布，远端完整下载校验及下载版启动；给可下载试玩链接 | API/下载SHA/实际运行记录；原发行保留 |

不存在需要再次让用户同意的普通构建/上传步骤。若实际工具或标准阻塞，保留真实失败、修复受影响部分；现有允许标明限制的测试候选交付路线继续适用，不降低正式验收标准。

## 本轮实际结果

- RC2-01通过：Edit `nunit-20261110-101318` 1018/1018、Play `nunit-20261110-103648` 185/185；新三项菜单/桌面用例实际执行。1789项gate、首尾绑定、五程序集/198源PDB一致；完整Play176目录/3157文件归档，含17空目录，原件未删。
- RC2-02通过构建范围：Windows `build-windows-20261110-103917` / GUID `75e7f6b0f1c34075a6527abbc6829e8b`；Mac `build-macos-20261110-104006` / GUID `b523ca601806410ca9fbb7ff3d943c8c`；各Player两程序集/105源PDB一致，Mac六Mach-O双架构。构建警告与旧版一致；Mac实机未测。
- RC2-03已真实执行，性能仍有缺陷：145正常/175调试回放、19 UI/50动画、自动原生显示和独立进程恢复通过；180.0108082秒P95 16.847ms >16.7ms，GPU有效覆盖97.2184%。使用原有诊断候选路径，不改失败报告。构建305文件前后SHA不变；原runner普通存档守卫保存相等布尔，缺少前置逐文件SHA归档，明确此证据边界。物理焦点/显示器场景未测。
- RC2-04通过候选封包范围：三ZIP逐成员/CRC/来源复核，Win311/Mac306/Kit17成员；完整Mac ZIP嵌入Kit，双平台README、身份及性能限制同新源。127既有资源逐SHA复用；新Player菜单/桌面图审完成，详情入场遮罩截图不计预览可见。人工风格未签署。
- RC2-05完成下载与启动观察范围：GitHub发行ID440245825于2026-11-10T11:22:20Z公开，三附件全量下载SHA/CRC匹配。下载新目录01真实执行18资源阶段，但闲置inputRows1导致辅助诊断incomplete；02在SettingsTab0等待稳定条件时内部180秒截止，原始失败均保留。未将两轮拼成通过。三守卫true、无新PNG；CUA应用访问审批超时、未成功截图或输入。公开结果见 `evidence/v071-rc2/delivery/github-published-release.json`；后续人工试玩、Mac和正式性能仍待验。

独立复核：`evidence/v071-rc2/review/` 保存精简报告副本与来源SHA；原始记录保留 `cache/v071-rc2/` 和实际运行目录。Editor退出的动态字体缓存已保留变更后恢复精确已测字节，临时GameView恢复原Free Aspect，双构建后的源干净，无Unity写锁。
