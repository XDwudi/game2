# v0.7 钱包返工与研习颜色路由只读复核

最新状态（NUnit044648 / 045449）：720p及1080p各九种钱包状态的独立实图复核通过，NUI01在两分辨率所测范围关闭。原失败保留，非整版 ACCEPTED。本代理未参与钱包实现，可给独立技术/实图观察，但本记录不代替人工风格或整版验收。仅写本报告与bindings，未改Assets、Editor、规则或标准。

## 钱包候选源检查

输入 `artifacts/cache/v07-wallet-rework/`，对应 art0.7.0 / p0.6.0 schema4。候选manifest四项SHA全部一致；生产 `GameApp.Art07Wallet.cs`、`Art07WalletPlayModeTests.cs` 与候选逐字节一致。

方案实际做了三件事：金币单位拆为独立MoneyUnit；金额保持38×字号、完整十进制、不自动缩字/换行/省略；利息下移到独立104ref高两行区。仅超过普通284ref宽时使用304ref数字轨道和必要字距（0.25步，最低−4），每次重画先恢复字距0。没有金额解释、购买价、C01/C41源数或存档规则变化。

静态坐标可容纳：150%金额行盒约82.54ref，分配84；利息两行以21×150%分配104，文本框止于主画面y310，导航y316开始。Brand/货币/金额/利息需以实际glyph ink核查，不能仅凭这些估算判通过。十位数轨道需要真实最小字间隙≥0.5物理像素及GPU独立验证；候选没有缩减字体或修改原阈值。

测试覆盖0、1000010、1000000000三值×100/125/150，共9种/分辨率；使用合法隔离Debug商店，C41+C01实际利息源2，金额/单位/利息完整字形、相邻digit ink、导航、真实GPU差分、页签读操作与state/save一致均有断言。适用视口1920×1080与1280×720，必须分别实际执行。

## 已发生的真实失败与根因校正

- NUnit043900钱包用例在第一次点击研习页签时 `Art07ProductCard:165` 的First抛 `Sequence contains no matching element`；未完成9状态。
- NUnit044120加入FirstOrDefault默认0后，第一次点击同页签在 `Art07ProductCard:167` 的 `Config.Hand(all_blue)` 抛 `Unknown configured ID: all_blue`；仍未完成9状态。
- 该问题不应解释为“未购买升级在Run中省略等级”。Core `GameSession.NewRun` :202–203创建全部牌型与颜色的0级条目；`GameSession.Validation` :48–54要求完整且唯一的配置数量，Debug已提交Run也如此。Core商店的Level及购买/自选升级使用完整数组。
- 真实错误是UI把颜色商品误当牌型：Core `GameSession.Shop:112` 对 `StudyIds`（包含handDefinitions及colorDefinitions）统一生成category=`hand_upgrade`；UI `Art07Shop:164`仅用category=`color_upgrade`识别颜色。于是合法all_blue/all_red读错handLevels，第一次抛First；默认0只掩盖第一处，下一步仍错取Hand配置。
- 正确工程修复应由UI按Config.colorDefinitions/contentId识别颜色，分流等级表与倍率定义，不改Core或Run完整性。root已确认按这个根因修并删除错误注释。`ShowStudy`本来按all_blue/all_red分流，完整Run下First合法；没有证据要求额外修改它。
- ScoreCalculator快照与Handbook只读输入确实允许未列等级默认0；这与Run完整性不是同一契约，不能把前者推广到商店状态。

## 先前失败批次的有限画面（历史）

已打开044120失败批次唯一钱包截图：`C:/Users/f/AppData/Local/Temp/XDwudi/DiceCraft P0/DiceCraftTests_b7a078900cae42608979ec1bcba5e142/extra-capture/000-wallet-100-0.png`（1920×1080、100%）。可见“金币”单独在顶部右侧、金额0在下一行；利息“每10金币利息+3 / 计息本金上限50”完整且与导航有间隔。这个单帧没有原NUI01大数条件，不足以关闭7位125/150或10亿缺陷。

以上历史阶段尚未有新的720钱包/大数通过证据；原NUI01四张失败图与NEW_UI_REVIEW.md保持不变。以下为随后实际044648批次，不以新通过覆盖原失败记录。

## 044648：720p独立实图复核

实际报告 `evidence/v07/unity/nunit-20261101-044648.xml` 三项3/3，其中钱包用例独立完成44.58秒、9种状态及实际页签0→4读操作。Toast两项由其他最终复核者评价，本代理不签自己参与实施的长Toast质量。

精确原目录：`C:/Users/f/AppData/Local/Temp/XDwudi/DiceCraft P0/DiceCraftTests_df511a95a3ac47209bfde8a32e508929/extra-capture`。9张PNG均已逐张原尺寸打开；PNG、geometry、sidecar、报告与源快照逐字节归档到 `artifacts/cache/v07-wallet-review-720-044648/`。所有PNG SHA与sidecar一致，state/save before-after均一致。

