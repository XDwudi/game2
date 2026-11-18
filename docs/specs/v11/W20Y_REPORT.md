# W20Y 报告：色板键 `buttonSecondary`

| 项 | 内容 |
| --- | --- |
| 任务 | [STATE](../../STATE.md)“V1.1 开发”的 W20Y（协调者 2026-11-18，为黑金与彩绘玻璃加的接口） |
| 接口 | [THEME_SPEC](THEME_SPEC.md) §2 的 `buttonSecondary` 一行与表后的乘色规则（`a35670b4`） |
| 输入版本 | `codex/v1.1` 的 `a35670b4` |
| 实施 | W20Y 子 agent；Unity 6000.6.7f1，Windows 10，主检出（与 artgen 批量共用，Unity 运行时它自动暂停） |
| 状态 | VERIFYING，交协调者复核。实施者不签署质量结论 |
| 测试改动 | 本报告第 7 节（没有改写任何已有的 C# 断言；两条 Python 清单测试的可选键集合各加一个键） |

## 0 摘要

- 新色板键 `buttonSecondary`（alpha 0 = 不填）：`GameApp.SolidButton11` 画的每个按钮的面色，即行动行的次要动作、跳过演出、查看骰子、上一手明细、计分行的明细按钮、商店里的按钮，以及卡面与商品行上的价格按钮。
- 不填时与改动前逐位相同：`lerp(background, panel, .55)`，禁用时再向 `panelLight` 混 25%。填了以后只换基色：禁用的 25% 混合从新基色算起，贴图仍是 `ui_chip_formula`，字仍由 `LabelOn11` 按画出的颜色选（禁用的普通按钮仍是 Muted）。颜色在绘制时读 `UiKit.ButtonSecondary(fallback)`，没有静态缓存。
- EditMode 新增 4 条（解析、不填等于原插值、无效值忽略、三套发布风格不填）。PlayMode 新增 `PLAY_W20Y_04`：运行时注入的测试风格（`buttonSecondary` D8B26A，另有全白、全黑两份），1080p 与 720p 下 10 个按钮的面心“画出 = 贴图 × 乘色”，差 0–1 级；字色都是 `LabelOn11` 对画出颜色的选择；四套发布风格在同样的点仍是原插值。
- 逐像素：默认、瑞士、唐三彩、爆彩漫画，两种分辨率各 84 张，改动前两次、改动后两次。非零差都在 3D 骰子上（≤ 2 级），噪声参照之外的变化像素为 0。
- 全量测试（源码冻结在 `05044756`）：EditMode 2437/2437、PlayMode 1080p 244/244、720p 244/244、Python 334/334；三份 NUnit 报告 `quality_check.py nunit` 都是 CHECKS_PASSED，绑定的 3254 个文件与 HEAD 相同。
- 发现（未改）：界面经过 8 位线性缓冲，深色面画出来比乘色暗几级（第 9.2 节第 1 条）；禁用的普通二级按钮字色固定为 Muted，浅色 `buttonSecondary` 下只有 1.60:1（第 9.2 节第 2 条）；计分行的因子筹码、说明筹码、总分筹码不是 `SolidButton11`，不随这个键变（第 9.2 节第 3 条）。

## 1 提交

| 提交 | 内容 | 文件 |
| --- | --- | --- |
| `05044756` | feat(v1.1 W20Y): palette key buttonSecondary for the second-level buttons | `UI/Theme/ThemePalette.cs`、`UI/UiKit.cs`、`UI/GameApp.Play11.cs`（只改 `SolidButton11` 与它的注释）、`Tests/EditMode/Presentation/ThemeInterfaceW20YTests.cs`（+ .meta）、`Tests/PlayMode/W20YButtonSecondaryPlayModeTests.cs`（+ .meta）、`tests/test_art_v11_sancai.py`、`tests/test_art_v11_comic.py` |
| 本文件所在提交 | docs(v1.1 W20Y) | 本报告；THEME_SPEC 的状态行与 `buttonSecondary` 一行；STATE 的 W20Y 段与文件锁行 |

