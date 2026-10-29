# P0 v0.4 验收映射独立复核

spec_audit，2026-10-29 UTC。权威条件为 `docs/tasks/P0-IMPLEMENTATION.md` 原冻结表与 `docs/specs/P0_v0.4.md`。范围截至 build05 / Player04；后续 UI 性能修复正在进行，本表不是整个项目的最终 ACCEPTED。

`audit_acceptance_mapping.py` 已真实执行：附录 C 全 70 ID（A01–12/B01–20/C01–15/D01–08/E01–15）映射到真实通过的 NUnit 用例；52 黄金原预期未改；38 内容 ID 对应 38 图标及可编辑 SVG 源。此映射只补完整性，行为结论同时依赖此前逐项源码审查、独立 .NET 故障/边界重跑、真实 Unity 测试与播放器。

| ID | 当前技术结论 | 实际证据与边界 |
|---|---|---|
| AC-CONFIG | 通过 | 原规格配置机械提取一致；11 牌型/38 内容；缺失、非法配置实际拒绝；EditMode05 |
| AC-SCORE | 通过 | 原 S01–S52 全 18 字段、B01–20；独立 52 黄金和 3072 解释算例；实际 Unity TRACE57；预览不改状态/RNG/IO |
| AC-PROB | 通过 | 5 指定向量、RNG 前序列、2916 纹理组合与最小单位检查；规则宿主及实际 EditMode05 |
| AC-STATE | 通过 | A/C/D 的真实 Session 参数化 114 项、独立冻结重试与恢复；实际 UI 输入/锁定/目标/损坏回归 |
| AC-CONTENT | 通过于本轮范围 | 20C/7I/4O/7U 命中/边界覆盖；真实 UI 遥控和 U01–07 取消/目标/重复属性；38 图标及源对应。后续池化需重跑监听与 UI 状态 |
| AC-ECON | 通过 | E01–15、独立种子商店 oracle；真实 ShopBoundary2；Player04 非 Debug 97 命令六关完成 |
| AC-SAVE | 通过 | 独立实际 Windows 文件备份损坏/直接 Save/恢复；真实 Unity JsonUtility 持久化 11 项；真实锁文件 Roll/Submit 多次失败后重试；冻结结果且一次效果 |
| AC-UNITY | 通过于 build05 | EditMode05 325、PlayMode08 18 均真实非零，XML 与 JSON 名称相符；当前池化改动需后续编译及 PlayMode |
| AC-VISUAL | 技术通过于 build05 | Player03 独立 18 图与 Player04 9 图，覆盖 720/1080/1440、10 骰/7 槽/10C/5I、永久/当前/有效分层、逐步公式；风格保留用户验收。新池化需画面回归 |
| AC-BUILD | 通过于 build05 | 0 error/0 warning；真实 WindowsPlayer，97 正常命令、分数/磁盘/渲染匹配，31 次实际落地；20 PDB 文档绑定源 |
| AC-PERF | **失败，REWORK** | Player04 180.003 秒，完整 CPU/GPU/UI/GC，扩容 0；帧 P95 23.5467 ms 超 16.7 ms。详见 PLAYER04_REVIEW.md；不降低标准 |
| AC-SOURCE | 源件部分通过，最终交接待完成 | 原规格/config/golden SHA、可编辑 Unity/Blender/SVG、Blender 重开验证与真实导入/构建；主 agent 报已提交 cb589f2。最终性能修复/证据提交与 Git 推送待主 agent 完成并验证 |
| AC-EXPERIENCE | **未运行** | 5–8 人各至少 2 局须真实参与者与用户判断；AI 不代签、不把自动回放作为玩家测试 |

规格符合性：已覆盖核心、状态、内容、经济、存档、Windows 功能；性能未满足。制作质量：已发现视觉和交互问题在指定旧构建修复且实测，但新池化仍需回归。实际运行：具备真正可运行与可复现证据，仍不能整体验收。失败历史保留，手机平台仅为本轮有理由不适用，没有移动通过结论。
