"""Bind real Unity evidence to a frozen source commit; never invent test status."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SCOPES = ["DicePrototype/Assets", "DicePrototype/Packages", "DicePrototype/ProjectSettings"]
IDENTITY = "DicePrototype/Assets/Resources/BuildIdentity.json"
CONFIG = "DicePrototype/Assets/Game/Data/P0Config.json"
GOLDEN = "DicePrototype/Assets/Game/Data/GoldenScores.json"


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
    if platform not in platforms:
        raise ValueError(f"{platform}: wrong report mode")
    return {"mode": platform, "passed": expected, "names": sorted(names), "report": entry(path)}


def identity():
    dirty = subprocess.check_output(["git", "status", "--porcelain", "--", *SCOPES], cwd=ROOT, text=True)
    if dirty.strip():
        raise ValueError("Commit all project sources before creating identity:\n" + dirty)
    cfg = read_json(ROOT / CONFIG)
    value = {"releaseVersion": "p0.5.0", "sourceCommit": subprocess.check_output(
        ["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        "contentConfigVersion": cfg["configVersion"], "contentConfigSha256": sha(ROOT / CONFIG),
        "saveSchemaVersion": cfg["saveSchemaVersion"], "goldenCaseSetSha256": sha(ROOT / GOLDEN),
        "unityEditorVersion": "6000.6.7f1", "backend": "Mono"}
    path = ROOT / IDENTITY
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(value))


def gate(args):
    tests = [verify_nunit(ROOT / args.edit, "EditMode", 552), verify_nunit(ROOT / args.play, "PlayMode", 24)]
    proof = read_json(ROOT / args.pdb)
    if proof.get("mismatches") != 0 or len(proof.get("assemblies", [])) < 5:
        raise ValueError("Independent Unity PDB audit is missing or failed")
    documents = proof.get("documents", [])
    if not documents:
        raise ValueError("PDB audit contains no project source documents")
    for document in documents:
        source = Path(document["source"])
        if not source.is_absolute():
            source = ROOT / "DicePrototype" / source
        expected = document["expectedChecksum"]
        algorithm = "sha1" if len(expected) == 40 else "sha256"
        actual = hashlib.new(algorithm, source.read_bytes()).hexdigest()
        if not document["matches"] or actual != expected:
            raise ValueError("Current source differs from compiled PDB: " + str(source))
    for assembly in proof["assemblies"]:
        if not assembly["pdbMatchesDll"]:
            raise ValueError("PDB does not match assembly")
        for suffix, key in (("dll", "dllSha256"), ("pdb", "pdbSha256")):
            path = ROOT / "DicePrototype/Library/ScriptAssemblies" / (assembly["name"] + "." + suffix)
            if sha(path) != assembly[key]:
                raise ValueError("Recompiled after PDB audit: " + str(path))
    value = read_json(ROOT / IDENTITY)
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
    result = {"schema": "v05-build-gate-1", "sourceCommit": head, "editModePassed": 552,
              "playModePassed": 24, "scopes": SCOPES, "identity": value, "tests": tests, "files": files}
    target = ROOT / "evidence/v05/unity/build-gate.json"
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"gate": str(target), "boundFiles": len(files), "editMode": 552, "playMode": 24}))


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("identity")
    p = sub.add_parser("gate")
    p.add_argument("--edit", required=True)
    p.add_argument("--play", required=True)
    p.add_argument("--pdb", required=True)
    args = parser.parse_args()
    if args.command == "identity":
        identity()
    else:
        gate(args)


if __name__ == "__main__":
    main()
