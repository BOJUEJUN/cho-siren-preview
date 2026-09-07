"""Slice user-authorized AI 3x3 sheets, preserve alpha, normalize independent UI icons."""
import argparse
import json
import uuid
from collections import deque
from pathlib import Path
from PIL import Image, ImageDraw

parser = argparse.ArgumentParser()
parser.add_argument('--sheet', action='append', required=True, help='group=absolute.png')
parser.add_argument('--output', required=True)
parser.add_argument('--contact', required=True)
parser.add_argument('--replace-generated', action='store_true')
args = parser.parse_args()
output = Path(args.output)
output.mkdir(parents=True, exist_ok=True)
records = []
icons = []
for spec in args.sheet:
    group, source = spec.split('=', 1)
    sheet = Image.open(source).convert('RGBA')
    alpha = sheet.getchannel('A')
    if alpha.getextrema()[0] != 0:
        raise ValueError(f'{group}: generated background is not transparent')
    for index in range(9):
        col, row = index % 3, index // 3
        box = (round(col * sheet.width / 3), round(row * sheet.height / 3),
               round((col + 1) * sheet.width / 3), round((row + 1) * sheet.height / 3))
        cell = sheet.crop(box)
        # AI grids can have a few pixels from the neighbouring cell near a crop edge.
        # Remove only tiny disconnected edge fragments, never repaint the item itself.
        mask = cell.getchannel('A').point(lambda a: 255 if a > 24 else 0)
        pixels = mask.load()
        seen = set()
        components = []
        for yy in range(cell.height):
            for xx in range(cell.width):
                if not pixels[xx, yy] or (xx, yy) in seen:
                    continue
                queue = deque([(xx, yy)])
                seen.add((xx, yy))
                points = []
                while queue:
                    px, py = queue.popleft()
                    points.append((px, py))
                    for nx, ny in ((px-1, py), (px+1, py), (px, py-1), (px, py+1)):
                        if 0 <= nx < cell.width and 0 <= ny < cell.height and pixels[nx, ny] and (nx, ny) not in seen:
                            seen.add((nx, ny))
                            queue.append((nx, ny))
                components.append(points)
        largest = max(map(len, components), default=0)
        a = cell.getchannel('A')
        ap = a.load()
        for points in components:
            cx = sum(p[0] for p in points) / len(points)
            cy = sum(p[1] for p in points) / len(points)
            edge = cx < cell.width*.12 or cx > cell.width*.88 or cy < cell.height*.12 or cy > cell.height*.88
            if edge and len(points) < largest*.06:
                for px, py in points:
                    for nx in range(max(0, px-2), min(cell.width, px+3)):
                        for ny in range(max(0, py-2), min(cell.height, py+3)):
                            ap[nx, ny] = 0
        cell.putalpha(a)
        bounds = cell.getchannel('A').point(lambda a: 255 if a > 12 else 0).getbbox()
        if bounds is None:
            raise ValueError(f'{group}/{index}: empty cell')
        sprite = cell.crop(bounds)
        sprite.thumbnail((224, 224), Image.Resampling.LANCZOS)
        icon = Image.new('RGBA', (256, 256))
        icon.alpha_composite(sprite, ((256 - sprite.width) // 2, (256 - sprite.height) // 2))
        name = f'accessory-{group}-{index+1:02d}-v1.png'
        destination = output / name
        if destination.exists() and not args.replace_generated:
            raise FileExistsError(destination)
        icon.save(destination, optimize=True)
        # Stable importer metadata for generated files; original input is never modified.
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'cho-siren:collection54:' + name).hex
        (output / (name + '.meta')).write_text(f'''fileFormatVersion: 2
guid: {guid}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  isReadable: 0
  textureSettings:
    filterMode: 1
    wrapU: 1
    wrapV: 1
  maxTextureSize: 256
  textureFormat: 1
  textureCompression: 0
  spriteMode: 1
  spritePixelsToUnits: 100
  spriteMeshType: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  alphaIsTransparency: 1
  alphaUsage: 1
  textureType: 8
  assetBundleName:
  assetBundleVariant:
''')
        icons.append((group, index + 1, icon))
        records.append({'group': group, 'number': index + 1, 'source': source,
                        'sourceCell': box, 'alphaBounds': bounds, 'output': str(destination)})
contact = Image.new('RGB', (9 * 144, ((len(icons) + 8) // 9) * 174), '#11132b')
draw = ImageDraw.Draw(contact)
for n, (group, number, icon) in enumerate(icons):
    thumb = icon.resize((138, 138), Image.Resampling.LANCZOS)
    x, y = n % 9 * 144 + 3, n // 9 * 174
    contact.paste(thumb, (x, y), thumb)
    draw.text((x + 28, y + 142), f'{group}-{number:02d}', fill='#e5dcff')
contact_path = Path(args.contact)
contact_path.parent.mkdir(parents=True, exist_ok=True)
contact.save(contact_path)
contact_path.with_suffix('.json').write_text(json.dumps(records, ensure_ascii=False, indent=2))
print(json.dumps({'count': len(records), 'output': str(output), 'contact': str(contact_path)}))
