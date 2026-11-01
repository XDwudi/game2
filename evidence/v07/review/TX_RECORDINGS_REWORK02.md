# TX 连续录制第二轮复核

复核人：`/root/v07_shop_ui`；2026-11-01 UTC。输入为 art0.7.0 / p0.6.0 / schema4，依据 `docs/specs/v07/ART_SPEC.md` 的 S11、S13、O05/O06、U05/U07 和减少动态要求。生产、Unity、规则和验收标准均未由本次复核修改。

**结论：本批八条录像的证据一致性通过；旧 TX01—TX05 的指定缺陷在本批 1920×1080 样本中已修复。此结论只针对下述录像与源快照，不能签署全版美术 ACCEPTED，也不能替代 Player、跨配置和人工体验验收。**

## 证据范围与来源

- 八路清单：`artifacts/cache/v07-transaction-routes-02.json`。清单自身仍写 `status:not_executed`，它是路线计划；本次执行事实来自八个 `recording.json`、实际 PNG、收据和 MP4，不把该旧字段当运行结果。
- 源快照：`artifacts/cache/v07-build-preflight/movies-0414-source/preflight.json`，时间 `2026-11-01T04:17:51.412305+00:00`，HEAD `1fbc3cce9d84237fa98b31d4b73b08e6f5e909ae`。该文件明确不是构建验收。
- 八路 `source-snapshot.sha256` 的实际 SHA256 均为 `b3c57170df1e1f9f6c4459e32a848c2fd21aca13fef1e14cc5607052138ad6bb`。每份含 125 个源/配置文件；其中 83 个与 preflight 所列候选逐字节匹配，另 42 个未改动的已跟踪源与记录 HEAD 的内容匹配（Git blob 比较明确规范化 CRLF；录像原文件哈希另外按原始字节核对）。
- Unity `6000.6.7f1`，WindowsEditor / Windows 10.0.19045 / RTX 3080 / D3D11。Runtime MVID `b5254e2b-fea4-45de-b803-7a41b53df2e0`；Core MVID `8f690e0d-a075-4b72-98e4-2cb64ced2fa3`。嵌入的 p0.6 BuildIdentity 是历史构建身份，本次按源快照与实际程序集身份限定，不将其称为 p0.7 Player 包。
- 隔离存档目录为 `artifacts/cache/v07-movies-0414-isolated-save`。八路普通存档原始哈希均维持 `e47852404c83400cd515001101402b91eece0218dda0cb0c7e4b844a49ae4e1d`。
- 这是固定 1/60 演出时钟的离线视觉记录。没有实时性能、180 秒稳定性、音频或人工实时观影通过的声明。

完整文件哈希、逐流 RNG、收据、前后状态、影片信息、所看 48 张实帧的哈希，以及逐文件源匹配见 `tx-recordings-rework02/bindings.json`。全部 512 帧的逐帧哈希在原 `frames.jsonl` 中；该 JSONL 本身亦被本复核绑定。

审查期间当前工作区已继续集成；发现五个源文件与录像快照不同：`Runtime/GameApp.cs`、`Runtime/Verification/PlayerVerification.V06.cs`、`Tests/PlayMode/Art07InteractionPlayModeTests.cs`、`UI/GameApp.Art07.cs`、`UI/GameApp.Art07Interactions.cs`（均在 `DicePrototype/Assets/Game/` 下）。本报告不把录像重绑定到这些新字节。直接涉及本次修复的 TransactionMotion、Effects、UpgradeSurface、UpgradePreview 等所读源仍匹配录像快照；五个变化的精确捕获/当前 SHA 已保留在 bindings 中。

## 连续性、事务与随机流

独立读取全部 **512 个 PNG**：文件数与索引连续、每个原始字节 SHA256 与对应 JSONL 相符；帧的 `videoSeconds` 均为 index/60。八个 MP4 用已安装 FFmpeg 完整解码，退出 0、无解码错误；FFprobe 的解码帧数分别匹配 PNG，PTS 无缺口且按 1/60 递增，1920×1080、60 fps、无音频。另将每个 MP4 的全部解码帧按顺序与原 PNG 序列做 SSIM，All 值为 0.996430—0.998429；这是有损编码对应性诊断，未新增或替代验收阈值。

