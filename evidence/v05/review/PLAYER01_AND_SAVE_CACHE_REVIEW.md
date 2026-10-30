# Windows Player01、双平台候选与存档优化独立复核

审查者 `/root/v05_spec_audit`，2026-10-30。生产代码、Unity、Player 和包均只读；审查者运行独立 .NET/Python 检查，并查看实际 Player 截图。**Player01 功能检查通过，性能不通过；不能作为最终交付放行。Mac 原生执行未完成。**

## Player01 证据与结论

实际运行目录 `evidence/v05/player/player-20261030-052509-0a70fdd0`，Windows x64、Unity 6000.6.7f1、1920×1080、Ryzen 9 5900X / RTX 3080、约64GB内存；非Editor、Development Build。源提交 `7121336e6d5e9b36f22401f09d188ff7b6bcae0d`，buildGuid `edf4152e4d0f4daa9e0c0d53a894934f`，Runtime MVID `61e4bcd3-a1f9-4505-9eb6-870b76b37151`。

`audit_player_independent.py` 独立读取 JSONL、完整 CSV、存档和报告；`windows-player01-independent-audit.json` 绑定原文件SHA并保留结果：

- normal种子20261037，161条合法命令的11字段逐条符合独立预先生成轨迹，状态保存/渲染匹配，最终第9关RunFailed。正常模式没有完成24关，不应作为通关率或平衡通过。
- 单初态Debug24为178条连续命令，24次StartStage、23次LeaveShop，风险关2/5/8/11/14/17/20/23，第24关RunWon。它验证流程覆盖，明确不是正常经济或难度证据。
- 全部留存 active/backup 的实际payload SHA匹配；压力前后normal存档原字节SHA相同。第三关checkpoint的本地提交、写盘重载、重复命令检查已记录；外平台导入状态not_requested，不能代替Windows↔Mac续玩。
- 53条动画检查、20次手册纯读取检查通过；Unity错误、计时警告、拒收黑图均0。
- 原始CSV10723行，剔除采样开始时唯一首partial后为10722完整帧，无Unity帧号缺口、时钟单调，全部interval等于原elapsed差值。所有报告metric与三条独立timestamp差值的分位数、均值、样本数均独立重算一致。
- 180.0112231秒、LateUpdate间隔P95 **16.770000000008167ms > 16.7ms**，最大57.0961ms。MainThread P95 17.0629ms，GPU P95 0.24064ms，UI BuildBatch P95 0.0079ms，GC 298次。报告budget_exceeded正确，未丢弃慢帧、未更换验收计时点、未放宽阈值。

## 实际画面

逐图查看K8/10控制器压力场景的720p、1080p、1440p；底部4普通槽+6免槽卡片、两行共10控制器、5道具、8有序提交栏和2未锁骰均完整可读，基础+5与×1.2附魔标记可见，无缺字方框。

另查看1080p普通手册，以及720p手册底部、达标继续/结束、商店、分数明细末端、骰子六面详情。可以读到手册13种始终列出、当前K/骰数、升级次数/倍率、C03导致全蓝不可达的原因、红组0–8曲线；达标状态仍保留投掷及结束本轮；精确公式显示两次1.2与最终仅一次向下取整。永久/当前/有效点数分层与六面概率可读。

发现 **UI-R01（轻微）**：720p与1080p右下开发版水印遮住重复手册按钮末尾，顶部手册入口仍完整。root已将底部手册移至x1340、Toast宽1260；新布局需重构建截图复验，不能用旧图当修复证据。具体已查看文件及SHA见 `player01-visual-inspection.json`。没有声称逐张查看所有PNG或进行了真人试玩。

## 构建和包

`windows-player01-pdb.json` 与 `macos-candidate01-pdb.json` 各自核对2个实际Player程序集、25份项目源码，DLL/PDB GUID一致、源码checksum0不匹配。它们绑定本节旧候选，不自动覆盖后续优化。

`candidate01-package-independent.json` 直接读取两个ZIP全部字节、CRC、候选清单内每文件SHA/大小/模式和嵌入身份。Windows308个成员、Mac303个成员；各有3个元数据/许可/说明文件未列入旧candidate.files，内容实际存在且整体ZIP摘要正确。root已修改打包脚本重读全部ZIP成员生成清单，并对symlink以ZIP内target字节计算SHA；后续新包需重验关闭此记录。

独立解析Mac包内6个fat Mach-O头及每slice头/边界，全部包含x86_64与arm64，主程序为MH_EXECUTE。未执行任何Mac应用，也未证明Gatekeeper/签名/Metal或M5性能通过。

Mac BuildReport的73条warning保留；原日志筛选108行/78种，含重复与unsupported提示，不能将筛选行数当作BuildReport计数。大部分为Unity/URP/TMP内置shader编译告警。实际PC_Renderer启用了SSAO，骰子使用URP/Lit，因此不能笼统称这些告警全部无关或无影响；Metal实际画面和原生运行是剩余平台验收。

## 存档性能修复复核

root针对Player01失败调整AtomicSaveStore：候选snapshot先完整验证；最近成功写入的原始字节作私有缓存；下一次仅在现有active逐字节相同时跳过旧文件解析，首次/外部变化仍走原始读取校验。缓存仅在Replace/Move成功后更新。未改变原子文件替换、损坏active隔离、有效backup保护和存档规则。

只读代码审查未见新正确性缺陷。独立新增真实Windows文件IO边界（System.Text.Json辅助适配，非Unity）：同长度同mtime的外部hash损坏不信任缓存；非法冻结历史不改active/不产生tmp或backup；锁住active使Replace失败后旧active保留且重试成功。连原回归 **run07 538/538**，34份输入前后SHA稳定。`independent-managed-run07.json/.log/-binding.json` 保留证据。

实际Unity **EditMode03 554/554、PlayMode04 24/24** 已逐叶子检查，无删除原用例、无跳过；新增2条真实IO回归覆盖非法候选与外部有效替换。`unity-edit03-play04-report-audit.json` 绑定XML和CLI报告；`unity-prebuild-pdb03-save-cache.json` 5程序集47源文档全部匹配。门禁阈值由552增加到554，12个独立门禁负例检查重新通过，证据 `delivery-gate-independent-save-cache-tests.json/.log`。

代码差异确认仅存档缓存/候选验证、底部UI布局及对应测试/门禁计数；probe压力日程、CSV采样点、16.7ms门槛、生产帧率器没有改动。性能修复效果需新双平台包与完整Player复跑后判断；本次没有签署整版本ACCEPTED。
