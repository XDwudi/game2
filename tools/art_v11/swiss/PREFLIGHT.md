# W12 瑞士现代主义 UI kit 准备记录

开工记录：READY → IMPLEMENTING，2026-11-16。实施：主 agent；调用调查：tint_review；独立视觉复核：visual_review；测试编写：tests 子 agent。最终状态见 README：VERIFYING → REVIEW，文件锁释放。

- 输入：本轮用户完整任务、DEV_PLAN W12 / §4.6、THEME_SPEC、theme_contract.json（86 件，引用 49 件）、UiKit 与调用方；旧美术风格仅作技术背景，最新用户指定的瑞士方向优先。
- 范围：49 件 UI、桌面、纸纹平铺、预览、默认对照图、生成脚本、中文说明与测试。无玩法、插画、3D、C#、导入设置修改；色板仅建议。
- 写入所有权：主 agent 写 tools/art_v11/swiss 与三处获准产物路径；tests 子 agent 仅写 tests/test_art_v11_swiss.py；复核者只读。无 Unity / Blender 锁。
- 工具实测：E:/anaconda3/python.exe，Pillow 9.4.0；49 件默认 PNG 全部存在。依赖字体来自本仓库。采用 4 倍矢量绘制 + LANCZOS，无图像生成。
- R1 历史缺口：当时五张概念图缺失；R2 已由协调者归档到 `ArtSource/StyleKit/themes/swiss/concepts/`，本轮在改动前实际打开 roll / shop / select / card / triptych 五图。旧说明与证据保存在 `ArtSource/Art11/swiss/history/r1/`。
- 调用风险：多数界面底图承载浅字，保留深色中心；中性贴图不能烘焙红色；fx_score_plate 额外乘 Blue/Gold、refresh 额外乘 Ink。以只读调用审查确定。
- 跨系统场景：切换主题 → 同名精灵加载 → 复制默认 border/PPU → Image.color 相乘 → Sliced 拉伸。代表件覆盖纸面、深面、空心框和仅横向切片；不修改任何运行时接口。
- 运行环境：Windows 离线 Python。用户明确 Unity 由协调者导入检查，实机和人工风格验收在交接时保留待验。
- 用户本轮禁止修改允许清单以外文件，优先于通用 STATE 更新条款；状态和交接记录放在本目录 README 中，由协调者合并 STATE。

| 条件 | 方法、场景与期望 | 证据 | 验收责任 |
| --- | --- | --- | --- |
| W12-01 | 契约全部 49 件同尺寸 RGBA，无全透明，strict 审计无缺件 | audit 日志、单测 | 独立复核 |
| W12-02 | 两次完整构建每个 PNG 字节相同；与交付件一致 | 单测 | 独立复核 |
| W12-03 | 用户列出的 17 件中性资源可见像素 HSV 平均饱和度 <8/255；刷新图标也保持中性 | 单测 | 独立复核 |
| W12-04 | 九宫格中心平坦，框线位于边距内；横向条中央列一致 | 单测、对照图与拉伸图 | 独立复核 |
| W12-05 | 桌面 RGB 1920×1080、低对比中央区；平铺 1024² 无缝；预览 480×270 | 单测、目检 | 独立复核 |
| W12-06 | 23 token 建议；文字对比 4.5 / 4.5 / 3 | README 测量 | 独立复核 |
| W12-07 | 旧新 Python 测试全量通过；不越界写入 | unittest 日志、最终文件核对 | 独立复核 |
| F-THEME-02 / AX-08 | 真实 1080p / 720p、乘色、骰点、字体可读性 | 本轮未运行 | 协调者导入、人工判定 |

## R2 协调者返修准备

- 依据：本轮六项反馈、五张已归档参考及其 `ArtSource/StyleKit/themes/README.md` SHA 清单。最新明确要求小切角与真正的扁平骰影，替代 R1 的方角／无附加投影解释，无需重复确认。
- 范围与所有者保持不变；新增独立回归测试由 tests 子 agent 编写，visual_review 只读复核。README 的整理交给 tint_review，其唯一写入路径为该文件。
- 纸面规则线统一为 512 图上的 10 px，切角 16 px；沿 PPU200 在参考 UI 下为 5 px 线、8 px 切角。框线、红角标均不越过 40 px 边带。
- 深色面板保留浅字兼容；无上下边距的横条仅在左右端帽切角，中央整列平坦。卡名、插画、短文、稀有度与价格仍由代码绘制。
- 桌面保留 x360–1500/y330–950 的纸色中央；参考图中穿越骰池的大红面向下移，保留上扬斜向关系。
- 风险检查增加奶油底上的实际粗边、单级斜角、阴影面积与平坦 alpha；实际锁图标落在深色 Background 上，黑红图形增加纸白载体并在 22×24 尺寸检查对比度。
- 验收环境仍是 Python 与离线图片，Unity / 720p / 1080p 游戏截图由协调者完成。R2 测试与评审记录在 README 和 `ArtSource/Art11/swiss/`，不修改 STATE 或其他 worker 文件夹。
