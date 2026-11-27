"""R-05. Extract the frozen Windows ZIP into a fresh directory and play a complete trace in all six themes.

Generate the trace with BalanceSim trace --full --seed 5000 --out <path>.
python tools/replay_v111.py --trace <path> --out evidence/v111/replays/final
"""
import argparse
import json
import re
import subprocess
import tempfile
import zipfile
from pathlib import Path

from PIL import Image

if __package__:
    from . import package_v111 as pack
else:
    import package_v111 as pack

THEMES = pack.PLAYER_THEMES
WINDOWS = pack.TOP + "-win-x64.zip"
WINDOW_SIZE = (1920, 1080)
MENU_PAGES = ("menu", "handbook", "settings", "new-run", "stage-preview")
PLAY_PAGES = ("HandReady-normal", "pause", "Selecting-normal", "play-pending", "play-locked", "play-scoring",
              "StageSettlement-normal", "Shop-normal", "HandReady-boss", "StageSettlement-boss", "Shop-boss", "loot")
PHASE_LABEL = re.compile(r"(?:HandReady|Selecting|StagePreview|StageSettlement|Shop|RunWon|RunFailed)-(?:normal|pressure|boss)")
ERROR_LINE = re.compile(r"^\s*(?:Unhandled (?:exception|Exception)|(?:[\w.]+\.)?\w*Exception\s*:|"
                        r"Assertion failed\b|Crash!!!)", re.MULTILINE)


def ref(root, path):
    return {"path": path.relative_to(root).as_posix(), "sha256": pack.delivery.sha(path)}


SCORE_FRACTION = r"[≈−-]?\s*\d[\d,]*\s*[/／]\s*\d[\d,]*"


def player_text_patterns(root):
    # These captures show ordinary game pages. The separately accessible version page
    # and copyable diagnostic records have a different text contract.
    rules = pack.delivery.read_json(root / "tools/v11_text/glossary_rules.json")["rules"]
    config = pack.delivery.read_json(root / pack.delivery.CONFIG)
    ids = [re.escape(row["id"]) for group in ("handDefinitions", "colorDefinitions", "enchantments")
           for row in config[group]]
    checks = [(r["id"], re.compile(r["regex"])) for r in rules
              if r["id"] in {"dev-item-id", "dev-die-id", "dev-face-id", "dev-card-id",
                             "dev-variable", "dev-enum", "dev-version", "dev-exception"}]
    checks.extend([
        ("content ID", re.compile(r"(?<![A-Za-z0-9_])(?:[IOUB]\d{2}|L[1-6]|"
                                  r"(?:OFFER|STAGE|CYCLE|GIFT|RUN)_[A-Za-z0-9_]+)(?![A-Za-z0-9_])")),
        ("internal phase", re.compile(r"(?<![A-Za-z0-9_])(?:HandReady|StagePreview|Selecting|StageSettlement|"
                                      r"Shop|RunFailed|RunWon)(?![A-Za-z0-9_])")),
        ("config ID", re.compile(r"(?<![A-Za-z0-9_])(?:" + "|".join(ids) + r")(?![A-Za-z0-9_])")),
        ("art version", re.compile(r"(?<![A-Za-z0-9_])art\d+\.\d+\.\d+(?![A-Za-z0-9_])")),
        ("score fraction", re.compile(r"(?:×\s*(?:[\u3400-\u9fff]+\s+)?|倍率[：:]?\s*)" + SCORE_FRACTION)),
    ])
    return checks


# Committed score details use PlayerNumberFormat.Unlimited: grouped, unabridged decimal
# values, including a rounded value marked with ≈. The drawer and reader share one hand.
SCORE_VALUE = r"≈?[−-]?[0-9]+(?:,[0-9]{3})*(?:\.[0-9]{1,2})?"
SCORE_FORMULA = re.compile(rf"基础\s+{SCORE_VALUE}(?:\s+×\s+[^×=\n]+?\s+{SCORE_VALUE})+"
                           rf"\s*=\s*{SCORE_VALUE}（最后去掉小数）")
SCORE_STEP = re.compile(rf"[^：\n]+：\s*{SCORE_VALUE}\s+×\s+(?P<factor>{SCORE_VALUE})\s+→\s+{SCORE_VALUE}")


