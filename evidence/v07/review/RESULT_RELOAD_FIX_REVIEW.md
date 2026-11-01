# 终局重载修复：独立代码、红绿回归与实际画面复核

结论：**RR01 非 Submit 空占位导致的终局崩溃，最小修复和真实红绿专项证据通过限域复核；S18 仍有 RC02 时长首绘错误，需返工，不能签本处完整结果页或整版通过。** 审查者 `/root/v07_spec_audit` 未修改生产、标准或测试，未运行 Unity/Player。输入为 69d2f4d 上的候选，最终最小源码已提交为 `4f1a25cbd7ed885bd863c24411f1075d7888a18f`；最终字节的完整回归另行绑定，本文不预填结果。

## RR01 修复符合已验证的收据合同

`GameApp.Art07Results.cs:37` 只把 `submission?.breakdown != null` 改为 `command.kind == CommandKind.Submit`。独立原字节比较确认：该一次替换外，生产文件没有其他改动。总分、最大单手、精确倍率分数计算、展示和规则均未改；没有将空字符串兜底为 0，也没有忽略真实 Submit 的损坏数据。

这与 `GameSession.Validation.cs:82–88,203–219` 的保证一致：所有收据都有 command，只有 Submit 必须提供完整且经过重算校验的提交明细。实际失败存档的 127 条非 Submit 已有空嵌套对象，不能以非 null 判定其类型；此前独立根因记录在 `artifacts/cache/v07-result-reload-review/ROOT_CAUSE.md`。

最终 `Art07EdgeScreensPlayModeTests.cs` 仅插入 58 行新测试与帮助方法；移除这一段后，文件与 69d2f4d 的旧文件逐字节相同。因此既有四类终局测试和其他旧断言未被删除、弱化或重新排布。

新增用例具有有效的失败检出能力：

- 通过普通新局、真实 UI 投掷/锁定/三次 Submit 到 RunFailed，走生产 AtomicSaveStore；未手改 phase 或构造假的可选对象。
- 真正 Load 后明确断言存在非 Submit 空 breakdown，随后经公共 EnterDebugWorkspace → ReturnNormalWorkspace，再冷 Bootstrap → Continue。两条路线均检验统计与普通状态/存档字节不变。
- 总分和最大分来自本次逐条成功 Submit 的 `receipt.score`；最高倍率先断言本场景结构/红骰/控制器/附魔因子固定，再取实际颜色因子。没有重复调用待测结果统计方法生成期望值。
- 仅在内存副本上损坏一条真实 Submit，仍要求现有验证器抛 RuleViolation；没有把错误真提交隐藏成零分。

## 实际红绿结果与字节身份

| 报告 | 实际结果 | 范围 | 耗时 |
| --- | --- | --- | ---: |
| 122700 | 0/1，1 失败，0 跳过 | 新重载测试；原生产实现 | 1.698889 秒 |
| 122742 | 2/2，0 失败/跳过 | 新重载测试＋既有四类终局测试 | 4.5692809 秒 |

122700 的真实失败是新测试的 ReturnNormalWorkspace 调用抛 `Missing decimal score`，堆栈定位 `ResultStatisticRows07:39`，与实际 Player 故障一致，不是辅助初始化失败。122742 新用例通过耗时 2.467560 秒，既有终局用例通过耗时 2.055403 秒。

两报告的原 XML path/SHA 与 binding 一致；各自开始/结束 1360 文件清单和加载程序集清单完全相同。相对 69d2f4d 完整回归，红批仅测试源变化，绿批仅测试源及这一个结果页源变化。两报告中的 report 项记录 path/SHA，不含 bytes；本审查另记录实际文件大小，没有补造原报告字段。

| 文件身份 | SHA256 |
| --- | --- |
| 原生产 Results（红批） | `c431d5cdced3abdd3a77a3b73b0cc94d67be06378c5748004fda752499c5145d` |
| 修复 Results（绿批及最终） | `f740fb4e419648a7a78e3547835e1ecbc6a6299e0cae1bf016f0fe29a018130a` |
| 红/绿实际测试候选 r02 | `d261ea7b2978928837bc365ac3b768cc7dcc7274839c8153905c082bbe63983d` |
| 最终恢复旧段原字节的测试文件 | `a964222c360afce41f7aabdd26bb3d5b9ceb50d7b720690b681eefc4200de680` |

