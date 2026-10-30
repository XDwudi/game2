"""Extract the exact authoritative JSON blocks, without changing rule values."""
import hashlib
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parents[1]
source = ROOT / "docs/specs/P0_v0.5.md"
text = source.read_text(encoding="utf-8-sig")
blocks = re.findall(r"```json\s*\n(.*?)\n```", text, re.S)
if len(blocks) != 2:
    raise SystemExit(f"Expected exactly 2 JSON blocks, got {len(blocks)}")
entries = []
for block, name in zip(blocks, ["P0Config.json", "GoldenScores.json"]):
    data = json.loads(block)
    output = ROOT / "DicePrototype/Assets/Game/Data" / name
    output.write_text(block + "\n", encoding="utf-8", newline="\n")
    entries.append({"path": output.relative_to(ROOT).as_posix(), "sha256": hashlib.sha256(output.read_bytes()).hexdigest()})
    print(name, "keys:", list(data) if isinstance(data, dict) else f"list[{len(data)}]")
report = ROOT / "evidence/v05/rules/import.json"
report.parent.mkdir(parents=True, exist_ok=True)
report.write_text(json.dumps({"source": source.relative_to(ROOT).as_posix(), "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(), "outputs": entries}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
