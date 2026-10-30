# P0-002：v0.5 增量迭代契约

2026-10-30（America/New_York）。状态 REVIEW（Windows技术限域通过；Mac与人工体验未完成）。输入为本轮用户需求与原样归档的 `docs/specs/P0_v0.5.md` / `.docx`；起点 `36025ff17235993fa00f92241b0fc59ad6a9dd35`。用户请求优先，附件执行性文字只作为待核对的设计数据。正文、完整配置、100条黄金输入与预期、附录C/D/E共同定义规则，不擅自重设玩法。

## 范围与交付

2026-10-30交付渠道更新：用户已明确同意使用GitHub发行附件替代额度耗尽的LFS，具体授权和新验收映射见 `P0-V05-RELEASE-ASSETS.md`。下文原LFS要求保留来历，以此最新决定为准；游戏与Mac实机验收标准不变。

在既有 Unity 6000.6.7f1 / URP / uGUI / Mono 工程增量实现24关、58内容、40控制器及附魔、8栏与独立骰池、12种Lv8强化牌型、达标后选择、13行牌型手册、schema3隔离旧Run。保留源美术与既有动画，补足20图标与附魔标记。交付Windows x64、macOS Universal包、同源版本/配置/黄金集清单、真实验证、Git LFS远程双包。联网玩法、移动平台、新Boss、经济再平衡不在范围。

本次用户确认有 macOS 26.6.1 / Apple Silicon M5；连接或人工执行验收方式待安排。缺少实机结果仅阻塞Mac相应验收，不阻塞其余工作。Intel组件须检查架构，Intel实机尚无条件。

## 所有权与依赖

| 写入者 | 可写路径及产物 | 依赖 |
|---|---|---|
| v05_core_rules | Core/Rules、Data配置/黄金、Tests/EditMode/Rules、规则独立harness与证据 | 附录A/B/C/D/E；发布ControllerInstance和ScoreBreakdown接口 |
| v05_session | Core/Session、Tests/EditMode/Session、会话harness与证据 | 规则接口；负责所有持久状态、事务、生成与后置效果 |
| root | Runtime、Editor、PlayMode/Persistence测试、资产扩展、文档、构建/包与Git | 上述接口；唯一Unity/场景/项目设置写入者 |
| v05_spec_audit | evidence/v05/review及规格审查文档 | 默认只读生产；独立复核整个任务跨度 |

ControllerInstance实例保存附魔，不放定义中；计分返回触发集合和精确来源。UI只读会话快照、调用公共命令和同一规则服务。同一场景/Unity编辑器全程单写。源码提交和产物提交分离，以sourceCommit绑定双包；不覆盖既有证据。

## 验收

| ID | 场景、方法与期望 | 证据 | 验收责任 |
|---|---|---|---|
| V05-RULE | Unity EditMode及独立算法核对100黄金×26字段；附录C 98条ID逐条映射，精确概率、一次取整、无预览副作用 | NUnit、独立结果、覆盖表及源哈希 | 独立审查 |
| V05-FLOW | 24关23商店、达标继续/主动结束/耗尽、全部后置顺序、唯一结算、原子替换与存档失败重试 | Session测试、实际Player命令轨迹与存档 | 独立审查 |
| V05-SAVE | schema2保留备份不迁移Run，schema3全部稳定态恢复；不重抽、不重复效果 | 真实IO测试、退出续玩证据 | 独立审查 |
| V05-UI | 原文P06–P14；13行手册/20次无副作用/动态刷新、附魔及费用显示；720p/1080p/1440p10骰8栏10控制器5道具 | PlayMode、实际Player截图与交互检查 | 独立技术审查；风格由用户 |
| V05-PERF | 1080p目标60FPS，10骰8栏10控制器5道具连续操作≥180秒；沿用真实完整帧P95≤16.7ms，同时报告CPU/GPU/Present/GC/UI与缺采样 | 两平台独立原始帧数据和硬件配置；不能跨平台替代 | 独立审查 |
| V05-BUILD | 原文P01–P05/P15–P16：独立Profile、同sourceCommit/config/golden哈希、Windows解压启动、Mac原生arm64/Universal原生组件检查及真实续玩/确定性对比 | BuildReport、包清单、Mac实际报告 | 独立审查+用户Mac运行 |
| V05-GIT | 两包同次交付提交、LFS完整对象上传后干净检出下载哈希一致；无凭据/缓存/存档泄漏 | LFS fsck/远程校验、manifest、变更日志 | 独立审查 |
| V05-EXP | 40–75分钟、难度节奏、乐趣为设计目标；真人结果未提供不写通过 | 试玩工作表/反馈 | 人工 |

失败保留并修复后重验；不通过更改原预期、删用例或缩压力场景放行。Windows已知长事务分配与帧尾风险重新测量。技术实施者最多提交REVIEW，独立审查负责技术结论；体验/风格/正式发布由人工决定。

## 进度

- 原样规格归档；独立确认Word与MD正文一致，配置含58内容/24关，黄金100例。
- 规则、会话并行制作；UI/存档/资产/构建集成由root处理。
- 已完成554项EditMode、24项PlayMode、Windows新解压包实际Player、三分辨率和180秒压力检查；独立技术签署见 `evidence/v05/review/FINAL_TECHNICAL_REVIEW.md`。
- Windows与macOS候选包同源构建成功；Mac原生与跨平台存档待用户M5执行，真人体验待验。Git远程恢复见交付说明与manifest；v0.4通过不自动延用。
