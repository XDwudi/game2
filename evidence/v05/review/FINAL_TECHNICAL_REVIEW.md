# P0 v0.5 独立技术审查

审查者：`/root/v05_spec_audit`。2026-10-30。限定输入为归档 P0 v0.5 MD/Word、用户本轮改动、实现契约；不修改生产代码、原文、验收门槛。审查执行包括独立规则/概率/商店算例、真实文件IO、实际Unity报告、Player原始数据、截图、源码/PDB与完整ZIP检查。

**当前结论：Windows核心实现、指定设备上的自动功能/UI与性能检查通过；macOS构建/Universal结构与转交工具通过静态检查，原生运行和平台验收未完成。整个双平台版本不标ACCEPTED。远程恢复及Windows新进程续玩证据另附后续交付复核。** 真人体验、风格、难度/胜率和正式发行不由本审查代签。

## 冻结版本与通过证据

| 项目 | 最终已复核证据 |
|---|---|
| 游戏源版本 | `967f309a6be402612514714f46f3743421267799` |
| 配置 / 黄金集 | p0.5.0 / schema3；配置SHA `6732ab356049166c58a0d6ededf6bb2be8484690ac79eb9b96e303fd03588cfc`；黄金SHA `2fb088ab96e1261b286c120f8c93d84e351e98d2412e835194adb2aa11467a42` |
| 规格符合性 | MD/Word正文交叉核对；A/B附件与生产配置逐项一致；100黄金×26字段；附录C 98 ID映射。`CORE_SESSION_REVIEW.md`、`APPENDIX_C_COVERAGE.md` |
| 独立计算 | 1024完整计分/解释、512极端权重概率、128完整商店轨迹独立对照；最终托管538/538，含缓存新增3个实际文件IO边界。`independent-managed-run07*` |
| 原生Unity | EditMode03 554/554、PlayMode04 24/24，0失败/跳过/未定；旧用例无删减。`unity-edit03-play04-report-audit.json` |
| 编译来源 | 最新Editor 5程序集47文档，双平台Player各2程序集25文档，DLL/PDB身份和源checksum全匹配。`unity-prebuild-pdb03-save-cache.json`、`windows-candidate02-pdb.json`、`macos-candidate02-pdb.json`；`final-candidate02-build-binding.json`进一步连接ZIP内DLL/PDB字节、BuildReport身份/GUID与实际Windows MVID，零差异 |
| Windows实际Player | `player-20261030-054053-169bea91`；buildGuid `182571f77341471d9fd9acd120b05e26`；Runtime MVID `653f6fb3-eab0-41a5-b900-7d8fa8ff4c8d` 与实际DLL一致 |
| 流程 | 正常161步，11个预期字段逐条一致，终态第9关RunFailed；Debug24为178步、24关/23商店/8风险，终态RunWon。实际存档、渲染状态与记录一致；20次手册纯读取，53条动画检查 |
| 视觉 | 原生截图复核720p/1080p/1440p下10骰、K8、10控制器、5道具、附魔与多层数据；新版底部入口避开水印。`player02-visual-inspection.json` |
| 包完整性 | Windows308、Mac303成员全部与manifest文件集合/大小/SHA/模式一致；ZIP CRC、嵌入身份通过。`candidate02-package-independent.json` |
| Mac Universal | 6个实际Mach-O的fat/slice头和边界独立解析，主程序及5组件均含arm64+x86_64；不等于Intel或M5实机通过 |
| Mac转交工具 | 36合成检查及原归档漏洞输入复验通过；工具包14成员、22项内容绑定检查通过，脚本/真实Windows结果/最新Mac包一致。`mac-tools-independent-run02*`、`mac-kit-candidate02-independent.json` |

## Windows性能结果与界限

环境为Windows 10 x64、Unity 6000.6.7f1 / Mono / Development Build、1920×1080 D3D11，Ryzen9 5900X、RTX3080、约64GB RAM。压力包含10活动骰、8提交槽、10控制器、5道具，真实重投/调序/待锁清空、自动存档与UI预览持续启用；10秒warmup后连续180.006992秒。

原始CSV10747行，唯一首partial区间不计入完整帧，其余10746帧全部参与独立统计；帧号无缺口、时钟单调、原始interval与elapsed差值一致。`windows-player02-independent-audit.json` 逐项复算报告值：

