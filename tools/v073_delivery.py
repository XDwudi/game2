"""Bind real Unity evidence to a frozen source commit; never invent test status."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
from quality_check import check_nunit

ROOT = Path(__file__).resolve().parents[1]
SCOPES = ["DicePrototype/Assets", "DicePrototype/Packages", "DicePrototype/ProjectSettings"]
IDENTITY = "DicePrototype/Assets/Resources/BuildIdentity.json"
CONFIG = "DicePrototype/Assets/Game/Data/P0Config.json"
GOLDEN = "DicePrototype/Assets/Game/Data/GoldenScores.json"
HISTORICAL_MINIMUM = {"EditMode": 797, "PlayMode": 30}  # Verified v0.6 floor, never the v0.73 final count.
ASSEMBLIES = {"DiceCraft.Core", "DiceCraft.Runtime", "DiceCraft.EditMode", "DiceCraft.PlayModeTests", "DiceCraft.Editor"}


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def source_paths():
    return sorted(p for scope in SCOPES for p in (ROOT / scope).rglob("*") if p.is_file()
                  and not any(part.endswith("~") for part in p.relative_to(ROOT).parts))


def entry(path):
    return {"path": path.relative_to(ROOT).as_posix(), "sha256": sha(path)}


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def verify_nunit(path, platform, expected):
    check_nunit(ROOT, path.relative_to(ROOT).as_posix(), expected, [])
    root = ET.parse(path).getroot()
    leaves = root.findall(".//test-case")
    names = [x.attrib["fullname"] for x in leaves]
    if root.attrib.get("result") != "Passed" or len(leaves) != expected or len(set(names)) != expected:
        raise ValueError(f"{platform}: incomplete or non-passing suite")
    for key in ("total", "passed", "testcasecount"):
        if int(root.attrib[key]) != expected:
            raise ValueError(f"{platform}: inconsistent {key}")
    if any(int(root.attrib.get(key, 0)) for key in ("failed", "skipped", "inconclusive")):
        raise ValueError(f"{platform}: failed/skipped/inconclusive cases")
    if any(x.attrib.get("result") != "Passed" for x in leaves):
        raise ValueError(f"{platform}: non-passing leaf")
    platforms = [p.attrib.get("value") for p in root.findall(".//property") if p.attrib.get("name") == "platform"]
    if set(platforms) != {platform}:
        raise ValueError(f"{platform}: wrong report mode")
    return {"mode": platform, "passed": expected, "names": sorted(names), "report": entry(path)}


def identity():
    dirty = subprocess.check_output(["git", "status", "--porcelain", "--", *SCOPES], cwd=ROOT, text=True)
    if dirty.strip():
        raise ValueError("Commit all project sources before creating identity:\n" + dirty)
    cfg = read_json(ROOT / CONFIG)
    if cfg.get("configVersion") != "p0.6.0" or type(cfg.get("saveSchemaVersion")) is not int or cfg["saveSchemaVersion"] != 4:
        raise ValueError("v0.73 art release must preserve rules p0.6.0 / save schema4")
    value = {"releaseVersion": "p0.7.3", "artVersion": "art0.7.3", "sourceCommit": subprocess.check_output(
        ["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        "contentConfigVersion": cfg["configVersion"], "contentConfigSha256": sha(ROOT / CONFIG),
        "saveSchemaVersion": cfg["saveSchemaVersion"], "goldenCaseSetSha256": sha(ROOT / GOLDEN),
        "unityEditorVersion": "6000.6.7f1", "backend": "Mono"}
    path = ROOT / IDENTITY
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(value))


def validate_discovery(inventory, mode, expected):
    names = inventory.get("names", [])
    if (type(expected) is not int or expected < HISTORICAL_MINIMUM[mode]
            or inventory.get("mode") != mode or inventory.get("unity") != "6000.6.7f1"
            or not isinstance(names, list) or len(names) != expected
            or any(not isinstance(name, str) or not name for name in names)
            or len(set(names)) != expected):
        raise ValueError("Incomplete/duplicate discovery or configured count mismatch: " + mode)
    return names


def gate(args):
    tests = []
    inventories = []
    for mode, report, expected in [("EditMode", args.edit, args.expect_edit_count), ("PlayMode", args.play, args.expect_play_count)]:
        path = ROOT / f"evidence/v073/unity/discovery-{mode}.json"
        inventory = read_json(path)
        names = validate_discovery(inventory, mode, expected)
        test = verify_nunit(ROOT / report, mode, len(names))
        if test["names"] != sorted(names):
            raise ValueError("Executed suite differs from complete discovered inventory: " + mode)
        tests.append(test)
        inventories.append(entry(path))
    proof = read_json(ROOT / args.pdb)
    assemblies = proof.get("assemblies", [])
    if proof.get("mismatches") != 0 or len(assemblies) != len(ASSEMBLIES) or {a["name"] for a in assemblies} != ASSEMBLIES:
        raise ValueError("Independent Unity PDB audit is missing or failed")
    documents = proof.get("documents", [])
    if not documents:
        raise ValueError("PDB audit contains no project source documents")
    seen_documents = set()
    group_counts = {name: 0 for name in ASSEMBLIES}
    for document in documents:
        source = Path(document["source"])
        if not source.is_absolute():
            source = ROOT / "DicePrototype" / source
        source = source.resolve()
        if not source.is_relative_to((ROOT / "DicePrototype/Assets/Game").resolve()) or source in seen_documents or document["assembly"] not in ASSEMBLIES:
            raise ValueError("Invalid or duplicate PDB source document: " + str(source))
        seen_documents.add(source)
        group_counts[document["assembly"]] += 1
        expected = document["expectedChecksum"]
        algorithm = "sha1" if len(expected) == 40 else "sha256"
        actual = hashlib.new(algorithm, source.read_bytes()).hexdigest()
        if not document["matches"] or actual != expected:
            raise ValueError("Current source differs from compiled PDB: " + str(source))
    for assembly in proof["assemblies"]:
        if not group_counts[assembly["name"]] or group_counts[assembly["name"]] != assembly["projectDocuments"]:
            raise ValueError("Incomplete PDB document inventory: " + assembly["name"])
        if not assembly["pdbMatchesDll"]:
            raise ValueError("PDB does not match assembly")
        for suffix, key in (("dll", "dllSha256"), ("pdb", "pdbSha256")):
            path = ROOT / "DicePrototype/Library/ScriptAssemblies" / (assembly["name"] + "." + suffix)
            if sha(path) != assembly[key]:
                raise ValueError("Recompiled after PDB audit: " + str(path))
    expected_sources = {p.resolve() for p in (ROOT / "DicePrototype/Assets/Game").rglob("*.cs")
                        if not any(part.endswith("~") for part in p.parts)}
    if seen_documents != expected_sources:
        raise ValueError("PDB audit omits current project C# source files")
    test_files = [entry(p) for p in source_paths() if p.relative_to(ROOT).as_posix() != IDENTITY
                  and not p.relative_to(ROOT).as_posix().startswith(("DicePrototype/Assets/InitTestScene", "DicePrototype/Assets/Screenshots"))]
    actual_assemblies = sorted([{"name": a["name"], "mvid": a["mvid"], "dllSha256": a["dllSha256"]} for a in assemblies], key=lambda a: a["name"])
    for test in tests:
        binding_path = (ROOT / test["report"]["path"]).with_suffix(".binding.json")
        binding = read_json(binding_path)
        if binding.get("schema") != "v07-test-binding-1" or binding.get("report") != test["report"]:
            raise ValueError("NUnit result is missing its actual runtime binding")
        for phase in ("started", "finished"):
            snapshot = binding[phase]
            if sorted(snapshot["assemblies"], key=lambda a: a["name"]) != actual_assemblies:
                raise ValueError("NUnit ran different loaded assemblies: " + phase)
            if sorted(snapshot["files"], key=lambda f: f["path"]) != sorted(test_files, key=lambda f: f["path"]):
                raise ValueError("NUnit source snapshot differs from current source: " + phase)
        test["binding"] = entry(binding_path)
        inventories.append(test["binding"])
    value = read_json(ROOT / IDENTITY)
    if (value.get("releaseVersion") != "p0.7.3" or value.get("artVersion") != "art0.7.3" or value.get("contentConfigVersion") != "p0.6.0"
            or value.get("saveSchemaVersion") != 4 or value.get("unityEditorVersion") != "6000.6.7f1" or value.get("backend") != "Mono"):
        raise ValueError("Expected frozen p0.7.3/art0.7.3/rules p0.6.0/schema4/Unity 6000.6.7f1/Mono identity")
    if value["contentConfigSha256"] != sha(ROOT / CONFIG) or value["goldenCaseSetSha256"] != sha(ROOT / GOLDEN):
        raise ValueError("Build identity does not match current config/cases")
    head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    if value["sourceCommit"] != head:
        raise ValueError("Build identity source commit is stale")
    dirty = subprocess.check_output(["git", "status", "--porcelain", "--", *SCOPES], cwd=ROOT, text=True)
    if dirty.strip():
        raise ValueError("Buildable source changed after freeze:\n" + dirty)
    files = [entry(p) for p in source_paths()]
    files.extend([test["report"] for test in tests])
    files.append(entry(ROOT / args.pdb))
    files.extend(inventories)
    result = {"schema": "v07-build-gate-1", "sourceCommit": head, "editModePassed": tests[0]["passed"],
              "playModePassed": tests[1]["passed"], "scopes": SCOPES, "identity": value, "tests": tests,
              "pdbAudit": entry(ROOT / args.pdb), "files": files,
              "expectedCounts": {"EditMode": args.expect_edit_count, "PlayMode": args.expect_play_count},
              "countPolicy": "Explicit frozen full-discovery counts; historical v0.6 floor is not v0.73 acceptance evidence"}
    target = ROOT / "evidence/v073/unity/build-gate.json"
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"gate": str(target), "boundFiles": len(files), "editMode": tests[0]["passed"], "playMode": tests[1]["passed"]}))


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("identity")
    p = sub.add_parser("gate")
    p.add_argument("--edit", required=True)
    p.add_argument("--play", required=True)
    p.add_argument("--pdb", required=True)
    p.add_argument("--expect-edit-count", required=True, type=int, help="Actual complete frozen Unity EditMode discovery count")
    p.add_argument("--expect-play-count", required=True, type=int, help="Actual complete frozen Unity PlayMode discovery count")
    args = parser.parse_args()
    if args.command == "identity":
        identity()
    else:
        gate(args)


if __name__ == "__main__":
    main()
