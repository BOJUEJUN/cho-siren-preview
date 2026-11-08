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


def upload_host_of(cfg):
    """2026-11-08: uploads go through COS global acceleration (enabled on the bucket) unless COS_ACCELERATE=0;
    public URLs keep the regular Guangzhou domain."""
    if os.environ.get("COS_ACCELERATE", "1") == "0":
        return host_of(cfg)
    return f"{cfg['bucket']}.cos.accelerate.myqcloud.com"


def public_url(cfg, key):
    return f"https://{host_of(cfg)}/{urllib.parse.quote(key)}"


def authorization(cfg, method, path, headers, params=None):
    now = int(time.time())
    key_time = f"{now - 60};{now + 3600}"
    sign_key = hmac.new(cfg["secretKey"].encode(), key_time.encode(), hashlib.sha1).hexdigest()
    names = sorted(k.lower() for k in headers)
    header_string = "&".join(
        f"{urllib.parse.quote(n, safe='')}={urllib.parse.quote(str(headers[next(k for k in headers if k.lower() == n)]), safe='')}"
        for n in names)
    params = params or {}
    param_names = sorted(k.lower() for k in params)
    param_string = "&".join(
        f"{urllib.parse.quote(n, safe='')}={urllib.parse.quote(str(params[next(k for k in params if k.lower() == n)]), safe='')}"
        for n in param_names)
    http_string = f"{method.lower()}\n{path}\n{param_string}\n{header_string}\n"
    string_to_sign = f"sha1\n{key_time}\n{hashlib.sha1(http_string.encode()).hexdigest()}\n"
    signature = hmac.new(sign_key.encode(), string_to_sign.encode(), hashlib.sha1).hexdigest()
    return (f"q-sign-algorithm=sha1&q-ak={cfg['secretId']}&q-sign-time={key_time}&q-key-time={key_time}"
            f"&q-header-list={';'.join(names)}&q-url-param-list={';'.join(param_names)}&q-signature={signature}")


MULTIPART_MIN = 64 * 1024 * 1024
PART_SIZE = 16 * 1024 * 1024
PART_WORKERS = int(os.environ.get("COS_PART_WORKERS", "8"))


def _request(cfg, method, key, params=None, body=None, extra=None, timeout=300):
    host = upload_host_of(cfg)
    params = params or {}
    query = "&".join(f"{k}={urllib.parse.quote(str(v), safe='')}" if v != "" else k for k, v in params.items())
    url = f"https://{host}/{urllib.parse.quote(key)}" + (f"?{query}" if query else "")
    request = urllib.request.Request(url, data=body if body is not None else (b"" if method in ("POST", "PUT") else None), method=method)
    request.add_header("Host", host)
    for name, value in (extra or {}).items():
        request.add_header(name, value)
    request.add_header("Authorization", authorization(cfg, method, "/" + key, {"Host": host}, params))
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.status, dict(response.headers), response.read()


def upload_multipart(cfg, local, key, content_type, extra_headers):
    """2026-11-08: big installers go up in 16 MB parts, 8 at a time, each retried; a stalled
    single connection no longer holds the whole release back."""
    import concurrent.futures
    import re
    status, _, body = _request(cfg, "POST", key, {"uploads": ""}, b"", dict(extra_headers, **{"Content-Type": content_type}))
    upload_id = re.search(rb"<UploadId>([^<]+)</UploadId>", body).group(1).decode()
    size = os.path.getsize(local)
    parts = [(n + 1, n * PART_SIZE, min(PART_SIZE, size - n * PART_SIZE)) for n in range((size + PART_SIZE - 1) // PART_SIZE)]

    def send(part):
        number, offset, length = part
        with open(local, "rb") as handle:
            handle.seek(offset)
            data = handle.read(length)
        for attempt in range(8):
            try:
                _, headers, _ = _request(cfg, "PUT", key, {"partNumber": number, "uploadId": upload_id}, data,
                                         {"Content-Length": str(length)}, timeout=180)
                etag = next(v for k, v in headers.items() if k.lower() == "etag")
                return number, etag
            except Exception as error:  # network hiccup: back off and resend this part only
                if attempt == 7:
                    raise
                time.sleep(min(60, 3 * (attempt + 1)))

    with concurrent.futures.ThreadPoolExecutor(PART_WORKERS) as pool:
        done = sorted(pool.map(send, parts))
    xml = "<CompleteMultipartUpload>" + "".join(
        f"<Part><PartNumber>{n}</PartNumber><ETag>{etag}</ETag></Part>" for n, etag in done) + "</CompleteMultipartUpload>"
    status, _, body = _request(cfg, "POST", key, {"uploadId": upload_id}, xml.encode(), {"Content-Type": "application/xml"}, timeout=600)
    if status != 200 or b"<Error>" in body:
        raise SystemExit(f"multipart complete failed: HTTP {status}")


def upload(local, key, content_type=None, quiet=False):
    cfg = load_config()
    size = os.path.getsize(local)
    content_type = content_type or mimetypes.guess_type(local)[0] or "application/octet-stream"
    if local.endswith(".apk"):
        content_type = "application/vnd.android.package-archive"
    if size >= MULTIPART_MIN:
        upload_multipart(cfg, local, key, content_type, {"Cache-Control": "no-cache"} if key.endswith(".json") else {})
        url = public_url(cfg, key)
        if not quiet:
            print(url)
        return url
    path = "/" + urllib.parse.quote(key)
    headers = {"Host": upload_host_of(cfg), "Content-Length": str(size), "Content-Type": content_type}
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
