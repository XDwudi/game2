"""Read-only PNG/manifest integrity audit. This is not an art acceptance gate."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import io
import json
from pathlib import Path, PureWindowsPath
import platform
import re
import sys
import warnings

import PIL
from PIL import Image


REPO_ROOT = Path(__file__).resolve().parents[1]
SHA256 = re.compile(r"[0-9a-fA-F]{64}\Z")


class AuditError(ValueError):
    pass


def contained(root: Path, value: str | Path, *, relative_only: bool = False) -> Path:
    if not isinstance(value, (str, Path)) or not str(value).strip():
        raise AuditError("path must be a non-empty string")
    raw = str(value)
    win = PureWindowsPath(raw)
    path = Path(raw)
    if relative_only and (path.is_absolute() or win.drive or win.root):
        raise AuditError(f"manifest path must be repository-relative: {raw}")
    resolved = (root / path).resolve()
    if not resolved.is_relative_to(root):
        raise AuditError(f"path escapes root: {raw}")
    return resolved


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def unique_object(pairs: list[tuple[str, object]]) -> dict:
    result = {}
    for key, value in pairs:
        if key in result:
            raise AuditError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def read_bound(root: Path, entry: dict, label: str) -> tuple[dict, bytes]:
    if not isinstance(entry, dict):
        raise AuditError(f"{label}: expected an object")
    expected = entry.get("sha256")
    if not isinstance(expected, str) or not SHA256.fullmatch(expected):
        raise AuditError(f"{label}: invalid or missing sha256")
    path = contained(root, entry.get("path"), relative_only=True)
    if not path.is_file():
        raise AuditError(f"{label}: missing file: {entry.get('path')}")
    data = path.read_bytes()
    actual = digest(data)
    if actual != expected.lower():
        raise AuditError(f"{label}: SHA256 mismatch: {entry['path']} (actual {actual})")
    return {"path": path.relative_to(root).as_posix(), "sha256": actual,
            "bytes": len(data)}, data


def inspect_png(data: bytes, dimensions: object, require_alpha: bool) -> dict:
    if (not isinstance(dimensions, list) or len(dimensions) != 2
            or any(type(v) is not int or v <= 0 for v in dimensions)):
        raise AuditError("dimensions must be two positive integers")
    with warnings.catch_warnings():
        warnings.simplefilter("error", Image.DecompressionBombWarning)
        with Image.open(io.BytesIO(data)) as image:
            if image.format != "PNG":
                raise AuditError("asset is not a PNG")
            if getattr(image, "n_frames", 1) != 1:
                raise AuditError("animated PNG is outside this single-image audit")
            image.verify()
        with Image.open(io.BytesIO(data)) as image:
            image.load()
            if list(image.size) != dimensions:
                raise AuditError(f"dimensions mismatch: actual {list(image.size)}, expected {dimensions}")
            mode = image.mode
            alpha = image.convert("RGBA").getchannel("A")
            histogram = alpha.histogram()
            minimum, maximum = alpha.getextrema()
            if maximum == 0:
                raise AuditError("asset is entirely transparent (no visible pixels)")
            if require_alpha and minimum == 255:
                raise AuditError("transparency required but asset is fully opaque")
            return {"format": "PNG", "mode": mode, "dimensions": list(image.size),
                    "alphaExtrema": [minimum, maximum],
                    "transparentPixels": histogram[0],
                    "partialAlphaPixels": sum(histogram[1:255]),
                    "opaquePixels": histogram[255],
                    "visibleBounds": list(alpha.getbbox())}


def audit(root: Path, manifest_path: str | Path, *, require_alpha: bool = False,
          runtime_dir: str | Path | None = None) -> dict:
    root = root.resolve()
    report = {
        "schemaVersion": 1,
        "scope": "PNG and manifest integrity only; not game or artistic acceptance",
        "createdUtc": datetime.now(timezone.utc).isoformat(),
        "tool": {"path": "tools/art_asset_audit.py",
                 "sha256": digest(Path(__file__).read_bytes()),
                 "python": platform.python_version(), "pillow": PIL.__version__},
        "root": str(root), "requireAlpha": require_alpha,
        "runtimeDirectory": None, "inputs": [], "assets": [], "errors": [],
        "notVerified": [
            "style consistency, subject correctness, edge halos, legibility and visual quality",
            "Unity importer settings, GUID bindings, scene usage and actual rendering",
            "native editable-source roundtrip, game behaviour, builds and performance",
            "human approval, rights clearance and production acceptance",
        ],
    }
    try:
        path = contained(root, manifest_path)
        manifest_bytes = path.read_bytes()
        report["manifest"] = {"path": path.relative_to(root).as_posix(),
                              "sha256": digest(manifest_bytes)}
        manifest = json.loads(manifest_bytes.decode("utf-8-sig"), object_pairs_hook=unique_object)
        if not isinstance(manifest, dict):
            raise AuditError("manifest must be an object")
        assets = manifest.get("assets")
        if not isinstance(assets, list) or not assets:
            raise AuditError("assets must be a non-empty array")
        references = manifest.get("references")
        if not isinstance(references, list):
            raise AuditError("references must be an array (may be empty)")
        runtime = contained(root, runtime_dir) if runtime_dir is not None else None
        if runtime is not None:
            if not runtime.is_dir():
                raise AuditError("runtime directory does not exist")
            report["runtimeDirectory"] = runtime.relative_to(root).as_posix()
        bound_inputs = [(key, {"path": manifest.get(key),
                                "sha256": manifest.get(key + "Sha256")})
                        for key in ("inputSpec", "contentSource")]
        bound_inputs += [(f"reference[{i}]", item) for i, item in enumerate(references)]
        for label, entry in bound_inputs:
            try:
                bound, _ = read_bound(root, entry, label)
                report["inputs"].append({"role": label, **bound})
            except (AuditError, OSError) as error:
                report["errors"].append(str(error))
        ids, paths, runtime_names = set(), set(), set()
        for index, asset in enumerate(assets):
            item = {"index": index, "result": "FAILED"}
            report["assets"].append(item)
            try:
                if not isinstance(asset, dict):
                    raise AuditError("asset must be an object")
                content_id = asset.get("contentId")
                if not isinstance(content_id, str) or not content_id.strip():
                    raise AuditError("contentId must be a non-empty string")
                item["contentId"] = content_id
                if content_id.casefold() in ids:
                    raise AuditError(f"duplicate contentId: {content_id}")
                ids.add(content_id.casefold())
                bound, data = read_bound(root, asset, f"asset[{index}]")
                item.update(bound)
                asset_path = contained(root, bound["path"])
                if asset_path in paths:
                    raise AuditError(f"duplicate asset path: {bound['path']}")
                paths.add(asset_path)
                if asset_path.suffix.lower() != ".png":
                    raise AuditError("asset path must have .png suffix")
                item["image"] = inspect_png(data, asset.get("dimensions"), require_alpha)
                if runtime is not None:
                    runtime_name = asset_path.name.casefold()
                    if runtime_name in runtime_names:
                        raise AuditError(f"runtime filename collision: {asset_path.name}")
                    runtime_names.add(runtime_name)
                    counterpart = contained(root, runtime / asset_path.name)
                    runtime_bound, _ = read_bound(root, {
                        "path": counterpart.relative_to(root).as_posix(),
                        "sha256": bound["sha256"]}, "runtime PNG")
                    meta = contained(root, str(counterpart) + ".meta")
                    if not meta.is_file():
                        raise AuditError(f"runtime .meta missing: {meta.relative_to(root)}")
                    meta_bytes = meta.read_bytes()
                    if not meta_bytes:
                        raise AuditError(f"runtime .meta is empty: {meta.relative_to(root)}")
                    item["runtime"] = {**runtime_bound,
                                       "metaPath": meta.relative_to(root).as_posix(),
                                       "metaSha256": digest(meta_bytes)}
                item["result"] = "INTEGRITY_PASSED"
            except (AuditError, OSError, ValueError, SyntaxError,
                    Image.DecompressionBombError, Image.DecompressionBombWarning) as error:
                item["error"] = str(error)
                report["errors"].append(f"asset[{index}]: {error}")
    except (AuditError, OSError, ValueError) as error:
        report["errors"].append(str(error))
    report["checkedAssets"] = len(report["assets"])
    report["passedAssets"] = sum(a["result"] == "INTEGRITY_PASSED" for a in report["assets"])
    report["result"] = "FAILED" if report["errors"] else "INTEGRITY_PASSED"
    return report


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--root", type=Path, default=REPO_ROOT)
    parser.add_argument("--output", required=True, help="New report path within root; never overwrites")
    parser.add_argument("--require-alpha", action="store_true",
                        help="Reject fully opaque PNGs; does not prove a clean cutout")
    parser.add_argument("--runtime-dir", help="Directory containing same-named byte-identical PNGs and .meta files")
    args = parser.parse_args(argv)
    try:
        root = args.root.resolve()
        output = contained(root, args.output)
        if output.exists():
            raise AuditError("output already exists; use a new filename to preserve inputs and prior evidence")
        report = audit(root, args.manifest, require_alpha=args.require_alpha,
                       runtime_dir=args.runtime_dir)
        output.parent.mkdir(parents=True, exist_ok=True)
        with output.open("x", encoding="utf-8", newline="\n") as stream:
            json.dump(report, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
        print(f"{report['result']}: {report['passedAssets']}/{report['checkedAssets']} PNG assets; {output}")
        for error in report["errors"]:
            print(error, file=sys.stderr)
        return 1 if report["errors"] else 0
    except (AuditError, OSError) as error:
        print(f"AUDIT_ERROR: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