复核者实际打开 **48 张原始 PNG**，包括交易前、关键变化、中间问号、完成边界及归位帧。没有声称逐张目视全部 512 帧或进行了实时播放器观影；连续性结论由全序列检查支持，制作质量结论由实帧和对应源支持。

每条录像都在第 15 帧发生唯一一次提交：actionSequence 恰好 +1，processedReceipts 恰好新增一条，其 commandId 与 `transaction.receiptId`、cycle、提交后的 stateHash 对应。前 15 帧维持 before state/save，之后所有交易和 hold 帧维持 after state/save，没有演出推进状态或反复写盘。通用顶层 `receiptId` 是计分记录字段，在这些交易帧中为空；本次检查使用交易专用字段，没有将空值误判为匹配。

| 路线（目录均为 `artifacts/cache/v07-tx-…-02`） | 帧数 / 演出时长 | 金币 | 收据中的真实随机调用 | 实际目视帧索引 |
| --- | ---: | --- | --- | --- |
| refreshcontrollers-normal | 63 / 280 ms | 110→107 | shop 12；dice/effect 0 | 14,15,20,23,31,32 |
| buycontroller-normal | 67 / 340 ms | 110→104 | 三流均 0 | 14,15,25,36,37 |
| expandcontrollers-normal | 67 / 340 ms | 110→103 | shop 2；dice/effect 0 | 14,25,35,36 |
| expanditems-normal | 67 / 340 ms | 110→104 | shop 1；dice/effect 0 | 14,25,33,35,36 |
| randomface-normal | 68 / 360 ms | 110→109 | effect 1；dice/shop 0 | 14,15,19,23,25,27,30,36,37 |
| glassdie-normal | 72 / 420 ms | 110→109 | 三流均 0 | 14,15,22,28,35,40,41 |
| refreshcontrollers-reduced | 54 / 120 ms | 110→107 | shop 12；dice/effect 0 | 14,15,19,22,23 |
| randomface-reduced | 54 / 120 ms | 110→109 | effect 1；dice/shop 0 | 14,15,18,19,21,22,23 |

每次随机调用的 beforeState→afterState 首尾连续并与真实前后状态相符，全部 `overridden:false`；无调用的流保持不变。普通购买为真实 C21 免槽报价，没有暗中新增随机调用。U07 的一次 effect 抽取只改变所选永久面：Normal 为 D146/F01，Reduced 为 D191/F01，均 1→2、红色不变；其余面完全相同。U05 仅 D151 的 isGlass false→true 和 dataRevision 改变，六面记录完全不变。所有设置 fixture 的公开命令均有 accepted=true、pendingSave=false 的记录；这些是明确的隔离调试构造，不是正常流程通关证明。

刷新只重建六个控制器报价，其他类别完整保持；O05 从 5→6 个控制器报价，O06 从 4→5 个道具报价，原报价除被购买的服务外不变。装备槽/背包容量未因 O05/O06 增加。每次动作的真实报价、收据与余额扣除一致。捕获源通过已启用、至少 40 px、完全在屏内且中心首个 Raycast 命中的按钮执行交易；leading frame 可见该真实入口。录像没有另存独立输入设备事件轨迹，本报告不将源检查扩写成硬件鼠标输入证据。

## TX01—TX05 的规格与制作质量复核