def check_score_focus(frame, theme, image, page):
    # Ordinary TMP export may include offscreen siblings. The recorder marks the exact TMP
    # whose glyphs it checked in the scroll viewport during this image's own capture.
    focus = frame.get("scoreFocus")
    if not isinstance(focus, dict) or not all(isinstance(focus.get(k), str) for k in ("path", "text", "handId")):
        raise ValueError(f"Missing same-frame score focus: {theme}/{image}")
    path, text, hand = focus["path"], focus["text"], focus["handId"]
    matching = [field["text"] for field in frame["fields"] if field["path"] == path]
    if matching != [text] or not re.fullmatch(r"H[0-9]{4,}", hand):
        raise ValueError(f"Score focus differs from its rendered field or hand: {theme}/{image} {path}")
    if page == "score-details":
        valid = (path.endswith("/ScoreDrawerTable/Viewport/Content/Formula") and SCORE_FORMULA.fullmatch(text.strip()))
    else:
        step = SCORE_STEP.fullmatch(text.strip())
        valid = (re.search(r"(?:^|/)ScoreDetail/Viewport/Content/GlobalStep(?:0|[1-9][0-9]*)$", path)
                 and step and step["factor"] != "1")
    if not valid:
        raise ValueError(f"Unexpected focused score field or value: {theme}/{image} {path}: {text}")
    return hand


def check_frame_text(root, tour, names, theme):
    checks = player_text_patterns(root)
    frames = tour.get("textFrames")
    if not isinstance(frames, list) or len(frames) != len(names):
        raise ValueError("Missing player text frames: " + theme)
    count = 0
    score_hands = {}
    for image, frame in zip(names, frames):
        if not isinstance(frame, dict) or frame.get("image") != image:
            raise ValueError(f"Player text frame differs from screenshot: {theme}/{image}")
        fields = frame.get("fields")
        if not isinstance(fields, list) or not fields:
            raise ValueError(f"Empty player text frame: {theme}/{image}")
        multiplier_rows = set()
        for field in fields:
            if not isinstance(field, dict) or not all(isinstance(field.get(key), str) and field[key].strip()
                                                     for key in ("path", "text")):
                raise ValueError(f"Invalid player text field: {theme}/{image}")
            parent, _, component = field["path"].rpartition("/")
            if component in ("Title", "Label", "Caption") and field["text"].strip().rstrip("：:").endswith("倍率"):
                multiplier_rows.add(parent)
        for field in fields:
            path, value = field["path"], field["text"]
            parent, _, component = path.rpartition("/")
            if (re.fullmatch(SCORE_FRACTION, value.strip()) and
                    (re.search(r"(?:^|/)(?:Multiplier|(?:Random)?Factor\d+|Formula|ReceiptFactors)(?:/|$)", path)
                     or parent in multiplier_rows and component in ("Value", "Amount"))):
                raise ValueError(f"score fraction in player text: {theme}/{image} {path}: {value}")
            # K is also a legal user-bound key on the roll button and the binding controls.
            if re.search(r"(?:^|/)Roll/(?:[^/]+/)?Label$", path):
                value = value.replace("[K]", "")
            elif re.search(r"(?:^|/)Bind(?:Roll|Lock|Submit|Clear|Handbook)/(?:[^/]+/)?Label$", path) and value == "K":
                value = ""
            for rule, pattern in checks:
                if pattern.search(value):
                    raise ValueError(f"{rule} in player text: {theme}/{image} {field['path']}: {field['text']}")
            count += 1
        page = re.fullmatch(r"[0-9]+-(score-details|score-steps)\.png", image)
        if page:
            score_hands[page[1]] = check_score_focus(frame, theme, image, page[1])
    if score_hands and (set(score_hands) != {"score-details", "score-steps"} or len(set(score_hands.values())) != 1):
        raise ValueError("Score drawer and step reader do not share a captured hand: " + theme)
    return count


