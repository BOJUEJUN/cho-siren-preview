#!/usr/bin/env python3
"""2026-11-08 (少爷: "把我点击喜欢的这些歌都放到游戏里面…注意自己同步我的选择").

Mirrors the hearted takes of the local 选歌台 (Builds/song-picker/likes.json + catalog.json, audio in
Builds/music-1108) into the game: Assets/StreamingAssets/Music/song-<id>-<take>-<sha>.mp3 plus a
"suno-<id>-<take>" track in Assets/Resources/Data/music-playlist.json. Un-hearted takes are removed,
new ones are added (flagged isNew), the two authored songs are left alone. Run before every build.
"""
import glob, hashlib, json, os, shutil, subprocess, sys
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PICKER = os.path.join(ROOT, 'Builds/song-picker')
MUSIC_OUT = os.path.join(ROOT, 'Assets/StreamingAssets/Music')
PLAYLIST = os.path.join(ROOT, 'Assets/Resources/Data/music-playlist.json')
COVERS = ['cover-lilith', 'cover-amethyst', 'cover-catalena', 'cover-coralie']
FIXED_COVERS = {'rrose': 'cover-lilith', 'rcrown': 'cover-amethyst', 'rqueen': 'cover-lilith', 'manager': 'cover-catalena',
                'siren': 'cover-coralie', 'moonride': 'cover-amethyst', 'kirari': 'cover-catalena', 'midnight': 'cover-coralie'}


def duration(path):
    info = subprocess.run(['afinfo', path], capture_output=True, text=True).stdout
    line = [l for l in info.splitlines() if 'estimated duration' in l]
    return round(float(line[0].split(':')[-1].split()[0]), 2) if line else 0.0


def main():
    catalog = json.load(open(os.path.join(PICKER, 'catalog.json'), encoding='utf-8'))
    likes = {k for k, v in json.load(open(os.path.join(PICKER, 'likes.json'), encoding='utf-8')).items() if v}
    music_dir = os.path.normpath(os.path.join(PICKER, catalog['music_dir']))
    takes = {}
    for batch in catalog['batches']:
        for song in batch['songs']:
            for take, file in song['takes'].items():
                takes[f"{song['id']}-{take}"] = (song, take, os.path.join(music_dir, file))
    data = json.load(open(PLAYLIST, encoding='utf-8'))
    authored = [t for t in data['tracks'] if not t['id'].startswith('suno-')]
    old = {t['id']: t for t in data['tracks'] if t['id'].startswith('suno-')}
    wanted, keep_files = [], set()
    for key in sorted(likes, key=lambda k: list(takes).index(k) if k in takes else 1e9):
        if key not in takes:
            continue
        song, take, src = takes[key]
        tid = 'suno-' + key
        sha = hashlib.sha1(open(src, 'rb').read()).hexdigest()[:12]
        name = f"song-{key}-{sha}.mp3"
        dst = os.path.join(MUSIC_OUT, name)
        if not os.path.exists(dst):
            shutil.copyfile(src, dst)
        keep_files.add(name)
        cover = FIXED_COVERS.get(song['id']) or COVERS[int(hashlib.md5(song['id'].encode()).hexdigest(), 16) % len(COVERS)]
        entry = old.get(tid) or {"id": tid, "artist": "CHO-SIREN", "categories": ["home"], "isNew": True,
                                  "unlock": "", "unlockHint": "", "clip": ""}
        entry.update({"title": song['title'], "cover": "Art/Music1101/" + cover, "file": name, "duration": duration(src)})
        wanted.append(entry)
    counts = Counter(t['id'][5:].rsplit('-', 1)[0] for t in wanted)
    for t in wanted:
        base, take = t['id'][5:].rsplit('-', 1)
        t['title'] = t['title'].split(' (')[0] + (f" ({take.upper()})" if counts[base] > 1 else '')
    for path in glob.glob(os.path.join(MUSIC_OUT, 'song-*.mp3')):
        if os.path.basename(path) not in keep_files:
            os.remove(path)
            if os.path.exists(path + '.meta'):
                os.remove(path + '.meta')
    order = ["id", "title", "artist", "cover", "file", "clip", "categories", "isNew", "unlock", "unlockHint", "duration"]
    tracks = authored + [{k: t[k] for k in order} for t in wanted]
    lines = ['{', '  "tracks": ['] + ['    ' + json.dumps(t, ensure_ascii=False) + (',' if i < len(tracks) - 1 else '')
                                      for i, t in enumerate(tracks)] + ['  ]', '}']
    open(PLAYLIST, 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    added = sorted(set(t['id'] for t in wanted) - set(old)); removed = sorted(set(old) - set(t['id'] for t in wanted))
    print(f"liked songs in game: {len(wanted)} (added {added}, removed {removed})")


if __name__ == '__main__':
    sys.exit(main())