源码在 `05044756` 冻结，此后 `DicePrototype/` 没有再变。改动前的截图在 `a35670b4` 上拍（`DicePrototype/` 与 W20Xb 冻结的 `f2c399f4` 相同）。

## 2 改动

| 文件 | 改动 |
| --- | --- |
| `UI/Theme/ThemePalette.cs` | 新字段 `buttonSecondary`，缺省 `new Color(0,0,0,0)`（不填）；加进 `Hexes` 的同一条声明（`...,section,caption,buttonSecondary;`）与 `From` |
| `UI/UiKit.cs` | `ButtonSecondary(Color fallback)`：填了返回这个值，否则返回 `fallback`，与 `Section`、`Caption` 同样写法 |
| `UI/GameApp.Play11.cs` | `SolidButton11` 一行：`var tint=UiKit.ButtonSecondary(Color.Lerp(UiKit.Background,UiKit.Panel,.55f));`，其后的禁用混合、贴图、字色未改；注释说明 W20Y |

`SolidButton11` 的调用处没有改：商店按钮（`ShopButton14`）与价格按钮（`PriceButton13`）之后照旧用 `LabelOn11(face, …)` 为自己的字选色，读的是这里设好的面色。

## 3 验收逐项

| ID | 方法与场景 | 结果 | 证据 |
| --- | --- | --- | --- |
| W20Y-1 实现 | 读代码；EditMode 与 PlayMode 见下 | `ThemePalette`、`UiKit.ButtonSecondary`、`SolidButton11` 按第 2 节实现；颜色在绘制时读，没有静态字段 | `05044756` |
| W20Y-2 Python 清单测试 | `test_theme_json_schema`（唐三彩、漫画）的可选键集合加 `buttonSecondary`；先用独立脚本按真实的 `ThemePalette.cs` 与两份 theme.json 求值，再跑全量 | 独立求值：`Hexes` 30 个键，去掉可选键后 24 个，两条断言都成立；不改时唐三彩会是 25 ≠ 24。全量结果见第 6 节 | 第 6、7 节 |
| W20Y-3 EditMode | `ThemeInterfaceW20YTests` 4 条（第 7.1 节） | 开发时 9/9（含 W20X 的 5 条，`nunit-20261118-062805`）；全量见第 6 节 | 第 6 节 |
| W20Y-4 PlayMode | `PLAY_W20Y_04`：测试风格在内存里建（`GameTheme.CreateForTests` + `ThemeRegistry.RegisterForTests`，Resources 下没有文件），10 个按钮的面心，直线核对与直接核对，字色规则；四套发布风格的回退 | 开发时 1080p、720p 都通过，问题 0；全量见第 6 节；数字见第 4 节 | `artifacts/cache/v11/w20y/<分辨率>/w20y-04/w20y-04.txt`（开发运行的在 `w20y-04-dev.txt`） |
| W20Y-5 逐像素 | W20X-05 的截图测试 `PLAY_W20X_05`（未改），改动前两次（`a35670b4`）、改动后两次（`05044756`），每次单独运行；噪声参照与 W20Xb 第 D 项相同 | 两种分辨率、四套风格：噪声参照之外的变化像素 0；非零差 ≤ 2 级，都在 3D 骰子上 | 第 5 节；`artifacts/cache/v11/w20y/<分辨率>/identity/`（`identity-y.md`、`identity-y.json`） |
| W20Y-6 全量 | 冻结源码上 EditMode、PlayMode 1080p 与 720p、Python；绑定与 HEAD 比对；`quality_check.py nunit` | 见第 6 节 | 第 6 节 |
| W20Y-7 文档 | THEME_SPEC 只改 `buttonSecondary` 一行与状态行；本报告；STATE 只改 W20Y 段与文件锁行 | 见第 8 节 | 本提交 |

## 4 W20Y-4 的数字

