# v0.71 全量美术测试发行

全部71项活跃内容插画与公共资源已接入，源码冻结`41aface80bba6c1e008bd3d35344210a28003553`。本页记录当前全量版本；首批249d980三包为历史，保留在Releases/p0.7.1/review-candidate，不用于本次发行。

发行p0.7.1、美术art0.7.1；规则p0.6.0/schema4、Unity6000.6.7f1/Mono。50控制器、7道具、7服务、7强化统一为高清限色印刷风格；公共符号、5附魔、纸面、卡背、字标、桌布、临时骰轻影/小钟与高Combo环/短弧同步接入。复用经技术复核的陶瓷母模型及3件已合格插画，来源与失败修订稿保留，PNG不冒称分层PSD。

## 当前验证结果

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

## 包与下载

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

本次只通过GitHub发行三个具名成品ZIP。自动审批拒绝源码推送，因此不推送本次游戏Git源码或独立开发证据目录。发行标签使用已存在远端b11694f8a2f7d767d2113e9b467713807efa403a作为附件位置；包内实际构建身份仍41aface。GitHub自动生成的Source code下载不是本版源码。MacVerificationKit包含执行验证所需脚本与Windows对照数据。

## 运行与反馈

Windows完整解压后运行DicePrototype.exe，保留全部Data/DLL。Mac保持完整.app；MacVerificationKit已包含Mac包，执行`bash VerifyMac.command`并按[Mac说明](tasks/P0-V071-MAC-VERIFICATION.md)回传结果。

R投掷、L确认锁定、Enter提交、Backspace清除待选、H手册、Esc设置/关闭弹窗；已锁骰可拖动排序，右键查看骰子详情。设置提供文字比例、颜色辅助和减少动态。反馈请带页面、种子、分辨率、文字比例、控制器及附魔顺序、复现步骤和截图。

## 追溯

- [远端下载与隔离启动独立复核](../evidence/v071-full/review/REMOTE_DELIVERY_41AFACE_REVIEW.md)；公开响应`release-publish-20261109-054035-982477.json`与匿名可达性`public-link-check.json`位于上述发行证据目录。公开元数据/同一三附件的独立绑定见`cache/v071-full/public-delivery-review/final-review.bindings.json`。
- [全量素材路径](specs/v071/ASSET_MAP.md)、[完整回归独立复核](../evidence/v071-full/review/FULL_REGRESSION_41AFACE_REVIEW.md)。
- 实际Player参考：`evidence/v071/player-41aface-0455/native-deferred-independent/player-20261109-045708-df970e2d`；原始CSV与严格失败记录均保留。
- Play全现场173隔离目录、3121文件、979PNG，逐SHA归档`cache/v071-full/full-replay-044536`；空目录也记录。
- 字体退出缓存变动曾触发两次构建门禁失败；已保存差异并恢复精确已测字节，未修改门禁或旧测试结果。最终双构建身份与配置/Golden SHA一致。
