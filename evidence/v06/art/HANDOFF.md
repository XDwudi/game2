# P0 v0.6 资产模块交接

任务 P0-003；输入为原始 v0.6 规格第27/28/34章与附录A。沿用原型SVG/Pillow几何、Segoe UI Bold文本及合成音效，不引入外部图像或音频。没有修改Unity场景、Prefab、ProjectSetup、Blender源或导入metadata。

## 内容

- 71个活动内容图标完整映射；C04/C16/C23不在活动manifest。退役PNG/SVG留存供旧版本追溯。
- 新增C41–C53、O05–O07共16个符号；更新C09×4、C10+10、C13+3、C14×3、C27+10、C30×4、C38首尾蓝刷、C39+6共8个旧符号。
- 五附魔128px透明角标及256px框层：slotless/base10/mult2/submit1/roll1。除色彩外有空槽、加十、倍数、提交文档、骰杯五种形状；submit1与C21扩提交容量不同。
- 新音效6条：enchant_base10、enchant_mult2、reorder、refresh、cannon、debug。活动音效共16条，44.1kHz/16bit/mono，短起音和渐弱尾音。
- asset_map.json为p0.6.0资产schema3，名称从游戏配置读取；规则强度仍以配置为权威，图标中的数字只作可见提示。

## 制作与实看

运行build_symbols_v06.py --samples后，实际查看samples.png中的C41/C42/C27/O05以及五种附魔，确认面具向右、玻璃炮口、加十、商店货架及预算图形可区分，再扩展全内容。之后实际打开NewContentContactSheet、AllContentContactSheet、64px图标、32px附魔角标及5种框层，未见裁切/重叠或旧加值残留；这是离线原型可读性检查，不是游戏内验收或人工美术批准。

## 真实检查

`python evidence/v06/art/validate_assets.py`：71图标唯一、5附魔透明尺寸/源SVG、16条WAV非静音无削波首尾零、12点阵alpha连通域对应1–6全部通过。检查绑定原始规格/配置/生成器/验证器/每件PNG/SVG/WAV的SHA。

随后再次运行生成器，179个源/导出/映射路径SHA完全一致，见reproduction.json。所有新图标都有可编辑SVG和确定生成器；没有只保留栅格导出。

未做Unity导入/Player截图和主观听感；由root串行集成并独立复核。旧10音效与骰子Blender/FBX/点阵沿用已有资源，没有从本次离线检查推定其跨平台状态。

## 复现与集成

- `python art/scripts/build_symbols_v06.py --samples` / 无参数完整生成。
- `python evidence/v06/art/validate_assets.py` 字节与覆盖检查。
- 先导入Texture为Sprite/Alpha/Clamp/no mipmap，现有生成器仍负责Unity对象与资源引用。播放音效只响应成功命令/正式事件，预览不播放；从asset_map的audio映射读取新ID，旧enchant_base5/mult120不再活动。

脚本复用build_symbols.py及build_symbols_v05.py的几何/保存函数；它们只作为库导入，不运行旧main，因而不会覆盖骰体/旧证据或重新创建退役内容映射。
