"""Local, read-only integrity checks. This does not accept game quality or reviews."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import sys
import xml.etree.ElementTree as ET


class CheckError(ValueError):
    """A check cannot establish its stated integrity condition."""


class NoDTDTreeBuilder(ET.TreeBuilder):
    """Reject a DTD after XML decoding, including UTF-16 documents."""

    def doctype(self, name: str, pubid: str | None, system: str | None) -> None:
        raise CheckError("DTD/entity declarations are unsupported")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise CheckError(message)


def text(value: object, field: str) -> str:
    require(isinstance(value, str) and bool(value.strip()), f"{field}: expected nonempty string")
    return value


def relative_name(value: object, field: str) -> str:
    name = text(value, field)
    require("\\" not in name and ":" not in name, f"{field}: use a root-relative forward-slash path")
    parts = name.split("/")
    require(not PurePosixPath(name).is_absolute() and all(p not in ("", ".", "..") for p in parts),
            f"{field}: absolute, empty, dot and parent path segments are forbidden")
    require(not any(ord(c) < 32 for c in name), f"{field}: control character in path")
    return name


def contained_file(root: Path, value: object, field: str) -> Path:
    name = relative_name(value, field)
    resolved = (root / name).resolve()
    require(resolved.is_relative_to(root), f"{field}: path escapes root")
    require(resolved.is_file(), f"{field}: missing regular file: {name}")
    return resolved


def unique_pairs(pairs: list[tuple[str, object]]) -> dict:
    result = {}
    for key, value in pairs:
        require(key not in result, f"JSON duplicate key: {key}")
        result[key] = value
    return result


def read_manifest(root: Path, name: str) -> dict:
    path = contained_file(root, name, "manifest")
    data = json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=unique_pairs,
                      parse_constant=lambda value: (_ for _ in ()).throw(CheckError(f"JSON constant: {value}")))
    require(isinstance(data, dict), "manifest: expected object")
    require(type(data.get("schema_version")) is int and data["schema_version"] == 1,
            "manifest: schema_version must be integer 1")
    return data


def nonempty_list(value: object, field: str) -> list:
    require(isinstance(value, list) and bool(value), f"{field}: expected nonempty array")
    return value


def verify_entries(root: Path, entries: list, seen: set[str], field: str,
                   *, nonempty_files: bool = False, upstream: bool = False) -> list[Path]:
    paths = []
    for i, entry in enumerate(entries):
        label = f"{field}[{i}]"
        require(isinstance(entry, dict), f"{label}: expected object")
        if upstream:
            relative_name(entry.get("upstream_path"), f"{label}.upstream_path")
        path = contained_file(root, entry.get("path"), f"{label}.path")
        identity = os.path.normcase(str(path))
        require(identity not in seen, f"{label}: duplicate resolved path")
        seen.add(identity)
        digest = entry.get("sha256")
        require(isinstance(digest, str) and re.fullmatch(r"[0-9a-fA-F]{64}", digest) is not None,
                f"{label}.sha256: expected 64 hex characters, no placeholders")
        if nonempty_files:
            require(path.stat().st_size > 0, f"{label}: empty evidence/input file")
        with path.open("rb") as stream:
            actual = hashlib.file_digest(stream, "sha256").hexdigest()
        require(actual == digest.lower(), f"{label}: SHA-256 mismatch: {entry['path']}")
        paths.append(path)
    return paths


def check_sources(root: Path, manifest: str) -> dict:
    data = read_manifest(root, manifest)
    sources = nonempty_list(data.get("sources"), "sources")
    seen: set[str] = set()
    ids: set[str] = set()
    for i, source in enumerate(sources):
        label = f"sources[{i}]"
        require(isinstance(source, dict), f"{label}: expected object")
        source_id = text(source.get("id"), f"{label}.id")
        require(source_id not in ids, f"{label}: duplicate source id")
        ids.add(source_id)
        url = text(source.get("url"), f"{label}.url")
        require(url.startswith("https://") and len(url) > 8, f"{label}.url: expected HTTPS source URL")
        commit = source.get("commit")
        require(isinstance(commit, str) and re.fullmatch(r"[0-9a-fA-F]{40}", commit) is not None,
                f"{label}.commit: expected pinned 40-character Git commit")
        files = nonempty_list(source.get("files"), f"{label}.files")
        checked = verify_entries(root, files, seen, f"{label}.files", upstream=True)
        license_file = contained_file(root, source.get("license_path"), f"{label}.license_path")
        require(license_file in checked, f"{label}: license must be included in hashed files")
        require(license_file.stat().st_size > 0, f"{label}: license file is empty")
    return {"checked_sources": len(sources), "checked_files": len(seen)}


def check_snapshot(root: Path, manifest: str) -> dict:
    data = read_manifest(root, manifest)
    seen: set[str] = set()
    counts = {}
    for section in ("inputs", "artifacts"):
        entries = nonempty_list(data.get(section), section)
        verify_entries(root, entries, seen, section, nonempty_files=True)
        counts[section] = len(entries)
    return {"checked_files": len(seen), **counts}


def integer_attribute(node: ET.Element, name: str) -> int:
    value = node.get(name)
    require(value is not None and re.fullmatch(r"[0-9]+", value) is not None,
            f"{node.tag}.{name}: missing or invalid nonnegative integer")
    return int(value)


def validate_nunit_structure(doc: ET.Element, version: int) -> list[ET.Element]:
    """Validate placement before counting: metadata cannot contain hidden cases."""
    leaf_tags = {"environment", "culture-info", "property", "category", "message",
                 "stack-trace", "output", "filePath", "description", "setting",
                 "command-line", "id", "test", "class", "method", "cat", "prop"}
    children = {tag: set() for tag in leaf_tags}
    children.update({"properties": {"property"}, "categories": {"category"},
                     "reason": {"message"}, "failure": {"message", "stack-trace"}})
    if version == 3:
        metadata = {"properties", "reason", "failure", "output", "attachments", "assertions"}
        filter_nodes = {"or", "and", "not", "id", "test", "class", "method", "cat", "prop"}
        children.update({"test-run": {"command-line", "filter", "test-suite"},
                         "test-suite": metadata | {"environment", "settings", "test-suite", "test-case"},
                         "test-case": metadata, "settings": {"setting"},
                         "attachments": {"attachment"}, "attachment": {"filePath", "description"},
                         "assertions": {"assertion"}, "assertion": {"message", "stack-trace"}})
        children.update({tag: filter_nodes for tag in ("filter", "or", "and", "not")})
    else:
        metadata = {"categories", "properties", "reason", "failure", "output"}
        children.update({"test-results": {"environment", "culture-info", "test-suite"},
                         "test-suite": metadata | {"results"}, "test-case": metadata,
                         "results": {"test-suite", "test-case"}})
    result_tags = {"test-run", "test-results", "test-suite", "test-case", "assertion"}
    result_attributes = {"result", "executed", "success", "passed", "failed", "failures",
                         "errors", "not-run", "skipped", "inconclusive", "invalid"}
    cases = []
    for node in doc.iter():
        require(isinstance(node.tag, str) and not any(c in node.tag for c in "{}:"),
                "XML namespaces are unsupported; cannot ignore namespaced results")
        require(node.tag in children, f"unsupported NUnit element: {node.tag}")
        require(not any("{" in key or ":" in key for key in node.attrib),
                f"{node.tag}: namespaced attributes are unsupported")
        if node.tag not in result_tags:
            require(not result_attributes.intersection(node.attrib),
                    f"{node.tag}: result attributes in metadata are unsupported")
        for child in node:
            require(child.tag in children[node.tag],
                    f"illegal NUnit parent/child: {node.tag}/{child.tag}")
        if node.tag in result_tags:
            require(not node.get("label"), f"{node.tag}: result label requires review")
            for flag in ("executed", "success"):
                if flag in node.attrib:
                    require(node.get(flag, "").lower() == "true", f"{node.tag}.{flag}: must be true")
            if "runstate" in node.attrib:
                require(node.get("runstate") in ("Runnable", "Explicit"),
                        f"{node.tag}.runstate: non-runnable or unknown state")
        if node.tag == "assertion":
            require(node.get("result") == "Passed", "assertion has non-passing or unknown result")
        if node.tag == "test-case":
            cases.append(node)
    return cases


def check_nunit(root: Path, report: str, min_tests: int, required: list[str]) -> dict:
    require(type(min_tests) is int and min_tests >= 1, "min-tests must be at least 1")
    require(all(isinstance(name, str) and name.strip() for name in required), "required test names cannot be empty")
    require(len(required) == len(set(required)), "duplicate required test name")
    path = contained_file(root, report, "report")
    raw = path.read_bytes()
    require(raw.strip() != b"", "report is empty")
    try:
        doc = ET.fromstring(raw, parser=ET.XMLParser(target=NoDTDTreeBuilder()))
    except CheckError:
        raise
    except (ValueError, LookupError) as error:
        raise CheckError(f"unsupported XML encoding: {error}") from error
    require(doc.tag in ("test-run", "test-results"), "expected NUnit 3 test-run or NUnit 2 test-results root")
    version = 3 if doc.tag == "test-run" else 2
    cases = validate_nunit_structure(doc, version)
    require(len(cases) >= min_tests, f"only {len(cases)} case details; require at least {min_tests}")
    require(not any(node.tag in ("failure", "error") for node in doc.iter()), "report contains failure/error details")
    names = []
    for case in cases:
        name = text(case.get("fullname") if version == 3 else case.get("name"), "test-case full name")
        names.append(name)
        status = case.get("result")
        expected = "Passed" if version == 3 else "Success"
        require(status == expected, f"test {name}: result is {status!r}, expected {expected}")
        if version == 2:
            require(case.get("executed", "").lower() == "true", f"test {name}: was not executed")
            require(case.get("success", "").lower() == "true", f"test {name}: success is not true")
        elif "executed" in case.attrib:
            require(case.get("executed", "").lower() == "true", f"test {name}: was not executed")
        require(not case.get("label"), f"test {name}: result label requires review")
    require(len(names) == len(set(names)), "duplicate full test name; cannot establish distinct cases")
    missing = sorted(set(required) - set(names))
    require(not missing, "required tests missing: " + ", ".join(missing))
    if version == 3:
        nodes = [doc, *doc.iter("test-suite")]
        for node in nodes:
            require(node.get("result") == "Passed", f"{node.tag}: non-passing or unknown suite/run result")
            require(not node.get("label"), f"{node.tag}: result label requires review")
            count = len(list(node.iter("test-case")))
            for attr, expected in (("total", count), ("passed", count), ("failed", 0),
                                   ("inconclusive", 0), ("skipped", 0)):
                if node is doc or attr in node.attrib:
                    actual = integer_attribute(node, attr)
                    require(actual == expected, f"{node.tag}.{attr}: summary {actual} disagrees with all-passing case details {expected}")
            if node is doc or "testcasecount" in node.attrib:
                require(integer_attribute(node, "testcasecount") >= count,
                        f"{node.tag}.testcasecount: fewer declared tests than case details")
            for attr in ("warnings", "errors", "not-run", "ignored", "invalid"):
                if attr in node.attrib:
                    require(integer_attribute(node, attr) == 0, f"{node.tag}.{attr}: nonzero count")
    else:
        require(integer_attribute(doc, "total") == len(cases), "test-results.total disagrees with case details")
        for attr in ("errors", "failures", "not-run", "inconclusive", "ignored", "skipped", "invalid"):
            require(integer_attribute(doc, attr) == 0, f"test-results.{attr}: nonzero count")
        for node in doc.iter("test-suite"):
            require(node.get("result") == "Success", "NUnit 2 suite has non-success or unknown result")
            require(node.get("executed", "").lower() == "true" and node.get("success", "").lower() == "true",
                    "NUnit 2 suite is not executed successfully")
    return {"format": f"NUnit{version}", "checked_cases": len(cases), "required_tests": len(required)}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    sub = parser.add_subparsers(dest="command", required=True)
    sources = sub.add_parser("sources", help="verify pinned local source files and license presence")
    sources.add_argument("--manifest", required=True)
    snapshot = sub.add_parser("snapshot", help="verify declared input and artifact hashes")
    snapshot.add_argument("manifest")
    nunit = sub.add_parser("nunit", help="check NUnit result details and summary consistency")
    nunit.add_argument("report")
    nunit.add_argument("--min-tests", required=True, type=int)
    nunit.add_argument("--require-test", action="append", default=[])
    args = parser.parse_args(argv)
    result = {"check": args.command, "integrity_only": True}
    try:
        root = args.root.resolve()
        require(root.is_dir(), "root must be an existing directory")
        if args.command == "sources":
            details = check_sources(root, args.manifest)
        elif args.command == "snapshot":
            details = check_snapshot(root, args.manifest)
        else:
            details = check_nunit(root, args.report, args.min_tests, args.require_test)
        result.update(status="CHECKS_PASSED", **details)
        code = 0
    except (CheckError, OSError, UnicodeError, json.JSONDecodeError, ET.ParseError) as error:
        result.update(status="CHECKS_FAILED", errors=[str(error)])
        code = 1
    print(json.dumps(result, ensure_ascii=True, sort_keys=True))
    return code


if __name__ == "__main__":
    sys.exit(main())
