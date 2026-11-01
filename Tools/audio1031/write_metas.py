#!/usr/bin/env python3
"""Unity import settings for the 2026-10-31 audio (keeps every existing GUID; new files get a fresh one).

SFX   (Audio/Sfx/*.wav)            Vorbis q0.70, Decompress On Load, preload  -> zero-latency one-shots
Voice (Voice/Enemy, Announcer, Battle/*-defeat-*)  Vorbis q0.50, Compressed In Memory, preload
Files are already mono and loudness-mastered, so forceToMono / normalize stay off (normalize would undo the
per-event loudness). WebGL uses the same setting (Unity re-encodes WebGL audio for the browser at build time).
"""
import pathlib
import re
import uuid

REPO = pathlib.Path(__file__).resolve().parents[2]
RES = REPO / 'Assets/Resources/Audio'

TEMPLATE = """fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: {load}
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: {quality}
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
TEXT = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def guid_of(meta):
    if meta.exists():
        m = re.search(r'^guid: ([0-9a-f]{32})', meta.read_text(), re.M)
        if m:
            return m.group(1)
    return uuid.uuid4().hex


def audio(path, load, quality):
    meta = path.with_name(path.name + '.meta')
    meta.write_text(TEMPLATE.format(guid=guid_of(meta), load=load, quality=quality))


def main():
    n = 0
    for p in sorted((RES / 'Sfx').glob('*.wav')):
        audio(p, 0, .7); n += 1
    for folder in ('Enemy', 'Announcer'):
        d = RES / 'Voice' / folder
        meta = d.with_name(folder + '.meta')
        meta.write_text(FOLDER.format(guid=guid_of(meta)))
        for p in sorted(d.glob('*.wav')):
            audio(p, 1, .5); n += 1
    for p in sorted((RES / 'Voice/Battle').glob('*-defeat-*.wav')):
        audio(p, 1, .5); n += 1
    j = RES / 'Voice/Enemy/barks.json'
    meta = j.with_name(j.name + '.meta')
    meta.write_text(TEXT.format(guid=guid_of(meta)))
    print('audio metas', n)


if __name__ == '__main__':
    main()
