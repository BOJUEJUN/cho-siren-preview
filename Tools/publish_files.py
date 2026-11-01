#!/usr/bin/env python3
"""Publish a game folder for delta updates (2026-10-22).

    python3 Tools/publish_files.py <platform> <game root> <version>

Every file under <game root> (the Windows game folder, or the Mac 幻域魅声.app bundle) is stored once on COS
under releases/files/<sha256> — files that are already there from an earlier version are skipped, so a
release only uploads what changed. Then releases/<platform>-files-<version>.json lists
{version, base, files:[{path, size, sha256}]} and its public URL is printed. The in-game updater
(AppUpdate1022) hashes the player's files and downloads only the ones whose hash differs.
"""
import hashlib
import json
import os
import sys
import tempfile
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cos_upload  # noqa: E402

SKIP_PARTS = ("BackUpThisFolder", "DoNotShip", ".DS_Store")


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def retry(action, attempts=5):
    """Network resets happen with several parallel connections; back off and try again."""
    for attempt in range(attempts):
        try:
            return action()
        except (urllib.error.URLError, ConnectionError, TimeoutError, OSError) as error:
            if isinstance(error, urllib.error.HTTPError) or attempt == attempts - 1:
                raise
            time.sleep(1.5 * (attempt + 1))


def exists(url):
    def head():
        try:
            with urllib.request.urlopen(urllib.request.Request(url, method="HEAD"), timeout=30) as r:
                return r.status == 200
        except urllib.error.HTTPError:
            return False
    return retry(head)


def main():
    platform, root, version = sys.argv[1], sys.argv[2], sys.argv[3]
    cfg = cos_upload.load_config()
    base = cos_upload.public_url(cfg, "releases/files/")
    entries = []
    for folder, _, names in os.walk(root):
        for name in names:
            full = os.path.join(folder, name)
            rel = os.path.relpath(full, root).replace(os.sep, "/")
            if any(part in rel for part in SKIP_PARTS) or os.path.islink(full):
                continue
            entries.append({"path": rel, "size": os.path.getsize(full), "full": full})
    with ThreadPoolExecutor(8) as pool:
        for entry, digest in zip(entries, pool.map(lambda e: sha256(e["full"]), entries)):
            entry["sha256"] = digest

    def push(entry):
        if exists(base + entry["sha256"]):
            return 0
        retry(lambda: cos_upload.upload(entry["full"], "releases/files/" + entry["sha256"], "application/octet-stream", quiet=True))
        return entry["size"]

    unique = list({e["sha256"]: e for e in entries}.values())
    with ThreadPoolExecutor(4) as pool:
        uploaded = sum(pool.map(push, unique))
    manifest = {"version": version, "base": base,
                "files": [{"path": e["path"], "size": e["size"], "sha256": e["sha256"]} for e in entries]}
    path = os.path.join(tempfile.mkdtemp(), f"{platform}-files-{version}.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False)
    retry(lambda: cos_upload.upload(path, f"releases/{platform}-files-{version}.json", "application/json", quiet=True))
    total = sum(e["size"] for e in entries)
    sys.stderr.write(f"{platform}: {len(entries)} files, {total / 1048576:.0f} MB, uploaded {uploaded / 1048576:.1f} MB new\n")
    print(cos_upload.public_url(cfg, f"releases/{platform}-files-{version}.json"))


if __name__ == "__main__":
    main()
