# P0-003 — v0.6 实施契约

状态：诊断测试附件交付完成，整版REVIEW且V06-DISPLAY/PERF保留REWORK；Mac与人工待验，非ACCEPTED。当前证据与下载见 `../DELIVERY_V06.md` 和 `../../evidence/v06/review/REVIEW_CONCLUSION.md`。日期：2026-10-31。协调/集成：root；规则：v06_rules；会话：v06_session；独立规格与技术复核：v06_spec_review；体验与 Mac 实机验收：用户。

## 输入与授权

- 用户本轮明确要求按框架开发 v0.6；权威输入为原样归档的 `docs/specs/P0_v0.6.md`（SHA256 `36118847044185925057ea33b342756c97498f044bb558a684c82902084245c8`）及同名 Word（`974cd0d33b5cd47f99caf8ae77706bd93b7ae8514e837013f18c16ec8f122274`）。完整正文、附录 A–E 共同构成要求，不能只实现聊天摘要。
- BASE：`2db287a`，旧游戏冻结源 `967f309a6be402612514714f46f3743421267799`。已有 STATE 阅读记录及三个 v0.5 ZIP 保留。
- 用户本轮再次授权 GitHub 发行附件：Windows、macOS Universal、M5 验收工具包共同上传并提供下载链接；用户在 M5 人工运行测试。不沿用已失败的 LFS 路线，也不代签 Mac 实机通过。

## 范围与交付

完整 v0.6（50 控制器、71 内容、13 牌型/2颜色、schema4、24关）；所有附录定义的效果、状态归属、顺序、价格、商店服务与刷新、拖拽、显示设置回退、独立调试工作区。保留可编辑资产源、测试、实际运行证据和双平台包。旧存档隔离保留，不猜测迁移。非目标：移动端、联网、正式美术量产、未经确认的玩法重设计、代替真人体验判断。

## 文件所有权和接口

| 所有者 | 可写范围 | 禁止项 |
|---|---|---|
| v06_rules | Core/Rules、Data/P0Config.json、GoldenScores.json、Tests/EditMode/Rules、v06规则工具/证据 | 会话、UI、编辑器、全局设置 |
| v06_session | Core/Session、Tests/EditMode/Session、v06会话工具/证据 | Rules、Data、Runtime/UI、编辑器 |
| root | Runtime、UI、Editor、PlayMode/Persistence测试、资产、构建、交付、状态文档 | 不并写子模块 |
| v06_spec_review | evidence/v06/review、规格审计文档 | 生产代码/验收标准只读 |

规则 Evaluate 保留；Score/target/baseTotal 等为规范十进制字符串，经 BigInteger 计算，钱包 checked Int64。AbilitySource 区分 emitter 与 stateOwner；C41 StageStart 绑定实例。会话拥有全部规则事务、RNG和候选存档，UI只发命令与展示；ReorderControllers/RefreshControllers/ApplyDebugScene 接口由会话实现。DebugScene 为独立 DTO，CreateDebugWorkspace 克隆独立 runId，Runtime 使用独立 AtomicSaveStore。配置数字键通过窄范围 PrepareJsonForUnity 转换，原始 JSON 不改。

## 验收与责任

以下各项须绑定输入、源快照、工具版本与真实产物，保留失败。测试者实施自检，v06_spec_review 独立复核；人工项由用户。

| ID | 场景与方法 | 期望/阈值 | 证据位置与责任 |
|---|---|---|---|
| V06-INPUT | Word/MD 正文与表、附录配置/黄金 | 一致；50/7/7/7、13+2、160例完整 | review/input-structure；独立 |
| V06-RULE | 附录B逐字段与附录C效果/附录D边界，托管+Unity EditMode | 160原例全字段符合；共享状态/复制/大整数/零分/纯预览正确 | rules、unity；独立复核 |
| V06-TX | 顺序、退款、刷新、扩容、存档失败重试与读档 | 无重复扣款/重抽；schema4验证；正常限制与debug隔离 | session、persistence；独立 |
| V06-UI | 规格P01–P20/P25–P30真实运行与PlayMode | 所有规定入口和反馈可用；拖拽取消不保存；debug32可访问；准确公式 | unity、player、截图；独立 |
| V06-DISPLAY | P21–P24 三模式/分辨率/15秒确认、超时/Esc/重启恢复/输入 | 设置独立、帧后回读；当前可用设备实测，其余明确未验 | player/display；root+独立；Mac用户 |
| V06-ASSET | 71活动图标/5附魔、源导出及实机辨识 | 映射完整，沿用已确认原型风格；骰子3D源可用 | art、player；独立技术/人工观感 |
| V06-RUN | 真实正常Run至胜/明确失败；另debug24/23商店流 | 每步状态/显示/磁盘/RNG一致；debug不冒充平衡 | player轨迹；独立 |
| V06-PERF | 1080p/10骰8栏10控制器5道具≥180秒 | 既有LateUpdate帧间隔P95≤16.7ms；采样点不改，完整报告CPU/GPU/UI/GC | player原始CSV/报告；独立 |
| V06-PLATFORM | Windows x64、Mac Universal构建/原生库检查；M5实机 | 同config/schema/源码；M5运行、Metal、存档交换由用户执行 | builds、mac-kit；人工Mac |
| V06-DELIVERY | 三ZIP GitHub附件上传、全量远程下载恢复 | 大小/SHA/CRC相符，Windows新路径启动 | delivery；独立 |

规格第39章 P01–P30 是上述验收的逐条子条件，完整保留，不以上表缩减。最终 Windows 技术结论、Mac待验与体验结论分别记录；未经人工结果不置整版 ACCEPTED。

## 执行

先验证精确计分/序列化、共享状态事务及显示回退/拖拽代表性交互，再扩展全部UI和资源。Unity写操作仅root串行；子agent托管自检不打开同一工程。集成后Unity真实全套测试、Player完整流程与压力、独立复核、修复重验、构建发布。普通实现授权已具备。
