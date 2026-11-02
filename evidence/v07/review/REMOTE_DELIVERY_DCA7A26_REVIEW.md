# dca7a26 远端三包与 Windows 新解压运行独立复核

复核者：`/root/v07_run_review`。日期：2026-11-02 UTC。仅写本报告及绑定记录；未联网、未启动 Player/Editor、未读取实时 Registry、未修改包、工具、生产源或标准。

**规格符合性：远端三附件完整性与 Windows 新目录隔离诊断启动通过。制作质量：此配送范围无新增阻断，可以发布明确标记失败与待验项的诊断候选。整版仍未 ACCEPTED。** 本报告审阅的服务器快照仍为草稿 release `431044653`，不预填公开发布成功；公开状态、最终 URL 和 tag 解析须由随后真实发布记录证明。

## 三个实际下载包

独立重算三个下载文件的完整 SHA-256、字节数与 ZIP CRC，并核安全成员清单。下载回执、服务器上传响应和 `release-download-20261102-034203-962631.json` 的 asset ID、名称、大小、摘要和 URL 一致；tag 为 `p0.7.0-rc.1`，目标提交为 `dca7a2610f39d65088b9541549d3465c388fd3e7`。三回执均记录 HTTP 200 和 `release-assets.githubusercontent.com`；复核者检查其原始本地证据与实际下载字节，没有另行发出 HTTP 请求。

| 附件 | Asset ID | 字节数 | ZIP 项 | SHA-256 |
|---|---:|---:|---:|---|
| Windows x64 | 700392401 | 87,218,149 | 309 | `c823056517851a786cfe8a185c43ad6829e3bdf2f5d8686eb9b17ff6a39169b1` |
| macOS Universal | 700392416 | 127,598,578 | 304 | `437985740644806f220da8217aaf187a19c816421a20dd917e5578f2190641b6` |
| M5 验收工具包 | 700392347 | 127,707,336 | 16 | `e3807850f72fa0de3c41f1bed1364254b530356e0086ae78f926d70c8e9e23c6` |

Windows 309 项、Mac 304 项均完整读取，逐项 SHA、大小和 ZIP mode 与冻结 package proof 一致，包内 BuildIdentity 与 manifest 一致。Kit 的 16 项输入逐项 SHA 匹配 manifest；其中 `inputs/macos.zip` 与实际下载的 Mac 包完全同 SHA、同大小。下载回执分别于 03:39:06、03:41:07、03:42:02 UTC 生成。

Mac 主程序实际为包含 x86_64 与 arm64 两 slice 的 fat Mach-O，主程序 mode 为 `0100755`。此项只确认静态结构，不代表 M5/Intel、Metal、签名、公证、Gatekeeper、quarantine 或 Mac 原生解压运行通过。

Kit 的 `VerifyMac.command` 保持 LF、无 BOM，mode 实际为 `0100666`。包内说明明确采用终端 `bash VerifyMac.command`，不依赖脚本执行位；没有声称可直接双击执行。本轮最初审计曾误设 launcher 必须有执行位，随后依据既有包内说明纠正审计假设，未改变产品、包字节或验收标准。

## 真实远端包的新目录运行

实际输入为 `artifacts/cache/v07-release-review-download/DicePrototype-p0.7.0-win-x64.zip`，不是将本地包改名作为远端证据。草稿 browser URL 的 `untagged-c3095d2fc99df8ae7a31` 段与真实上传、服务器发行记录及下载回执绑定；它不冒充 Git tag。

运行适配器的正式证据副本 `evidence/v07/delivery/run_downloaded_windows.py` SHA 为 `e0bbcab91e6aa0ded5d32d83d1381fa94ac67606702b2f13d7de6c608b3ca169`，与已独立审查版本逐字节相同。原 v3 runner、manifest、package proof、实际下载 ZIP、下载/上传回执和指定 server release record 共 7 项运行输入哈希重新核对一致。

实际路径为 `artifacts/cache/v07-remote-fresh-extract-0342/player/DicePrototype.exe`，隔离输出和存档位于同次运行的 `run/diagnostic` 与其 `isolated-saves`。EXE、Runtime DLL、boot.config 和真实原构建报告均绑定原始期望 SHA；没有改写报告 output 或伪造构建报告。

- PID **4436**：03:42:09.963987–03:43:20.163219 UTC，自然 exit **0**；未超时、未被 runner 终止，结束时未继续运行。
- 实际 WindowsPlayer、非 Editor，Unity `6000.6.7f1`，1920×1080 Windowed / Direct3D11；记录设备 Ryzen 9 5900X、RTX 3080。
- build GUID `388c2eee37244ba48bb656114b6320bf`；Runtime MVID `509a9a1c-df39-43d7-927f-c5f787977a35`；完整源、配置、黄金集及版本身份与冻结 manifest 一致。
- outer status 为 `downloaded_extraction_diagnostic_completed_not_acceptance`，runner/raw status 为 `diagnostic_completed_not_acceptance`；outer/runner errors 均为空，原 raw error 为空字符串。`wholeReleaseAccepted` 与 `formalPerformanceAcceptance` 保持 false。

