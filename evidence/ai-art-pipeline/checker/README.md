# 美术检查器本轮验证

- 日期：2026-11-08；工作起点 HEAD：`246646b0e4fb87595727b0fbf48559f1947230ba`。本工具与测试为未提交新增文件；实际脚本 SHA 绑定在报告 `tool.sha256`。
- 执行者：`art_checker` 实施 agent；尚不构成独立质量复核。
- 环境：Windows、Python 3.11.4、Pillow 9.4.0；不启动 Unity、Blender 或其他生产编辑器。
- `unittest-20261108.txt` 为 `python -m unittest discover -s tests -v` 的实际输出，33 项通过（新增美术检查器 13 项 + 原有 20 项），退出 0。负例覆盖空清单、重复 JSON 键/ID/路径、缺失与损坏、路径越界、哈希/尺寸失配、透明度要求、runtime 副本和 `.meta`、输出覆盖拒绝、CLI 非零失败等。
- 当前真实四图报告：`illustrations-20261108-r02.json`，4/4 `INTEGRITY_PASSED`，退出 0。清单、规格、配置、3 张参考图、4 张 PNG、runtime 副本及 `.meta` 的实际哈希在报告内；图像均实际解码，并核对需要 alpha。
- `illustrations-20261108.json` 为首次通过时的历史报告；随后仅新增重复 JSON 键拒绝，已重跑全套测试与真实素材检查，使用 r02 作为当前证据，不覆写旧报告。

实际素材命令：

```powershell
python tools/art_asset_audit.py --manifest ArtSource/Art071/Illustrations/manifest.json --require-alpha --runtime-dir DicePrototype/Assets/Art/Art071/Illustrations --output evidence/ai-art-pipeline/checker/illustrations-20261108-r02.json
```

报告只证明记录覆盖的文件完整性，不能验收风格、图像边缘、审美、游戏显示、ID 语义、源文件重开、平台构建或性能。生产清单与图片没有被本工具修改；无图像编辑、安装依赖或 Unity 写操作。
