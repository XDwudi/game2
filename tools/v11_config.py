"""Generate p1.1.0 text fields from the frozen p1.0.0 base and text appendix."""
import argparse
import json
from pathlib import Path

from v11_text import extract

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "tools/balance_sim/baseline/P0Config.p1.0.0.json"
TARGET = ROOT / "DicePrototype/Assets/Game/Data/P0Config.json"


def build(base=None, content=None):
    base = json.loads(SOURCE.read_bytes()) if base is None else base
    # Copy input so callers can compare it after generation.
    cfg = json.loads(json.dumps(base, ensure_ascii=False))
    if cfg["configVersion"] != "p1.0.0":
        raise ValueError("Expected frozen p1.0.0 base")
    content = extract.extract(base=base)[0] if content is None else content
    if set(content) != set(extract.CATEGORIES):
        raise ValueError("Text categories must exactly match the six definition categories")
    cfg["configVersion"] = "p1.1.0"
    for category in extract.CATEGORIES:
        entries = content[category]
        by_id = {entry["id"]: entry for entry in entries}
        expected = [entry["id"] for entry in cfg[category]]
        if len(by_id) != len(entries) or len(set(expected)) != len(expected) or set(by_id) != set(expected):
            raise ValueError(f"Missing, extra, or duplicate text ID: {category}")
        for index, entry in enumerate(cfg[category]):
            text = by_id[entry["id"]]
            if set(text) != {"id", "shortText", "details"}:
                raise ValueError(f"Unexpected text keys: {entry['id']}")
            if not isinstance(text["shortText"], str) or not text["shortText"].strip():
                raise ValueError(f"Empty or non-string shortText: {entry['id']}")
            # v1.1 coordinator decision: details may be empty (the appendix leaves it empty when the short text
            # already says everything; the UI then shows the short text alone), but never None or blank-only.
            if not isinstance(text["details"], str) or (text["details"] and not text["details"].strip()):
                raise ValueError(f"Blank or non-string details: {entry['id']}")
            if len(text["shortText"]) > 28:
                raise ValueError(f"shortText exceeds 28 characters: {entry['id']}")
            if any(key in entry for key in ("shortText", "details")):
                raise ValueError("Frozen base already contains v1.1 text fields")
            entry["displayName"] = entry["displayName"].replace("控制器", "卡牌")
            anchor = "description" if "description" in entry else "displayName"
            output = {}
            for key, value in entry.items():
                output[key] = value
                if key == anchor:
                    output["shortText"] = text["shortText"]
                    output["details"] = text["details"]
            cfg[category][index] = output
    return cfg


def render():
    return extract.render(build())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    content, audit = extract.extract()
    config_text = extract.render(build(content=content))
    outputs = ((extract.CONTENT, extract.render(content)), (extract.AUDIT, extract.render(audit)), (TARGET, config_text))
    if args.check:
        different = [str(path.relative_to(ROOT)) for path, text in outputs if not path.exists() or path.read_bytes() != text.encode("utf-8")]
        print("DIFFERS: " + ", ".join(different) if different else "config and text data match generator")
        return int(bool(different))
    for path, text in outputs:
        path.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote p1.1.0: {audit['entryCount']} entries / {audit['textFieldCount']} text fields")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