| 旧缺陷 | 规格符合性结论 | 制作质量实帧结论与边界 |
| --- | --- | --- |
| **TX01 中文旧卡快照变暗色实心方块** | **本批限域通过。** 快照按 TMP 主/回退材质保留字体图集，并使用可复用的自有材质，避免来源卡禁用释放 fallback 材质后失效。 | 购买 f15、刷新 Normal f15/f20、Reduced f15 的中文名称、稀有度、说明与价格保留可辨字形；中间淡出只降低透明度，没有旧版矩形字块。新报价与最后状态文字正常。本批不是所有字体回退组合/长期材质泄漏的穷举，相关专项另验。 |
| **TX02 现金票遮住钱包** | **本批负向消费限域通过。** 票据在商店顶部空白区，钱包仍显示真实已提交余额，未为了避让隐藏余额。 | 八路可见票据位于约 x1080/y32 起的 220×58 空白带，向上微移的轨迹不碰左钱包、利息、货架标题或刷新按钮；数字没有被图标覆盖。只覆盖本批 1080 负向消费，正收入、720、150% 等仍须对应配置证据。 |
| **TX03 U05 一开始就青色、只像平面方块** | **旧起始材质/姿态缺陷本批通过。** 从真实 previous 陶瓷色与 Glass=0 向 committed 材质插值，整骰属性一次提交，六面数据不变，420 ms。 | f15 是暖奶油陶瓷；f22/f28/f35/f40 连续成为青灰玻璃外观，顶部/正面/右面始终可见，蓝色点阵保持不透明与原位置，没有裂纹暗示未来风险。此项关闭错误起色和平面姿态；玻璃在所有光照/镀金/纹理组合中的半透明识别质量仍属专门材质验收范围，不能由本片全部签署。 |
| **TX04 U07 假三点、无单次问号翻片；后续问号倒置** | **本批 Normal 与 Reduced 限域通过。** 旧点→问号→收据新点单次揭示，视觉逻辑无随机调用。Normal 使用单调 0→90→180 半翻；问号使用相机投影的上/右方向独立保持正向。 | Normal f15/f19 为旧1点及变窄贴片；f23/f25/f27 是钩在上、孤立点在下的正向问号，未镜像；f30 新2点贴片仍较窄，f36 完整2点。旧/新点不叠加。对全部22个 Normal 动画帧的辅助红色连通域检查为 1→0→2，未出现3；该像素诊断只补充实看，不另立门槛。Reduced f15→f18/f19→f21/f22 为静态1→正向问号→2，未见旋转。其全部8个动画帧也为 1→0→2；UI tracks 的四元数均为 identity，源中 Reduced TurnDegrees=0。 |
| **TX05 扩栏描边只有窄条** | **本批 O05/O06 限域通过。** 边框先取最终布局的角点，然后才对新卡 TrackReveal；旧报价没有被重新生成或翻转。 | O05 f25 的框已是完整约160宽，而卡处于窄半翻；f35 完整框包围第六张2233卡。O06 f25 的框已是完整约280宽，f33/f35 包围“新增道具位5 / 增加提交 / 2金币·道具页”卡，不再只有左侧48宽。完成 f36 时临时提示归位/清理；O06 此时仍处服务页，新道具应在道具页查看，不把临时提示消失误判为丢商品。空候选情况不在这两条片中。 |

Normal 刷新中只货架旧卡退去、新卡展开；道具等其他区保持。Reduced 刷新是原位淡变，全部记录的目标四元数均为 identity。购买的“已购买”章、真实新装备实例以及钱包更新一致；交易完成边界没有留下旧快照覆盖最终可操作界面。末帧 metadata 的 playing/busy 均已结束、快照数清零。

## 未覆盖与交接

本批没有覆盖：720/1440/16:10/超宽、100/125/150% 完整字体矩阵（录制 metadata 未独立保存 fontScale，不能据外观推定完整缩放证据）、Fast/Instant、销售与替换净收入、所有升级组合、空合法候选、失焦/跳过、音频、macOS、Windows 独立 Player、实时性能和人工美术标杆批准。它不替代上述门槛，也不以 NUnit 通过作为整版制作质量签字。

旧 `TX_RECORDINGS_REVIEW.md` 和 `v07-tx-*-01` 保留为原失败证据。本报告只是旧五项缺陷在指定第二轮样本中的关闭记录。后续最终源的影响分析与必要重测由 root 统筹，并将本报告的限域结果写入 STATE/最终验收矩阵。
