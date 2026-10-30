# P0 v0.5 阶段交付独立复核

审查者 `/root/v05_spec_audit`，2026-10-30。只读生产、原包和原失败现场；写入仅限review证据。承接 `FINAL_TECHNICAL_REVIEW.md`：游戏源版本 `967f309a6be402612514714f46f3743421267799`，本地原候选交付 `940b8fad10a29f3a3845ad3e33f954d9d90a89a2`。本文不把源码推送、本地LFS完整或本地启动等同于远程双包恢复。

**结论：Windows指定设备上的完整Player02功能/视觉/性能通过；本地新解压包的跨进程checkpoint恢复和安全退出通过；后续延长回放发生一次受控文件替换故障，其原现场完整且隔离副本重试恢复通过。源码远程克隆已验证。双包远程交付被GitHub LFS额度阻塞，Mac原生验收未执行；整版本仍未ACCEPTED。**

## 新进程恢复的实际范围

`evidence/v05/delivery/windows-short-restore-process.json`记录实际PID29584、EXE绝对路径 `Builds/v05/new-process-restore-01/DicePrototype.exe`、启动参数、起止时间、CloseMainWindow与进程退出码0。上一完整Player02进程已退出。本次从**本地**Windows候选ZIP重新解压；独立读取解压目录308个成员，逐文件SHA全部与ZIP清单一致，见 `delivery-independent-binding02.json`。

实际结果目录 `evidence/v05/delivery/windows-short-restore/player-20261030-060026-8f47a7a4`。新进程Runtime MVID `653f6fb3-eab0-41a5-b900-7d8fa8ff4c8d`、buildGuid `182571f77341471d9fd9acd120b05e26`均匹配已验收包；真实读取上一进程第三关锁定后的schema3 checkpoint，继续Submit得 **324分**，输入/输出规范状态哈希与旧进程预期完全相同，结果写盘重读、重复命令幂等检查通过，原checkpoint文件SHA不变。

审查者查看新进程实际主菜单截图，正常渲染、中文可读。root在目标检查完成后正常关闭窗口，退出0。Probe报告保持 `running`、正常回放0步，这是有意的短检查范围，**不是新的完整回放或压力通过**；完整性能结论仍来自Player02。该探针验证跨进程稳定态恢复，不声称进行了真人点击菜单“继续”的体验测试，也不把Windows→Windows当作跨平台。

## 另一次延长回放的真实I/O失败

前一启动PID28816的记录在 `evidence/v05/delivery/windows-new-process/player-20261030-055802-1685d517`。外部checkpoint导入已经通过；随后继续普通回放，在index121的Roll遇到“存档写入失败：无法删除要被替换的文件”。Probe失败退出，完整回放未通过；OS退出码当时未捕获，报告declared exit不能替代OS证据。

独立读取原现场，三份envelope内容摘要均正确：

| 文件 | 序号 | 骰子RNG | 剩余投掷 | 作用 |
|---|---:|---:|---:|---|
| active-run.json | 121 | 2945252426 | 1 | 上次成功锁定后的已提交状态 |
| active-run.json.backup | 120 | 2945252426 | 1 | 上一有效版本 |
| active-run.json.tmp | 122 | 2474475134 | 0 | 本次Roll的完整候选，未覆盖active |

为检查恢复正确性，审查者只把原active/backup复制到review隔离目录，取原tmp中的真实下一条Roll命令，持有backup读取句柄并禁止删除。在实际Windows文件IO中复现**逐字相同**的错误。GameSession返回pendingSave，内存仍为121，active/backup原字节未变；释放句柄后调用RetrySave，**整个RunState与原生失败现场tmp候选完全一致**到122，重复命令不重抽，磁盘重读一致。原三个现场文件前后字节完全不变。

证据为 `native-io-retry-review.json`、`independent-managed-run08.json/.log/-binding.json`，最终独立托管回归 **539/539**，34份输入前后SHA稳定。此重放使用System.Text.Json辅助适配与真实WindowsIO，不冒充再次Unity测试。

可以确认原子保护、失败暂停和重试没有丢失已提交状态或重复消费RNG，未发现需要修改生产规则的缺陷；原生现场的具体占用者仍未知，不能据此归咎杀毒软件或证明以后永不遇到I/O失败。失败报告保留为实际运行限制，不被先前Player02通过掩盖。人工遇到存档失败应使用现有重试流程。

## 远程源码与包状态

独立实际检查 `artifacts/cache/v05-remote-source-verify`：origin为用户指定GitHub地址，HEAD为967f309，工作树干净，`git fsck --full --no-reflogs`退出0。547份tracked Assets/Packages/ProjectSettings源文件均与已验证源码一致；其中417份原始字节差异仅为Git `core.autocrlf=true`带来的CRLF/LF换行。首次严格源字节比较保留在 `delivery-independent-binding.json`，逐项归因及修正记录在 `delivery-independent-binding02.json`；二进制、ZIP与PDB仍按完整原始字节SHA验证，未放宽包完整性标准。

root捕获的 `evidence/v05/delivery/lfs-upload-blocked.json`记录实际 `git push origin main` exit1，GitHub返回：`This repository exceeded its LFS budget`。需要3个LFS对象，上传0；没有绕过pre-push把缺失对象指针当作交付成功。该错误是实际仓库额度外部阻碍，不是自动审批拒绝，也不是本地ZIP损坏。`local-lfs-integrity.json`记录原候选940b8fa本地 `git lfs fsck OK`，3个对象SHA与本审查已经独立核对的Windows包/Mac包/工具包一致；**本地fsck不是远程LFS检查**。

root保留原候选到本地 `codex/v05-lfs-candidate`，从main的待发布提交移除三ZIP跟踪但保留本地字节，准备正常推送源码、文档与证据。后续文档提交SHA以实际Git输出为准，不把本审查写作时间之后的推送预填通过。额度恢复或用户选择替代交付方式后，应同步上传完整双包、更新manifest并重新执行P15；当前P15状态为 **BLOCKED / NOT_COMPLETED**。

## 剩余与交接

Mac Universal候选303成员、6个双架构原生组件和转交工具已静态核验；M5原生arm64/Metal、ditto重包、鼠标/触控板、稳定态退出续玩、180秒性能、Windows↔Mac双向checkpoint与最终Mac包远程恢复仍未验。Intel只检查slice，移动端未验。40–75分钟目标、难度/经济与5–8名参与者的正常Run体验仍需真人。没有把工程通过写成风格、乐趣或正式发行批准。

本阶段独立复核结束，review文件写锁释放；审查者未持有生产/Unity/包写锁，未验范围为Mac原生与双向续玩、双包远程交付恢复及真人体验。
