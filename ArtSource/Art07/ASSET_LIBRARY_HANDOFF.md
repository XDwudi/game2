# art0.7.0 可复用资产库交接

当前状态为 IMPLEMENTING / 待实机与独立复核。权威输入是 `docs/specs/v07/dice_art_v0_7_manifest.json`，不得把本生成器或文件存在清单解释成美术通过。

## 执行与所有权

由 root 独占 Unity 执行 `DiceCraft.Editor.Art07AssetLibrary.Build()`。我未启动 Unity；root 的 `Review/library-build-20261031-173524.json` 证明首轮实际生成成功。之后补入完整/紧凑卡切换、附魔角标、爆发等级、收据指定转盘停位和池复位，需要重新执行后绑定新证明。最终编译日志为 `Review/library-compile-final.log`，是当前安装 Unity 6000.6.7f1 程序集上的独立 .NET 类型/API 编译，0 错误；45 条警告仅为检查工程同时引用现有 Runtime 的同名新类、JsonUtility 注入字段的 CS0436/CS0649，不等于引擎实际测试。

仅新增这三个代码文件：

- `DicePrototype/Assets/Game/Editor/Art07AssetLibrary.cs`
- `DicePrototype/Assets/Game/Runtime/Presentation/Art07PrefabView.cs`
- `DicePrototype/Assets/Game/Runtime/Shaders/Art07LocalFx.shader`

生成时仅修改资产、导入设置及 SpritePackerMode=SpriteAtlasV2，在临时预览场景构造预制体后销毁，不打开或保存业务场景。没有菜单命令或自动全量构建钩子；预构建钩子只幂等校对三张图集。没有新增玩法、Core 依赖、规则 RNG 或存档逻辑。

## 实际产物

`Build()` 创建 7 个指定材质、10 个有曲线的 AnimationClip、26 个含真实图形/文字/交互或网格的 Prefab、3 个 SpriteAtlas V2、独立数字 TMP 和 5 个配置 JSON。数字字体使用现有 Noto 源生成静态 512 图集并回退现有 CJK TMP；不复制中文字体源和许可证。五个骰子动画从真实 FBX 曲线取数并按清单精确重定时，五个 UI 动画直接保留可编辑曲线。

每次实际成功输出 `Review/library-build-UTC.json`，含 Unity 版本、数目、产物路径，以及生成器、视图、shader、原清单的 SHA256。`runtimeVerified=false` 是有意保留的边界。脚本重复执行保存原有 .meta/GUID。六个额外 `default_face_N.asset` 是骰子预制体可见的 1..6 默认点面，颜色沿源 face_map 的 1/4 红、其他蓝，供实例绑定真实数据前检视；它们不增加清单完成数量。

图集内核 UI 和内容图标用 Point；四个像素 FX（方屑、星、卡裂、短焰）归 UI pixel，三种柔和线/环/光晕归 soft Bilinear。均无 mipmap、无 rotation/tight packing、padding4、alpha dilation、最大2048、内置交付。`.spriteatlasv2` 与原 `.spriteatlas` 路径通过 `path_mappings.json` 显式映射。

## 运行接入契约

root 应把真实 Prefab 引用写入现有运行库/Bootstrap 的序列化引用中。`art07_resource_map.json` 用于 StableID→路径/GUID 检查，**不是 Resources.Load 路径**。未接引用的视图不会自动替换现有动态 UI；文件生成也不能冒充已经接入游戏。

UI Prefab 放入已有 ScreenSpace Canvas 下，不另建 Canvas 或 EventSystem。调用 `Bind(instanceId, heading, exactValue, description, picture)`，文本使用收据的精确字符串；`SetField` 支持计分条 base/hand/color/redGroup/controllers/enchantments；`SetFraction` 只接受已经算好的显示比例。交互使用 `BindAction`，它清除上次回调，业务行为仍由调用方提交。

控制器卡使用 `SetCompact(false/true)` 在 160×224 与 208×104 之间切换，标题/当前值/图标由同一 Bind 同步；`BindBadge` 同步 32/28 尺寸附魔角标。完整技能长文仍由 tooltip/modal 的动态文字显示。文字缩放和小屏重排由现有 UI 主流程负责，需独立验证；不能因这个固定设计尺寸母版存在而认定 720p/150% 已通过。

骰子 `BindDie` 的六个 Mesh 是局部 XY 平面点面，分别位于已建立的 F01..F06 子锚点，不得传入已经整体旋转的世界空间网格。结果方向写入 `FaceOrientation`，外部滚动只操作 `RollPivot`。默认母骰与六个实际网格已有材质，非空槽壳。原 FBX 动画包含根和其他静态 TRS 曲线；必须沿现有 DiceTable 的无 Renderer 采样 rig 获取 RollPivot 增量，**不可直接 SampleAnimation 到游戏中的结果朝向根节点**，以免覆盖收据姿态。

FX `autoTick=true` 时使用 unscaledDeltaTime。接入共用 PresentationClock 时设置 `autoTick=false`，每帧调用 `Evaluate(normalized)`，到点调用 `Complete()`。`SetPaused`、`SetReducedMotion`、`SetParticleBudget` 不触及规则。调用前先放置实例，再 Bind，再设置细项并 Play；移动目标为 Visual 子层的局部坐标，UI 卡根位置不被改动。连接线 `SetLink` 接受该 Visual 局部空间的起终点，跨行传 outsideX 创建外侧折线，箭头停在终点。

`SetRevealSlice(0..8)` 仅接受调用者从已提交收据映射的九扇区索引，显示 `exactValue` 才是权威 q；没有二次抽取。`SetBurstLevel(0..5)` 接受已经根据精确有理数选好的最高等级，配置 180/260/340/420/520/600ms、0/4/8/16/24/36 粒子及金环/扫光/短焰分层。它不判定倍率或达标；zero-score强制L0、每手最高一次、桌面抖动与数字缩放由外部现有调度器处理。减少动态取消粒子/位置/缩放/旋转，只留对应层级静态符号与文本。

回收到池时调用 `ResetForPool()`：清文本/ID/点击监听、恢复颜色/默认图标/默认点面材质与方向、清角标/连接、恢复全卡尺寸、停止计时。池规模和并发预算归既有表现调度器；本视图不会自己扩容或无上限实例化。

## 仍需 root 真实验证

- 重新 Build 后核对 26/7/10/3、5 JSON 与最终 generator/view SHA；重跑 `tools/art_v07/inventory_art.py`。当前文件清单已有 222/223 required，剩下 mixer；这仍只是文件存在。
- Unity 实际载入每个 Prefab，检查无缺失脚本、材质/shader 编译、图集像素/软边、TMP 字符与 full/compact 的布局。
- 真实池复用：A→B来源、带图标→默认图标、减动态→正常、L5→L0、折线→直线、Pause→Resume，确认无残留 ID/文本/颜色/监听/粒子。
- UI 完整/紧凑、32/28角标、720p/150%文字，真实按钮命中区域。当前新母版尚无此类截图。
- 动画与声音同步、掷骰停止后朝向、玻璃可读性、G147只一次L5且不出现达标章、最大场景预算与目标平台实际运行。
- Mixer 由 root 桌面操作创建，不在本生成器中写空壳。现有 C07 虚线复制目标、C42纸卡/风险色严格 motif 复核仍待独立结论，沿 `HANDOFF.md` 保留。

本文件不代签美术 ACCEPTED、性能或 Mac 结果。
