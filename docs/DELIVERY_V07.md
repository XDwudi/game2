# 骰子构筑 P0 v0.7 · 美术与界面试玩说明

这一版围绕“看清骰子状态、理解得分来源、辨认构筑效果”更新了画面与操作反馈。玩法沿用 v0.6，初始金币、牌型、控制器及计分规则以现有配置为准。

**当前交付状态：源码 `dca7a26` 的诊断测试候选已发布到 [GitHub p0.7.0-rc.1](https://github.com/XDwudi/game2/releases/tag/p0.7.0-rc.1)。三个附件已完整下载核对SHA-256与ZIP完整性，Windows远端包已在全新目录完成隔离启动诊断。** Windows玩法与界面回放已完成；性能和独占全屏仍未通过，Mac与人工试玩待验。这份说明不代表整版验收通过。

## 这一版能看到什么

- **统一的桌面与卡片。** 深青桌布、奶油纸卡、红蓝骰点和像素印刷插画形成一致风格；菜单、新局、商店、详情、设置与调试界面使用同一套视觉语言。
- **更清楚的3D骰子状态。** 陶瓷、玻璃、黄金边与骰面纹理有不同表现。待选骰有虚线和空心勾，已锁骰有锁定标记与可移动抓手；序号、颜色和状态文字与骰点分开。骰子详情分开显示永久六面、当前投出结果和有效值。
- **更容易跟上的计分过程。** 分项预览、来源明细、控制器命中提示与逐步计分相互对应；高倍率、达标印章、收入纸签和关卡结算各有明确反馈。大分数保留详情入口，改点、染色等永久变化显示在对应骰子上。
- **更直观的商店和构筑信息。** 上方固定控制器货架，下方分成道具、研习、骰强、服务、骰柜五页；价格、稀有度、附魔与容量各有位置。购买、刷新、扩容及骰子强化有对应演出，确认面板说明本次对象与变化。
- **适应不同阅读与演出偏好。** 设置提供100%／125%／150%文字、颜色辅助、正常／快速／即时速度及减少动态；分辨率与全屏、无边框、窗口模式可在画面设置中选择。组合试验采用独立工作区，满配内容可通过表格和分页查看。

## 下载与试玩入口

三个附件均可从 [同一个 GitHub Release](https://github.com/XDwudi/game2/releases/tag/p0.7.0-rc.1) 下载：

| 附件 | 用途 | 最终文件名、大小、SHA-256与下载链接 |
|---|---|---|
| Windows x64 | 在Windows试玩 | `DicePrototype-p0.7.0-win-x64.zip`；87218149 bytes（83.18 MiB）；SHA-256 `c823056517851a786cfe8a185c43ad6829e3bdf2f5d8686eb9b17ff6a39169b1`；[下载](https://github.com/XDwudi/game2/releases/download/p0.7.0-rc.1/DicePrototype-p0.7.0-win-x64.zip) |
| macOS Universal | 在Mac试玩，包含Apple Silicon与Intel架构，实机运行待验 | `DicePrototype-p0.7.0-macos-universal.zip`；127598578 bytes（121.69 MiB）；SHA-256 `437985740644806f220da8217aaf187a19c816421a20dd917e5578f2190641b6`；[下载](https://github.com/XDwudi/game2/releases/download/p0.7.0-rc.1/DicePrototype-p0.7.0-macos-universal.zip) |
| M5验收工具包 | 内含同版Mac包、检查脚本和Windows参考结果，用于运行并收集M5结果 | `DicePrototype-p0.7.0-MacVerificationKit.zip`；127707336 bytes（121.79 MiB）；SHA-256 `e3807850f72fa0de3c41f1bed1364254b530356e0086ae78f926d70c8e9e23c6`；[下载](https://github.com/XDwudi/game2/releases/download/p0.7.0-rc.1/DicePrototype-p0.7.0-MacVerificationKit.zip) |

Windows完整解压后运行 `DicePrototype.exe`，保留同目录的Data与DLL。Mac保留完整 `DicePrototype.app`；要在M5收集测试结果，解压工具包，按《执行说明.md》通过终端启动 `VerifyMac.command`，无需另下Mac包。详细操作见随包的《开始游玩.txt》（[说明源文件](PLAYER_README_V07.txt)）。

开始新挑战后，先投掷，点选待锁骰，确认锁定，再提交全部已锁骰。默认快捷键为 **R投掷、L确认锁定、Enter提交、Backspace清除待选、H牌型手册**；锁定后默认不能普通解锁。右键骰子可查看详情，已锁骰可拖动调序。达标且仍有提交次数时，可继续本关或结束本轮。

需要试组合时，在设置的辅助页输入 **`XD is god`**，注意大小写且不加首尾空格，再进入独立组合试验。可配置控制器和附魔、道具、骰子六面及牌型等级；应用后运行测试手。调试结果与普通挑战分开记录。

## 当前仍需完成

- **Windows性能：** 本次1080p压力采样180.005549秒、10115帧，帧间隔P95为16.7929ms，高于16.7ms门槛，仍为失败。GPU有效采样覆盖率95.6401%，已达到95%覆盖要求；覆盖达标不等于帧率达标。
- **Windows显示：** 本机严格测试和独立显示测试的独占全屏请求均未达到可确认状态，并回退到窗口模式；三模式完整验收未通过，具体原因尚未确定。未确认设置的跨进程恢复已单独通过，不能替代完整显示验收。
- **资源诊断的边界：** 同源成品已完成补充采集：稳定10骰桌面闲置20.002秒、1200有效帧，其中1199帧零GC，1帧分配132B；10个普通骰体共1880三角面。采集期间输入、动作序号和存档保持稳定。它只覆盖所采场景，最高档特效、完整资源峰值与UI延迟仍未验，不能替代正式180秒性能结论。
- **声音：** AudioMixer总线尚未接入，当前使用分类音量回退；正式混音与人工听审未完成。
- **M5实机：** 尚未取得本版M5运行结果；原生图形、显示、持续性能和跨平台存档仍须实机验证。
- **人工试玩：** 整体美术风格、动画节奏、可读性与游戏体验尚待你的实际确认。已有独立实图复核只代表其记录的场景与范围。

本次实测与待交付项：

| 项目 | 最终结果 |
|---|---|
| 冻结源码、Windows／Mac构建身份 | 源码 `dca7a26`；[Windows构建成功（0错3警）](../evidence/v07/unity/build-windows-20261102-023220.json) / [Mac构建成功（0错77警，仅交叉构建）](../evidence/v07/unity/build-macos-20261102-023332.json) |
| 同源完整Unity回归 | [1013项Edit通过](../evidence/v07/unity/nunit-20261102-020742.xml)，及[158项Play通过](../evidence/v07/unity/nunit-20261102-022904.xml)，零失败/跳过；[同源独立复核](../evidence/v07/review/FULL_REGRESSION_DCA7A26_REVIEW.md) |
| 实际Windows Player流程 | [本次串行运行记录](../evidence/v07/player-dca7a26-0234/serial-summary.json)：独立跳过原生显示的参考运行完成普通145步和Debug24的175步；普通轨迹第8关失败符合预期，调试流程不代表正常通关率 |
| Windows显示与性能 | 严格运行/独立显示运行在独占全屏失败；参考运行P95 16.7929ms仍超预算，GPU覆盖95.6401%；不合并为严格通过 |
| Windows异常恢复与守卫 | [跨进程恢复通过](../evidence/v07/player-dca7a26-0234/separate-process-display-recovery/runner-summary.json)：恢复1280×720窗口且清除待确认；runner记录普通存档比较未变（未单独持久化该时段前后清单，不能逐项重算）；305个构建文件已独立逐SHA核对 |
| 独立资源诊断 | 成品实际退出0；9次快照、20.002秒稳定闲置；构建、普通存档、游戏偏好和引擎显示偏好外部守卫未变。[原始采集归档](../evidence/v07/resources/v07-resource-player-dca7a26-0243/archive.json)；结算段Draw Calls峰值214，高于180起始目标，闲置为96；不宣称整版资源通过 |
| 三个ZIP校验、完整下载与新目录启动 | 本地三包CRC/SHA与版本绑定通过；[同源清单](../Releases/p0.7.0/review-candidate/review-candidate-manifest.json)；[三个附件完整下载及Windows新目录运行的独立复核](../evidence/v07/review/REMOTE_DELIVERY_DCA7A26_REVIEW.md)通过其交付范围；新解压运行不是完整正常游玩或存档续玩测试 |
| GitHub Release页面 | [p0.7.0-rc.1](https://github.com/XDwudi/game2/releases/tag/p0.7.0-rc.1)，预发布；用户已明确授权源码与证据同步，完整分支已推送 |

本版标识为发行 `p0.7.0`、美术 `art0.7.0`、规则 `p0.6.0`、存档 `schema4`。包内若附有限制说明，请一并阅读；旧失败记录不会因新版草稿或局部检查通过而删除。

试玩反馈请附版本页、种子、分辨率与文字比例、控制器顺序及附魔、复现步骤，尽量附游戏内截图或短录像。重点可记录“哪里看不清、哪一步难以理解、哪段动画过快或拖沓”，便于逐项调整。

工程验证详情见 [STATE](STATE.md)。前次成品的终局读档异常及首帧用时错误，分别通过了 [统计修复复核](../evidence/v07/review/RESULT_RELOAD_FIX_REVIEW.md) 与 [时长修复复核](../evidence/v07/review/RESULT_CLOCK_FIX_REVIEW.md)；新结算验证图保存在 [实际画面目录](../evidence/v07/media/result-reload-123717/)。这些专项不替代新完整套件、成品运行或人工试玩。

美术源与现有运行证据见 [资源索引](../evidence/v07/review/ART_RESOURCE_TRACEABILITY.md)、[最终画面增量](../evidence/v07/review/FINAL_0F04E51_SCREEN_ART_INCREMENTAL_REVIEW.md)、[抓手及商店增量](../evidence/v07/review/FINAL_0F04E51_GRIP_SCREEN_REVIEW.md) 和 [九段演出复核](../evidence/v07/review/FINAL_0F04E51_CLIPS_REVIEW.md)。历史媒体保留原采集身份，各报告只覆盖所列场景；新增终局恢复由本次专项补充。

本版已在 [GitHub预发布](https://github.com/XDwudi/game2/releases/tag/p0.7.0-rc.1) 提供下载。三个本地包已完成[独立封包复核](../evidence/v07/review/PACKAGE_DCA7A26_REVIEW.md)，结论仅覆盖本地封包与版本绑定。源码与内部证据的同步状态、工程剩余项见 [STATE](STATE.md)；实际下载入口见上方三项附件。

公共Krita源的补充原生重开检查遇到当前工具启动失败，62项仍未完成该补证；旧71项成功和已有结构检查保持各自范围。详见[实际失败与已知成功样本对照](../evidence/v07/review/KRITA_COMMON_NATIVE_CHECK_BLOCKED.md)，不判定为资源已损坏。

本源已修复调试面板关闭时文字变成矩形色块的问题；新增真实GPU回归包含主字体与回退字体，并纳入1013项Edit、158项Play完整回归。独立复核已实际查看本轮9张Player截图，其中严格/参考运行的Debug32退出过渡未再出现该字块；此观察只覆盖所看场景，不代表整版人工美术与体验验收完成。

本轮成品的实际范围见[Player独立复核](../evidence/v07/review/PLAYER_DCA7A26_REVIEW.md)。普通145步与调试175步、显示恢复、性能、图像各按各自记录判断；不能把独立运行拼成严格全通过。

资源执行、分类、闲置和参数拒绝的范围见[资源独立复核](../evidence/v07/review/RESOURCE_PLAYER_DCA7A26_REVIEW.md)。结算段Draw Calls峰值214高于180起始目标，仍需后续调整；该观察不更改原规格的建议预算措辞。

本次远端包新解压运行使用真实下载文件与309项完整成员校验，PID4436自然退出0；普通文件、游戏偏好、引擎显示偏好和解压树前后逐项一致。仅证明已下载包的隔离启动及所列页面/资源诊断，未额外执行正常存档续玩；不覆盖原180秒性能与原生显示失败。原始证据见[远端运行归档](../evidence/v07/delivery/remote-fresh-extract-0342/archive.json)。

下一轮性能定位见[已有实验与当前CPU证据](../evidence/v07/review/PERFORMANCE_NEXT_DCA7A26.md)及[结算绘制分析](../evidence/v07/review/drawcall-analysis/README.md)。这些是只读分析，尚无实测修复收益，不替代原验收。
