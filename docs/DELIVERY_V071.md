# 骰子构筑 v0.71 本地交付候选

**当前是表现层开发候选，最终完整回归、Windows/macOS 双构建和 Windows Player 运行仍待执行。** 本说明没有新增下载地址；历史 v0.7 发布包不是本版产物，不以历史通过数为本版放行。

发行身份为 `p0.7.1`、美术 `art0.7.1`，玩法配置保持 `p0.6.0`、存档 `schema4`。目标 Windows x64 与 macOS Universal，Unity 6000.6.7f1 / Mono。当前进度以 [STATE](STATE.md)、[实施契约](tasks/P0-V071-IMPLEMENTATION.md) 和 [美术规格](specs/v071/ART_SPEC.md) 为准。

## 本轮内容

已接入首批 C48「有也不交」、C29「简单顺子」、C03「首当其冲」和 I07「增加投掷」四张主题插画；C29 是附件透明样件原件，其余三张为内建 imagegen 生成原图。源图、提示词和引用都保留。**其余 67 项仍使用旧图标，未宣称 71 项全量美术生产完成。**

公共纸卡、按钮与信息层使用统一组件，五个新九宫格表面文件保留可重跑源；其中三个文件共用同一图像字节，不能按文件数计作三张独立绘制资产。新陶瓷骰子母模型保留 Blender 源和 FBX；精确点阵、颜色、价格、名称与状态仍由引擎按原数据生成，不取生成图内的装饰为规则。

页面改造使用同一套组件覆盖对局、商店、手册、菜单、选关、结算与设置。A01—A18 的 [素材路径表](specs/v071/ASSET_MAP.md) 明确哪些新制、哪些复用、哪些由代码构成，并不表示 18 类都另制了独立图片。原有点数/颜色/售价、锁序、存档与 RNG 规则保持；基金控制器仍保留实际基金售价例外。

## 当前产物与证据

| 项目 | 位置 | 当前结论 |
| --- | --- | --- |
| 插画源、参数与生成记录 | `ArtSource/Art071/Illustrations/` | 4 张源/运行导出及稳定 ID 已接入；人工风格确认另行进行 |
| 公共表面与骰子源 | `ArtSource/Art071/UI/`、`ArtSource/Art071/Dice/` | 可编辑/可重跑源保留；Blender 原有 26 项保存重开/回导记录不替代引擎最终外观 |
| 普通入口与隔离样板 | `DicePrototype/Assets/Game/Scenes/Bootstrap.unity`、`ArtStyleLab.unity` | 共用同一素材；Lab 使用独立 cache 存档，生产 BuildProfile 仅引用 Bootstrap |
| 开发中页面截图 | `cache/v071/screens-1080/` | 已实际捕获 48 张 1920×1080 Editor 图；反映其报告 MVID，修复后最终新图仍待绑定 |
| 样板连续片段 | `cache/v071/recording-01/recording-vfr-lossless.mp4` | 122 帧；实际时间戳 VFR，movie 时长 13.048892 秒，逐帧解码 RGB 与原 PNG 相同；不是 60fps 性能证据 |
| 最终完整 Unity 回归 | `evidence/v071/unity/` | 待最终源冻结后的实际 Edit/Play 全量报告、发现清单、PDB 与 gate；不填写历史通过数 |
| Windows x64 | `Builds/v071/Windows/DicePrototype.exe` | 最终构建及实际启动待执行 |
| macOS Universal | `Builds/v071/macOS/DicePrototype.app` | 最终交叉构建待执行；M5/Intel 原生运行单独验收 |
| 两平台包与 M5 验证工具包 | `Releases/p0.7.1/` 或 `review-candidate/` | 版本工具已准备，最终封包/大小/SHA/分发待实际执行 |

现有截图、录制和离线工具检查只证明各自范围；不能替代最终新构建的运行、性能或交互验证。特别是初次页面截图的内嵌 BuildIdentity 仍为历史身份，应以其实际 artVersion、Runtime/Core MVID、配置和 PNG SHA 作为开发证据绑定，不能冒称最终 p0.7.1 Player 证据。

## 使用与已知边界

完整解压后启动 Windows `DicePrototype.exe` 或 Mac `.app`；不要拆走 Data/DLL。默认操作为 R 投掷、点选或 1—9/0 待选、L 确认锁定、Enter 提交、Backspace 清除待选、H 手册、Esc 暂停/关闭最上层。修改过的键位以设置页为准。完整操作、路径与复现见 [工程 README](../DicePrototype/README.md)。

ArtStyleLab 是可操作样板，使用 `cache/v071/lab-saves/<时间戳>/`；不能当作普通挑战存档、正常玩家通关或性能样本。真实录制覆盖悬停、确认锁定、提交、重投与待选，后续源修改使相应旧证据需要重验。

1080p 初次复核发现手册/研习小例骰误用纸卡边框影响点阵读数；修复已提出并实施，最终新图尚待复核。当前不将旧图标数量或页面存在作为风格验收通过。最大正常容量与超常 Debug32 区分：既有 Debug32 对局 HUD 只提供前五道具入口，其余仍可从组合试验库存页管理，不是数据丢失，本轮不扩展库存系统。

历史 Windows 性能/原生独占、AudioMixer、旧公共资源原生补证、Mac 与人工风格/听审/体验缺口仍保留。若本版实际运行再次失败，应保留原报告；deferred、native-only 或独立恢复的局部成功不能拼成 strict 通过。Windows 交叉构建不能替代 Mac 实机，当前没有移动端验收结论。

## 最终封包时更新

协调者需用真实结果补入源提交、BuildIdentity、两平台构建 GUID、完整回归报告、Windows 实际启动范围、包大小与 SHA，并链接修复后实际图片和独立复核。未执行项继续写“待验”，失败保留明确失败；人工风格与发布决定不由本说明自动批准。当前整版未 ACCEPTED。