最后两份测试文件的差异仅为换行形式及候选第 31 行的一个额外空行；其余有序文本完全一致。**红绿报告仍绑定 d261…，不改绑 a964…；后者需由新的实际完整回归绑定。** 初次辅助比较过严地假定 splitlines 完全相等及 report 含 bytes，因此停止；识别这两项具体格式差异后按实际字段与逐行位置完成核对，未修改任何产品或测试断言。

红批 Runtime MVID `40bf476e-46fc-401d-99ac-cf636e7371cf`；绿批为 `ee32969a-b944-411a-940b-c039b8a15ecf`。红绿实际 PlayModeTests MVID 均为 `d2270290-46d4-4214-9df9-fc4223aec507`。本文只核这两份实际加载记录，不把后来重编测试程序集视作红绿运行时文件。

红归档 1 目录/3 文件/239,710 字节，绿归档 2 目录/20 文件/6 PNG/7,180,566 字节。目录与 NUnit output 匹配，各归档文件 SHA/大小及归档报告/binding 核对一致。

## 两张新实际画面与 RC02

实际打开并查看绿批 1920×1080、100% 字号的两张图，路径在 `v07-result-reload-green-122742/DiceCraftTests_d4bc6b224966401ca084eb7f56f155c7/art07-edge-screens/`：

- `S18-normal-terminal-return.png`：frame 34，SHA `0a30321bde88089c80bbf0513b1e0e9f30029a2516f56c06a3ff1f9abf4762cb`。
- `S18-normal-terminal-cold-reload.png`：frame 53，SHA `b4288084b24de199b7cf4c5142d6251948facd2bcf3c46558b69ea3f817c1799`。

两张元数据的 Runtime MVID 与绿批一致，RunFailed 和状态哈希相同。实图完整显示总得分 14、最大单手 6、最高倍率 ×3、种子 70991/普通对局、最后有效提交骰和三个结果按钮；数字、标题、按钮没有观察到裁切/重叠。冷恢复通知位于右下，不盖主要统计或按钮。仅这两张是本轮实看，其余四张归档图只核完整性，未代签新视觉结论。

**RC02 仍有真实语义错误：两张新普通局图都显示“用时 旧存档未记录”。** 它不是本轮新增断言覆盖到的内容。原 S18 规格 ART_SPEC.md:310–312 要显示用时，不能把统计数值正确视作整张结果页正确。

只读调用链确认如下：

1. 普通新局成功后 `Runtime/GameApp.cs:150` 调 `StartRunClock07`；Results.cs:20–21 写入该 run 的计时键和值 0。新测试不是无计时键的旧存档。
2. `GameApp.Debug.cs:66–74` 进入调试时调用 StartRunClock07，先保存普通时钟，再把内存 clock ID 切到调试局。`:78–83` 返回普通 Session 后立即 Refresh，没有先绑定普通时钟。
3. Results.cs:45 在终局首绘读取仍属于调试局的 clock ID，因不匹配而选择旧档文案。后续 `TickRunClock07:25–28` 虽绑定并读取已有普通计时键，但不会更新 EndStats 标签。
4. 冷启动亦如此：菜单期间 Tick 的 `inMenu` 提前返回；`GameApp.Art07StatusDetails.cs:23–29` 的 Continue 先 Refresh，时钟尚未绑定。下一 Update 再读取正确字段时，标签已绘好且不会随之重建。
5. 既有 LoadBootstrap 只载场景并等待 StartupReady；Setup/Teardown 只处理其十二个公共偏好，没有删除该 runSeconds 键，不能把实图归因为测试清理。

最小后续方向是在终局统计读取前同步当前 run 的时钟身份和已存值，保留尚未保存的同局内存值；不能调用 Start/reset、增加播放时长、覆盖旧键或伪造缺失记录。保留旧档缺键及调试缺键的原明确提示；针对有键重载补足首绘和后续帧断言。此项已立即交 root 处理，本文不代写生产或取消正在执行的测试。

## 交接范围

RR01 的最小产品修复、测试有效性、红绿身份通过限域复核；RC02 保持 REWORK，新增两张图不签“完整结果页无缺陷”。root 已报告因 RC02 正常取消 4f1a25c 正在执行的完整 Play；该批属于不完整运行，本文不计通过，也不等待或推造取消 NUnit。后续时钟补丁的红绿、最终完整回归、新 Player、Mac/性能/人工验收须另按真实证据处理，旧 Player 失败保留。所有外传限制保持。

完整只读审计及 SHA 见 `artifacts/cache/v07-result-reload-review/fix-candidate-red-green-audit.json` 和同名 bindings。由 root 更新 STATE。
