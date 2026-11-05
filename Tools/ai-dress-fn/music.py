"""Personal-test Suno 6 song routes. No key is sent to the game.

Integration: package this module beside index.py; call music.route(event) before dress routing.
Existing COS_* and RUNNINGHUB_AI_KEY are reused. MUSIC_TEST_CODE (or TEST_CODE) is mandatory;
without it the paid endpoint stays disabled. GET /music/config and provider price-preview are free.
POST /music submits once per requestId. COS's signed x-cos-forbid-overwrite claim prevents duplicate submits
only on a bucket whose versioning has never been enabled; verify that prerequisite before deployment.
"""
import base64
import datetime
import hashlib
import hmac
import json
import os
import re
import time
import urllib.error
import urllib.parse
import urllib.request

MODEL = "rhart-audio/suno-v6/custom"
HOST = "https://www.runninghub.ai/openapi/v2/"
GENRES = {"K-pop": "melodic K-pop girl group, polished pop production", "电子": "melodic electronic pop, clean synths",
          "流行": "melodic contemporary pop", "摇滚": "melodic pop rock, punchy live drums"}
MOODS = {"明亮": "bright, playful, uplifting", "热血": "energetic, confident, anthemic", "温柔": "warm, tender, intimate"}
CORS = {"Access-Control-Allow-Origin": "*", "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
        "Access-Control-Allow-Headers": "Content-Type,X-Test-Code", "Content-Type": "application/json; charset=utf-8"}


def reply(code, value):
    return {"isBase64Encoded": False, "statusCode": code, "headers": CORS,
            "body": json.dumps(value, ensure_ascii=False)}


