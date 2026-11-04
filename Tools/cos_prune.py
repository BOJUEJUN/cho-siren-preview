#!/usr/bin/env python3
"""Free COS space left behind by full releases (2026-11-04).

    python3 Tools/cos_prune.py            # dry run: list what would go and how much space it frees
    python3 Tools/cos_prune.py --apply    # delete them
    python3 Tools/cos_prune.py --keep 3   # keep the newest three versions instead of two

Kept: the newest N versions' installers and file lists (the current one plus a rollback), every *-latest
alias, app-version.json, cg-refs/, and every releases/files/<sha256> blob a kept file list still points at.
Removed: older versioned installers (.apk/.zip/.exe/.dmg), older mac-/windows-files-<v>.json lists, blobs no
kept list references, and the test/ scratch objects. The in-game updater and the website only read the
newest version and the latest aliases, so nothing a player can reach is touched.
"""
import argparse
import json
import re
import sys
import urllib.request
from concurrent.futures import ThreadPoolExecutor

sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
import cos_cleanup  # noqa: E402  (list/delete helpers)
import cos_upload  # noqa: E402

INSTALLER = re.compile(r"^releases/CHO-SIREN-(?:Windows-Setup-|Windows-|Mac-update-|Mac-)?(\d+\.\d+\.\d+)\.(apk|zip|exe|dmg)$")
FILE_LIST = re.compile(r"^releases/(?:mac|windows)(?:-test)?-files-(\d+\.\d+\.\d+)\.json$")


def list_all(cfg):
    """Every key with its size (cos_cleanup.list_keys only lists installers)."""
    import urllib.parse
    import xml.etree.ElementTree as ET
    host = cos_upload.host_of(cfg)
    out, marker = [], ""
    while True:
        query = {"max-keys": "1000"}
        if marker:
            query["marker"] = marker
        request = urllib.request.Request(f"https://{host}/?{urllib.parse.urlencode(query)}")
        request.add_header("Authorization", cos_upload.authorization(cfg, "GET", "/", {"Host": host}))
        root = ET.fromstring(urllib.request.urlopen(request, timeout=60).read())
        ns = root.tag.split("}")[0].strip("{") if root.tag.startswith("{") else ""
        tag = (lambda n: f"{{{ns}}}{n}") if ns else (lambda n: n)
        for item in root.findall(tag("Contents")):
            out.append((item.find(tag("Key")).text, int(item.find(tag("Size")).text)))
        if root.find(tag("IsTruncated")).text != "true":
            return out
        marker = out[-1][0]


def version_key(v):
    return tuple(int(p) for p in v.split("."))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--keep", type=int, default=2)
    args = parser.parse_args()
    cfg = cos_upload.load_config()
    objects = list_all(cfg)
    versions = sorted({m.group(1) for k, _ in objects if (m := INSTALLER.match(k))}, key=version_key)
    kept = set(versions[-args.keep:])
    referenced = set()
    for platform in ("mac", "windows"):
        for v in kept:
            url = cos_upload.public_url(cfg, f"releases/{platform}-files-{v}.json")
            referenced.update(re.findall(r"[0-9a-f]{64}", urllib.request.urlopen(url, timeout=60).read().decode()))

    doomed = []
    for key, size in objects:
        installer, listing = INSTALLER.match(key), FILE_LIST.match(key)
        if installer and installer.group(1) not in kept:
            doomed.append((key, size))
        elif listing and listing.group(1) not in kept:
            doomed.append((key, size))
        elif key.startswith("releases/files/") and key.rsplit("/", 1)[-1].split(".")[0] not in referenced:
            doomed.append((key, size))
        elif key.startswith("test/"):
            doomed.append((key, size))

    total = sum(s for _, s in objects)
    freed = sum(s for _, s in doomed)
    print(f"bucket: {len(objects)} objects, {total / 1e9:.2f} GB")
    print(f"keeping versions: {', '.join(sorted(kept, key=version_key))}")
    print(f"to remove: {len(doomed)} objects, {freed / 1e9:.2f} GB  ->  {(total - freed) / 1e9:.2f} GB left")
    if not args.apply:
        print("dry run only; add --apply to delete")
        return
    with ThreadPoolExecutor(max_workers=8) as pool:
        list(pool.map(lambda item: cos_cleanup.delete(cfg, item[0]), doomed))
    print(f"deleted {len(doomed)} objects, freed {freed / 1e9:.2f} GB")


if __name__ == "__main__":
    main()
