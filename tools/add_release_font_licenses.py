"""Add the missing font licences to the published p0.9.0-rc.1 and p1.0.0-rc.1 pre-releases (user, 2026-11-22: "字体许可证补").

Both releases' game packages ship three OFL fonts (the same files as today, checked by blob id at the tag targets):
Noto Sans CJK SC (dynamic fallback), Noto Sans SC Bold (static atlas) and Liberation Sans (TextMesh Pro's default
asset), but no licence file (FONTS_WRAP section 6). The published zips stay untouched (their receipts and hashes stay
valid); this adds one asset, FONT-LICENSES.zip, with each font's copyright notice (read from its own name table) and
the OFL text from the repository, and appends a short note to each release's text.

Credentials come from Git's configured helper and never enter files or output (as tools/publish_v10_release.py).
Dry run by default; pass --execute to touch the network. Idempotent: an existing asset with the same SHA-256 is kept and
the note is added only once. Every upload is read back and re-downloaded (SHA-256). Receipts go to
evidence/v11/delivery/font-licenses-<tag>.json.

usage: python -I tools/add_release_font_licenses.py [--execute]   (needs git on PATH and fontTools)
"""
import argparse
import hashlib
import io
import json
import subprocess
import sys
import urllib.error
import urllib.request
import zipfile
from datetime import datetime, timezone
from pathlib import Path

from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parents[1]
API = "https://api.github.com/repos/XDwudi/game2"
UPLOADS = "https://uploads.github.com/repos/XDwudi/game2"
EVIDENCE = ROOT / "evidence/v11/delivery"
RELEASES = {"p0.9.0-rc.1": 445721792, "p1.0.0-rc.1": 446359171}
ASSET = "FONT-LICENSES.zip"
MARKER = "<!-- font-licenses 2026-11-22 -->"
FONTS = ROOT / "DicePrototype/Assets/Game/Fonts"
TMP_FONTS = ROOT / "DicePrototype/Assets/TextMesh Pro/Fonts"
NOTE = (
    "\n\n" + MARKER + "\n"
    "**字体许可证 / Font licences**（2026-11-22 补充）：本发布的游戏包内含 Noto Sans CJK SC、Noto Sans SC Bold 与 "
    "Liberation Sans 三款字体，均以 SIL Open Font License 1.1 授权；各字体的版权声明与许可证全文见附件 `FONT-LICENSES.zip`。\n"
    "The game packages of this release include Noto Sans CJK SC, Noto Sans SC Bold and Liberation Sans, licensed under "
    "the SIL Open Font License, Version 1.1; the attached `FONT-LICENSES.zip` holds each font's copyright notice and "
    "the full licence text.\n"
)


def name_record(path, name_id):
    names = TTFont(str(path), lazy=True, fontNumber=0)["name"]
    record = names.getName(name_id, 3, 1, 0x409) or names.getName(name_id, 1, 0, 0)
    return record.toUnicode().strip() if record else ""


