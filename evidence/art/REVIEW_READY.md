# P0-E 资产交接：REVIEW

输入：P0 v0.4，配置 p0.4.0；实施者 `/root/dice_art`；2026-10-28。原规格和核心规则未修改。写入范围仅 art/、Assets/Game/Art/、Assets/Game/Audio/、evidence/art/，未操作Unity或Git。

实际使用 Blender 5.2.3 LTS / e13b728c6880 与 Python/Pillow 9.4.0。FBX exporter 的42个RNA字段已记录，实际要求的参数全部存在；网络官方API页读取失败，实施依据为本机真实RNA，不把网页访问视为验证。

| 检查 | 实际结果 | 证据 |
|---|---|---|
| Blender后台启动/RNA探测 | 通过 | blender_probe.json |
| 模型制作/FBX导出 | 通过，300三角、152顶点、1×1×1、无骨骼 | build_dice.log、dice_generation.json、fbx_export_preset.json |
| .blend重新打开 | 通过，6个物理面marker | verify_dice.log、dice_reopen.json |
| Roll_A/B端点 | 通过，60fps帧1–43，首末local位置/旋转0、scale1 | dice_reopen.json |
| 六面离线渲染 | 通过，已目视核对1–6和红1/4、蓝2/3/5/6 | render_views.log、art/previews/DiceSixViews.png |
| 38图标 | 全部256×256透明PNG、独立SHA；联系表已目视核对 | symbols_generation.json、asset_validation.json、IconContactSheet.png |
| 12点数PNG | 实际alpha连通域数量1–6均正确；图集1024×1024 | asset_validation.json、PipContactSheet.png |
| 10音效 | WAV/44.1kHz/16bit/mono、非静音、无削波 | asset_validation.json |
| 源及导出版本绑定 | SHA256已记录 | asset_validation.json |
| Unity导入/游戏镜头/动态表现 | 未执行，由root串行集成 | 必须补实际引擎和构建证据 |
| 美术/体验接受 | 未执行，不能实施者自签 | 用户判断 |

生成日志曾出现用户缩略图缓存路径写入警告；实际BLEND保存、重开与所有PNG渲染成功，原始日志保留。无规则依赖于该缓存。普通/玻璃两种材质在可编辑BLEND中保留；Unity URP材质仍由集成方重建。

详细路径、图集UV和复现命令见 art/README.md 与 Assets/Game/Art/asset_map.json。骰体保持无点数，运行时永久/当前点数由Core快照驱动显示，不能用预览几何代替规则。

提交REVIEW后释放资产写入锁；后续必要返工再由主协调者分派。docs/STATE.md由root统一更新。
