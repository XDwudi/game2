# v0.71 中文 Bold 字型生产记录

此包修复实际 TMP 包对 Bold Ellipsis 无普通字型回退的缺口，使用同一 Noto 黑体家族的真实粗体。依据 `docs/specs/v071/ART_SPEC.md` 字体规范和 `cache/v071/tmp-ellipsis-investigation.md`；不改变文本内容、玩法、价格或字号标准。

## 已完成的源交付

- 原系统源：`C:/Windows/Fonts/Noto Sans SC Bold (TrueType).otf`，不修改原文件。
- 可追溯源：本目录 `NotoSansSC-Bold.otf`；生产导入副本：`DicePrototype/Assets/Game/Fonts/NotoSansSC-Bold.otf`。
- 两副本实际均8,716,392字节，SHA256 `d1961be1161ea1be08496c920862d06ea5c23a757628f4fd69368de1d9f51bed`。
- `SOURCE.json` 保存真实元数据、来源、许可、SHA、配方和覆盖。fontTools实际核对当前Regular的844个Unicode全部可用，含U+2026。`LICENSE.txt` 为OFL1.1全文；`COPYRIGHT.txt` 保留字体自身版权/商标信息。
- Bold为Noto Sans SC2.002，现有Regular为Noto Sans CJK SC2.004；不是相同源版本，需实际比较字宽与轮廓。没有声称系统字型已经由Unity成功导入。

## 可复现生成入口

新工具：`DicePrototype/Assets/Game/Editor/V071FontSetup.cs`。在Unity停止Play且完成编译后调用：

```csharp
DiceCraft.Editor.V071FontSetup.BuildBold();
```

也可使用菜单 `DiceCraft/Art071/Build and bind real Chinese Bold font`。仅生成 `Assets/Game/Fonts/DiceChineseBold.asset`，为现有 `DiceChinese.asset` 的 `fontWeightTable[7].regularTypeface` 绑定真实Bold字型；不改场景或UiKit。

配方：从Regular.characterTable取得当前全部去重Unicode，按48pt、padding7、SDFAA、2048×2048烘焙；完成后Static。先在内存验证全字集，再建立资产和材质/atlas子资产。已有良好Bold资产复用，不删除重建；若源、配方或字集不匹配会拒绝并保留原件。已有不同Bold映射不会被覆盖。

工具同时验证实际Bold省略号API返回新字型、Regular查询仍返回旧字型，前后Regular atlas原始字节SHA/字形表/字表不变，其余字重和全部Italic槽保持。只保存两个受影响字体资产，不全局SaveAssets。

## 待 root 实际验证

本制作代理没有运行Unity，也没有生成TMP资产或.meta。root独占引擎导入、执行和保存；应检查首轮及重复调用日志、源字集覆盖、资产完整引用、正常与150%字号的粗体卡名/省略号、Normal/Bold池复用、原FacePool告警用例及相关glyph检查。不得把本包源码/文件SHA检查提升为字体外观或整版通过。
