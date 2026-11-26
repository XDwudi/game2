# W9a 备注：Unity 里的集成与首次实跑

工作包 W9a，交接包 [briefs/W9A_UNITY_INTEGRATION.md](briefs/W9A_UNITY_INTEGRATION.md)。起点提交 `be058fe1`，分支 `codex/v1.1.1`。实施者 Claude Opus 5.5（协调者为主会话的 Claude）。本包期间是 Unity 工程唯一的写入者，同一时间只开一个 Editor。

## 0. 进度

| 项 | 状态 | 提交 |
| --- | --- | --- |
| 1 美术入库 | 完成：入库、导入、绑定、裁切表；`tests/test_art_v11_crops.py` 与 `test_art_v11_install.py` 26 条全过（改前 4 条失败）。新美术的实机画面在第 5 项 | 见提交记录 “art(v1.1.1 W9a)” |
| 2 字体图集重烘 | 完成烘焙；`V21TitleFontAssetTests` 在第 3 项跑 | 见提交记录 “fonts(v1.1.1 W9a)” |
| 3 EditMode 全量 | 未开始 | |
| 4 PlayMode 1080p、720p 全量 | 未开始 | |
| 5 实看画面 | 未开始 | |
| 6 收尾 | 未开始 | |

## 1. 美术入库

### 1.1 复制与清理（`tools/art_v11/install_art.py`）

扩展了现有安装工具，没有手工复制：

- 新增 `--batch <清单>`（`install_batch`）：读 `install_manifests/w8-install.json`，先整批核对再写。每行插画用原有的 `choose` 按清单的版本核对生成记录（`runValid`、版本、归档路径、`jobPath`、内容 id、原图 SHA-256 等于记录），再要求原图 SHA-256 等于清单；图标核对源文件 SHA-256 等于清单；目标路径必须是该主题该 id 的固定位置（默认插画 `Art071/Illustrations/illustration_<小写 id>.png`，五套 `Resources/Themes/<主题>/Art/illustration_<ID>.png`，图标 `Art/Art11/Icons/<主题>/icon_<ID>.png`）；删除清单只接受已移除 id 的插画或图标 PNG 与 `.meta`。任何一项不符整批拒绝（退出码 2），一个文件也不写。复制先写临时文件、核对哈希再替换；`--check` 只核对。
- 整套安装（原有模式）原来要求 `ArtSource/Art11/<主题>/illustrations/` 下的文件夹与配置 id 完全一致；三张已移除卡的文件夹按 W8 的约定作为历史保留，所以加了 `RETIRED_IDS`（C90、C128、C145）：这些文件夹不装、不进清单，其它不在配置里的文件夹仍然整次拒绝。
- 与 W22i 不同的地方：W22i 只用整套模式（五套主题插画与徽记），默认插画与图标不经安装工具（默认插画早就在库里，图标由 `make_crops.py` 烘焙）。本包的默认插画和 24 个图标是 W8 预先烘好、带哈希的成品，所以用批次模式按清单逐字节复制；之后再跑一次 `make_crops.py` 重生成裁切表与全部图标，核对它烘出的 24 个图标与 W8 的逐字节相同（见 1.3）。

运行结果：`--batch … --check`（装之前）`BATCH_CHECK_FAILED W8 120`（48 个文件未装 + 72 个旧文件还在）；`--batch …` `BATCH_INSTALLED W8 files=48 written=48 retired=72 removed=72`；再次 `--check` `BATCH_CHECK_OK W8 files=48 retired=72`；再装一次 `written=0`。整套模式 `INSTALLED written=0 removed=0`，五套清单（`install_manifests/<主题>.json`）各自换成 185 个 id（去掉三张、加上四张，所选版本与 W8 清单相同）；`--check` `INSTALL_CHECK_OK`。

装之前核对过：48 个源文件的 SHA-256 与 W8 清单一致；24 张插画所选版本都等于“带有效记录的最新版本”；72 个待删文件都存在；48 个目标都是新文件。

### 1.2 Unity 导入与绑定

都用批处理（`-batchmode -quit`），日志只抽汇总行：

| 入口 | 结果 |
| --- | --- |
| `V09Setup.Integrate` | `ART09_CONTENT_INTEGRATED illustrations=185 reimported=4`：四张新默认插画按 v0.71 的默认插画规则（精灵、不压缩、上限 2048、无 mipmap）重导入；`Bootstrap.unity` 与 `ArtStyleLab.unity` 的 `GameApp.contentIcons` 按配置顺序重建为 185 张 |
| `V22iArtSetup.Integrate`（内含 `V13CardSetup.BindCompactIcons`） | `V22I_ART_INTEGRATED themed=990 defaultEmblems=7 reimported=0`（5 套 ×（185 插画 + 7 徽记 + 6 战利品），新图在导入时已经由 `V11ThemeImport` 的主题规则设好）；`V13_COMPACT_ICONS_BOUND comic=185 deco=185 default=185 glass=185 sancai=185 swiss=185 reimported=0`（新图标在导入时已由 `V13IconImport` 设好） |

