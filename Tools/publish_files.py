#!/usr/bin/env python3
"""Publish a game folder for delta updates (2026-10-22).

    python3 Tools/publish_files.py <platform> <game root> <version> [--previous <old game root>] [--no-legacy-whole]

2026-11-08 small patches: files of 16 MB or more are also listed as content-defined chunks
(Tools/patch/cdc.swift; the game's AppUpdate1022.Cdc cuts identically). Only chunks that the previous
version's copy of that file does not contain are uploaded, so a release that adds a few pictures uploads a
few chunks instead of the whole 800 MB resources file. Updaters from 0.9.40 rebuild such a file from their
local chunks plus the new ones. Older updaters still need the whole changed file: it is uploaded too unless
--no-legacy-whole is given (use that once every player is on 0.9.40 or later).

Every file under <game root> (the Windows game folder, or the Mac 幻域魅声.app bundle) is stored once on COS
under releases/files/<sha256> — files that are already there from an earlier version are skipped, so a
release only uploads what changed. Then releases/<platform>-files-<version>.json lists
{version, base, files:[{path, size, sha256}]} and its public URL is printed. The in-game updater
(AppUpdate1022) hashes the player's files and downloads only the ones whose hash differs.
"""
import hashlib
import json
import os
import subprocess
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


CHUNK_MIN_FILE = 16 * 1024 * 1024
HERE = os.path.dirname(os.path.abspath(__file__))


def cdc_tool():
    """Compile Tools/patch/cdc.swift once into Builds/tools/cdc (Swift ships with Xcode)."""
    source = os.path.join(HERE, "patch", "cdc.swift")
    binary = os.path.join(HERE, "..", "Builds", "tools", "cdc")
    if not os.path.exists(binary) or os.path.getmtime(binary) < os.path.getmtime(source):
        os.makedirs(os.path.dirname(binary), exist_ok=True)
        subprocess.run(["swiftc", "-O", source, "-o", binary], check=True, stderr=subprocess.DEVNULL)
    return binary


def chunks_of(path):
    out = subprocess.run([cdc_tool(), path], check=True, capture_output=True, text=True).stdout
    return [(int(o), int(n), h) for o, n, h in (line.split() for line in out.splitlines() if line.strip())]


def main():
    args = sys.argv[1:]
    legacy_whole = "--no-legacy-whole" not in args
    previous = None
    if "--previous" in args:
        previous = args[args.index("--previous") + 1]
    args = [a for i, a in enumerate(args) if a != "--no-legacy-whole" and a != "--previous" and (i == 0 or args[i - 1] != "--previous")]
    platform, root, version = args[0], args[1], args[2]
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

    # Big files: list their chunks; upload the chunks the previous version's copy does not have.
    chunk_jobs = {}
    for entry in entries:
        if entry["size"] < CHUNK_MIN_FILE:
            continue
        entry["chunks"] = chunks_of(entry["full"])
        old_path = os.path.join(previous, entry["path"]) if previous else None
        known = {h for _, _, h in chunks_of(old_path)} if old_path and os.path.exists(old_path) else set()
        for offset, size, digest in entry["chunks"]:
            if digest not in known:
                chunk_jobs[digest] = (entry["full"], offset, size)

    def push_chunk(item):
        digest, (path, offset, size) = item
        if exists(base + digest):
            return 0
        with open(path, "rb") as handle:
            handle.seek(offset)
            data = handle.read(size)
        piece = os.path.join(tempfile.mkdtemp(), digest)
        with open(piece, "wb") as out:
            out.write(data)
        retry(lambda: cos_upload.upload(piece, "releases/files/" + digest, "application/octet-stream", quiet=True))
        os.remove(piece)
        return size

    with ThreadPoolExecutor(8) as pool:
        uploaded_chunks = sum(pool.map(push_chunk, chunk_jobs.items()))

    def push(entry):
        if "chunks" in entry and not legacy_whole:
            return 0
        if exists(base + entry["sha256"]):
            return 0
        retry(lambda: cos_upload.upload(entry["full"], "releases/files/" + entry["sha256"], "application/octet-stream", quiet=True))
        return entry["size"]

    unique = list({e["sha256"]: e for e in entries}.values())
    with ThreadPoolExecutor(4) as pool:
        uploaded = sum(pool.map(push, unique)) + uploaded_chunks

    def listed(e):
        item = {"path": e["path"], "size": e["size"], "sha256": e["sha256"]}
        if "chunks" in e:
            item["chunks"] = [{"sha256": h, "size": n} for _, n, h in e["chunks"]]
        return item

    manifest = {"version": version, "base": base, "files": [listed(e) for e in entries]}
    path = os.path.join(tempfile.mkdtemp(), f"{platform}-files-{version}.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False)
    retry(lambda: cos_upload.upload(path, f"releases/{platform}-files-{version}.json", "application/json", quiet=True))
    total = sum(e["size"] for e in entries)
    sys.stderr.write(f"{platform}: {len(entries)} files, {total / 1048576:.0f} MB, uploaded {uploaded / 1048576:.1f} MB new "
                     f"({uploaded_chunks / 1048576:.1f} MB of it chunks; legacy whole files {'on' if legacy_whole else 'off'})\n")
    print(cos_upload.public_url(cfg, f"releases/{platform}-files-{version}.json"))


if __name__ == "__main__":
    main()
