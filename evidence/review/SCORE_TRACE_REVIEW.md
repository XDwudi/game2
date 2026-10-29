# RV-UI-06 逐步计分解释复核

后续关闭记录（2026-10-29 UTC）：真实EditMode05的57条TRACE通过、PlayMode07 18/18通过且PDB源校验匹配；Player03实际720p顶部/中部/底部与1080p/1440p交叉截图已清楚显示逐骰及全局前值/操作/后值，滚动可达。结合下面独立.NET检查，RV-UI-06关闭于build04，详见 `PLAYER03_REVIEW.md`。性能与人工体验不包含在此关闭结论内。

spec_audit，2026-10-29 UTC。生产源码只读，审查工具仅在evidence/review；没有执行Unity。

输入：`Core/Rules/ScoreExplanation.cs`、调用它的`ScoreCalculator.cs`、`UI/GameApp.Modals.cs`。准确Core源码SHA在 `score-trace-independent-results.json`，不是以前Unity报告的版本。

**规格符合性：纯逻辑通过，最终UI未完成。** 共享DescribeDie按C10/C13/C17/C19新增贡献→玻璃→本面镀金生成前值/操作/后值；Calculator使用同一实现，避免规则条件复制。全局取冻结breakdown中的牌型、红组、触发控制器ID，decimal保留中间精度，最后一次Floor后加C20。没有UI重判型或修改Session/RNG。当前制作实现补齐原spec615解释缺口，须由实际长明细展示确认可达。

**制作质量：当前范围通过，性能影响待实测。** Build核对逐骰贡献及最终分数，异常不静默显示假结果；重复构造的trace不共享可写结果。UI内容高度依据每骰步骤和全局步骤计算，底部留足Triggered区域；静态未发现必然覆盖。ScoreCalculator现在每次Evaluate会为每骰创建List/闭包/ScoreStep/数组，属于新增分配，需在新版真实CPU/GC/帧预算中审视；无实测预算超标时不把它定为失败。

**实际运行：独立.NET通过；Unity和Player待新证据。** 命令：

`dotnet run --project evidence/review/ScoreTraceAudit/ScoreTraceAudit.csproj -c Release -- E:/agent_workspace/gpt/game2`

- 52原始黄金全部18字段未改变；新增trace逐步算术、前后连续、贡献/最终结果与权威breakdown一致。
- 3072个独立定值oracle：有效源pip1–6，永久六面和6/13/14/36，16种加点组合，玻璃/镀金4组合，C12开关；直接编码原规格的+4/+2/+3/+5及阈值，不调用实施函数计算期望。
- 包含有小数的中间结果、多全局控制器乘数、C20最终+6；输入序列化前后相同，修改一次返回值不污染下一次结果。
- 最初审查fixture错误地生成pip12/六面和72，被配置上限正确拒绝；修正为P0合法范围后通过。没有修改实现、配置、黄金或标准。失败输出保留在 `score-trace-independent.log`，最终 `score-trace-independent-complete.log`。

关闭RV-UI-06仍需：真实Unity受影响回归、720/1080/1440长明细顶部/中段/底部画面、历史冻结值与新一手变化隔离、滚动末尾C20及永久变更反馈可读。以前PlayMode06与Player02不能当作新trace代码已运行。
