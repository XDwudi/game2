# AI 美术管线验证记录

日期：2026-11-08。输入BASE：`246646b0e4fb87595727b0fbf48559f1947230ba`。完整范围见 `docs/tasks/AI-ART-PIPELINE.md`；未提交产物以本目录文件快照绑定。

## 实际执行

| 操作 | 结果与边界 | 原始/对应证据 |
|---|---|---|
| 当前项目只读审计 | 实际看9张代表图，读取源/导出/组件记录；不算新Unity运行 | `docs/research/AI_ART_PROJECT_AUDIT.md` |
| 第一方工具研究 | 搜索并打开研究表中来源，未安装外部代码/模型 | `docs/research/AI_ART_TOOLS_RESEARCH.md` |
| `python .../skill-creator/scripts/quick_validate.py .agents/skills/game-art-production` | 首次失败：脚本默认GBK读取UTF-8中文导致 `UnicodeDecodeError: 'gbk' codec can't decode byte 0x8e in position 370`；无技能通过结论 | 本行保留真实失败；未修改上游验证器 |
| `python -X utf8 .../skill-creator/scripts/quick_validate.py .agents/skills/game-art-production` | 修正解释器编码后 exit0，`Skill is valid!`；只验证结构，非行为保证 | 独立review亦实跑；`skill-validation.txt` |
| `python -m unittest discover -s tests -v` | 首批33/33；包含13新增检查器与20既有测试，失败/越界/损坏/输出覆盖负例 | `checker/unittest-20261108.txt`；独立review重跑 |
| PNG checker：现有4图 + require-alpha + runtime-dir | 4/4完整性通过，报告含源码/输入SHA和环境；不签艺术通过 | `checker/illustrations-20261108-r02.json` |
| 内置 imagegen 单次试验 | 真实返回一张图、原字节归档；没有切到独立API | `ArtSource/AIConcepts/imagegen-probe-20261108/manifest.json`及prompt/原图 |
| PNG checker：概念试验 | 1/1完整性通过；透明边缘与实机仍待验 | `imagegen-probe-integrity.json` |
| 导航与差异 | root核7份入口文档共76本地链接，缺失0；生产路径diff为空。STATE保留原CRLF，默认diff空白检查把CR视作尾随空白，`git -c core.whitespace=cr-at-eol diff --check`实际通过 | 独立review另核入口及范围，未为格式检查改全文件 |

本次保留生产原图、历史失败、旧检查器与源副本；没有修改 `references/vendor` 或 `sources.lock.json`，来源副本重验不适用。没有运行 Unity/Blender、没有引擎构建或性能测试；纯工具和指导任务不伪造这些结果。

## 状态与限制

技能行为与全任务独立结论见 `review/`。框架文档不等于强制CI，PNG工具不解析Unity导入、不检验玩法ID语义、不核权利真实性、不审美评分。当前新增技能和工具可继续用于授权制作；美术标杆、剩余67项量产和原v071全版验收状态保持原决定。
