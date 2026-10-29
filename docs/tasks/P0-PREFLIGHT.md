# P0-001 准备度与工具接通记录

此文件补全实施契约中的准备度记录，汇总已执行检查；不是事后捏造开工前全部通过。规则开工前范围、接口与验收已记录在 P0-IMPLEMENTATION.md，工具、资产及引擎风险在对应切片开始前逐步实测。

- 输入：原始 P0 v0.4 Markdown/Word，source_manifest.json；配置 p0.4.0/schema2。
- root负责集成和唯一Unity写操作；core_rules、session_engine、dice_art按目录分工，spec_audit独立只读生产复核。
- 可自主决定：既定范围内C#模块、序列化适配、工程导入、渲染API与缺陷修复。玩法、数值、体验、平台扩大、量产与发布仍由用户决定。

| 检查 | 依据与当前结论 |
|---|---|
| 范围与交付 | 六关、38内容、11牌型、固定商店、保存恢复、Windows原型及证据已冻结；无新增玩法 |
| 正常/边界/失败 | 原规格52黄金、70流程验收及专项概率合同；实际覆盖以NUnit结果为准 |
| 单一权威 | Data/P0Config.json与GoldenScores.json由规格提取，不重复维护数值；原文件哈希保留 |
| 接口和所有权 | 纯C#Core与Unity运行时适配分开；Session命令事务，ScoreCalculator纯函数，DiceTable只表现 |
| 工具接通 | Unity6000.6.7f1真实许可、URP导入、脚本编译、场景保存和Windows构建已执行；Blender5.2.3脚本、重开/导出/离线验证已执行 |
| 验证计划 | 纯规则→事务→独立复核→Unity EditMode/PlayMode→Windows正常run→三分辨率→180秒压力；不把早期工具目录当接通证据 |
| 资源样例 | 先完成单骰六面/两段翻滚/组合层及对应源件，再批量38符号；Player动画差异发现后进入REWORK，必须真实重建复跑 |
| 平台边界 | 本轮Windows x64，D3D11明确配置；移动端和真人5–8人至少两局不受本轮自动测试替代 |
| 证据与复核 | evidence/review独立审查及逐版本SHA绑定；失败报告保留；修改后重跑受影响检查 |

## 跨系统风险与运行检查

1. Roll：UI可操作状态→Session克隆→玻璃先损坏→面采样→保存候选→发布→动画；存盘失败重试相同RNG候选，Skip/Pause不能重投。
2. Submit：有序锁定→冻结永久/当前数据→纯计分→C07深复制→C15永久染红→胜败优先级→清理临时骰→保存；预览不写盘或触发Committed。
3. Shop：固定供给→动态价格/容量资格→明确目标→事务改造→保存→永久六面及概率同步；出售不补货，失败不扣费。
4. 恢复：schema/config/hash/结构检查→活动文件或上一有效backup→完整3RNG/remote override/receipt恢复；双损坏保留文件。
5. UI：单一输入入口、模态屏蔽、Unity销毁对象判空、绘制面与物理faceId对应；批处理输入路由作为测试环境明确设置并恢复。

## 已发生的工具/集成返工

- Unity新增asmdef/源文件在旧Editor导入缓存中不一致，通过真实重启重新导入；旧程序集结果标stale，不当当前证据。
- DX12交互Editor帧计数未前进，切换D3D11批处理Editor后实际PlayMode执行；Windows构建使用已验证D3D11，不声称DX12通过。
- JsonUtility对JSON null字符串读取的差异在黄金加载器严格适配，原黄金数据及全部18字段断言不变。
- Generic动画在Editor可无Animator采样，Player拒绝；补采样骨架Animator后需真实Player重跑。

当前结论：工具与实现路径可继续验证；整体ACCEPTED必须依赖最终运行、独立复核及人工体验责任边界，不能由此准备度文件代签。
