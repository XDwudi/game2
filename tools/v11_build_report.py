"""Read Unity 6000.6.7f1's real serialized .buildreport with the Editor's binary2text."""
import re
import subprocess
import tempfile
from pathlib import Path


def value(block, name, kind):
    matches = re.findall(rf'^\s*{name} (.*?) \({re.escape(kind)}\)$', block, re.MULTILINE)
    if len(matches) != 1:
        raise ValueError(f"Missing or ambiguous Unity BuildReport field {name}")
    if kind == "string":
        if not (matches[0].startswith('"') and matches[0].endswith('"')):
            raise ValueError("Invalid serialized string " + name)
        return matches[0][1:-1]
    return matches[0]


def rows(block, name, kind):
    match = re.search(rf'^\t{name}  \(vector\)\n\t\tsize (\d+) \(int\)\n((?:\t\t[^\n]*\n|\n)*)',
                      block, re.MULTILINE)
    if not match:
        raise ValueError("Missing BuildReport vector " + name)
    parts = match[2].split(f"\t\tdata  ({kind})\n")
    if parts[0].strip() or len(parts) - 1 != int(match[1]):
        raise ValueError("Incomplete BuildReport vector " + name)
    return parts[1:]


def parse(text):
    records = re.split(r"^ID: (-?\d+) \(ClassID: \d+\) (\w+)\n", text, flags=re.MULTILINE)
    objects = {}
    for index in range(1, len(records), 3):
        object_id, type_name, body = records[index:index + 3]
        if object_id in objects:
            raise ValueError("Duplicate Unity object id")
        objects[object_id] = (type_name, body)
    summaries = [body for name, body in objects.values() if name == "BuildReport"]
    if len(summaries) != 1:
        raise ValueError("Expected one native BuildReport")
    main = summaries[0]
    summary = main.split("\tm_Summary  (BuildSummary)\n", 1)[-1].split("\n\tm_", 1)[0]
    result = {name: value(summary, name, kind) for name, kind in (
        ("buildGUID", "GUID"), ("platformName", "string"), ("buildProfilePath", "string"),
        ("outputPath", "string"), ("totalSize", "UInt64"), ("totalErrors", "int"), ("buildResult", "int"))}
    files = {}
    for row in rows(main, "m_Files", "BuildReportFile"):
        path = value(row, "path", "string")
        if not path or path in files:
            raise ValueError("Invalid or duplicate build output " + path)
        files[path] = int(value(row, "totalSize", "UInt64"))
    assets = set()
    packed_objects = 0
    groups = set()
    for ref in rows(main, "m_Appendices", "PPtr<Object>"):
        target = value(ref, "m_PathID", "SInt64")
        if value(ref, "m_FileID", "int") != "0" or target not in objects:
            raise ValueError("Unresolved BuildReport appendix " + target)
        kind, body = objects[target]
        if kind == "PackedAssets":
            if target in groups:
                raise ValueError("Duplicate packed assets appendix " + target)
            groups.add(target)
            for record in rows(body, "m_Contents", "BuildReportPackedAssetInfo"):
                source = value(record, "buildTimeAssetPath", "string").replace("\\", "/")
                guid = value(record, "sourceAssetGUID", "GUID")
                if not re.fullmatch("[0-9a-f]{32}", guid):
                    raise ValueError("Malformed packed asset GUID")
                assets.add((source, guid))
                packed_objects += 1
    if not files or not packed_objects or groups != {key for key, (kind, _) in objects.items() if kind == "PackedAssets"}:
        raise ValueError("Incomplete native BuildReport packed asset or output inventory")
    result.update(files=files, assets=assets, packedGroups=len(groups), packedObjects=packed_objects)
    return result


def read_native(path, binary2text):
    with tempfile.TemporaryDirectory() as temp:
        decoded = Path(temp) / "report.txt"
        subprocess.run([str(binary2text), str(path), str(decoded), "-largebinaryhashonly"], check=True)
        return parse(decoded.read_text(encoding="utf-8-sig"))
