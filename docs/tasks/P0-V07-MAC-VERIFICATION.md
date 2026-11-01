# P0-004 v0.7 — M5 实机验收与最终 Mac 封装

状态：工具与操作说明已准备，真实 v0.7 Mac 运行及人工验收待执行。本文不预填任何平台通过结果。具体包状态、Windows 原始报告和已知失败以同包 manifest / REVIEW_CANDIDATE.json 为准；v0.6 的历史失败不能当作 v0.7 的新测量或通过。

输入：`docs/specs/v07/ART_SPEC.md`、`P0-V07-ART.md` 全部 ART01–22、UI01–32、FX01–13，并保留 v0.6 原玩法与平台标准。发行 p0.7.0；美术 art0.7.0；规则 p0.6.0 / 存档 schema4；Unity 6000.6.7f1 / Mono。测试对象是用户的 M5，实际 OS、CPU、GPU、进程架构由脚本记录。Windows 交叉构建和离线工具测试不代表 Mac 通过。

主协调者提供同一冻结源的 Windows/Mac 构建、真实 Windows 报告和两份规则轨迹；用户在 M5 运行并做人工检查，独立审查者审阅原始证据。正常/调试轨迹可使用已核对未变的 p0.6.0 输入，仍必须由本版 Player 逐条重新执行。

正式工具默认完整原生显示及 180 秒/P95≤16.7ms 性能标准。若包明确包含受限候选说明，Windows 参考只按其已测范围比较，失败观察原样保留；Mac 本身仍须完整严格验收。脚本不会把受限状态改成整版 ACCEPTED。

## 收到的完整工具包

```text
DicePrototype-MacVerification/
  VerifyMac.command
  verify_mac.py
  package_checks.py
  compare_replays.py
  执行说明.md
  inputs/
    macos.zip
    BUILD_IDENTITY.json
    normal-run-trace.json
    debug-24-trace.json
    windows-results/
      player-report.json
      normal-replay.jsonl
      debug24-replay.jsonl
      cross-platform-checkpoint/
        active-run.json
        command.json
        expected.json
```

`inputs` 由真实产物填入；源码脚本不附预填通过结果。包内两个轨迹均须与 Windows Player 报告的 SHA-256 完全相同。正常轨迹允许实际失败；debug24 是独立的 24 关、23 商店、8 风险关流程测试，不是难度或胜率证明。

## 在 M5 上运行

需要 Python 3.9+；不要求 Unity、Blender、Rosetta 或第三方 Python 包。脚本不安装软件。打开终端，输入 `bash `（末尾保留空格），将 `VerifyMac.command` 拖到终端后回车。完整工具包无需额外参数；通过 bash 启动不依赖脚本自己的执行位。

保持电源、游戏窗口可见；自动阶段不要操作、最小化或运行另一份探针。脚本会生成桌面的全新结果目录，完整自动过程含真实显示模式切换/15秒超时、正常和 debug24 轨迹、分辨率截图及至少180秒压力采样，需数分钟。已有包、结果、用户普通存档及其他进程不覆盖。

系统若拒绝运行，保留提示和已生成证据。脚本不移除 quarantine、不关闭 Gatekeeper、不重签、不伪称公证；带隔离属性且系统评估不接受时停止。由 Mac 上正常系统授权或后续正式签名流程解决，随后重新验证。

指定输入的完整命令：

```bash
bash /绝对路径/VerifyMac.command \
  --package /绝对路径/macos.zip \
  --trace /绝对路径/normal-run-trace.json \
  --debug-trace /绝对路径/debug-24-trace.json \
  --windows-results /绝对路径/windows-player结果目录 \
  --identity /绝对路径/BUILD_IDENTITY.json \
  --output /绝对路径/尚不存在的新目录
```

`--timeout` 默认1800秒，只限制整个 Player 进程，不能缩短180秒采样或15秒真实确认倒计时。程序失败时保留所有部分结果，不反复更改阈值重试。

## 自动过程、证据与期望