测试风格（默认色板与默认贴图，只填 `buttonSecondary`）：`w20y-probe` D8B26A，`w20y-white` FFFFFF，`w20y-black` 000000。点取按钮面的中心；那一帧里，画在它上面、并覆盖该点的图形（字、金币、图标）暂时关掉，读出的就是按钮面本身。

- 直线核对（W20X-02 的方法）：在线性光下，画出的颜色是乘色的一次函数；测试风格的像素应落在全黑、全白两份的像素连线上，容差 3 级。
- 直接核对：三份风格在该点都画出自己的乘色（3 级以内），说明贴图在那里是不透明的白色，于是“贴图 × 乘色”就是乘色本身；测试风格的像素与乘色比较，容差 3 级。
- 字色：按 `LabelOn11` 的规则，用读出的像素重算：字本身的颜色（文字色；价格的金色；禁用时 Muted）达到 4.5:1 就用它，否则取 ink、paper、文字色中对比度最高的一个。测试要求实际字色与之相同（8 位）。

两种分辨率的数字相同（只有屏幕坐标不同），下表是 1920×1080：

| 画面 | 按钮 | 状态 | 乘色 测试 / 白 / 黑 | 画出 测试 / 白 / 黑 | 直线推算（差） | 直接（差） | 字：测试 / 白 / 黑 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 对局（两颗已锁） | `Roll` 重掷（行动行次要按钮） | 可用 | D8B26A / FFFFFF / 000000 | 216,178,106 / 255,255,255 / 0,0,0 | 216,178,106（0） | 0 | ink 7.81:1 / ink 15.63:1 / 文字色 18.19:1 |
| 对局 | `ViewDice` 查看骰子 | 可用 | 同上 | 同上 | 0 | 0 | 同上 |
| 对局（选中一颗） | `Clear` 取消选择（行动行次要按钮） | 可用 | 同上 | 同上 | 0 | 0 | 同上 |
| 对局（计分之后） | `PreviousSubmission` 上一手明细 | 可用 | 同上 | 同上 | 0 | 0 | 同上 |
| 对局（计分之后） | `StablePresentationDetails07` 完整记录（计分行的明细按钮） | 可用 | 同上 | 同上 | 0 | 0 | 同上 |
| 普通商店 | 卡面价格按钮 `Buy_OFFER_00001`（7 · 购买） | 可用 | 同上 | 同上 | 0 | 0 | 价格：ink 7.81:1（金色只有 1.14:1）/ ink / 金色 11.98:1；“购买”：ink / ink / 文字色 |
| 普通商店 | 商品行价格按钮 `Buy_OFFER_00005`（2 · 购买） | 可用 | 同上 | 同上 | 0 | 0 | 同上 |
| 普通商店 | `ViewCycle` 查看本章（商店按钮） | 可用 | 同上 | 同上 | 0 | 0 | ink / ink / 文字色 |
| 满货商店 | 卡面价格按钮 `Buy_OFFER_00001`（7 · 卡牌已满 10 张） | 禁用 | AE9A66 / CCD4D5 / 0C1516 | 174,154,102 / 204,212,213 / 13,22,22 | 175,154,101（1） | 0 | ink 5.67:1（Muted 只有 1.60:1）/ ink 10.38:1 / Muted 10.67:1 |
| 满货商店 | 道具价格按钮 `Buy_OFFER_00005`（2 · 道具栏已满） | 禁用 | 同上 | 同上 | 175,154,101（1） | 0 | 同上 |

禁用的乘色 AE9A66 = D8B26A 向 panelLight 315358 混 25%，与改动前的规则相同。测试风格没有触发 `LabelOn11` 的加深或调浅（三份风格的面色都等于预期的乘色）。

四套发布风格不填这个键（测试先断言 `buttonSecondary.a == 0`）：同样的 10 个点，面色都等于改动前的插值，在绘制时读取；字色都符合同一规则。

