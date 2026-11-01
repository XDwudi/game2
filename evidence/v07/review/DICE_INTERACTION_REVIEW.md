# 骰子悬停、锁入与保存重试限域复核

审查者 `/root/v07_shop_ui`，本轮骰子交互生产实施者为art/root；输入art0.7.0、p0.6.0/schema4。审查者未编写本轮生产实现，但给Normal新测试提供过BothClocks采样更正，不能宣称该采样修改由自己独立验收；真实执行、追加字形几何断言由root完成。这里只读源、原始PNG、NUnit和绑定，不运行Unity，不改门槛。

**结论：列出的悬停/抬升、锁入事务与C53预览时点、Fast/Reduced/Instant/Skip/失焦终态、真实保存失败Retry/Abandon行为通过；最后发现的动画标签压过路骰面缺陷已由071343的1080和072315的720修复图闭环。没有据此签整版或持续性能通过。**

## 真实证据与范围

| 归档 | NUnit实际 | 已实看相关图 |
| --- | --- | --- |
| `v07-dice-1080-064842` | 3/3 | 全19PNG：100/125/150% hover、rise60、rise120共9；Normal lock10/229/230共3；首掷ready/dots091共2；Fast/Focus/Instant/Reduced/Skip终态共5 |
| `v07-settle-dice-1080-065952` | 9/9批次 | Dice真实文件锁、Retry229、Retry230、Abandon后再次Confirm共4 |
| `v07-combined-720-070131` | 14/14批次 | 同上720共4 |
| `v07-dice-cold-720-070724` | 2/2 | BothClocks更正Normal10/229/230共3 |
| `v07-dice-cold-1080-070806` | 2/2 | 同上1080共3 |
| `v07-follow-cold-1080-071343` | 2/2 | 标签跟随实际骰后Normal10/229/230共3 |
| `v07-dice-follower-720-072315` | 1/1 | 新标签跟随实际骰的720 Normal10/229/230共3 |

逐PNG SHA见 `artifacts/cache/v07-settlement-backdrop-review/reviewed-images.json`，整批归档/源/程序集/NUnit核对见同目录 `actual-evidence-audit.json`。全部列出归档文件SHA/bytes正确、每批起止物理源清单与加载MVID/DLL SHA不变。不是最终提交构建或Portable PDB校验声明。

## 观察与断言对应

- **悬停和待锁**：三字号9图可见真实骰轮廓和120ms小幅抬升，空心勾位于骰旁，没有压点阵。读取实际测试保留轮廓PropertyBlock、120ms上升6设计像素、80ms落回、真实geometry与存档不变检查；图片不是用UI假骰代替3D。
- **Normal锁入**：同一真实Confirm只提交一次，顺序按点击而非排序；新锁骰25ms错开、单骰180ms。229ms仍显示旧C53范围4–1,152，230ms完整锁入后显示31–8,064且仍标“查看9种结果”。没有提前揭开共享q或为了截图抽取实际随机结果。源及通过测试检查state/save/sequence/冻结旧snapshot，而不只核UI数字。
- **证据采样失败保留**：064842 Normal测试禁用DiceTable自动钟后只推进lock钟，漏池reflow，导致静止尾图蓝5与标签错位。这是采样前提缺陷；不能用其NUnit通过签尾图。cache补丁`cc9a17cbcced22008fdb50728a57597a90ab91c3dceff77c2033f253c2d6c004`仅改原5个Normal时点为BothClocks并加230ms全部table结束断言，原C53/顺序/保存阈值未变；070724/070806重新实际执行后尾图位置正确。
- **真实生产短暂遮挡**：修正采样后的010ms仍暴露最终位置静态◇蓝压正在移出的蓝4骰面；这不是旧漏钟的继续推断，而是实际图中的独立问题。root加renderer侧标签跟随实际投影，071343及072315的010ms标签跟随最右蓝5，229/230仍对齐；两个分辨率均已实看。原旧图保留，新增真实glyph与全部可见骰faceRect无交叠断言仍保留。072315另处于集成loader后的批次，不混写为071343同一MVID；具体源绑定分别保留。
- **首掷按钮**：真实ready标“掷骰2[R]”；091ms点阵只在按钮内，不遮池骰。有限点阵没有循环的判定来自测试时间序列，静态091ms图本身不证明持续动画节奏。
- **Fast/Reduced/Instant/Skip/Focus**：五终态原图中3/8锁位、7颗剩余池骰、预览10,920及标签/材质一致，未见重复骰/空栏骰残留。对应测试保留Fast时长、Reduced/Instant落终态以及Skip/失焦无额外事务断言；终态PNG不被描述为完整实时连续录像。
- **真实文件锁Retry**：两尺寸4图均实看。失败窗口明确“本次结果尚未保存”，背景旧4–1,152、只锁1颗、待确认3；失败和反复Retry未入账，原候选/RNG/日志/文件字节保持。解除文件锁后仅一个候选提交，229ms仍旧范围、230ms才31–8,064，重复旧监听不再提交。第二次真实失败Abandon后确认剩余骰，最终5/5与40–10,304，未复用旧candidate/snapshot。测试用原AtomicSaveStore真文件句柄，不反射篡改核心状态或伪造异常。

这些样本为Unity真实渲染、受控表现时钟的功能/视觉证据，**不是60Hz持续性能、Windows独占全屏或Mac实机通过**。默认完整回归仍由root后续执行；本报告不修改任何原验收阈值。
