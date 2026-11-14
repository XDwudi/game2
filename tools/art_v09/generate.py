"""Generate the v0.9 card illustrations with the local Codex CLI's built-in image_gen tool.

One Codex run per asset, same common paragraph and the same two reference images as the 71 v0.71 illustrations.
Each result is archived untouched under ArtSource/Art09/Illustrations/<ID>/ with its prompt and a record.

    python tools/art_v09/generate.py --codex <path to codex.exe> [--only C54,C55] [--workers 2] [--redo C60]

The Codex binary is the user's own installation (they asked for its image model to be used). Nothing here reads
Codex credentials; the CLI authenticates itself. An existing original.png is never overwritten: --redo moves it to
rejected-N.png next to a note the caller is expected to fill in.
"""
import argparse
import hashlib
import json
import re
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
PLAN = ROOT / "tools/art_v09/prompts.json"
OUT = ROOT / "ArtSource/Art09/Illustrations"
CONFIG = ROOT / "DicePrototype/Assets/Game/Data/P0Config.json"


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def image_prompt(plan, entry):
    source, _, line = plan["commonPromptSource"].partition("#line")
    common = (ROOT / source).read_text(encoding="utf-8").splitlines()[int(line) - 1]
    return "\n".join([
        common,
        f"Content ID: {entry['id']}; display name for context only: {entry['name']}. Do NOT render this name.",
        f"Subject: {entry['subject']}",
        f"Distinct silhouette: {entry['silhouette']}. Change only the subject from the references; keep the established drawing style, palette discipline and edge weight.",
    ])


def inspect(path):
    with Image.open(path) as image:
        if image.mode != "RGBA":
            raise ValueError(f"{path.name}: mode {image.mode}, expected RGBA")
        alpha = image.getchannel("A")
        histogram = alpha.histogram()
        if histogram[0] == 0 or histogram[255] == 0:
            raise ValueError(f"{path.name}: alpha has no fully transparent or no fully opaque pixels")
        return {"dimensions": list(image.size), "mode": image.mode, "alpha0": histogram[0], "alpha255": histogram[255],
                "alphaPartial": sum(histogram[1:255]), "alphaBounds": list(alpha.getbbox())}


def run_one(codex, plan, entry, log_dir):
    folder = OUT / entry["id"]
    folder.mkdir(parents=True, exist_ok=True)
    target = folder / "original.png"
    prompt = image_prompt(plan, entry)
    if target.exists() and (folder / "record.json").exists():
        return entry["id"], "exists"
    text = ""
    if not target.exists():
        text = generate(codex, plan, prompt, target, log_dir / f"{entry['id']}.log")
    if not target.exists():
        return entry["id"], "no file (see log)"
    try:
        facts = inspect(target)
    except ValueError as error:
        target.rename(folder / "invalid.png")
        return entry["id"], f"invalid: {error}"
    source = re.findall(r"SOURCE=(.+)", text.replace("\r\n", "\n").replace("\n", ""))
    (folder / "prompt.txt").write_text(prompt + "\n", encoding="utf-8")
    record = {
        "id": entry["id"], "name": entry["name"], "createdUtc": datetime.now(timezone.utc).isoformat(),
        "tool": "Codex CLI built-in image_gen, one call, two reference images attached", "modelSnapshot": "not exposed by the tool",
        "codexCli": subprocess.run([str(codex), "--version"], capture_output=True, text=True).stdout.strip(),
        "file": "original.png", "sha256": sha(target), **facts, "sourcePixelsAltered": False,
        "originalToolPathAsReported": source[-1].split("SAVED=")[0].strip() if source else None,
        "promptFile": "prompt.txt", "promptSha256": sha(folder / "prompt.txt"),
        "references": [{"path": r["path"], "sha256": sha(ROOT / r["path"]), "role": r["role"]} for r in plan["references"]],
    }
    (folder / "record.json").write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return entry["id"], "ok"


def generate(codex, plan, prompt, target, log_path):
    request = "\n".join([
        "You are generating one game illustration. Use the built-in image_gen tool exactly once (no CLI or API fallback, do not write code to draw the image). The two attached images are style references only.",
        "",
        "Image prompt to pass to image_gen (request a REAL transparent background):",
        prompt,
        "",
        f"After the image is generated, copy the generated PNG file (unchanged bytes) to this exact path: {target}",
        "Then reply with exactly two lines: SOURCE=<absolute path of the generated file under the Codex home> and SAVED=<destination path>. Do not modify any other file in the repository.",
    ])
    command = [str(codex), "exec", "-C", str(ROOT), "-s", "workspace-write", "-c", 'model_reasoning_effort="low"']
    for reference in plan["references"]:
        command += ["-i", str(ROOT / reference["path"])]
    command.append("-")
    done = subprocess.run(command, input=request.encode("utf-8"), capture_output=True, timeout=1200)
    text = (done.stdout + b"\n" + done.stderr).decode("utf-8", "replace")
    log_path.write_text(text, encoding="utf-8")
    return text


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--codex", required=True)
    parser.add_argument("--only")
    parser.add_argument("--redo")
    parser.add_argument("--workers", type=int, default=2)
    parser.add_argument("--logs", default=str(ROOT / "artifacts/cache/v09/art-logs"))
    args = parser.parse_args()
    plan = json.loads(PLAN.read_text(encoding="utf-8"))
    entries = plan["entries"]
    if args.redo:
        for content_id in args.redo.split(","):
            folder = OUT / content_id
            if (folder / "original.png").exists():
                index = 1
                while (folder / f"rejected-{index}.png").exists():
                    index += 1
                (folder / "original.png").rename(folder / f"rejected-{index}.png")
        entries = [e for e in entries if e["id"] in args.redo.split(",")]
    elif args.only:
        entries = [e for e in entries if e["id"] in args.only.split(",")]
    log_dir = Path(args.logs)
    log_dir.mkdir(parents=True, exist_ok=True)
    failed = 0
    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        for content_id, status in pool.map(lambda e: run_one(Path(args.codex), plan, e, log_dir), entries):
            print(content_id, status, flush=True)
            failed += status not in ("ok", "exists")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
