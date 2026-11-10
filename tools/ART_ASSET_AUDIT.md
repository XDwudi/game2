# PNG 美术资源完整性检查

`art_asset_audit.py` 是只读辅助工具，复用 `ArtSource/Art071/Illustrations/manifest.json` 的已有结构。它不生成、修改、裁切或补哈希，也不作风格评分和美术验收。

在仓库根目录运行（Python 3.10+，Pillow；本次验证为 Python 3.11.4 / Pillow 9.4.0）：

```powershell
python tools/art_asset_audit.py --manifest ArtSource/Art071/Illustrations/manifest.json --require-alpha --runtime-dir DicePrototype/Assets/Art/Art071/Illustrations --output evidence/ai-art-pipeline/checker/illustrations-current.json
```

`--root` 默认为脚本所在仓库；输入相对路径相对于这个 root。`--output` 必填，必须在 root 内且尚不存在，重跑请使用新文件名。不会覆盖任何输入或旧报告。`--runtime-dir` 可选，仅用于导出件与源 PNG 应逐字节相同的流程；经过压缩或其他正规转换的导出须用另一个已有导出清单核对，不能用改哈希让此检查通过。

工具读取 `inputSpec` / `inputSpecSha256`、`contentSource` / `contentSourceSha256`、`references[].path/sha256`，以及非空 `assets[].contentId/path/sha256/dimensions`。清单内路径均为仓库相对路径；历史 `source` 字段可能是机器外部原图路径，仅作为历史元数据保留，本工具不访问它。`references` 可为空数组。多余字段不修改也不解释；`status` 的历史措辞不构成当前状态，实际阶段仍查 `docs/STATE.md` 和 `ASSET_MAP.md`。

检查与结果：

- SHA256、文件存在与路径边界、重复 ID/文件、PNG 真实格式/完整解码和尺寸。
- `contentId` 仅检查非空与唯一；配置文件只核对 SHA，ID 是否存在、名称/题材是否与规则相符仍须人工或另一个语义检查核对。
- 输出 alpha 极值、全透明/半透明/不透明像素数、非零 alpha 包围盒。全透明图始终失败；仅加 `--require-alpha` 时拒绝全不透明图。透明比例不设审美阈值，少量透明像素也不能证明背景干净。
- 指定 runtime 目录时检查同名 PNG 逐字节一致、非空 `.meta` 存在并记录哈希。它不解析 Unity importer、GUID 或场景引用。
- 退出 0 = `INTEGRITY_PASSED`；退出 1 = 检查失败且报告已写入；退出 2 = CLI/输出路径错误。依赖缺失导致 Python 导入失败亦为非零，不能算检查通过。

每份报告绑定输入清单、工具、输入文件和实际 PNG 的哈希，以及工具版本和 UTC 时间。报告中的 `notVerified` 明确保留风格、题材、边缘光晕、缩小可读性、Unity 导入与实际画面、可编辑源重开、构建性能、人工批准和权利核查等边界。不得把完整性通过写成游戏/艺术 `ACCEPTED`。

修改此工具后运行项目要求的全套确定性检查：

```powershell
python -m unittest discover -s tests -v
```

合成负例测试不证明生产美术质量；真实四图报告另行保存在 `evidence/ai-art-pipeline/checker/`。