| 验收项 | 实际方法与通过条件 | 原始证据 |
|---|---|---|
| P01–P04 身份与包 | 完整身份相等，真实40位源提交、配置/黄金64位哈希、schema4；归档路径/Unicode大小写冲突/符号链接越界检查 | input-archive、BUILD_IDENTITY、Player report |
| P02–P03 原生包结构 | 所有 Mach-O 解析实际 slice，均为 arm64+x86_64；主程序/Resources/Info.plist 完整 | prepared/fresh-app-inventory |
| P02 权限与封装 | 仅对新副本缺执行位的 MH_EXECUTE 补用户执行位；系统 ditto 封装后到全新目录解压；字节、mode、符号链接、slice 逐项相同 | ditto命令、逐文件清单、最终ZIP哈希 |
| P02 安全状态 | 记录 codesign display/verify、spctl、quarantine 原始结果；不据文件名或未签名假定通过 | security-before/final |
| P05–P20/P25–P30 流程 | `arch -arm64` 运行新解压包；报告非Editor、实际Arm64、Metal；正常和debug24每条命令的分数字符串、钱包、RNG、阶段、磁盘、UI及规则哈希与Windows一致 | player-process、sample、report、两份jsonl、截图 |
| P16 存档 | 实际加载Windows schema4检查点、继续提交、写盘重载、重复命令幂等、原件不变；导出Mac检查点 | imported-checkpoint、cross-platform-checkpoint |
| P21–P24 显示 | 当前设备支持的实际尺寸/三模式回读；15秒超时及Esc恢复；不支持尺寸明确记录，物理热插拔/焦点变化待人工 | displayChecks、截图 |
| P23 独立进程恢复 | 主探针通过后新建writer进程；出现durable ready receipt即外部终止该PID（15秒内），再启动不同PID的reader；实际恢复已确认模式尺寸并持久清pending | separate-process-display-recovery的ready/reader receipt、两进程日志和报告 |
| P16 / ART 性能 | 1080p、10骰、8栏、10控制器、5道具至少180秒，原LateUpdate帧间隔P95≤16.7ms；CPU/GPU/UI/GC原始报告保留 | performance-frames.csv、profiler-markers.tsv、report |

跨平台比较忽略展示层生成的 GUID，比较所有规则字段与规范化玩法状态。分数为规范十进制字符串，钱包为 Int64；禁止浮点转换大分数。debug轨迹本身固定身份，须逐条完全一致。实际Windows/Mac构建GUID不同是正常的，源码/内容/黄金集/美术版本/工具版本必须一致。当前 Player 报告 schema 为 p0-player-verification-7，probeRevision 为 p07 前缀；部分 v06-* 原始文件名为兼容保留，不能用文件名推定报告版本。

自动成功状态仍为 `automated_checks_passed_manual_checks_pending`，不是整版 ACCEPTED。Mac→Windows 的反方向读取要回传后在 Windows 真正执行。Intel 始终标记“含x86_64 slice，运行未验”。

## 用户人工检查（逐项记录，当前全部未执行）

自动完成后打开 `final-package-fresh-extract` 的 `.app`。记录实际日期/设备/操作/期望/结果及截图，不预填通过。自动探针用隔离存档；人工测试如已有普通挑战，先正常备份或使用独立 macOS 测试用户，不覆盖已有档。