def validated_trace(path):
    trace = pack.delivery.read_json(path)
    steps = trace.get("steps", [])
    if (trace.get("configVersion") != pack.VERSION or len(steps) < 2 or steps[0].get("kind") != "NewRun"
            or steps[0].get("seed") != trace.get("seed") or trace.get("terminalPhase") not in ("RunWon", "RunFailed")):
        raise ValueError("Expected a validated full p1.1.1 trace (BalanceSim trace --full)")
    labels = trace.get("captureLabels")
    if (not isinstance(labels, list) or labels[:len(MENU_PAGES)] != list(MENU_PAGES)
            or not all(isinstance(label, str) and re.fullmatch(r"[A-Za-z0-9-]+", label) for label in labels)
            or len(set(labels)) != len(labels) or not labels[-2].startswith(trace["terminalPhase"] + "-")
            or labels[-1] != "final-" + trace["terminalPhase"] or type(trace.get("finalStage")) is not int
            or trace["finalStage"] < 3):
        raise ValueError("Missing Core replay capture sequence or final stage; regenerate with --full")
    supplemental = set(MENU_PAGES) | {"pause", "play-pending", "play-locked", "play-scoring", "score-details", "score-steps", "loot"}
    if (any(label not in supplemental and not PHASE_LABEL.fullmatch(label) for label in labels[:-1])
            or any(label.startswith(("RunWon-", "RunFailed-")) for label in labels[:-2])
            or any(label not in labels for label in PLAY_PAGES)
            or "score-details" not in labels or "score-steps" not in labels
            or labels.index("score-details") <= labels.index("play-scoring")
            or labels.index("score-steps") != labels.index("score-details") + 1
            or [labels.index(label) for label in PLAY_PAGES] != sorted(labels.index(label) for label in PLAY_PAGES)
            or labels.index("pause") != labels.index("HandReady-normal") + 1
            or labels.index("loot") != labels.index("Shop-boss") + 1):
        raise ValueError("Missing or misordered F-THEME-02 page, play state, settlement or boss loot")
    kinds = [step.get("kind") for step in steps]
    if (not {"StartStage", "Roll", "ConfirmLocks", "Submit", "Continue", "LeaveShop"}.issubset(kinds)
            or not any(step.get("dieIds") for step in steps if step.get("kind") == "ConfirmLocks")):
        raise ValueError("F-THEME-02 playback needs real stage, selected dice, submission, settlement and shop commands")
    return trace


def extract_verified(archive_path, proof, directory):
    if archive_path.stat().st_size != proof["bytes"] or pack.delivery.sha(archive_path) != proof["sha256"]:
        raise ValueError("Windows ZIP differs from package receipt")
    expected = proof["files"]
    if proof["members"] != len(expected) or proof["package"] != WINDOWS or proof["platform"] != "windows":
        raise ValueError("Invalid Windows package receipt")
    with zipfile.ZipFile(archive_path) as z:
        names = z.namelist()
        if len(names) != len(set(n.casefold() for n in names)) or set(names) != set(expected):
            raise ValueError("Windows archive contains duplicate or unexpected members")
        for name in names:
            if not name.startswith(pack.TOP + "/") or "\\" in name or ":" in name or any(p in ("", ".", "..") for p in name.split("/")):
                raise ValueError("Unexpected ZIP path: " + name)
            path = pack.contained(directory, name)
            path.parent.mkdir(parents=True, exist_ok=True)
            with z.open(name) as src, path.open("xb") as dst:
                while chunk := src.read(1024 * 1024):
                    dst.write(chunk)
            if pack.delivery.sha(path) != expected[name]:
                raise ValueError("Extracted player file differs: " + name)
    exe = directory / pack.TOP / "DicePrototype.exe"
    if not exe.is_file():
        raise ValueError("Missing extracted Windows player")
    return exe