| 风格 | 可用：乘色 → 画出 | 可用的字 | 禁用：乘色 → 画出 | 禁用价格按钮的字 |
| --- | --- | --- | --- | --- |
| 默认 | 193639 → 22,53,56 | 文字色 11.36:1；价格金色 7.48:1 | 1F3D41 → 28,61,64 | Muted 6.81:1 |
| 瑞士 | 151618 → 22,22,22 | 文字色 16.20:1；价格的金色 E80E16 只有 3.88:1，按规则改用文字色（16.20:1，与改动前相同） | 232426 → 34,34,38 | Muted 8.67:1 |
| 唐三彩 | 2B2F15 → 42,46,22 | 文字色 11.94:1；价格金色 6.65:1 | 333E1A → 50,61,28 | Muted 5.92:1 |
| 爆彩漫画 | 032662 → 0,38,98 | 文字色 13.62:1；价格金色 10.56:1 | 05327C → 0,50,124 | Muted 6.88:1 |

深色乘色画出来比乘色暗 1–5 级，这是界面一直以来的画法，不是本任务的改动，见第 9.2 节第 1 条。

## 5 逐像素一致（W20Y-5）

截图测试 `PLAY_W20X_05_IdentityCapturesOfEveryScreenInFourStyles`（W20X，未改）：每套风格 21 个画面，减少动态开启，结束页的用时固定。每次运行只跑这一条测试，`DICECRAFT_W20X_CAPTURE_LABEL` 指向 `artifacts/cache/v11/w20y/<分辨率>/identity/<标签>/`。

| 标签 | 源码 | 1920×1080 | 1280×720 |
| --- | --- | --- | --- |
| before-a | `a35670b4` | `nunit-20261118-062033` | `-062207` |
| before-b | `a35670b4` | `-062348` | `-062527` |
| after-a | `05044756` | `-063426` | `-063607` |
| after-b | `05044756` | `-063747` | `-063926` |

比较脚本 `artifacts/cache/v11/w20y/tools/identity_y.py`（由 W20Xb 的 `identity_d.py` 改来）：噪声参照是每个画面上 before-a 对 before-b、after-a 对 after-b 在四套风格里变化的像素之并集，外扩 3 像素，与 W20Xb 第 D 项相同；区别是四套风格都判定（W20Xb 只判定默认）。每套风格都比较四组交叉（after-x 对 before-y），报最大通道差、变化像素数、落在噪声参照之外的像素数。不遮罩任何区域。

结果：两种分辨率都是 `CHANGED_PIXELS_OUTSIDE_NOISE 0`。下表列出所有非零项（“最大差 / 变化像素”），其余截图（1080p 84 张中的 73 张，720p 84 张中的 81 张）的四组交叉与两组噪声全是 0。

**1920×1080**

| 截图 | after-a 对 before-a | after-a 对 before-b | after-b 对 before-a | after-b 对 before-b | 噪声 before a/b | 噪声 after a/b | 噪声参照之外 | 位置（x, y） |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default-08-dice-styles | 1 / 61 | 1 / 61 | 1 / 58 | 1 / 58 | 0 | 1 / 3 | 0 | 692–767, 361–694 |
| swiss-08-dice-styles | 2 / 59 | 2 / 59 | 2 / 65 | 2 / 65 | 0 | 1 / 6 | 0 | 692–806, 361–694 |
| comic-08-dice-styles | 2 / 59 | 0 | 0 | 2 / 59 | 2 / 59 | 2 / 59 | 0 | 692–767, 692–694 |
| default-17-facepick-s1 | 1 / 3 | 0 | 1 / 3 | 0 | 1 / 3 | 0 | 0 | 1630–1632, 764–768 |
| swiss-17-facepick-s1 | 1 / 3 | 0 | 1 / 3 | 0 | 1 / 3 | 0 | 0 | 同上 |
| sancai-17-facepick-s1 | 0 | 1 / 3 | 0 | 1 / 3 | 1 / 3 | 0 | 0 | 同上 |
| default-18-facepick-s2 | 1 / 3 | 1 / 3 | 0 | 0 | 0 | 1 / 3 | 0 | 同上 |
| comic-18-facepick-s2 | 0 | 0 | 1 / 3 | 1 / 3 | 0 | 1 / 3 | 0 | 同上 |
| default-19-facepick-s3 | 1 / 3 | 1 / 3 | 1 / 3 | 1 / 3 | 0 | 0 | 0 | 同上 |
| sancai-19-facepick-s3 | 0 | 0 | 1 / 3 | 1 / 3 | 0 | 1 / 3 | 0 | 同上 |
| comic-19-facepick-s3 | 0 | 0 | 1 / 3 | 1 / 3 | 0 | 1 / 3 | 0 | 同上 |

