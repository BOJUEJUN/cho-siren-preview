#!/usr/bin/env python3
"""Keep COS storage small: delete installer packages older than the newest N versions (default 3).

    python3 Tools/cos_cleanup.py [keep]

Only versioned installer files under releases/ are touched (CHO-SIREN-<v>.apk, -Windows-<v>.zip,
-Windows-Setup-<v>.exe, -Mac-<v>.dmg/.zip, -Mac-update-<v>.zip). The *-latest copies, the delta file
lists and releases/files/<sha256> blobs are kept (a kept file list may still point at an old blob).
"""
import re
import sys
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET

sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
import cos_upload  # noqa: E402

PATTERN = re.compile(r"^releases/CHO-SIREN-(?:Windows-Setup-|Windows-|Mac-update-|Mac-)?(\d+)\.(\d+)\.(\d+)\.(apk|zip|exe|dmg)$")


def list_keys(cfg):
    host = cos_upload.host_of(cfg)
    keys, marker = [], ""
    while True:
        query = {"prefix": "releases/CHO-SIREN-", "max-keys": "1000"}
        if marker:
            query["marker"] = marker
        auth = cos_upload.authorization(cfg, "GET", "/", {"Host": host})
        request = urllib.request.Request(f"https://{host}/?{urllib.parse.urlencode(query)}")
        request.add_header("Authorization", auth)
        with urllib.request.urlopen(request, timeout=60) as response:
            root = ET.fromstring(response.read())
        ns = {"s": root.tag.split("}")[0].strip("{")} if root.tag.startswith("{") else {}
        find = (lambda e, t: e.findall(f"s:{t}", ns)) if ns else (lambda e, t: e.findall(t))
        for item in find(root, "Contents"):
            keys.append(item.find("s:Key", ns).text if ns else item.find("Key").text)
        truncated = (root.find("s:IsTruncated", ns) if ns else root.find("IsTruncated"))
        if truncated is None or truncated.text != "true" or not keys:
            return keys
        marker = keys[-1]


def delete(cfg, key):
    host = cos_upload.host_of(cfg)
    auth = cos_upload.authorization(cfg, "DELETE", "/" + key, {"Host": host})
    request = urllib.request.Request(f"https://{host}/{urllib.parse.quote(key)}", method="DELETE")
    request.add_header("Authorization", auth)
    with urllib.request.urlopen(request, timeout=60) as response:
        if response.status not in (200, 204):
            raise SystemExit(f"delete failed: {key} HTTP {response.status}")


def main():
    keep = int(sys.argv[1]) if len(sys.argv) > 1 else 3
    cfg = cos_upload.load_config()
    versioned = {}
    for key in list_keys(cfg):
        match = PATTERN.match(key)
        if match:
            versioned.setdefault(tuple(int(x) for x in match.groups()[:3]), []).append(key)
    versions = sorted(versioned, reverse=True)
    old = [key for v in versions[keep:] for key in versioned[v]]
    for key in old:
        delete(cfg, key)
    kept = ", ".join(".".join(map(str, v)) for v in versions[:keep])
    print(f"kept {kept}; deleted {len(old)} old installer files")


if __name__ == "__main__":
    main()