def _rh(path, body):
    request = urllib.request.Request(HOST + path, data=json.dumps(body).encode(), method="POST", headers={
        "Authorization": "Bearer " + os.environ["RUNNINGHUB_AI_KEY"], "Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=35) as response:
        return json.loads(response.read())


def _cos(method, key, data=None, create=False, etag=None):
    # COS PutObject ignores S3-style If-Match/If-None-Match. Never use them for billing guards.
    if etag is not None:
        raise ValueError("COS PUT ETag preconditions are not supported")
    if create and method != "PUT":
        raise ValueError("Create-only applies only to PUT")
    host = "%s.cos.%s.myqcloud.com" % (os.environ["COS_BUCKET"], os.environ["COS_REGION"])
    path = "/" + urllib.parse.quote(key, safe="/")
    headers = {"host": host}
    if method == "PUT":
        headers["x-cos-acl"] = "private"
    if create:
        headers["x-cos-forbid-overwrite"] = "true"
    names = sorted(headers)
    canonical = "&".join(urllib.parse.quote(k, safe="").lower() + "=" +
                         urllib.parse.quote(headers[k].strip(), safe="") for k in names)
    seconds = int(time.time()); span = "%s;%s" % (seconds - 60, seconds + 600)
    signing = hmac.new(os.environ["COS_SECRET_KEY"].encode(), span.encode(), hashlib.sha1).hexdigest()
    plain = "%s\n%s\n\n%s\n" % (method.lower(), path, canonical)
    sign = hmac.new(signing.encode(), ("sha1\n%s\n%s\n" %
                    (span, hashlib.sha1(plain.encode()).hexdigest())).encode(), hashlib.sha1).hexdigest()
    headers["Authorization"] = ("q-sign-algorithm=sha1&q-ak=%s&q-sign-time=%s&q-key-time=%s&"
        "q-header-list=%s&q-url-param-list=&q-signature=%s") % (os.environ["COS_SECRET_ID"], span, span, ";".join(names), sign)
    payload = json.dumps(data, ensure_ascii=False).encode() if data is not None else None
    req = urllib.request.Request("https://" + host + path, data=payload, method=method, headers=headers)
    with urllib.request.urlopen(req, timeout=10) as r:
        raw = r.read()
        return (json.loads(raw) if raw else None), r.headers.get("ETag")


def payload(body):
    genre = body.get("genre", "K-pop"); mood = body.get("mood", "明亮")
    if genre not in GENRES or mood not in MOODS:
        raise ValueError("请选择已有风格和情绪")
    duration = int(body.get("duration", 40)); tempo = int(body.get("tempo", 120))
    if duration not in (40, 80) or tempo not in (90, 120, 145):
        raise ValueError("请选择40/80秒和已有速度")
    instrumental = body.get("instrumental", True)
    if not isinstance(instrumental, bool):
        raise ValueError("人声选项不正确")
    title = str(body.get("title") or "我的新歌").strip()[:60]
    lyrics = str(body.get("lyrics") or "").strip()[:1600]
    if instrumental:
        lyrics = "[Instrumental]\n[Immediate melodic hook]\n[Groove variation]\n[Hook]\n[Resolved ending]"
    elif not lyrics:
        lyrics = "[Chorus]\nきらめく夜に　君と歌おう\nこの瞬間を　光に変えて\n手を伸ばせば　届くメロディー\n明日もきっと　輝いている\n[Outro]\n君と　この歌を"
    tags = "%s, %s, %s BPM, original composition, memorable chorus first, short intro, natural dynamics, resolved ending" % (GENRES[genre], MOODS[mood], tempo)
    if not instrumental:
        tags += ", Japanese lyrics, expressive female lead vocals, natural vocal phrasing"
    p = {"title": title, "prompt": lyrics, "tags": tags, "make_instrumental": "true" if instrumental else "false",
         "duration": duration, "audioFormat": "mp3", "autoLyrics": "false", "variety": "normal",
         "negativeTags": "clipping, harsh piercing treble, long ambient intro, vocal imitation"}
    if not instrumental:
        p["vocalGender"] = "f"
    return p


def _read(key):
    try:
        return _cos("GET", key)[0]
    except urllib.error.HTTPError as e:
        if e.code != 404:
            raise
        return None


def _claim_slot(prefix, count, owner):
    """Each private slot can be claimed once. Failed attempts are never released for reuse."""
    claim = {"request": owner, "created": int(time.time())}
    for slot in range(count):
        try:
            _cos("PUT", prefix + "/%s.json" % slot, claim, create=True)
            return slot
        except urllib.error.HTTPError as error:
            if error.code not in (409, 412):
                raise
    return None


def _quota(device, request_id):
    day = (datetime.datetime.utcnow() + datetime.timedelta(hours=8)).strftime("%Y-%m-%d")
    prefix = "ai-music-private/quota-slots/" + day
    limit = max(1, min(5, int(os.environ.get("MUSIC_DAILY_LIMIT", "2"))))
    total_limit = max(1, min(100, int(os.environ.get("MUSIC_GLOBAL_LIMIT", "10"))))
    device_slot = _claim_slot(prefix + "/devices/" + device, limit, request_id)
    if device_slot is None:
        return None
    global_slot = _claim_slot(prefix + "/global", total_limit, device + ":" + request_id)
    if global_slot is None:
        # Preserve the device claim: deleting it during a race could accidentally free a paid reservation.
        return None
    return limit - device_slot - 1


def _record_key(device, request_id):
    return "ai-music-private/requests/%s/%s.json" % (hashlib.sha256(device.encode()).hexdigest()[:24], request_id)


def _status(record, request_id):
    state = record.get("state")
    if state == "refused":
        return {"status": "failed", "error": record.get("error", "生成请求未被接受")}
    if not record.get("providerTask"):
        age = time.time() - record.get("created", 0)
        return {"status": "running" if age < 120 else "unknown", "task": request_id,
                "error": "请求状态待确认，请继续查询，不要重复生成" if age >= 120 else ""}
    result = _rh("query", {"taskId": record["providerTask"]})
    if result.get("status") == "SUCCESS":
        tracks = []
        for item in (result.get("results") or [])[:2]:
            url = item.get("url") or item.get("outputUrl") or ""
            parsed = urllib.parse.urlsplit(url)
            if parsed.scheme == "https" and parsed.hostname and not parsed.username:
                tracks.append({"url": url, "title": record["title"] + (" A" if not tracks else " B")})
        return {"status": "done", "tracks": tracks} if tracks else {"status": "failed", "error": "服务未返回歌曲文件"}
    if result.get("status") == "FAILED":
        return {"status": "failed", "error": "歌曲生成失败，请换一段歌词或描述"}
    return {"status": "running", "task": request_id}


def route(event):
    """Returns None for unrelated routes. Never automatically retries a paid submission."""
    path = (event.get("path") or event.get("rawPath") or "/").rstrip("/")
    if not re.search(r"/music(?:/|$)", path):
        return None
    method = (event.get("httpMethod") or event.get("requestContext", {}).get("httpMethod") or "GET").upper()
    if method == "OPTIONS":
        return reply(204, {})
    expected = os.environ.get("MUSIC_TEST_CODE") or os.environ.get("TEST_CODE") or ""
    headers = {k.lower(): str(v) for k, v in (event.get("headers") or {}).items()}
    if not expected or not os.environ.get("RUNNINGHUB_AI_KEY"):
        return reply(503, {"error": "作曲服务准备中", "enabled": False})
    if not hmac.compare_digest(headers.get("x-test-code", ""), expected):
        return reply(403, {"error": "当前版本未开通作曲测试"})
    try:
        if method == "GET" and path.endswith("/music/config"):
            q = _rh("price-preview/" + MODEL, payload({}))
            return reply(200, {"enabled": True, "model": "Suno6", "price": float(q.get("estimatedPrice") or 0),
                               "currency": q.get("currency", "USD"), "duration": 40})
        raw = event.get("body") or "{}"
        if event.get("isBase64Encoded"):
            raw = base64.b64decode(raw).decode()
        if len(raw) > 12000:
            return reply(413, {"error": "歌词太长，请缩短后再试"})
        body = json.loads(raw)
        query = event.get("queryString") or event.get("queryStringParameters") or {}
        device = str(body.get("device") or query.get("device") or "")[:128]
        request_id = str(body.get("requestId") or path.rsplit("/", 1)[-1])
        if not device or not re.fullmatch(r"[a-f0-9]{32}", request_id):
            return reply(400, {"error": "请求标识不完整"})
        key = _record_key(device, request_id)
        old = _read(key)
        if method == "GET":
            return reply(200, _status(old, request_id)) if old else reply(404, {"error": "未找到这个作曲任务"})
        if method != "POST" or not path.endswith("/music"):
            return reply(404, {"error": "未找到这个功能"})
        p = payload(body)
        digest = hashlib.sha256(json.dumps(p, sort_keys=True, ensure_ascii=False).encode()).hexdigest()
        if old:
            if old.get("payloadHash") != digest:
                return reply(409, {"error": "请求已存在，请查询原来的歌曲"})
            return reply(200, {"task": request_id, "status": old.get("state", "running"), "remaining": old.get("remaining", -1)})
        quote = _rh("price-preview/" + MODEL, p)
        if quote.get("currency") != "USD" or quote.get("estimatedPrice") is None:
            return reply(503, {"error": "暂时无法确认作曲价格，请稍后再试"})
        if float(quote["estimatedPrice"]) > min(.20, float(os.environ.get("MUSIC_MAX_USD", ".10"))):
            return reply(503, {"error": "本次价格超出测试额度，未提交生成"})
        record = {"state": "submitting", "created": time.time(), "payloadHash": digest, "title": p["title"]}
        try:
            _cos("PUT", key, record, create=True)
        except urllib.error.HTTPError as e:
            if e.code in (409, 412):
                return reply(202, {"task": request_id, "status": "running"})
            raise
        remaining = _quota(hashlib.sha256(device.encode()).hexdigest()[:24], request_id)
        if remaining is None:
            record.update(state="refused", error="今天的作曲测试次数已用完")
            _cos("PUT", key, record)
            return reply(429, {"error": record["error"]})
        record["remaining"] = remaining
        _cos("PUT", key, record)
        # Leave a submitting record on timeout/connection loss: the caller must query, never auto-submit again.
        result = _rh(MODEL, p)
        if result.get("taskId"):
            record.update(state="running", providerTask=result["taskId"])
        else:
            record.update(state="refused", error="作曲服务繁忙，本次未创建任务")
        _cos("PUT", key, record)
        return reply(200, {"task": request_id, "status": record["state"], "remaining": remaining})
    except (ValueError, TypeError, json.JSONDecodeError) as e:
        return reply(400, {"error": str(e)[:80] if isinstance(e, ValueError) and not isinstance(e, json.JSONDecodeError) else "参数格式不正确"})
    except Exception:
        # No upstream body, credentials, lyrics or stack trace reach the client/logs.
        return reply(502, {"error": "作曲服务暂时无法连接；已提交的任务可继续查询"})