导入设置核对（脚本把每个新 `.meta` 去掉 guid、内部 id、名字后与同目录的 `C01` 比）：48 个里 45 个逐行相同；另 3 个只差精灵的宽高，就是三张非正方形原图：swiss C154（1326×1187）、sancai I13 与 glass I13（1312×1199）。它们按 W22i 的做法处理：导入规则不变（上限 1024），裁切表里记 `imageSize`，图标按方框烘焙（swiss C154、sancai I13 用 `pad` 补边）。按 1024 上限缩放后 swiss C154 是 1024×917，边长不是 4 的倍数，Unity 会保持未压缩（RGBA32），与 W22i 发现 1 的 49 张同类；1312×1199 缩成 1024×936，可以压缩。这一点由 EditMode `W22i_EveryInstalledPictureIsImportedLikeTheSwissTrialImages` 逐张核对并在输出里列出。

悬空引用：删除前从 `be058fe1` 取出 36 个旧 `.meta` 的 guid，在 `Assets` 与 `ProjectSettings` 的场景、Prefab、资源、`.meta`、JSON 里搜。绑定前 24 处（`Bootstrap.unity` 的 `contentIcons` 3 处与 `compactIcons13` 18 处，`ArtStyleLab.unity` 的 `contentIcons` 3 处）；两次绑定之后 0 处。

### 1.3 裁切表与溯源表

- W8 的 15 个覆写方框（`w8-install.json` 的 `cropOverrides`）并进 `tools/art_v11/crops/make_crops.py` 的 `OVERRIDES`，数值与理由逐字相同（脚本核对 0 处不同）。每套覆写数：默认 3、瑞士 3、唐三彩 9、漫画 2、黑金 8、彩绘玻璃 3，都在测试要求的 10 个以内。
- 六套按 W22i 的命令重跑 `make_crops.py --theme <主题> --source <插画目录> --out Resources/IconCrops/<主题>.json --icons-dir Art/Art11/Icons`（没有写审查图，W8 已经做过）：每套 185 条。裁切表的差异只有去掉 C90、C128、C145 三行、加上四行新内容；新加的 24 条与 W8 的裁切记录（`artifacts/cache/v111/art/crops/<主题>.json`）逐字段相同。重烘的 1,110 个图标里，原有 1,086 个与提交里的逐字节相同（git 无改动），新的 24 个与 W8 交付的逐字节相同（批次 `--check` 通过）。
- 溯源：三张已移除卡的历史在 `ArtSource/Art11/subjects.json` 的 `notes` 里（W8 已写），本包没有改。

### 1.4 钉着“184”的断言

全仓搜过 `184`（C#、Python）。改写的见 [TEST_CHANGES.md](TEST_CHANGES.md) W9a 一节：两个 Python 测试文件、EditMode `W22iArtImportTests`、PlayMode `GameplayPlayModeTests`、`W22iArtInstallPlayModeTests`、`V13CardTemplatePlayModeTests`（只是信息文字）。现行内容数从配置数得：卡牌 150、道具 13、服务 11、骰面改造 11，共 185，与 `activeContentCount` 和 `ConfigValidation` 的 185 一致。其余搜到的 `184` 是版式坐标或旧版本工具（`tools/v10_config.py`），不是内容数。

## 2. 字体图集重烘

按 W21 的流程用现有入口 `V21TitleFontSetup.BuildAll`（批处理）：它先跑 `V071FontSetup.BuildBold`（按 `bold_chars.txt` 扩展粗体，保留已有字形的排布并核对常规体图集字节不变），再按 `title_chars.txt` 烘四套标题字体（三彩、漫画、黑金、彩绘玻璃；瑞士用默认字体），每套独立烘两次、比对字形表与图集像素一致后才保存（保留 guid 与子资源的 id）。字表是 W7a、W7b 已重生成的现行文件，本包没有改：标题字表 676 字（含 W7a 的“匙反意绝转钥”与 W7b 的“撤自”），粗体字表 1077 字（含 W7a 的“做偿匙反惩管纪罚钥零”与 W7b 的“悔”）。

日志汇总行：

- `V071_FONT_READY created=False bindingChanged=False required=1077 boldCharacters=1082 atlasCount=1 boldEllipsis=True regularAtlasUnchanged=True`
- `W21_SANCAI chars=661`、`W21_COMIC chars=662`、`W21_DECO chars=661`、`W21_GLASS chars=676`，都是一张 2048² 图集（三彩、黑金缺的 15 个符号、漫画缺的 14 个按 W21 的预期回落到粗体；彩绘玻璃不缺）。

改动的文件：`Game/Fonts/DiceChineseBold.asset`、`Resources/Themes/{sancai,comic,deco,glass}/Fonts/title.asset`。`DiceChineseFallback.asset`、`TMP Settings.asset` 没有变。`V21TitleFontAssetTests`（含 `FW02`、`Q5`）在第 3 项的 EditMode 全量里跑。

## 3. EditMode

## 4. PlayMode

## 5. 看图结论

## 6. 留给协调者决定的事

## 7. (c) 类问题（规则或会话层）

## 8. Unity 自动改写并被还原的文件
