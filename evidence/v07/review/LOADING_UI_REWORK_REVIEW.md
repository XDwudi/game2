# S02 启动加载 UI 与检查驱动限域独立复核

2026-11-01；复核者 `/root/v07_art`。输入 art0.7.0 / p0.6.0 / schema4，Unity 6000.6.7f1，Windows Editor 的真实 PlayMode。生产与 Editor 只读；没有重新运行或伪造引擎证据。

**结论：此次加载检查驱动的修订合理；两尺寸实际 150% 字号的 16 张指定加载/恢复截图在该范围内通过，未发现新增遮挡或残留加载提示。** 不代表整版、Player、性能或 Mac 通过。此 agent 曾实施 S22 说明 UI，故只对独立实施的异步加载及此次等待/字体证据修订给出结论，不自签 S22 全部质量；同批 Qualified 为本 agent 的实现，完全排除在独立质量签署范围外。

## 驱动变更核对

已逐 hunk 比较 `artifacts/cache/v07-loading-rework/Art07LoadingUiPlayModeTests.cs`（SHA `23d04a9c476d5ebbb0e6d5f0c0de4ee1e42c3e7b16325d7bc63be9341786a33a`）与实际 `DicePrototype/Assets/Game/Tests/PlayMode/Art07LoadingUiPlayModeTests.cs`（SHA `2a027ffeb1131452e4686283c2685b92db7d95b9bde3ecce8baa66bee8518ecd`）。

- 行 37–64：仅在 `StartupReady07` 已为真时，截图前等待实际激活的 MenuReveal、PanelMotion、ScreenReveal、ContinueVeil 自然结束，上限 5 秒，未结束仍失败。没有调用 Complete/Skip，没有改 alpha 或时钟。由 `app.StartCoroutine` 驱动真实 EOF 捕获。
- 三项用例在重新载入场景前设置真实 `art07.textScale=150`；元数据新增 `fontScale`，第一项另断言 1.5。未把元数据手写成 150%。
- 原 200 毫秒边界、30 秒 readiness 上限、真实 worker barrier 与线程不同、pending 无 Session/拒绝命令、存档逐字节不变、Continue 不再读一次、同一路径销毁重载互斥与旧结果不被采用等断言均保留。ready 截图的新等待不作用于 pending 采样。
- `GameplayPlayModeTests.cs:38–50,70–79` 仍在 Awake 前建立唯一隔离存档目录，备份并恢复/删除原 `art07.textScale` 等偏好，最后 Save。各用例 finally 释放等待屏障并清空静态钩子。没有为拍图改普通存档。

规格依据 `docs/specs/v07/ART_SPEC.md:176`：真实读取超过 200 毫秒才显示小提示，恢复稳定结果，不重新掷骰/抽 C53。旧 072142/072245 使用基线驱动，不能充当此次实际 150% 或自然完成截图的证据。

## 源与证据绑定

| 批次 | 整批实际结果 | 本次加载用例 | 实看图片 | 文件校验 |
| --- | --- | --- | --- | --- |
| `v07-qualified-loading-720-072646` | 8/8 | 3/3 | 8 张，1280×720，fontScale 1.5 | 122 个归档文件字节数/SHA 全匹配 |
| `v07-qualified-loading-1080-072807` | **8/9，保留失败** | 3/3 | 8 张，1920×1080，fontScale 1.5 | 128 个归档文件字节数/SHA 全匹配 |

每批开始/结束 1358 个源记录逐项相同，5 个程序集 MVID/DLL SHA 均一致。两批加载源、驱动与程序集相同；Runtime MVID `bdb19d63-7c5b-4184-8daa-dce05e1c1b05`，PlayModeTests MVID `622bc020-9f0f-4248-b09a-08b4ad01de8b`。16 份图旁 JSON 的实际尺寸、字体倍率与 MVID 全匹配绑定；PNG SHA 来自逐文件归档核验。

两批慢读用例各真实采样了 **12 帧 ≤200ms** 且无提示。首次提示 720 为 215.9632ms、1080 为 207.3119ms；重载排队首次提示分别 208.4331ms / 214.5386ms。fast 实际读取 13.5396ms / 13.3315ms，`hintShown=false`。这些是功能阈值证据，不是启动性能预算通过证明。

1080 原失败为 `PLAY_ART07_STATUS_QualifiedRewardsStayVisibleAboveNavigationAtAllTextScales`，`QualifiedContinueIncome overlaps DebugWorkspace font150`。该失败完整保留，不在此报告转成通过；root 后续 C01 行修复属于另一源版本/复验。

## 逐屏实际观感

下列每个 screen 均实际打开两尺寸原图，不以截图总数推断可读性。具体绝对/相对路径、SHA、元数据见 bindings。

| screen | 两尺寸观感与限域结论 |
| --- | --- |
| `slow-pending` | 桌布、游戏名、小型“正在读取存档…”可见；没有伪进度百分比、提前开放规则操作或大遮罩。提示无裁切。 |
| `destroy-reload-waiting-for-path` | 等待同一路径的真实 pending 状态可见，同样无可误用操作或残留上局界面。 |
| `slow-ready-continued` | 恢复后真实关卡预览可读：目标/收益/开始操作完整；恢复通知未压住目标数字或主按钮，没有加载提示残留。 |
| `fast-ready` | 菜单入场已自然结束；继续操作、种子 70953、状态和底部版本可读，四背卡/三骰可见，无加载闪层残留。 |
| `destroy-reload-ready` | 最终菜单稳定，种子 70954 和继续操作可读，未残留已销毁实例的提示。 |
| `backup-ready-notice` | 真实“已恢复可用检查点”及活动档失败/备份说明可读；复制、返回和关闭按钮不遮正文。 |
| `legacy-ready-notice` | 明示 p0.5.0/schema3 与 p0.6.0/schema4；可见滚动条及底部“保留备份并开始 v0.6 规则的新局”。长说明超出初始视口部分由滚动裁剪，未在本次逐页滚动复验。 |
| `worker-error-ready-notice` | 真实 IOException 与 isolated-worker-read-failure 可读，复制/返回按钮与正文分开；长堆栈在滚动视口中，本次不签完整堆栈逐页阅读或这些按钮的点击行为。 |

## 保留边界

此处检查最终 ready 帧及两个 pending 帧，不从静帧推定 180ms 过渡的连续运动品质。实际引擎测试负责异步读档、隔离与时间阈值；加载之外的 QF、旧存档新局事务、菜单整个风格及所有其他屏幕由各自证据负责。历史 SpriteAtlas 原生崩溃未因本批通过而被宣布根因关闭。

机器复核脚本与输出：`artifacts/cache/v07-loading-independent-review/audit.py`、`audit.json`；报告绑定：`LOADING_UI_REWORK_REVIEW.bindings.json`。root 负责最终 STATE 汇总；本次未写生产、标准或全局设置。
