"""CHO-SIREN 创意工坊 AI 换装 service (Tencent Cloud SCF, Python 3.9, stdlib only).

The game never holds a model key. It posts the member's still and an outfit request here; this function keeps
the RunningHub key (env RUNNINGHUB_API_KEY), asks Seedream 4.7 image-to-image to change only the outfit on a plain
green backdrop (the game keys the green out), limits each device per day, and returns the task to poll.

  POST /dress        {device, member, prompt, image (PNG base64)} -> {task, remaining}
  GET  /dress/<task> -> {status: running|done|failed, url?, error?}

Daily quota per device lives in COS (ai-quota/<date>.json) using env COS_* credentials.
"""
import base64
import datetime
import hashlib
import hmac
import json
import os
import time
import urllib.error
import urllib.parse
import urllib.request

RH = "https://www.runninghub.cn/openapi/v2"
MODEL = os.environ.get("AI_DRESS_MODEL", "seedream-v4.7/image-to-image")
DAILY_LIMIT = int(os.environ.get("AI_DRESS_DAILY_LIMIT", "5"))
GLOBAL_DAILY_LIMIT = int(os.environ.get("AI_DRESS_GLOBAL_LIMIT", "300"))
TEMPLATE = ("把图中人物的服装完全换成一套新衣服：{wish}。新服装的款式和颜色必须和原图明显不同，原来的衣服不要保留。"
            "严格保持人物的脸、五官、表情、发型、发色、身材、站姿和二次元画风不变，服装适合偶像女团，时尚但不暴露。"
            "背景必须是纯绿色 #00FF00，没有阴影、没有地面、没有其他物体，人物全身完整、居中。")
CORS = {"Access-Control-Allow-Origin": "*", "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
        "Access-Control-Allow-Headers": "Content-Type", "Content-Type": "application/json; charset=utf-8"}


def reply(code, body):
    return {"isBase64Encoded": False, "statusCode": code, "headers": CORS, "body": json.dumps(body, ensure_ascii=False)}


def rh(path, body):
    request = urllib.request.Request(f"{RH}/{path}", data=json.dumps(body).encode(), method="POST", headers={
        "Authorization": "Bearer " + os.environ["RUNNINGHUB_API_KEY"], "Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=40) as response:
        return json.loads(response.read())


# ------------------------------------------------------------------ COS quota

def cos_request(method, key, data=None):
    bucket, region = os.environ["COS_BUCKET"], os.environ["COS_REGION"]
    host = f"{bucket}.cos.{region}.myqcloud.com"
    path = "/" + key
    now = int(time.time())
    key_time = f"{now - 60};{now + 600}"
    sign_key = hmac.new(os.environ["COS_SECRET_KEY"].encode(), key_time.encode(), hashlib.sha1).hexdigest()
    http_string = f"{method.lower()}\n{path}\n\nhost={urllib.parse.quote(host, safe='')}\n"
    string_to_sign = f"sha1\n{key_time}\n{hashlib.sha1(http_string.encode()).hexdigest()}\n"
    signature = hmac.new(sign_key.encode(), string_to_sign.encode(), hashlib.sha1).hexdigest()
    auth = (f"q-sign-algorithm=sha1&q-ak={os.environ['COS_SECRET_ID']}&q-sign-time={key_time}&q-key-time={key_time}"
            f"&q-header-list=host&q-url-param-list=&q-signature={signature}")
    request = urllib.request.Request(f"https://{host}{urllib.parse.quote(path)}", data=data, method=method,
                                     headers={"Authorization": auth, "Host": host})
    with urllib.request.urlopen(request, timeout=10) as response:
        return response.read()


def take_quota(device):
    """Returns remaining uses after taking one, or -1 when the device or the day is used up."""
    day = (datetime.datetime.utcnow() + datetime.timedelta(hours=8)).strftime("%Y-%m-%d")
    key = f"ai-quota/{day}.json"
    try:
        counts = json.loads(cos_request("GET", key))
    except urllib.error.HTTPError as error:
        if error.code != 404:
            raise
        counts = {}
    device = hashlib.sha256(device.encode()).hexdigest()[:24]
    used, total = counts.get(device, 0), sum(counts.values())
    if used >= DAILY_LIMIT or total >= GLOBAL_DAILY_LIMIT:
        return -1
    counts[device] = used + 1
    cos_request("PUT", key, json.dumps(counts).encode())
    return DAILY_LIMIT - used - 1


# ------------------------------------------------------------------ handlers

def submit(body):
    device = str(body.get("device") or "")[:128]
    image = str(body.get("image") or "")
    wish = " ".join(str(body.get("prompt") or "").split())[:160]
    if not device or not image or not wish:
        return reply(400, {"error": "请求不完整"})
    if len(image) > 7_000_000:
        return reply(413, {"error": "立绘太大了"})
    remaining = take_quota(device)
    if remaining < 0:
        return reply(429, {"error": f"今天的 AI 换装次数用完了（每天 {DAILY_LIMIT} 次）"})
    result = rh(MODEL, {"prompt": TEMPLATE.format(wish=wish), "imageUrls": ["data:image/png;base64," + image],
                        "width": 1152, "height": 2048})
    task = result.get("taskId")
    if not task:
        return reply(502, {"error": result.get("errorMessage") or "AI 服务繁忙，请稍后再试"})
    return reply(200, {"task": task, "remaining": remaining})


def query(task):
    result = rh("query", {"taskId": task})
    status = result.get("status")
    if status == "SUCCESS":
        item = (result.get("results") or [{}])[0]
        return reply(200, {"status": "done", "url": item.get("url") or item.get("outputUrl")})
    if status == "FAILED":
        message = result.get("errorMessage") or ""
        friendly = "描述里有不合适的内容，换个说法试试" if "sensitive" in message.lower() or "审核" in message else "生成失败，换个描述再试试"
        return reply(200, {"status": "failed", "error": friendly})
    return reply(200, {"status": "running"})


def main_handler(event, context):
    method = (event.get("httpMethod") or event.get("requestContext", {}).get("httpMethod") or "GET").upper()
    path = event.get("path") or event.get("rawPath") or "/"
    if method == "OPTIONS":
        return reply(204, {})
    try:
        if method == "POST" and path.rstrip("/").endswith("/dress"):
            raw = event.get("body") or "{}"
            if event.get("isBase64Encoded"):
                raw = base64.b64decode(raw).decode()
            return submit(json.loads(raw))
        if method == "GET" and "/dress/" in path:
            return query(path.rsplit("/", 1)[-1])
        if method == "GET":
            return reply(200, {"ok": True, "service": "cho-siren ai-dress"})
        return reply(404, {"error": "not found"})
    except Exception as error:  # never leak the key or a stack trace
        print("ai-dress error", type(error).__name__, str(error)[:200])
        return reply(500, {"error": "AI 服务出错了，请稍后再试"})