**1280×720**

| 截图 | after-a 对 before-a | after-a 对 before-b | after-b 对 before-a | after-b 对 before-b | 噪声 before a/b | 噪声 after a/b | 噪声参照之外 | 位置（x, y） |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| default-08-dice-styles | 1 / 2 | 1 / 2 | 0 | 0 | 0 | 1 / 2 | 0 | 1009–1012, 217–219 |
| swiss-08-dice-styles | 1 / 63 | 1 / 52 | 1 / 2 | 1 / 13 | 1 / 11 | 1 / 61 | 0 | 528–1028, 184–276 |
| comic-08-dice-styles | 0 | 1 / 2 | 0 | 1 / 2 | 1 / 2 | 0 | 0 | 1009–1012, 217–219 |

说明：

- 所有非零差都在界面骰子层画的 3D 骰子上：骰子样式页的八颗样例骰（08），以及商店“我的骰子”第一颗骰子顶面的三个像素（17–19，1630–1632, 764–768；放大看过，是骰子顶面，不是按钮）。这与 W20X、W20Xb 测到的噪声位置相同。
- default-19-facepick-s3（1080p）：改动前两次彼此相同、改动后两次彼此相同，但前后之间差 1 级 3 像素。同一位置在这一画面的唐三彩、漫画两次改动后运行之间（after a/b）以及 S1、S2 的多次运行之间都变过，所以按规定的方法归入噪声；这是那颗 3D 骰子的逐次差，不是按钮。
- 画着二级按钮的截图除上表的 3D 骰子像素外都逐像素相同（看过其中几张确认按钮在画面上）：对局（取消选择、重掷、查看骰子）、对局上的模态框背后的对局画面、商店（卡面、商品行、骰子柜台的价格按钮，查看本章、手册、设置、刷新、指定升级）、满货商店（卡牌已满 10 张、道具栏已满、骰子已满 8 颗、暂不能买等禁用价格按钮）、选面第三步（取消、换一面、确认购买）。上一手明细、完整记录与跳过演出不在这组截图里，由 `PLAY_W20Y_04`（四套发布风格的面色）与代码保证。

## 6 全量测试（W20Y-6）

源码冻结在 `05044756`。三次 Unity 全量由脚本 `artifacts/cache/v11/w20y/tools/run_final.ps1` 依次运行，每次之前等到没有 Unity 进程后再空闲 240 秒。按上一次 Unity 退出的时间算，每次之前实际空闲：EditMode 5 分 49 秒（上一次是改动后截图 after-b 720p，01:39:39 退出；因为脚本从自己启动时才开始计时，比 3–5 分钟多了 49 秒，偏差，见 9.1）、1080p 4 分 00 秒、720p 4 分 01 秒（本机时间 UTC−5）。运行期间我没有跑其他吃 CPU 的任务；artgen 批量在 Unity 测试运行时自动暂停。