def check_run(root, theme, directory, proof, trace_path, trace, data_path):
    raw = directory / "tour.json"
    log = directory / "Player.log"
    tour = pack.delivery.read_json(raw)
    steps = len(trace["steps"]) - 1
    if (tour.get("schema") != "v11-windows-replay-1" or tour.get("status") != "completed" or tour.get("error") != ""
            or tour.get("theme") != theme or tour.get("buildGuid") != proof["buildGuid"]
            or tour.get("unityVersion") != proof["identity"]["unityEditorVersion"]
            or tour.get("rulesVersion") != proof["identity"]["contentConfigVersion"]
            or tour.get("artVersion") != proof["identity"]["artVersion"]
            or json.loads(tour["buildIdentity"]) != proof["identity"] or tour.get("isEditor") is not False
            or tour.get("platform") != "WindowsPlayer" or tour.get("traceSha256") != pack.delivery.sha(trace_path)
            or Path(tour["playerDataPath"]).as_posix().casefold() != data_path.replace("\\", "/").casefold()
            or tour.get("terminalPhase") != trace["terminalPhase"] or tour.get("traceSteps") != steps
            or tour.get("stepsSent") != steps or tour.get("logErrors") != 0 or tour.get("finalStage") != trace["finalStage"]):
        raise ValueError("R-05 actual theme, player identity, logs or completed trace differs: " + theme)
    title_atlas = {key: tour.get("titleAtlas" + key) for key in
                   ("Format", "Readable", "Width", "Height", "MipCount", "TextureBytes", "RuntimeBytes")}
    sizes = ("Width", "Height", "MipCount", "TextureBytes", "RuntimeBytes")
    if (type(title_atlas["Readable"]) is not bool or not isinstance(title_atlas["Format"], str)
            or any(type(title_atlas[key]) is not int or title_atlas[key] < 0 for key in sizes)):
        raise ValueError("Invalid Windows title atlas measurement: " + theme)
    if theme in pack.THEMES:
        if not title_atlas["Format"].strip() or any(title_atlas[key] == 0 for key in sizes[:-1]):
            raise ValueError("Missing Windows title atlas measurement: " + theme)
        # RuntimeBytes is the Unity profiler counter. A release player may report zero when it is unavailable.
        title_atlas["RuntimeBytesStatus"] = "reported" if title_atlas["RuntimeBytes"] else "unavailable"
    elif title_atlas["Format"] or title_atlas["Readable"] or any(title_atlas[key] for key in sizes):
        raise ValueError("Unexpected theme-specific title atlas: " + theme)
    else:
        title_atlas["RuntimeBytesStatus"] = "noTitleAtlas"
    text = log.read_text(encoding="utf-8-sig", errors="replace")
    if not text.strip() or ERROR_LINE.search(text):
        raise ValueError("Empty player log or Unity exception: " + theme)
    for marker in ("W15_MISSING_ART ", "V11 missing theme title font: "):
        if marker in text:
            raise ValueError("Missing release art or title font in Player.log for " + theme + ": " + marker.strip())
    names = tour["images"]
    if names != [f"{i:02d}-{label}.png" for i, label in enumerate(trace["captureLabels"])]:
        raise ValueError("Windows screenshots differ from phases reached by the Core replay: " + theme)
    if (tour.get("screenWidth"), tour.get("screenHeight")) != WINDOW_SIZE:
        raise ValueError("Windows replay did not run in the requested 1920x1080 window: " + theme)
    text_count = check_frame_text(root, tour, names, theme)
    pictures = []
    # The terminal phase and final-* consciously show the same result; Shop-boss can be the loot picker.
    # Compare genuinely different page types, including the new modal and live interaction captures.
    different = set(MENU_PAGES) | {"pause", "play-pending", "play-locked", "play-scoring", "score-details", "score-steps",
                                   "HandReady-normal", "Shop-normal", "StageSettlement-normal", "loot", trace["captureLabels"][-1]}
    key_pages = {name for name, label in zip(names, trace["captureLabels"]) if label in different}
    visible_pages = set()
    for name in names:
        if Path(name).name != name or not name.endswith(".png"):
            raise ValueError("Unexpected screenshot name")
        path = directory / name
        with Image.open(path) as img:
            if img.format != "PNG" or img.size != WINDOW_SIZE:
                raise ValueError("Screenshot dimensions differ from actual GameView: " + name)
            img.verify()
        with Image.open(path) as img:
            frame = img.convert("RGB")
            if all(low == high for low, high in frame.getextrema()):
                raise ValueError("Uniform screenshot: " + name)
            if name in key_pages:
                pixels = pack.digest(frame.tobytes())
                if pixels in visible_pages:
                    raise ValueError("Different F-THEME-02 pages must show different frames: " + theme + "/" + name)
                visible_pages.add(pixels)
        pictures.append(ref(root, path))
    return {"theme": theme, "exitCode": 0, "report": ref(root, raw), "playerLog": ref(root, log),
            "images": pictures, "textFieldCount": text_count, "steps": steps,
            "terminalPhase": tour["terminalPhase"], "titleAtlas": title_atlas}