| 指标 | 独立结果 |
|---|---|
| 原冻结LateUpdate间隔门槛 | P95 **16.69129999999086ms ≤ 16.7ms**，通过 |
| 帧间隔平均 / 最大 | 16.749537ms / 24.872ms |
| Main Thread P95 | 17.0311ms |
| FrameStart / PresentCalled / FrameComplete P95 | 17.0331 / 16.9871 / 16.9823ms，保留诊断值，不替换冻结门槛 |
| GPU P95 /有效覆盖 | 0.239616ms / 98.02717% |
| UI BuildBatch P95 /有效覆盖 | 0.007ms / 100% |
| GC平均分配 / 最大分配 | 48,674.449B/frame / 2,215,224B |
| GC collections | 196；Unity记录三generation均为196，不相加成588 |
| 错误 / 采样计时警告 / 帧缓存扩容 | 均0 |

通过只适用于该实际设备、构建和场景；不是每帧都≤16.7ms、不是物理scanout延迟保证，也不代表其他硬件/Mac/移动端性能。独立复核没有筛除慢帧、把缺失GPU值填零、移动采样点、降低压力或修改阈值。

第一轮Player01 P95 16.77ms失败仍保留。修复为AtomicSaveStore对上次成功写入且逐字节相同的旧active避免重复解析，同时完整验证新候选；外部变化/首次读取仍走完整校验。两条真实UnityIO与三条独立WindowsIO新增回归覆盖候选非法、外部替换、同长同mtime损坏、Replace失败重试。源码差异确认probe日程、采样器、生产帧率器均未改动。

## 已关闭发现

- V05-R01：C03持有时手册全蓝错误可达，已修复并独立重验；升级可购买资格仍独立计算。
- V05-R02：损坏lastSubmission能绕过加载校验并遮住有效backup，已对冻结快照/必需数组/26计分字段完整重算，实际IO恢复通过；Unity空嵌套默认对象仅在无Submit收据且恰等于默认记录时归一化。
- MAC-R01：归档symlink祖先及逐组件目标解析漏洞，原失败输入现在拒绝，正常Framework链接仍通过。
- UI-R01：右下水印遮住重复手册入口，新三分辨率实际图均避让，已关闭。
- 包清单：旧候选漏记元数据/许可/说明三成员，新包308/303全量匹配，已关闭。

所有旧失败报告/复现输入保留。详细过程见 `PLAYER01_AND_SAVE_CACHE_REVIEW.md`、`STATIC_FONT_AND_MAC_TOOL_REVIEW.md`、`BUILD_GATE_REVIEW.md`。

## 剩余验收

1. Mac需用户已确认的macOS26.6.1/M5上真实arm64/Metal执行、ditto重包、新目录解压、三分辨率视觉、鼠标/触控板/模态操作、180秒压力、正常关内与商店退出续玩。编译71条warning包含实际使用的URP/Lit与SSAO路径；没有把编译成功当作Metal无问题。Intel只确认slice存在，运行未验证。
2. Windows→Mac checkpoint、同种子/命令完整状态对照、Mac checkpoint返回Windows均需真实平台结果；目前仅Windows本地checkpoint通过。
3. 同交付commit的远程Git/LFS新目录恢复、两包哈希及相应原生启动待交付复核。root已报告实际GitHub push因仓库LFS额度超限被拒，候选交付commit `940b8fa` 此时仅本地存在；没有绕过pre-push上传缺失LFS对象的指针。此为外部交付阻碍，不是本地包损坏或代码测试失败；真实push日志与远程HEAD待交付复核附录绑定，不能把本地解压启动记成P15远程恢复。Windows新进程恢复可从本地候选独立补验。Mac最终ditto包如变化，必须更新SHA并重推同步清单。
4. 5–8名非设计参与者至少两次正常Run或继续已有正常Run的试玩未执行，不能声称24关正常难度、经济平衡、**40–75分钟**局长目标或乐趣已验证。时长依据v0.5规格第17行及实施契约V05-EXP，不沿用v0.4目标。Unity开发水印、Windows未签名和Mac签名/公证状态按交付限制保留。

此处的Windows通过为已举证的工程范围结论，不代签上述未完成条件。必须在剩余证据完成后再决定整版本验收。

## 后续交付复核

后续实际证据见 `DELIVERY_REVIEW.md`：本地新解压包的跨进程checkpoint导入/324分/状态哈希/幂等和主菜单渲染通过，正常关闭exit0；短探针不是第二次完整压力。另一延长回放发生文件替换错误，原active121/backup120/tmp122完整保留；隔离副本锁住backup复现同错误，释放后重试与原tmp整个状态精确一致，最终独立回归run08 **539/539**。占用者未知，不删除失败或宣称永不发生I/O错误。

源码远程clone967f309、干净状态及fsck已独立验证；包push实际因GitHub LFS额度超限被拒、上传0，P15未完成。原940b8fa候选保存在本地分支且包字节完整，源码/文档推送与双包上传状态分列。具体绑定为 `delivery-independent-binding02.json`、`native-io-retry-review.json`。本阶段review写锁释放，Mac原生/双向续玩、远程双包恢复与真人体验保持未验。
