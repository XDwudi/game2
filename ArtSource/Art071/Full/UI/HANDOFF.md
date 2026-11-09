# 公共高清资源交接（源与导出，待引擎接入）

2026-11-09 UTC。基线249d980；按用户全量v071授权制作，使用项目 game-art-production 的代码原生几何路线。没有启动Unity、修改Assets或原纸框/按钮。43件：5附魔、28公共符号、8 FX、卡背、字标。40个旧消费名全部保留，新件为 `fx_arc_short`、`ui_card_back`、`ui_wordmark_dicecraft`。

可编辑源是每件SVG及 `tools/art_v071/build_full_ui.py`；PNG是同一绘制指令抗锯齿输出，不是旧像素图放大。重新生成：`python -B -X utf8 tools/art_v071/build_full_ui.py --all`。依赖/实际版本在manifest，未安装新包。命令只写本Full目录，不写Assets。`--representatives` 可仅重建代表图。

规格与消费者详见 `manifest.json`；结果见 `source-export-verification.json`。实际连续重跑43资产的86源/导出，加3并排图共89个SVG/PNG逐字节一致；43个PNG完整RGBA解码、43个SVG可解析、透明边缘无裁切、无SVG文字/内嵌位图、40个旧名无漏。不是Unity、动态或最终美术通过。SVG为可编辑几何，不宣称在独立矢量编辑器重开保存；SVG边框与PNG栅格边缘可能有亚像素取样差异。

已实看 `representatives.png`、`../Symbols/catalogue.png`、`../FX/catalogue.png` 和 `ui_card_back.png`。先看base10/短弧/字标后扩展；修正了浅纸上白色动作符号缺少墨边和软光环分层感。字标预览容器裁切已修正，资产原图未裁切。最终字标/卡背骰印使用1/2/3装饰面，不承担规则示意。

接入要点：

- 符号/徽记/FX为256²，`Bilinear`、Clamp、无mipmap、RGBA透明、原尺寸导入、不裁窄source rectangle；建议FullRect，pivot中心，保留各自外侧安全边。不能继承旧Point过滤。
- `badge_enchant_*` 进入现 `app.enchantIcons`；28个 `ui_icon_*` 进入公共 `uiTemplates`，需要同时检查Prefab序列化引用。不能只覆盖目录而让旧GUID仍被实际对象使用。
- base10薄荷、mult2紫、submit1纸票、roll1蓝杯、slotless中心真实透明。root应同步移除/调整 `EnchantBadge` 旧的base10金/其他蓝色统一外框，避免覆盖本轮颜色区分。源图不包含+10、×2等动态规则值。
- `ui_icon_temporary` 是规格小钟。动作+ / × /close有浅纸内芯与墨边，两背景可读；现组件有Tint时应实看乘色后的结果。红圆蓝菱形仍保留辅助形状。
- FX RGB白色、alpha承载软边，供运行时着色。`fx_line_soft` 保留旧直线语义；`fx_arc_short` 为新短弧，不能误把所有copy关系折线自动替换成弧。新增短弧没有假称已有消费。`fx_halo_soft/ring_soft` 为连续径向alpha，非硬分层圆盘。
- 卡背768×1024，用Simple/preserveAspect呈现整体固定印刷面；不能作为九宫格拉伸中央骰印。菜单现卡背容器可直接放图，关闭重复的 `PrintedBorder/Inset/BackSeal` 子画面，沿用容器和牌叠位置。
- 字标1440×400，透明背景。纸上用原色、preserveAspect；替换菜单Title视觉时保持现菜单命中根/纸框/规则行和动态存档文字，不给字标再叠一份TMP标题。小尺寸品牌位需单独实际看可读性，不盲目压扁。
- 字标用现有Noto Sans SC Bold真实字形轮廓，字体文件SHA/来源在manifest；许可证与版权已复制为本目录 `NotoSansSC-Bold-OFL.txt`、`NotoSansSC-Bold-COPYRIGHT.txt`。没有新增字体软件或外部图形素材。

root后续负责Editor导入、实际28/32px角标、菜单、选关、道具、真实触发与ReducedMotion确认；本批作者不自行签署最终艺术ACCEPTED。旧性能/独占全屏/Mac实跑状态未改变。
