# artgen

标准化 AI 插画生成工具，用法、检查项与记录字段见 [docs/art/AI_ART_PIPELINE.md §4](../../docs/art/AI_ART_PIPELINE.md)，风格依据见 [docs/art/STYLE_GUIDE.md](../../docs/art/STYLE_GUIDE.md)。

```powershell
python tools/art_gen/artgen.py verify-kit
python tools/art_gen/artgen.py generate --job ArtSource/AIConcepts/<任务>/<ID>/job.json
```

修改本工具后运行 `python -m unittest discover -s tests -v`（含 `tests/test_artgen.py`，用 `tests/fixtures_artgen/fake_codex.py` 代替真实 Codex，不联网、不耗额度）。

工具不改动生产资源，不覆盖已有修订，不签署美术认可。漂移指标仅用于定位，不是通过门槛。