| 套件 | 结果 | 报告 | `quality_check.py nunit` |
| --- | --- | --- | --- |
| EditMode（headless） | 2437/2437，0 失败 0 跳过，1.6 分钟 | `evidence/v11/unity/nunit-20261118-064728.xml` | CHECKS_PASSED（`--min-tests 2437`，另要求 4 条 `ThemeInterfaceW20YTests`） |
| PlayMode 1920×1080（窗口） | 244/244，0 失败 0 跳过，30.9 分钟 | `evidence/v11/unity/nunit-20261118-072301.xml` | CHECKS_PASSED（`--min-tests 244`，另要求 `PLAY_W20Y_04`、`PLAY_W20X_05`、`PLAY_W20X_02`） |
| PlayMode 1280×720（窗口） | 244/244，0 失败 0 跳过，29.5 分钟 | `evidence/v11/unity/nunit-20261118-075716.xml` | CHECKS_PASSED（同上） |
| Python `unittest discover -s tests -v` | 334/334，OK（696 秒，低优先级，02:58–03:10，在 Unity 全量都结束之后，其间没有 Unity 进程）；改过的两条 `test_theme_json_schema` 都通过 | `artifacts/cache/v11/w20y/final-python.log` | — |

绑定：三份报告的 `.binding.json`（开始与结束两次快照，各 3254 个文件 = W20Xb 的 3250 个加 2 个新测试文件与它们的 .meta）与 HEAD 的文件逐个 SHA-256 相同，`DicePrototype/` 相对 HEAD 没有改动，报告本身的哈希也对（`artifacts/cache/v11/w20x/verify_binding.py` 输出 BINDING_MATCHES_HEAD）。核对过三次：HEAD 为 `05044756` 时；协调者在 03:04–03:06 提交了 `ccf02ea4`、`086335a2`、`9a8b8fec`（只有 `ArtSource/Art11/` 与 docs，`DicePrototype/`、`tests/`、`tools/` 没变）之后，HEAD 为 `9a8b8fec` 时；本文档提交之后。这三个提交落在 Python 全量运行期间，提交的瑞士插画此前已在磁盘上，所以 Python 读到的文件没有变化。全量里 `PLAY_W20Y_04` 写出的报告与开发运行的逐行相同（两种分辨率）。

与 W20Xb 交付时相比：EditMode 2433 → 2437（+4，`ThemeInterfaceW20YTests`）；PlayMode 243 → 244（+1，`PLAY_W20Y_04`）；Python 334（不变）。没有删除测试、没有降低阈值。

## 7 测试改动

没有删除测试、没有降低阈值、没有改写已有的 C# 断言。

### 7.1 新增测试

| 测试 | 文件 | 内容 |
| --- | --- | --- |
| `W20Y03_ButtonSecondaryParsesLikeEveryOtherKey` | `Tests/EditMode/Presentation/ThemeInterfaceW20YTests.cs` | 6 位、8 位（带 alpha）、带 `#` 的 6 位与 8 位都读出；其他键保持缺省；经 `UiKit` 读：填了就替换回退色，禁用 = 该色向 panelLight 混 25% |
| `W20Y03_WithoutTheKeyTheFaceIsTodaysLerp` | 同上 | 缺省不填；默认色板的面色与按 v0.73 常量（`UiKit.Hex`）算的旧插值完全相同，8 位为 193639 / 禁用 1F3D41；空色板、填了其他键的色板、瑞士 / 唐三彩 / 漫画 theme.json 读出的色板：回退色原样返回，面色等于各自的旧插值（逐位） |
| `W20Y03_InvalidValuesAreIgnored` | 同上 | 10 种无效写法（非十六进制、5 / 7 / 10 位、空、空格、`0x` 前缀、中间有空格）被忽略，旁边的有效键照常读出；名字相近的未知键被忽略；alpha 00 视为不填 |
| `W20Y03_ShippedStylesDoNotSetIt` | 同上 | 默认不填；瑞士、唐三彩、漫画的 theme.json 不含这个键，读出后也是不填（第 5 节逐像素比较的前提） |
| `PLAY_W20Y_04_SecondLevelButtonsTakeButtonSecondaryAsDrawn` | `Tests/PlayMode/W20YButtonSecondaryPlayModeTests.cs` | 第 4 节。复用 W20X-02 的 `TargetPixel20X`、`ScreenPixel20X`、`TintCheck20X`、`SameTint20X` 与 W20X-05 的 `IdentityWinningHand20X`；报告写到 `artifacts/cache/v11/w20y/<分辨率>/w20y-04/w20y-04.txt` |

