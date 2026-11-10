# v0.71 试玩包与交付记录

## rc.2：最新 Image2 主界面与桌面版（已发行诊断试玩候选）

2026-11-10，实际构建源为 `b005e0088b227b26e2f8fc44f1c59f68df0c0a35`，已在 [codex/v0.71](https://github.com/XDwudi/game2/tree/codex/v0.71) 上传。包含 Image2 主界面纸菜单、卡牌与真实陶瓷骰子布局，以及木沿、金缝线和深青毛毡桌面；规则仍为 p0.6.0/schema4，应用 p0.7.1 / 美术 art0.7.1，Unity6000.6.7f1/Mono。见 [主界面/桌面更新](tasks/P0-V071-MENU-TABLE.md)与 [rc.2 打包任务](tasks/P0-V071-RC2-PACKAGE.md)。

**`p0.7.1-rc.2` 已于2026-11-10 11:22:20 UTC公开为诊断试玩预发布**，release ID440245825，draft=false/prerelease=true。三个附件已在发布前完整下载，SHA/CRC 与本地封包一致；公开后匿名 API 与三个下载链接 HEAD 均 HTTP200，长度正确，同一附件 ID/内容保持。Windows 下载包已实际启动，但两次辅助诊断均未通过，具体边界见下文。原 rc.1 标签和三个附件保持不变。

[rc.2 发行页](https://github.com/XDwudi/game2/releases/tag/p0.7.1-rc.2)

- [Windows x64 试玩包](https://github.com/XDwudi/game2/releases/download/p0.7.1-rc.2/DicePrototype-p0.7.1-win-x64.zip)
- [macOS Universal 试玩包](https://github.com/XDwudi/game2/releases/download/p0.7.1-rc.2/DicePrototype-p0.7.1-macos-universal.zip)
- [Mac 验证工具包（已含 Mac 游戏包）](https://github.com/XDwudi/game2/releases/download/p0.7.1-rc.2/DicePrototype-p0.7.1-MacVerificationKit.zip)

### rc.2 实际验证结果

| 范围 | 实际结果 |
|---|---|
| Unity 完整回归 | Edit **1018/1018**（101318）、Play **185/185**（103648），均 0 失败/跳过；5 程序集/198 份源的 PDB 对照一致，gate1789 文件 |
| Windows x64 | `build-windows-20261110-103917.json` 成功，GUID `75e7f6b0f1c34075a6527abbc6829e8b` |
| macOS Universal | `build-macos-20261110-104006.json` 成功，GUID `b523ca601806410ca9fbb7ff3d943c8c`；6 个原生组件均含 arm64+x86_64，Mac 实机未运行 |
| Windows 实跑 | 采用本次 `strict-full/player-20261110-104130-079c5f75`；流程完成、0 Unity 错误，性能超预算导致 exit3，不能称 strict 全部通过 |
| 原生显示 | 本次自动原生显示检查通过；物理焦点丢失/显示器拔插未测。独立进程的待确认显示恢复通过 |
| 压力采样 | **180.0108082 秒**，P95 **16.847 ms > 16.7 ms**，性能 **FAILED**；GPU 有效覆盖 **97.218%**，本次覆盖达到原 95% 门槛，不能因此抵消 P95 失败 |
| 存档与文件保护 | runner 报告 `ordinarySaveFilesUnchanged=true`、`buildFilesUnchanged=true`；该原 runner 未持久保存前置详细哈希清单，不扩大为可独立重算的完整前后文件证明 |

完整 Play 的三个新菜单/桌面用例均实际执行；176 个隔离目录（17 个空目录）、3157 个文件已逐 SHA 保留于 `artifacts/cache/v071-rc2/full-play-archive/`。Editor 退出曾写入动态字体缓存，原件/差异保留，随后恢复为精确已测字节；没有修改测试结果或 gate 来放行新字节。

这是保留真实性能失败的诊断试玩候选，整版未 ACCEPTED。Mac/Metal/签名公证、物理显示场景、AudioMixer 正式总线以及人工美术、声音和体验仍待验；旧 rc.1 的失败记录也保留，不改写为本次结果。

### rc.2 成品包与远端校验

包目录为 `Releases/p0.7.1/menu-table-rc2/`，独立 proof 位于 `evidence/v071-rc2/packages/`。以下大小/SHA来自本次封包记录，并与三次真实远端完整下载的字节/SHA/CRC 一致。下载收据位于 `evidence/v071-full/delivery/github-release/p0.7.1-rc.2/review-candidate/`，三个 asset ID 分别为 Windows731100890、Mac731104688、Kit731111250；收据的构建源与发行标签提交均为 b005e00。

| 文件 | 字节 | SHA-256 |
|---|---:|---|
| DicePrototype-p0.7.1-win-x64.zip | 200441327 | `44ef23a3d91be201706b59c5f006886ec7198781f3cf4084c3d13adb93fe656a` |
| DicePrototype-p0.7.1-macos-universal.zip | 240794669 | `90b76360530c53c2fb46ed3e542b691225884c9259d2c5891030b9e81003b913` |
| DicePrototype-p0.7.1-MacVerificationKit.zip | 241475500 | `f6687b2e6ff41ae7c3433954239eb676859c2db8b160ddcd16bef407b8c31dd5` |

试玩请下载对应平台的**具名 ZIP 附件**，无需下载 GitHub 自动列出的 Source code。Windows 完整解压后运行 `DicePrototype.exe`；Mac 验证工具包已包含 Mac 游戏包，按其中执行说明运行 `bash VerifyMac.command`。Source code 是开发源码，不是开箱运行的游戏包。

### rc.2 下载版启动的实际边界

下载的 Windows ZIP 已在两个全新目录实际解压并启动，使用隔离的资源/空闲诊断入口；这与前述正式 Player 的180秒性能运行分开记录。

- `artifacts/cache/v071-rc2-remote-b005e00-01/`：完成18个阶段，到达 `after-idle`；20秒/1200行观察中 `inputRows=1`，不符合安静空闲条件，结果为辅助诊断未完成。
- `artifacts/cache/v071-rc2-remote-b005e00-02/`：进程自然退出码1，内部 `Stable` 等待到180秒期限，停在 `SettingsTab0`，辅助诊断未完成。
- 两次的普通存档、游戏偏好和构建文件守卫均报告未变；第一次 Unity 自身 Screenmanager 值有变化，第二次未变，不声称整个 Registry 始终未写入。两次均未产出 PNG。
- 后续桌面枚举确认了应用窗口，但应用授权超时，桌面工具未成功取得截图或执行输入，不作为手工试玩或截图证据。

第一次真实启动及多页面/场景流程支持“下载包可以启动”的结论，**不能写成辅助诊断通过、零输入空闲通过或整版验收通过**。两个失败/未完成记录均保留，没有降低条件或用无证据重复重试掩盖。正式性能仍为 FAILED；此次启动不替代 Mac、物理显示场景或人工体验验证。

rc.2 原始结果：`evidence/v071/player-b005e00-rc2/serial-summary.json` 及其 `strict-full/player-20261110-104130-079c5f75/`；独立恢复位于 `separate-process-display-recovery/`。两个下载目录分别保留 `remote-extraction-result.json` 和 `run/runner-summary.json`。完整 Play 复核见 `cache/v071-rc2/full-play-review/FINAL_REVIEW.md`，构建 gate 位于 `evidence/v071-rc2/unity/build-gate-b005e00.json`。

发布原始收据为上述发行证据目录的 `release-publish-20261110-112232-284364.json`；匿名 API/三链接 HEAD 及远端引用回读见 `evidence/v071-rc2/delivery/public-release-verification.json`。rc.2 标签与实际构建源同为 b005e00；main仍7de9857、rc.1仍b11694f，旧三个 asset ID 保持。公开可达性没有替代发布前的完整字节校验，也没有把两次辅助诊断失败或正式性能失败改为通过。

## 历史 rc.1：41aface 全量美术测试发行

全部71项活跃内容插画与公共资源已接入，源码冻结`41aface80bba6c1e008bd3d35344210a28003553`。以下记录2026-11-09的rc.1全量版本，不含后续Image2主界面/桌面更新；首批249d980三包为更早历史，保留在Releases/p0.7.1/review-candidate，不用于本次rc.2发行。

发行p0.7.1、美术art0.7.1；规则p0.6.0/schema4、Unity6000.6.7f1/Mono。50控制器、7道具、7服务、7强化统一为高清限色印刷风格；公共符号、5附魔、纸面、卡背、字标、桌布、临时骰轻影/小钟与高Combo环/短弧同步接入。复用经技术复核的陶瓷母模型及3件已合格插画，来源与失败修订稿保留，PNG不冒称分层PSD。

### rc.1 验证结果

| 范围 | 实际结果 |
|---|---|
| Unity完整回归 | Edit1018/1018、Play182/182，0失败/跳过；5程序集196源码PDB一致，gate1782文件 |
| Windows x64 | 045307构建，GUID e36a7047e1f44fe9b16387cbf61ab639，0错3警，Player105源码一致 |
| macOS Universal | 045424构建，GUID a8eee2a94a304703b6fb1e54b3877b87，0错79警，Player105源码一致；Mac实机未运行 |
| Windows独立窗口流程 | 普通145步、Debug24关/175步、18项UI、50项动画，0Unity错误；跨进程显示恢复通过 |
| 原生独占全屏 | strict及native-only确认失败，保留原始失败报告，不拼成strict通过 |
| 压力采样 | 180.0162698秒、10102帧，P95 **16.8078ms >16.7ms**；GPU计时有效覆盖**91.91% <95%**，性能FAILED |
| 存档与文件保护 | runner前后普通存档及305项成品文件守卫一致；普通存档前后SHA另外持久保存 |
| 美术证据 | 71图册逐项交叉作者复核，720/1080专项，13.0013326秒/140真实帧操作片段；人工风格未签署 |

以上是测试候选，不是整版ACCEPTED。Mac/Metal/签名公证、物理焦点/显示器变化、人工美术/声音/体验仍待验证；AudioMixer尚用分类音量回退。Windows10 19045、Ryzen9 5900X、RTX3080/D3D11、1920×1080窗口Development Build为本次运行环境。

### rc.1 包与下载

[GitHub发行页](https://github.com/XDwudi/game2/releases/tag/p0.7.1-rc.1)

- [Windows x64](https://github.com/XDwudi/game2/releases/download/p0.7.1-rc.1/DicePrototype-p0.7.1-win-x64.zip)
- [macOS Universal](https://github.com/XDwudi/game2/releases/download/p0.7.1-rc.1/DicePrototype-p0.7.1-macos-universal.zip)
- [Mac验证工具包（含Mac游戏包）](https://github.com/XDwudi/game2/releases/download/p0.7.1-rc.1/DicePrototype-p0.7.1-MacVerificationKit.zip)

三个成品包位于`Releases/p0.7.1/full-art-candidate`，独立proof在`evidence/v071-full/packages`。GitHub三个附件均已上传并完整下载，SHA/CRC一致；下载版Windows已从新目录完成实际资源诊断，退出码0。**2026-11-09 05:40:34 UTC已公开为诊断测试预发布**，release ID438762609，draft=false/prerelease=true。公开后匿名GET/三个canonical下载HEAD均200，长度匹配，标签实际锚点b11694f8；完整字节/CRC验证在发布前执行，同一三个asset ID和SHA保持。

| 文件 | 字节 | SHA-256 |
|---|---:|---|
| DicePrototype-p0.7.1-win-x64.zip | 197187916 | `cba88bd84ce7b7b97cadf27aa544cc1953f31ef13fd10774854eb585a2bc3163` |
| DicePrototype-p0.7.1-macos-universal.zip | 237545640 | `83c4fc9eb1bbcd74e180412ae8cdfded6345f3f34921d7cbbdb8e0725a6a3bc6` |
| DicePrototype-p0.7.1-MacVerificationKit.zip | 237649059 | `cb182df2b29c29ca3ad901602595886cbc149020a654c334ccdc6aae6626a54b` |

远端原始凭据位于`evidence/v071-full/delivery/github-release/review-candidate/`；新目录运行位于`artifacts/cache/v071-full-remote-41aface-01/`，明确为`downloaded_extraction_diagnostic_completed_not_acceptance`。普通存档、游戏偏好及构建文件前后保持；Unity自己的Screenmanager持久值有变化，单独记录，不能宣称整个Registry未写入。20.0031986秒/1200行空闲观察完成，1199行GC为0、1行为132B；这不是正式性能验收。实际资源native计数为585825814B、其中art484783772B，计数方法包含原生纹理对象，不等同独立GPU驻留。

rc.1发行当时只通过GitHub发行三个具名成品ZIP。自动审批拒绝了当时的源码推送，因此该次附件发行未推送游戏Git源码或独立开发证据目录。发行标签使用已存在远端b11694f8a2f7d767d2113e9b467713807efa403a作为附件位置；包内实际构建身份仍41aface。该旧标签下GitHub自动生成的Source code下载不是rc.1构建源码。MacVerificationKit包含执行验证所需脚本与Windows对照数据；后续用户授权的源码上传不改变这个历史标签身份。

## 运行与反馈

Windows完整解压后运行DicePrototype.exe，保留全部Data/DLL。Mac保持完整.app；MacVerificationKit已包含Mac包，执行`bash VerifyMac.command`并按[Mac说明](tasks/P0-V071-MAC-VERIFICATION.md)回传结果。

R投掷、L确认锁定、Enter提交、Backspace清除待选、H手册、Esc设置/关闭弹窗；已锁骰可拖动排序，右键查看骰子详情。设置提供文字比例、颜色辅助和减少动态。反馈请带页面、种子、分辨率、文字比例、控制器及附魔顺序、复现步骤和截图。

## 历史 rc.1 追溯

- [远端下载与隔离启动独立复核](../evidence/v071-full/review/REMOTE_DELIVERY_41AFACE_REVIEW.md)；公开响应`release-publish-20261109-054035-982477.json`与匿名可达性`public-link-check.json`位于上述发行证据目录。公开元数据/同一三附件的独立绑定见`cache/v071-full/public-delivery-review/final-review.bindings.json`。
- [全量素材路径](specs/v071/ASSET_MAP.md)、[完整回归独立复核](../evidence/v071-full/review/FULL_REGRESSION_41AFACE_REVIEW.md)。
- 实际Player参考：`evidence/v071/player-41aface-0455/native-deferred-independent/player-20261109-045708-df970e2d`；原始CSV与严格失败记录均保留。
- Play全现场173隔离目录、3121文件、979PNG，逐SHA归档`cache/v071-full/full-replay-044536`；空目录也记录。
- 字体退出缓存变动曾触发两次构建门禁失败；已保存差异并恢复精确已测字节，未修改门禁或旧测试结果。最终双构建身份与配置/Golden SHA一致。
