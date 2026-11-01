#!/usr/bin/env python3
"""Upload files to the CHO-SIREN Tencent COS bucket (installer packages + app-version.json).

Credentials are read at runtime from ~/.cho-siren-cos.json (bucket, region, secretId, secretKey) and are
never printed. Standard library only (COS v5 HMAC-SHA1 signing), no SDK install needed.

    python3 Tools/cos_upload.py <local file> <remote key> [content-type]
    python3 Tools/cos_upload.py --copy <existing key> <new key>      (server-side copy)
Prints the public URL of each uploaded object.
"""
import hashlib
import hmac
import json
import mimetypes
import os
import sys
import time
import urllib.parse
import urllib.request

CONFIG = os.path.expanduser("~/.cho-siren-cos.json")


def load_config():
    with open(CONFIG, encoding="utf-8") as handle:
        cfg = json.load(handle)
    for key in ("bucket", "region", "secretId", "secretKey"):
        if not cfg.get(key):
            raise SystemExit(f"{CONFIG} is missing {key}")
    return cfg


def host_of(cfg):
    return f"{cfg['bucket']}.cos.{cfg['region']}.myqcloud.com"


def public_url(cfg, key):
    return f"https://{host_of(cfg)}/{urllib.parse.quote(key)}"


def authorization(cfg, method, path, headers):
    now = int(time.time())
    key_time = f"{now - 60};{now + 3600}"
    sign_key = hmac.new(cfg["secretKey"].encode(), key_time.encode(), hashlib.sha1).hexdigest()
    names = sorted(k.lower() for k in headers)
    header_string = "&".join(
        f"{urllib.parse.quote(n, safe='')}={urllib.parse.quote(str(headers[next(k for k in headers if k.lower() == n)]), safe='')}"
        for n in names)
    http_string = f"{method.lower()}\n{path}\n\n{header_string}\n"
    string_to_sign = f"sha1\n{key_time}\n{hashlib.sha1(http_string.encode()).hexdigest()}\n"
    signature = hmac.new(sign_key.encode(), string_to_sign.encode(), hashlib.sha1).hexdigest()
    return (f"q-sign-algorithm=sha1&q-ak={cfg['secretId']}&q-sign-time={key_time}&q-key-time={key_time}"
            f"&q-header-list={';'.join(names)}&q-url-param-list=&q-signature={signature}")


def upload(local, key, content_type=None, quiet=False):
    cfg = load_config()
    size = os.path.getsize(local)
    content_type = content_type or mimetypes.guess_type(local)[0] or "application/octet-stream"
    if local.endswith(".apk"):
        content_type = "application/vnd.android.package-archive"
    path = "/" + urllib.parse.quote(key)
    headers = {"Host": host_of(cfg), "Content-Length": str(size), "Content-Type": content_type}
    if key.endswith(".json"):
        headers["Cache-Control"] = "no-cache"
    auth = authorization(cfg, "PUT", "/" + key, {"Host": headers["Host"]})
    with open(local, "rb") as body:
        request = urllib.request.Request(f"https://{headers['Host']}{path}", data=body, method="PUT")
        for name, value in headers.items():
            request.add_header(name, value)
        request.add_header("Authorization", auth)
        with urllib.request.urlopen(request, timeout=1800) as response:
            if response.status != 200:
                raise SystemExit(f"upload failed: HTTP {response.status}")
    url = public_url(cfg, key)
    if not quiet:
        print(url)
    return url


def copy(src_key, dst_key):
    """Server-side copy inside the bucket (seconds, no re-upload)."""
    cfg = load_config()
    host = host_of(cfg)
    headers = {"Host": host}
    auth = authorization(cfg, "PUT", "/" + dst_key, headers)
    request = urllib.request.Request(f"https://{host}/{urllib.parse.quote(dst_key)}", data=b"", method="PUT")
    request.add_header("Host", host)
    request.add_header("Authorization", auth)
    request.add_header("x-cos-copy-source", f"{host}/{urllib.parse.quote(src_key)}")
    with urllib.request.urlopen(request, timeout=600) as response:
        if response.status != 200:
            raise SystemExit(f"copy failed: HTTP {response.status}")
    print(public_url(cfg, dst_key))


if __name__ == "__main__":
    if len(sys.argv) >= 4 and sys.argv[1] == "--copy":
        copy(sys.argv[2], sys.argv[3])
    elif len(sys.argv) >= 3:
        upload(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) > 3 else None)
    else:
        raise SystemExit(__doc__)