def verify_index(root, index_path, proof):
    index = pack.delivery.read_json(index_path)
    if (index.get("schema") != "v111-replay-index-1" or index.get("windowsPackageSha256") != proof["sha256"]
            or index.get("sourceCommit") != proof["identity"]["sourceCommit"] or index.get("buildGuid") != proof["buildGuid"]
            or index.get("verifiedExtractedFiles") != proof["members"]
            or [r["theme"] for r in index["runs"]] != list(THEMES)):
        raise ValueError("Incomplete or stale six-theme Windows replay index")
    trace_path = pack.contained(root, index["trace"]["path"])
    if ref(root, trace_path) != index["trace"]:
        raise ValueError("Replay command trace has changed")
    trace = validated_trace(trace_path)
    menus = set()
    for row in index["runs"]:
        actual = check_run(root, row["theme"], index_path.parent / row["theme"], proof, trace_path, trace, index["playerDataPath"])
        if actual != row:
            raise ValueError("Changed Windows run evidence: " + row["theme"])
        menu = pack.contained(root, row["images"][0]["path"])
        with Image.open(menu) as img:
            pixels = pack.digest(img.convert("RGB").tobytes())
        if pixels in menus:
            raise ValueError("Two different themes produced identical menu pixels: " + row["theme"])
        menus.add(pixels)
    return ref(root, index_path)


def collect(root, trace_path, out, timeout):
    trace = validated_trace(trace_path)
    proof = pack.delivery.read_json(root / "evidence/v111/packages" / (WINDOWS + ".json"))
    build = pack.delivery.read_json(pack.contained(root, proof["buildReport"]["path"]))
    gate = pack.delivery.read_json(root / pack.GATE)
    if (proof.get("schema") != "v111-player-package-1" or proof["buildGuid"] != build["buildGuid"]
            or proof["identity"] != pack.verify_gate(root, gate, build)
            or proof["buildReport"] != ref(root, pack.contained(root, proof["buildReport"]["path"]))):
        raise ValueError("Windows replay input differs from the gated package")
    cache = root / "artifacts/cache/v111"
    cache.mkdir(parents=True, exist_ok=True)
    if out.exists():
        index_path = out / "index.json"
        if not index_path.is_file() or pack.delivery.read_json(index_path).get("trace") != ref(root, trace_path):
            raise FileExistsError("Existing replay output belongs to a different or incomplete run: " + str(out))
        return verify_index(root, index_path, proof)
    out.parent.mkdir(parents=True, exist_ok=True)
    # Keep partial evidence under a unique diagnostic path. Only a verified six-theme set gets the final name.
    attempt = Path(tempfile.mkdtemp(prefix=out.name + ".attempt-", dir=out.parent))
    with tempfile.TemporaryDirectory(prefix="r05-extracted-", dir=cache) as temp:
        exe = extract_verified(root / "Releases" / pack.VERSION / WINDOWS, proof, Path(temp))
        data_path = (exe.parent / "DicePrototype_Data").as_posix()
        runs = []
        for theme in THEMES:
            folder = attempt / theme
            folder.mkdir()
            command = [str(exe), "-screen-fullscreen", "0", "-screen-width", "1920", "-screen-height", "1080",
                       "-logFile", str(folder / "Player.log"), "--art073-capture", str(folder),
                       "--art073-trace", str(trace_path), "--v11-replay-theme", theme]
            subprocess.run(command, cwd=exe.parent, timeout=timeout, check=True)
            runs.append(check_run(root, theme, folder, proof, trace_path, trace, data_path))
        index = {"schema": "v111-replay-index-1", "windowsPackageSha256": proof["sha256"],
                 "sourceCommit": proof["identity"]["sourceCommit"], "buildGuid": proof["buildGuid"],
                 "playerDataPath": data_path, "verifiedExtractedFiles": proof["members"],
                 "trace": ref(root, trace_path), "runs": runs}
        target = attempt / "index.json"
        target.write_text(json.dumps(index, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        verify_index(root, target, proof)
        # Rebase the already verified references before publishing the directory in a single rename.
        for row in runs:
            for entry in [row["report"], row["playerLog"], *row["images"]]:
                relative = pack.contained(root, entry["path"]).relative_to(attempt)
                entry["path"] = (out / relative).relative_to(root).as_posix()
        target.write_text(json.dumps(index, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        attempt.rename(out)
        return verify_index(root, out / "index.json", proof)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--trace", required=True)
    parser.add_argument("--out", required=True, help="New repository-relative evidence directory")
    parser.add_argument("--timeout", type=float, default=7200, help="Seconds per full theme replay")
    args = parser.parse_args()
    print(json.dumps(collect(pack.ROOT, pack.contained(pack.ROOT, args.trace), pack.contained(pack.ROOT, args.out), args.timeout)))


if __name__ == "__main__":
    main()