| 字号 | 对应原PNG编号 / 金额 | 实图制作观察 | 多位金额最小相邻ink间隙 |
|---|---|---|---|
|100%|000=0、001=1000010、002=1000000000|金额完整单行，单位在独立顶部右列；两行利息与金额、设置/手册均留有清楚空隙。十位数未贴边。|七位2.515px、十位2.515px|
|125%|003=0、004=1000010、005=1000000000|原七位数最后“币”压利息问题已消失，金额和单位没有换行。十位数可逐位辨认，两行利息完整。|七位3.144px、十位3.144px|
|150%|006=0、007=1000010、008=1000000000|原“金币”压两行利息问题已消失；10亿使用宽轨道，数字仍完整并有分明字缝，未粘连，左/右均未越框。品牌与金币单位也有明确横向空隙。|七位3.773px、十位2.823px|

金额字体实际为38/47.5/57参考像素，没有自动缩字。仅十位150% tracking=-2.5，其他样本0；独立重读geometry确认每个金额实际渲染1/7/10字，完整等于数值字符串。Brand4字、MoneyUnit2字、Interest17个非空字符均无geometry错误；四类ink矩形再独立两两检查不相交，最低相邻数字间隙高于原0.5px阈值。数字0只有一个字符，报告间隙0是无相邻字符，不代表失败或将阈值放宽。

实际运行 C41+C01产生两个C01源，显示“每10金币利息+3 / 计息本金上限50”，全文和导航未碰撞。源检查确认颜色升级已按Config.colorDefinitions的contentId分流，并保持完整Run等级First契约；本批点击四页签未再抛all_blue异常。九张截图在道具页，不据此签署颜色升级卡具体倍率排版的独立实图通过。

绑定环境：Unity6000.6.7f1 / WindowsEditor / D3D11 / RTX3080 /1280×720。Runtime MVID `7c581c97-44f9-49fa-b764-2daaf941fd99`，测试MVID `96ce2763-cb35-44a0-96dd-9bfbd586c118`，录制源快照SHA `452aadfdb4a741580fb232a0cd84993d74a8479cab8d76493b5affab20b0ba7a`。这些是本批真实工作树绑定，不冒充BuildIdentity中历史发行commit。

独立结论：钱包NUI01在720p、0/1000010/10亿和100/125/150%范围通过；未发现新的钱包制作缺陷。1080p同源图、实际Player、Mac与人工美术仍待验。后续源码变更使受影响证据重新待复核。docs/STATE由root统一合并，本报告不改总发布状态。


## 045449：1080p追加独立实图复核

实际报告 `evidence/v07/unity/nunit-20261101-045449.xml` 共13项13/13。钱包用例完成0、1000010、1000000000 ×100/125/150%的九种状态；本代理逐张打开全部九张原尺寸PNG，独立重读PNG SHA、sidecar和glyph geometry，没有以测试计数代替审图。

精确原目录：`C:/Users/f/AppData/Local/Temp/XDwudi/DiceCraft P0/DiceCraftTests_b5efae3ce5e142b68af56080eba5f7c1/extra-capture`，逐字节归档 `artifacts/cache/v07-wallet-post-review-1080-045449/wallet/`。九图金额都完整单行，金币单位独立在右上，利息两行“每10金币利息+3／计息本金上限50”与金额、导航均无碰撞。150%十位数轨道稍宽但仍在左栏内，数字之间保持可辨字缝；未发现新的钱包制作缺陷。

| 字号 | 原PNG编号（0／1000010／10亿） | 七位最小ink间隙 | 十位最小ink间隙 |
|---|---|---:|---:|
|100%|000／001／002|3.773px|3.773px|
|125%|003／004／005|4.716px|4.716px|
|150%|006／007／008|5.659px|4.234px|

实际金额字体保持38／47.5／57参考像素，仅十位150% tracking=-2.5，其余0；每个金额实际渲染1／7／10字符，无省略或截断。独立检查所有四类标签ink两两不相交、完整字符数量、SHA、state/save/pending before-after一致，均满足原条件。单字符0的相邻间隙字段0仍按不适用解释，不删除0.5px多字符阈值。

环境为Unity6000.6.7f1 / WindowsEditor / Direct3D11 / RTX3080 /1920×1080。Runtime MVID `a6f0330c-52f3-428b-8bb0-877c4b30e971`、测试MVID `bb7831e6-a596-4d76-bada-4af5362989df`、源快照SHA `9bc394f5963e5a946113444b899a1883dadf95c812f1fc3efd924120eb0bb1d0`。NUnit绑定起止源码与程序集相同；不是Player发行版本证据。

当前独立结论：钱包NUI01在720p和1080p、0/1000010/10亿、100/125/150%所测范围关闭。历史失败及其根因记录保留；实际Player、Mac、人工美术及整版验收仍未完成。本批Toast由其他独立复核者负责，本代理不签自己实施的长Toast质量。POST另有制作缺陷，见POST_REWORK_REVIEW.md；不影响这里针对商店钱包的限域结论。