def bundle():
    """FONT-LICENSES.zip, byte-for-byte reproducible (fixed timestamps, fixed order)."""
    ofl = (FONTS / "LICENSE.txt").read_bytes()
    liberation = (TMP_FONTS / "LiberationSans - OFL.txt").read_bytes()
    noto = []
    for font, use_zh, use_en in [
        ("NotoSansCJKsc-Regular.otf", "正文的动态回落字体", "dynamic fallback for body text"),
        ("NotoSansSC-Bold.otf", "粗体字（静态字表）", "bold text (static atlas)"),
    ]:
        path = FONTS / font
        full, version, copyright_ = name_record(path, 4), name_record(path, 5).split(";")[0], name_record(path, 0)
        assert copyright_ and "Adobe" in copyright_, (font, copyright_)
        noto.append((font, full, version, copyright_, use_zh, use_en))
    lib_path = TMP_FONTS / "LiberationSans.ttf"
    lib_version = name_record(lib_path, 5)
    files = {}
    for font, full, version, copyright_, _, _ in noto:
        header = f"{full} ({version})\n{copyright_}\n\n".encode("utf-8")
        files[font.replace(".otf", "-OFL.txt")] = header + ofl
    files["LiberationSans-OFL.txt"] = liberation
    lines = [
        "字体许可证 / Font licences — DicePrototype p0.9.0-rc.1, p1.0.0-rc.1",
        "",
        "本发布的游戏包（Windows 与 macOS）内含以下字体，均以 SIL Open Font License, Version 1.1 授权。"
        "各字体的版权声明与许可证全文见同目录的文件；版权声明取自字体文件自身的名称记录。",
        "The game packages of this release (Windows and macOS) include the following fonts, all licensed under the SIL "
        "Open Font License, Version 1.1. Each font's copyright notice and the full licence text are in the files next to "
        "this one; the copyright notices are taken from the fonts' own name records.",
        "",
    ]
    for font, full, version, copyright_, use_zh, use_en in noto:
        lines.append(f"- {full}, {version} — {copyright_} — {use_zh} / {use_en} — {font.replace('.otf', '-OFL.txt')}")
    lines.append(f"- Liberation Sans, {lib_version} — Digitized data copyright (c) 2010 Google Corporation; "
                 "Copyright (c) 2012 Red Hat, Inc.; Reserved Font Names: Liberation, Arimo, Tinos, Cousine — "
                 "TextMesh Pro 的默认字体资源 / TextMesh Pro default font asset — LiberationSans-OFL.txt")
    lines += ["", "OFL: https://openfontlicense.org/ (http://scripts.sil.org/OFL)", ""]
    files["README.txt"] = "\n".join(lines).encode("utf-8")
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w", zipfile.ZIP_DEFLATED) as archive:
        for name in sorted(files):
            info = zipfile.ZipInfo("FONT-LICENSES/" + name, date_time=(2026, 11, 22, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            archive.writestr(info, files[name])
    return buffer.getvalue(), sorted(files)


def token():
    auth = subprocess.run(["git", "credential", "fill"], cwd=ROOT, input="protocol=https\nhost=github.com\n\n",
                          text=True, capture_output=True)
    if auth.returncode != 0:
        raise ValueError("Git credential helper failed")
    fields = dict(line.split("=", 1) for line in auth.stdout.splitlines() if "=" in line)
    if not fields.get("password"):
        raise ValueError("Git credential helper supplied no token")
    return fields["password"]


def call(method, url, secret, body=None, content_type="application/json", accept="application/vnd.github+json"):
    request = urllib.request.Request(url, data=body, method=method)
    request.add_header("Authorization", "Bearer " + secret)
    request.add_header("Accept", accept)
    request.add_header("X-GitHub-Api-Version", "2022-11-28")
    request.add_header("User-Agent", "release-publisher")
    if body is not None:
        request.add_header("Content-Type", content_type)
    try:
        with urllib.request.urlopen(request, timeout=600) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--execute", action="store_true")
    args = ap.parse_args()
    sys.stdout.reconfigure(encoding="utf-8")
    data, names = bundle()
    digest = hashlib.sha256(data).hexdigest()
    print(f"{ASSET}: {len(data)} bytes, sha256 {digest}, entries {names}")
    if not args.execute:
        for tag, rid in RELEASES.items():
            print(f"dry run: would add {ASSET} and the note to {tag} (release {rid})")
        return 0
    secret = token()
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    for tag, rid in RELEASES.items():
        status, raw = call("GET", f"{API}/releases/{rid}", secret)
        if status != 200:
            raise SystemExit(f"{tag}: GET release failed ({status})")
        release = json.loads(raw)
        assert release["tag_name"] == tag and release["prerelease"] and not release["draft"], tag
        existing = [a for a in release["assets"] if a["name"] == ASSET]
        if existing:
            asset = existing[0]
            print(f"{tag}: {ASSET} already present (id {asset['id']}, {asset['size']} bytes)")
        else:
            status, raw = call("POST", f"{UPLOADS}/releases/{rid}/assets?name={ASSET}", secret, data, "application/zip")
            if status != 201:
                raise SystemExit(f"{tag}: upload failed ({status}): {raw[:200]!r}")
            asset = json.loads(raw)
            print(f"{tag}: uploaded {ASSET} (id {asset['id']}, {asset['size']} bytes)")
        status, downloaded = call("GET", f"{API}/releases/assets/{asset['id']}", secret, accept="application/octet-stream")
        if status != 200 or hashlib.sha256(downloaded).hexdigest() != digest:
            raise SystemExit(f"{tag}: read-back failed ({status}) or SHA-256 differs")
        body = release.get("body") or ""
        if MARKER in body:
            print(f"{tag}: note already present")
            patched = False
        else:
            status, raw = call("PATCH", f"{API}/releases/{rid}", secret, json.dumps({"body": body + NOTE}).encode("utf-8"))
            if status != 200:
                raise SystemExit(f"{tag}: PATCH release failed ({status})")
            patched = True
            print(f"{tag}: note appended")
        receipt = {"schema": "v11-font-licenses-1", "utc": datetime.now(timezone.utc).isoformat(), "tag": tag,
                   "releaseId": rid, "asset": {"name": ASSET, "id": asset["id"], "size": len(data), "sha256": digest,
                                               "entries": names, "readBackSha256": digest},
                   "noteAppended": patched, "existingGameAssetsUntouched": [a["name"] for a in release["assets"] if a["name"] != ASSET]}
        (EVIDENCE / f"font-licenses-{tag}.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n",
                                                           encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