### 7.2 改动的断言

| 测试（文件） | 旧 | 新 | 理由 |
| --- | --- | --- | --- |
| `test_theme_json_schema`（`tests/test_art_v11_sancai.py`） | `optional = {"valueBadge", "buttonStep", "buttonStepPrimary", "section", "caption"}` | 再加 `"buttonSecondary"`；说明这组可选键的注释加了“W20Y buttonSecondary” | 这条测试读 `ThemePalette.Hexes` 的声明并钉住 24 个必填键；W20Y 按 THEME_SPEC §2 把新键写进 `Hexes`，它可以不填，不填时画法不变。不改时这条断言会是 25 ≠ 24。与 `df94b0b6` 处理 W20X 四个键的做法相同；24 个必填键与其余断言不变 |
| `test_theme_json_schema`（`tests/test_art_v11_comic.py`） | `optional = {"valueBadge"} \| w20x` | `optional = {"valueBadge", "buttonSecondary"} \| w20x`；注释同样加上 W20Y | 同上。漫画不填这个键，`set(palette) - w20x \| (keys & optional) == keys` 仍成立；“漫画必须填 W20X 的四个键”不变 |

## 8 THEME_SPEC 与 STATE 的改动

THEME_SPEC 只改两处：

1. 状态行：在 W20Xb 之后加“**W20Y 已实现**（2026-11-18，`05044756`：色板键 `buttonSecondary`，二级按钮 `SolidButton11` 的面色，见 W20Y 报告）”。
2. §2 `buttonSecondary` 一行：“（W20Y 起使用）”改为“（W20Y 起使用，`05044756` 实现）”，与 W20X 各行的写法相同。

表后一段的“由代码任务 W20Y 实现。”没有改（9.1 第 3 条、9.3 第 3 条）。

STATE 只改 W20Y 一段（交付情况、证据、待定项）与我的文件锁一行（已释放）。

## 9 偏差、发现、问题与没能验证的

### 9.1 偏差

1. `PLAY_W20Y_04` 没有覆盖“跳过演出”和其他几种明细按钮（只读收据、9 种结果、随机来源、覆盖来源）：前者只在演出进行中存在，后者要恢复或随机的局面。它们的面色与覆盖到的按钮由 `SolidButton11` 的同一行决定，第 5 节的截图里也没有它们。禁用的普通 `SolidButton11`（只在忙碌时出现）也没有测，它的字色固定为 Muted（见 9.2 第 2 条）。
2. 两份 Python 测试除了可选键集合，还改了解释这组键的注释（各 2 行），见第 7.2 节。
3. THEME_SPEC §2 表后一段仍写“由代码任务 W20Y 实现。”，按任务说明只改了 `buttonSecondary` 一行与状态行，这段没有动。
4. EditMode 全量之前实际空闲 5 分 49 秒，比规定的 3–5 分钟长 49 秒（脚本从自己启动时才开始计 240 秒）。空闲更长只会让与时间有关的测试更不容易失败，不会让应当失败的测试通过；结果 2437/2437，没有重跑。

### 9.2 发现（未改）