实际新解压目录共 **309 文件、190,759,584 字节**，逐文件重新计算 SHA、大小并核无增删，与 ZIP proof、`extracted-member-verification.json` 及前后构建守卫完全相等。运行证据的 **14 文件、1,516,472 字节**已逐项核对原 cache、正式归档与 `archive.json` 三方 SHA/大小一致；没有复制 Player 二进制到正式证据目录。

## 前后守卫

独立逐项比较保留的 `external-guards-before.json` 与 `external-guards-after.json`，两者内容和完整文件 SHA 均相同：

| 范围 | 条目数 | 结果 |
|---|---:|---|
| 全部解压文件 | 309 | 同路径、大小、SHA；另与实际文件重新计算核对 |
| 普通持久文件 | 17 | 本次前后保留清单同路径、大小、SHA |
| 白名单游戏偏好 | 36 | 同名称、类型、值摘要 |
| Unity Screenmanager 偏好 | 17 | 同名称、类型、值摘要，单独计数 |
| 诊断内部明确偏好 | 23 | 前、后、恢复后完全相同 |
| idle 隔离文件 | 3 | 前后摘要相同，且与归档的实际文件字节一致 |

内部 idle 状态 SHA 与有效 debug-run payload SHA 一致，pending 均为空；注册和删除的本次 runSeconds key 相同，记录先销毁 app 后清理。未读取用户实时 Registry，也未写回用户数据。

这些外部清单只证明 **03:42–03:43 本次新解压运行的时段**。不回溯补齐正式 180 秒 Windows 流程未保存逐项普通文件前后清单的历史缺口，也不等于正常存档退出续玩或跨平台双向恢复验收。

## 本次诊断的实际范围和数值

原始报告与 runner 摘要的 **18 项 phaseHistory** 完全相同，9 个资源快照覆盖主菜单、手册两页、设置三页、当前保存的单个商店页、转换为普通材质的十骰桌面和 idle 后状态。调试场景中的 Submit/EndStage/Continue 是诊断路线；没有重跑正常 145 步、Debug24 175 步或正式 180 秒性能。本次无 PNG，**实际观看截图为 0**，不声称新增视觉验收。

独立读取 `idle-frames.csv`：**1200 连续帧（2734–3933）**，每帧恰有一个有效计数样本、focused/motionIdle/pointerUnchanged/inputInactive 全真，actionSequence 恒为 5。CSV 最后 elapsed 为 **20.0004284 秒**，报告墙钟区间为 **20.0015655 秒**；两者口径分别保留。**1199 帧零 GC，第 2891 帧为 132 B**，不称全零分配，也未给该分配强行归因。idle 状态、pending 和三个隔离文件保持不变。

9 个快照均独立核对 Texture entity ID 无重复、nativeBytes 求和与分项一致。每个快照有 **94 个唯一纹理对象，总 133,554,394 B（127.367395 MiB）**：font 12,062,258 B（11.503466 MiB），referenced-art 40,896,704 B（39.002136 MiB），render-target 78,905,026 B（75.249697 MiB），unknown 1,690,406 B（1.612097 MiB）。这是 `GetRuntimeMemorySizeLong` 原生对象口径，不是物理显存驻留，也没有从全局计数中扣除 unknown/font 来制造预算通过。

最后两个快照的 **10 个活动普通 Body 共 1880 三角面**；其他 UI、轮廓、物理面及特效不混入此 Body 计数。不能据此判断最高档完整演出或所有分辨率峰值。

`resource-frames.csv` 为 **3932 连续行（frame 2–3933）**；首行三项 counter 为空，保留为缺测。结算阶段 **221 行 Draw Calls >180**，随后 Continue 阶段另 **2 行 >180**，峰值 **214**；idle 对应 1200 行均为 96。计数读取前一完成帧，跨 phase 边界的两行不删除。该诊断仍未满足全路线 Draw Calls 起始目标，不因退出码 0 而记为资源全面达标。

## 最终边界

可以放行的是 **已授权的三个明确诊断候选附件配送**，以及“远端下载文件完整、同版 Windows 能在全新目录启动隔离资源诊断”的技术事实。公开发布本身仍待 root 实际执行及回查。

原正式 Windows 180 秒 P95 **16.7929 ms >16.7 ms**、原生独占未稳定确认、结算绘制目标以及最高完整演出/UI 延迟仍未解决；AudioMixer、公共 Krita 原生补证、M5/Intel 实跑和人工美术/声音/体验仍待。整版与 Mac 未通过。本报告不改任何原失败记录，也不把 20 秒 idle 合并为正式性能通过。

绑定记录：[REMOTE_DELIVERY_DCA7A26_REVIEW.bindings.json](REMOTE_DELIVERY_DCA7A26_REVIEW.bindings.json)。历史范围另见 `PLAYER_DCA7A26_REVIEW.md`、`RESOURCE_PLAYER_DCA7A26_REVIEW.md` 和 `PACKAGE_DCA7A26_REVIEW.md`。
