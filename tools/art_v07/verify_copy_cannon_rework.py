"""Reopen the two repaired art07 masters with Krita; keep bounded evidence.

This is source/export validation, not a substitute for Unity or art acceptance.
The review directory must already contain the original before/ files.
"""
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw, ImageFont
import argparse, datetime, hashlib, io, json, os, shutil, subprocess, sys, zipfile
import xml.etree.ElementTree as ET
from build_art_samples import C, MANIFEST, ROOT, dilate


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def compare_visible(a, b):
    if a.size != b.size:
        return False
    # Compare RGB at every nonzero-alpha texel; ignore intentionally bled RGB.
    mask = a.getchannel('A').point(lambda v: 255 if v else 0)
    rgb = ImageChops.difference(a.convert('RGB'), b.convert('RGB'))
    visible = Image.new('RGB', a.size)
    visible.paste(rgb, (0, 0), mask)
    return not visible.getbbox() and not ImageChops.difference(a.getchannel('A'), b.getchannel('A')).getbbox()


def make_sheet(out, records):
    font = ImageFont.truetype('C:/Windows/Fonts/consola.ttf', 16)
    sheet = Image.new('RGB', (1104, 640), C['felt'])
    draw = ImageDraw.Draw(sheet)
    for row, record in enumerate(records):
        y = row * 320
        for col, (label, bg) in enumerate([('felt', C['felt']), ('paper', C['paper']), ('dark', C['bgDeep'])]):
            x = col * 368
            draw.rectangle((x, y, x + 367, y + 319), fill=bg)
            fg = C['ink'] if label == 'paper' else C['paper']
            draw.text((x + 12, y + 10), record['id'] + ' / ' + label, font=font, fill=fg)
            im = Image.open(ROOT / 'DicePrototype' / record['runtimePath']).convert('RGBA')
            for size, dx, dy in [(256, 8, 40), (64, 280, 60), (32, 296, 176)]:
                small = im.resize((size, size), Image.Resampling.NEAREST)
                sheet.paste(small, (x + dx, y + dy), small)
                draw.text((x + dx, y + dy + size + 6), str(size) + 'px', font=font, fill=fg)
    sheet.save(out / 'after-three-backgrounds.png')
    comparison = Image.new('RGB', (720, 256), C['felt'])
    draw = ImageDraw.Draw(comparison)
    for row, record in enumerate(records):
        for col, (label, path) in enumerate([('before', out / 'before' / (record['assetId'] + '.png')), ('after', ROOT / 'DicePrototype' / record['runtimePath'])]):
            im = Image.open(path).convert('RGBA')
            x, y = col * 360, row * 128
            for size, dx in [(64, 16), (32, 104)]:
                small = im.resize((size, size), Image.Resampling.NEAREST)
                comparison.paste(small, (x + dx, y + 24), small)
            draw.text((x + 164, y + 38), record['id'] + ' ' + label, font=font, fill=C['paper'])
    comparison.save(out / 'before-after-64-32.png')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--krita', required=True)
    parser.add_argument('--review', required=True)
    parser.add_argument('--preview-only', action='store_true')
    args = parser.parse_args()
    out = (ROOT / args.review).resolve()
    assert out.is_relative_to(ROOT / 'ArtSource/Art07/Review') and (out / 'before').is_dir()
    records = [dict(a, assetId=a['icon_id'], runtimePath=a['iconExport']) for a in MANIFEST['contentResources'] if a['id'] in ('C07', 'C42')]
    make_sheet(out, records)
    if args.preview_only:
        print(out / 'after-three-backgrounds.png')
        return
    exe = Path(args.krita).resolve()
    checks = []
    rows = []

    def check(name, value, detail):
        checks.append({'id': name, 'passed': bool(value), 'detail': detail})

    for record in records:
        asset_id = record['assetId']
        original_src = ROOT / record['iconSource']
        ora = original_src.with_suffix('.ora')
        # The native application writes a new candidate only. Production KRA
        # replacement is a separate reversible step after successful checks.
        candidates = out / 'candidate-sources'
        candidates.mkdir(exist_ok=True)
        src = candidates / (asset_id + '.kra')
        runtime = ROOT / 'DicePrototype' / record['runtimePath']
        reopened = out / (asset_id + '-krita-reopened.png')
        log = []
        for original, destination in [(ora, src), (src, reopened)]:
            if destination.exists():
                raise FileExistsError('Refusing to overwrite candidate evidence: ' + str(destination))
            command = [str(exe), '--nosplash', '--export', '--export-filename', str(destination), str(original)]
            try:
                result = subprocess.run(command, cwd=exe.parent, timeout=75, capture_output=True, creationflags=subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0)
            except subprocess.TimeoutExpired:
                log.append({'argv': command, 'timeoutSeconds': 75, 'completed': False})
                failed_stamp = datetime.datetime.now().strftime('%H%M%S')
                (out / (asset_id + '-krita-timeout-' + failed_stamp + '.json')).write_text(json.dumps(log, indent=2), encoding='utf8')
                raise
            log.append({'argv': command, 'exitCode': result.returncode, 'stdout': result.stdout.decode('utf8', 'replace'), 'stderr': result.stderr.decode('utf8', 'replace')})
            if result.returncode != 0 or not destination.exists():
                (out / (asset_id + '-krita-failed.json')).write_text(json.dumps(log, indent=2), encoding='utf8')
                raise RuntimeError('Krita did not complete ' + str(destination))
        (out / (asset_id + '-krita-log.json')).write_text(json.dumps(log, indent=2), encoding='utf8')
        im = Image.open(runtime).convert('RGBA')
        with zipfile.ZipFile(src) as archive:
            check(asset_id + '/kra_mime', archive.read('mimetype') == b'application/x-krita', 'Real Krita document')
            xml = ET.fromstring(archive.read('maindoc.xml'))
            layer_count = len(xml.findall('.//{*}layer'))
            check(asset_id + '/editable_layers', layer_count >= 4, layer_count)
        with zipfile.ZipFile(ora) as archive:
            merged = Image.open(io.BytesIO(archive.read('mergedimage.png'))).convert('RGBA')
        check(asset_id + '/ora_export_equal', compare_visible(im, merged), 'Visible RGB and every alpha match')
        check(asset_id + '/real_krita_reopen_equal', compare_visible(im, Image.open(reopened).convert('RGBA')), 'Actual KRA opened and exported by Krita')
        check(asset_id + '/source_size', im.size == (256, 256), list(im.size))
        box = im.getchannel('A').getbbox()
        check(asset_id + '/24px_safe_border', box[0] >= 24 and box[1] >= 24 and box[2] <= 232 and box[3] <= 232, box)
        small = im.resize((64, 64), Image.Resampling.NEAREST).resize((256, 256), Image.Resampling.NEAREST)
        check(asset_id + '/64px_logical_grid', compare_visible(im, small), '4x nearest-neighbor pixels, including transparency')
        translucent = sum(1 for a in im.getchannel('A').getdata() if 0 < a < 255)
        check(asset_id + '/translucent_body', translucent > 0, translucent)
        before = out / 'before' / (asset_id + '.png')
        check(asset_id + '/changed_from_reviewed_failure', sha(before) != sha(runtime), {'before': sha(before), 'after': sha(runtime)})
        for file in (src, ora, runtime):
            shutil.copy2(file, out / ('after-' + file.name))
        rows.append({'id': record['id'], 'motif': record['iconMotif'], 'palette': record['palette'], 'sourcePath': str(src.relative_to(ROOT)).replace('\\', '/'), 'targetSourcePath': record['iconSource'], 'sourceSha256': sha(src), 'oraSha256': sha(ora), 'runtimePath': record['runtimePath'], 'runtimeSha256': sha(runtime), 'layerCount': layer_count, 'reopenedSha256': sha(reopened)})
        print('REOPENED', record['id'], flush=True)

    # The alpha-bleed repair must not change existing opaque icon exports.
    unchanged = []
    for record in MANIFEST['contentResources']:
        if record['id'] in ('C07', 'C42'):
            continue
        with zipfile.ZipFile((ROOT / record['iconSource']).with_suffix('.ora')) as archive:
            im = Image.open(io.BytesIO(archive.read('mergedimage.png'))).convert('RGBA')
        expected = Image.open(ROOT / 'DicePrototype' / record['iconExport']).convert('RGBA')
        difference = ImageChops.difference(dilate(im), expected)
        equal = im.size == expected.size and not difference.convert('RGB').getbbox() and not difference.getchannel('A').getbbox()
        check(record['id'] + '/unaffected_export_rgba', equal, 'Revised bleed yields same existing RGBA without writing asset')
        unchanged.append({'id': record['id'], 'sha256': sha(ROOT / 'DicePrototype' / record['iconExport'])})
    result = {'createdUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'scope': 'C07/C42 offline repair only', 'status': 'REVIEW', 'unityVerified': False, 'humanStyleAccepted': False, 'specSha256': sha(ROOT / 'docs/specs/v07/ART_SPEC.md'), 'manifestSha256': sha(ROOT / 'docs/specs/v07/dice_art_v0_7_manifest.json'), 'generatorSha256': sha(ROOT / 'tools/art_v07/build_content_icons.py'), 'alphaExportHelperSha256': sha(ROOT / 'tools/art_v07/build_art_samples.py'), 'checkerSha256': sha(Path(__file__)), 'tool': 'Krita5.3.4', 'python': sys.version, 'passed': all(c['passed'] for c in checks), 'checkCount': len(checks), 'records': rows, 'unchanged69Exports': unchanged, 'checks': checks}
    (out / 'report.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf8')
    print(json.dumps({'passed': result['passed'], 'checks': len(checks), 'report': str(out / 'report.json')}))
    raise SystemExit(0 if result['passed'] else 1)


if __name__ == '__main__':
    main()
