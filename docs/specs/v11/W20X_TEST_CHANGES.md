# W20X 测试改写记录（风格接口补充）

| 项 | 内容 |
| --- | --- |
| 任务 | DEV_PLAN W20X：风格接口补充（[THEME_SPEC](THEME_SPEC.md) §2、§3、§3.1，`8bb0e075`） |
| 基线 | `codex/v1.1` 的 `43d90547`；爆彩漫画由 `e23a6209` 合入，其 Unity .meta 在 `a6c04b00` |
| 原则 | 不删除测试、不降低阈值、不缩小范围。下面列出每一处改动的断言或结构与理由 |

## 1 第 0 项：窗口测试不受真实鼠标位置影响

### 1.1 原因（实测）

W14 最终全量（`nunit-20261117-204408`、`-211002`）里鼠标停在 Game 视图上，两条测试失败：

- `PLAY_ART07_RESULTROWS_GameplayItemHoverUsesDelayAndKeepsInventoryUnchanged`：移出物品后共用的悬停框仍在（第 78 行）；
- `PLAY_V13_CT01_CT02_CardGalleryInBothThemesAtTwoTextSizes`：CT-02 量到卡面序号圆片的底色是纸色（1.05:1）。

机制：Bootstrap 场景里 UI 输入模块的 `Point` 动作带“初始状态检查”（场景 YAML `m_Flags: 1`）。每条测试重新加载 Bootstrap，模块启用时读到真实鼠标的当前位置（非默认值），就为它建一个指针，此后每帧在这个位置做射线检测，悬停其下的元素：

- 物品测试：光标下是另一张带悬停框的卡，它的提示在物品之后显示，接管了共用的提示框，物品移出时关不掉；
- 卡面画廊：光标下的卡被抬起（悬停时上移 8、放大 1.03），量字形位置与截图之间卡在动，取样落到圆片外。

### 1.2 改动

| 文件 | 改动 | 理由 |
| --- | --- | --- |
| `Tests/PlayMode/W20XPointerIsolationPlayModeTests.cs`（新） | `IsolateRealPointers20X`：停用平台创建的（native）鼠标、笔、触摸屏，并把它们的 `position` 设为控件默认值 (0, 0)；`RestoreRealPointers20X` 交还 | 停用后真实输入事件被 Input System 丢弃；位置归零，初始状态检查找不到可用的指针，卡片悬停倾斜（`Art07ButtonMotion` 直接读 `Pointer.current`）也不再随光标位置变化。不移动操作系统光标，只改 Input System 里设备的状态 |
| `Tests/PlayMode/GameplayPlayModeTests.cs` | `Setup` 在加载 Bootstrap 前调用隔离；`Teardown` 在卸载场景后停掉残留的模拟光标并交还设备 | 只作用于 `GameplayPlayModeTests`（全部 UI 测试）；资源诊断是单独的测试类，仍然观察真实鼠标与键盘 |
| `Tests/PlayMode/Art07ResultRowsPlayModeTests.cs` | 物品悬停测试的主体移到 `GameplayItemHover07(beforeHover)`；原测试 `StartRun` 后以 `null` 调用 | 回归测试复用同一主体；断言、时限、顺序都没改 |
| `Tests/PlayMode/V13CardTemplatePlayModeTests.cs` | CT-01/CT-02 的主体移到 `W13GalleryRun(afterStart)`；`W13Output` 可被回归测试改到自己的文件夹（`w13Output20X`） | 同上；原测试的输出位置不变 |

隔离开关：启动 Editor 前设环境变量 `DICECRAFT_W20X_POINTER_ISOLATION=off` 可关闭隔离，只用于下面的反向对照，不改变任何断言。

### 1.3 新增测试

| 测试 | 内容 |
| --- | --- |
| `PLAY_W20X_00_StrayRealMouseLeavesTheGameplayItemHoverTestUnaffected` | 在真实 Mouse 设备上每次更新排一个状态事件，模拟停住的光标，并把输入路由设为“全部进 Game 视图、忽略焦点”（最坏情况）。两个位置各跑一遍物品悬停测试的原主体：(a) 第一张卡（另一个带悬停框的元素，W14 的失败方式）；(b) 被悬停的物品本身。每个位置先做正向对照：暂时解除隔离，同一事件让 UI 模块命中该元素并弹出悬停框；恢复隔离后原主体通过，结尾断言模块没有真实鼠标的指针、设备位置仍是 (0, 0)、模拟光标一直在发事件 |
| `PLAY_W20X_00_StrayRealMouseLeavesTheCardGalleryContrastTestUnaffected` | 模拟光标停在 `GalleryCompactTall13_0_3`（默认风格 100%、第 4 页，W14 失败的那张）。正向对照：解除隔离时该卡被模块命中并抬起（子元素位移 > 4）。之后整段 CT-01/CT-02（两套风格 × 两种字号 × 全部 7 页）在光标停留下跑完并通过；截图与报告写到 `artifacts/cache/v11/w20x/<分辨率>/stray-ct02/` |

### 1.4 证据（1920×1080）

| 运行 | 结果 | 说明 |
| --- | --- | --- |
| 反向对照（隔离关闭）`nunit-20261117-232035` | 0/2 | 画廊：重现 W14 的失败标签（default p2/p4、swiss p2/p4 的序号圆片，1.05–2.41:1）。物品：解除隔离后模块启用时又读到停住的位置，对照的悬停框关不掉 |
| 反向对照第二次（只在隔离开启时等待悬停框关闭）`nunit-20261117-232310` | 0/2 | 画廊同上；物品：原主体第 80 行（悬停开始时已有别人的悬停框）失败 |
| 隔离开启 `nunit-20261117-232630` | 14/14 | 两条回归测试、两条原失败测试，以及自己驱动指针的测试（紧凑卡悬停、骰子悬停、商店图标按钮悬停、卡片信息）、注入键盘的四条测试、20 秒空闲资源诊断、Bootstrap 引用检查 |

两次反向对照的正向对照都通过（模块命中目标元素，元素有反应），说明模拟事件与真实光标的作用相同。