| 条件 | 操作与期望 | 状态 |
|---|---|---|
| P04 / ART01 版本页 | 发行 p0.7.0、美术 art0.7.0、规则 p0.6.0/schema4、源码/配置/黄金哈希与输入身份一致，长哈希可读 | 待验 |
| P05–P12 正常流程 | 鼠标/触控板选骰、确认锁定、提交调序、不可普通解锁；达标继续或结算，收益一次 | 待验 |
| P08–P10/P13–P14 UI与手册 | 牌型页14行（13可强化+散点）、颜色页2条独立乘区完整，中文无方框；滚动不误触背景，分辨率变化无遮挡 | 待验 |
| P17–P18 控制器拖拽 | 商店/关卡预览拖动后保存顺序；原位、拖出或Esc取消不变；开始关卡后禁止；检查复制右邻冻结 | 待验 |
| P19–P20 商店刷新 | 只变控制器、正常3/5/7；服务免费首次0/5/7；余额不足不变化，重新读档不重抽 | 待验 |
| P25/P27 密码与隔离 | 设置精确输入 `XD is god`；错误大小写/空格不解锁；输入框聚焦Enter不提交当前手；返回普通挑战及重启状态不污染 | 待验 |
| P26–P30 Debug32 | 访问并应用32骰/32控制器/32道具/32提交容量；选择附魔、六面点数颜色/当前结果、结构与颜色等级；滚动可达、无截断；清理测试手后继续 | 待验 |
| P21–P24 三模式 | 实际全屏（Mac原生）、无边框、窗口，按当前显示器自动选择可见尺寸；确认15秒内生效，超时/Esc恢复 | 待验 |
| P21–P24 异常恢复 | 应用但未确认时退出进程后重新打开恢复上次确认值；实际切焦点、拔插外接显示器/换显示器保持可操作；记录硬件限制 | 待验 |
| P16 退出续玩 | 关内锁定后正常退出重开、商店退出重开，金币/骰面/顺序/附魔/报价/次数一致；debug重新进入保持独立 | 待验 |
| S10–S15 商店与详情 | 六张控制器货架固定；下方五页切换可读；所有已拥有控制器与附魔可达；骰面详情 F02 中/F01 上/F06 下/F05 底/F04 左/F03 右，永久/当前/有效分层清楚，概率展开不改骰面 | 待验 |
| S17 辅助与声音 | 三页设置、四路音量、三档速度、减少动态、色盲辅助、100/125/150% 文字可操作；720p/150% 关键按钮不裁切，关闭与主要动作保持可达 | 待验 |
| S19/S22 引导与恢复 | 重新查看真实首局引导、独立固定骰练习入口可区分；保存失败说明、重试/放弃事务不会重复扣款、重抽或污染普通存档 | 待验 |
| ART/FX 表现 | 普通手和 G147 等固定收据按原规格观察计分顺序、连击、复制 owner、音效与动画；正常/快速/即时及减少动态最终结果一致；按需要回传固定60fps录像 | 待验 |
| 整体体验 | 动画、计分可读性、长数/高容量、触控板操作、商店价格与成长体验 | 待验 |

## 回传与协调者后续

回传整个 `MacVerification-Evidence.zip`、`evidence-package-sha256.json`、Mac生成的 `DicePrototype-p0.7.0-macos-universal.zip`，以及上表人工结果。失败也回传；日志可能含本机路径，公开前只脱敏身份路径，不能改测试值。

协调者用同源 Windows Player 对回传 `cross-platform-checkpoint` 执行 `--verify-import-save <该目录>`，实际加载、继续和重复命令检查，完成反方向。独立审查者逐张看截图和帧数据，不只看退出码。若Mac签名/公证/附票/重压改变包字节，重新绑定SHA-256、大小、新解压运行证据。

交付渠道是 GitHub Release 的三个完整 ZIP 附件（Windows、Mac、M5工具包），不是 Git LFS。计划候选 tag 为 `p0.7.0-rc.1`（实际是否发布以发行页面与 manifest 为准），源目标来自真实 BuildIdentity + Git。已发布附件不删除或暗中覆盖；后续Mac最终包需显式新候选/发行版本记录。所有附件须全量远程下载验证SHA/大小/CRC，Windows新解压目录实际运行。发布候选不等于完成Mac/真人验收。

## 工具自验与限制

```powershell
python -m unittest discover -s tools/macos/v07 -p 'test_*.py' -v
python -m unittest discover -s tests -v
python tools/macos/v07/compare_replays.py --windows-results <真实Windows目录> --mac-results <真实Mac目录> --output <新比较报告.json>
```

合成测试仅验证解析、拒绝、比较、凭据重定向与版本绑定逻辑，不产生Mac通过证据。脚本不上传GitHub、不代签人工结论。主探针中的独立进程恢复项仍保留not_executed原值，脚本另外执行writer/reader并绑定其实际receipt，不改原报告冒充通过。物理显示拔插和焦点变化仍需人工；不能用组件重建冒充系统重启。
