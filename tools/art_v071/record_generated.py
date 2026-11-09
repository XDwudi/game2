"""Archive one observed built-in ImageGen result, never generate or edit pixels."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('id')
    parser.add_argument('generated_path', type=Path)
    parser.add_argument('--prompts', type=Path, default=ROOT / 'cache/v071-full/prompts.json')
    args = parser.parse_args()
    plan = json.loads(args.prompts.read_text(encoding='utf-8-sig'))
    rows = [e for e in plan['entries'] if e['id'] == args.id]
    if len(rows) != 1:
        raise ValueError('ID must match exactly one actual authored prompt')
    job = rows[0]
    output = ROOT / 'ArtSource/Art071/IllustrationsFull' / args.id
    output.mkdir(parents=True, exist_ok=True)
    target = output / 'original.png'
    if target.exists() or (output / 'record.json').exists():
        raise ValueError('Do not overwrite a previous image or record; keep revision history')
    with Image.open(args.generated_path) as image:
        image.load()
        if image.format != 'PNG' or image.mode != 'RGBA':
            raise ValueError('Expected original transparent RGBA PNG')
        dimensions = list(image.size)
        alpha = image.getchannel('A').getextrema()
        if alpha != (0, 255):
            raise ValueError('Expected genuine transparency and an opaque subject')
    shutil.copyfile(args.generated_path, target)
    if sha(args.generated_path) != sha(target):
        raise ValueError('Copy byte mismatch')
    (output / 'prompt.txt').write_text(job['prompt'] + '\n', encoding='utf-8')
    record = {'id': args.id, 'displayName': job['displayName'], 'category': job['category'],
        'configDescription': job['configDescription'], 'subject': job['subject'],
        'tool': 'built-in imagegen', 'modelSnapshot': 'not exposed by tool',
        'sourceToolPath': str(args.generated_path), 'file': 'original.png',
        'sha256': sha(target), 'dimensions': dimensions, 'mode': 'RGBA',
        'promptFile': 'prompt.txt', 'promptSha256': sha(output / 'prompt.txt'),
        'references': [{'path': str(Path(p).relative_to(ROOT)).replace('\\', '/'),
            'sha256': sha(p), 'role': 'style master' if i == 0 else 'style sample'}
            for i, p in enumerate(job['referenced_image_paths'])],
        'configSha256': sha(ROOT / 'DicePrototype/Assets/Game/Data/P0Config.json'),
        'editableSourceType': 'original raster PNG, not layered KRA/PSD',
        'status': 'generated candidate; independent visual and engine checks pending'}
    (output / 'record.json').write_text(json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'id': args.id, 'path': str(target), 'sha256': sha(target), 'dimensions': dimensions}, ensure_ascii=False))


if __name__ == '__main__':
    main()
