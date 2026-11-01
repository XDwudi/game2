# 0734源码检查点候选清单独立复核

2026-11-01 UTC，复核者 `/root/v07_spec_audit`，输入 `artifacts/cache/v07-build-preflight/checkpoint-0734/`。只读Git、生产与候选，未暂存、提交、改Assets或运行Editor。只写本报告、bindings和cache审计脚本。

**结论：这份1,679路径清单可作为源码检查点提交范围。** 另按root明确计划加入 `.gitattributes`、`.gitignore` 及清单生成后完成的独立复核文档。没有发现候选内应先移除的旧包、安装器、临时场景或缓存，也没有发现实际工程新增文件、可编辑美术源或必要文档遗漏。**这不是正式发布冻结、完整运行通过或整版ACCEPTED；AudioMixer硬缺口仍在。**

## 实际完成的检查

| 检查 | 结果与限制 |
| --- | --- |
| NUL/TXT清单与preflight分类一致 | 1,679唯一路径，逐项对应；总124,265,787字节（118.51MiB）。独立重读全部文件的大小/SHA，零漂移、零读中变化。 |
| 候选范围 | Unity工程788、ArtSource459、evidence391、tools29、docs11、README1；分组是暂存变化数量，不是全项目总量。 |
| 禁止混入项 | 无Releases ZIP、artifacts/cache、artifacts/tools、EXE/MSI/DLL/PDB/DMP、Library/Temp/Obj/bin/Logs/UserSettings/Screenshots、InitTestScene。没有超过50MiB的候选单文件。 |
| 七个历史发行ZIP | 全部重新流式算SHA/大小，与`observed-01`及本次preflight完全一致；总806,548,011字节，未入候选、未暂存。没有移动、重打包或删除旧包。 |
| 真实物理工程覆盖 | Assets/Packages/ProjectSettings当前1,359个非`~`物理文件，全部已tracked或在候选；仅既有生成型BuildIdentity例外。没有被Git ignore悄悄漏掉的其他实际build文件。 |
| Unity资源配套 | 候选中所有实际导入资源的meta均已tracked或入候选，无孤立meta。规则排除Unity不导入的`Validation~`目录；没有修改GUID或重新生成meta。 |
| 可编辑资源与复现工具 | `.kra/.ora/.blend/.fbx/.wav`源均在tracked或候选；候选含139KRA、137ORA、1BLEND、8FBX，52WAV为源/运行两套。ArtSource README、来源/许可说明、face map、导出脚本、生成/检查脚本和既有失败记录均保留。此项仅检查来源可交接，不代替真实Krita/Blender重新打开或美术验收。 |
| 必要文档 | 原三份v07输入、实施/准备度/录制/交付工具/M5契约、README、STATE、交付草稿及玩家说明均入候选或已tracked。交付稿明确未发布/待验，没有虚构v07链接。 |
| 非导入验证宿主 | `Validation~`8文件是3个csproj、2个C#宿主、3份有时间戳历史托管结果；没有bin/obj或编译产物。按现有仓库政策保留可复现宿主及失败/结果记录，不把它们当运行缓存或Unity通过。 |
| ignore与换行增量 | `.gitignore`仅增加`artifacts/tools/`；`.gitattributes`仅增加`tools/macos/v07/*.command text eol=lf`，未删除原条目或改变旧ZIP的LFS规则。`git check-attr`确认v07.command为text+LF；未改工作文件以伪造索引转换结果。 |
| 日志防误纳入 | 候选日志为导出/独立编译/单元测试/FFmpeg解码等证据；未纳入原始Editor崩溃启动日志。文本扫描未发现私钥、GitHub令牌、Unity启动令牌值模式；不是完整安全审计。 |
| Git只读 | 本次审计前后index SHA一致；未调用Git写操作。HEAD仍`1fbc3cce9d84237fa98b31d4b73b08e6f5e909ae`。 |

## 清单生成后的文档增量

独立检查时只有 `LOADING_UI_REWORK_REVIEW.md`、`LOADING_UI_REWORK_REVIEW.bindings.json` 已新落盘但未列入0734清单；root已明确会显式追加。root另说明Qualified新独立报告尚在写，完成后同样追加；不能用0734旧清单声称已包括这些后来文件。本报告与本bindings也是审计后新文件，root可明确追加。

这些是文档追溯增量，不是本次发现漏掉了运行源码。无需为等待每份新评审不断重复全部1,679文件hash；在实际暂存/提交时检查明确追加路径及暂存diff即可。若生产源另有变化，则重新核受影响源，不能套用此次零漂移结论。

## 检查点与正式验收的边界

- 实际发现列表为 **999个唯一EditMode、152个唯一PlayMode**，不是测试结果。本轮没有运行完整测试，也没有把新增专项拼成整套通过。
- AudioMixer资产仍不存在；四总线/内部组与正式路由、声音实听未完成。允许先保存此源码检查点并运行全量验证，不能将此提交称为满足全部v07内容的发行冻结。
- 现有BuildIdentity仍为历史p0.6、最终build-gate未形成。待正确源检查点/最终源确定后再生成对应身份与gate，不能手动更新hash掩盖遗漏。
- 全量Edit/Play、同源媒体、Player双构建、原生模式/恢复、性能、OS DPI、Mac M5与人工体验仍按原门槛执行。原崩溃和失败保留；本次清单检查不解释崩溃原因。
- 原始大量PNG/录像与运行目录仍在被忽略的cache，评审文档引用它们并不等于已把完整原始媒体放入Git。本次是源码检查点；最终交付证据包仍须按交付合同形成可追溯归档。

完整逐文件当前SHA、清单输入SHA、七ZIP、Git增量、例外和发现清单SHA见 `SOURCE_CHECKPOINT_0734_REVIEW.bindings.json`。复算脚本为 `artifacts/cache/v07-build-freeze-review/audit_checkpoint.py`。root负责实际暂存/提交、STATE和随后验证；此报告不代执行Git写操作。