1. **深色界面颜色画出来偏暗**。按钮面的乘色与读出的像素：默认 193639 → 22,53,56，瑞士 151618 → 22,22,22，唐三彩 2B2F15 → 42,46,22，漫画 032662 → 0,38,98；禁用 1F3D41 → 28,61,64，05327C → 0,50,124。浅色完全相同（D8B26A → 216,178,106）。12 组读数全部符合“每个通道先转线性光、按 8 位取整、再转回 sRGB”的算法（`round(lin(c)×255)/255`），推断界面是在 8 位线性缓冲里合成的（未直接查看渲染管线设置）。这是改动前就有的画法，所有界面颜色都一样，逐像素比较里前后相同。影响：风格作者给很深的 `buttonSecondary`（或其他深色）时，画出来会暗几级（这里最多 5 级：漫画红通道 05 画成 00）。
2. **禁用的普通二级按钮字色固定为 Muted**。`SolidButton11` 禁用时字色是 `UiKit.Muted`，不经 `LabelOn11`（任务要求保持）。用到它的是忙碌时的“查看骰子”“上一手明细”，以及锁定演出进行中计分行的随机结果按钮（9 种结果 / 随机来源 / 覆盖来源）。浅色 `buttonSecondary` 下这一行字很淡：测试风格 D8B26A 的禁用面 AE9A66 上，Muted B8C9C5 只有 1.60:1。价格按钮与商店按钮禁用时已经用 `LabelOn11(face, Muted)`，在同一面上取 ink（5.67:1）。建议：禁用的普通二级按钮也用 `LabelOn11(face, Muted)`。默认、瑞士、唐三彩、漫画的禁用面上 Muted 都达到 4.5:1（6.81 / 8.67 / 5.92 / 6.88，第 4 节），所以这四套画面不会变。是否改，请协调者定；黑金、彩绘玻璃填浅色值之前最好定下来。
3. **计分行的筹码不随这个键变**。计分行左边的说明筹码 `FormulaCaption11`、因子筹码 `FormulaChip11`（可点，未点亮时）与总分筹码 `FinalScoreChip11`（未到最终分时）也用 `ui_chip_formula`，乘 `lerp(background, panel, .5)`，看起来像二级按钮，但不是 `SolidButton11`。任务只改 `SolidButton11`，所以它们在黑金的黑色桌面上仍是深色。需要的话另加键或扩展这个键，请协调者定。
4. **AX-02 按颜色家族找主按钮**（`W11PrimaryColoured`：色相差 ≤ 10°、饱和度差 ≤ 0.2、明度在主色的 60% 到 +0.1 之间）。`buttonSecondary` 若落在主按钮色的家族里，该风格跑 AX-02 / SH-04 时会被当成第二个主按钮。黑金（绯红主按钮，金或铜色二级）与彩绘玻璃（钴蓝主按钮，羊皮纸色二级）的方向都离得很远，提醒风格作者。
5. **黑金、彩绘玻璃的清单测试**：两套的 `tests/test_art_v11_deco.py`、`…_glass.py` 在各自工作树里，若照唐三彩与漫画的写法钉住“去掉可选键后 24 个”，合并到含本提交的 `codex/v1.1` 后需要把 `buttonSecondary` 放进可选键或色板。工作树不在我的可写范围，没有查看。
6. 8 位写法带 alpha 时（如 C9A45C80）面是半透明的，`LabelOn11` 会把其下的颜色算进去（`RenderedFill11`）；PlayMode 没有测半透明值。THEME_SPEC 没有禁止；建议风格作者填不透明值。

### 9.3 问题

1. 9.2 第 2 条：禁用的普通二级按钮是否改用 `LabelOn11(face, Muted)`？（四套发布风格不变。）
2. 9.2 第 3 条：计分行的三种筹码是否也需要可设的颜色？
3. THEME_SPEC §2 表后一段的“由代码任务 W20Y 实现。”要不要改为已实现（任务说明只允许我改 `buttonSecondary` 一行与状态行，所以没动）？

### 9.4 没能验证的

- 150% 文字下没有测（面色与字号无关；`LabelOn11` 的选择也与字号无关）。逐像素截图同 W20X，只在 100% 文字下拍。
- 没有真实的黑金或彩绘玻璃风格可测（不在本检出）；只用内存里的测试风格。
- 渲染管线的缓冲格式没有直接查看，9.2 第 1 条是从读数推断的。
- macOS 与移动端未运行。
