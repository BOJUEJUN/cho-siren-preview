#!/usr/bin/env python3
"""Publish the signed Android APK through the existing public GitHub repository.

COS default domains reject public APK GET requests. Keep COS as an archive, but
use a GitHub release asset for a directly installable download until a custom COS
domain is configured. Existing Git credentials are used without being printed.

Usage: python3 Tools/github_android.py path/to/CHO-SIREN.apk 0.9.10
"""
import hashlib
import json
import os
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request

REPOSITORY = "BOJUEJUN/cho-siren-preview"
ASSET_NAME = "CHO-SIREN.apk"
API = "https://api.github.com/repos/" + REPOSITORY


def credentials():
    result = subprocess.run(
        ["git", "credential", "fill"],
        input="protocol=https\nhost=github.com\n\n",
        text=True, capture_output=True, timeout=30, check=True,
        env={**os.environ, "GIT_TERMINAL_PROMPT": "0"})
    values = dict(line.split("=", 1) for line in result.stdout.splitlines() if "=" in line)
    if not values.get("password"):
        raise SystemExit("GitHub credentials are unavailable; no release was changed.")
    return values["password"]


def publish(path, version):
    size = os.path.getsize(path)
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for part in iter(lambda: handle.read(4 * 1024 * 1024), b""):
            digest.update(part)
    expected_digest = "sha256:" + digest.hexdigest()
    token = credentials()

    def request(url, method="GET", payload=None, content_type="application/json", length=None):
        data = json.dumps(payload).encode() if isinstance(payload, dict) else payload
        headers = {"Authorization": "Bearer " + token, "Accept": "application/vnd.github+json",
                   "X-GitHub-Api-Version": "2022-11-28", "User-Agent": "CHO-SIREN-release"}
        if data is not None:
            headers["Content-Type"] = content_type
        if length is not None:
            headers["Content-Length"] = str(length)
        req = urllib.request.Request(url, data=data, headers=headers, method=method)
        with urllib.request.urlopen(req, timeout=1800 if length else 30) as response:
            return json.load(response)

    tag = "v" + version
    try:
        release = request(API + "/releases/tags/" + urllib.parse.quote(tag))
    except urllib.error.HTTPError as error:
        if error.code != 404:
            raise
        # Drafts may not yet have a tag ref. Reuse a prior interrupted upload.
        release = next((item for item in request(API + "/releases?per_page=100")
                        if item.get("tag_name") == tag), None)
        if release is None:
            main = request(API + "/git/ref/heads/main")["object"]["sha"]
            release = request(API + "/releases", "POST", {
                "tag_name": tag, "target_commitish": main, "name": "CHO-SIREN " + version,
                "body": "Android 安装包。下载 CHO-SIREN.apk 后安装，已有存档会保留。",
                "draft": True, "prerelease": False})
    existing = next((asset for asset in release.get("assets", []) if asset["name"] == ASSET_NAME), None)
    if existing:
        if existing.get("size") != size or existing.get("digest") != expected_digest:
            raise SystemExit("A different APK already exists for this version; use a new version.")
        asset = existing
    else:
        upload = release["upload_url"].split("{", 1)[0] + "?name=" + urllib.parse.quote(ASSET_NAME)
        with open(path, "rb") as body:
            asset = request(upload, "POST", body, "application/vnd.android.package-archive", size)
        if asset.get("size") != size or asset.get("digest") != expected_digest:
            raise SystemExit("GitHub asset size or SHA256 does not match the signed local APK.")
    if release.get("draft"):
        release = request(API + "/releases/" + str(release["id"]), "PATCH", {"draft": False, "make_latest": "true"})
        asset = next(item for item in release["assets"] if item["name"] == ASSET_NAME)
    # A HEAD request to COS previously passed while the actual download was a
    # forbidden XML page. Exercise the public GET path, not the authenticated API.
    public = urllib.request.Request(asset["browser_download_url"], headers={
        "Range": "bytes=0-65535", "User-Agent": "CHO-SIREN-download-check"})
    with urllib.request.urlopen(public, timeout=60) as response, open(path, "rb") as local:
        if response.status not in (200, 206) or response.read(65536) != local.read(65536):
            raise SystemExit("Public APK GET did not return the signed APK bytes.")
    print(asset["browser_download_url"])


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    publish(sys.argv[1], sys.argv[2])
