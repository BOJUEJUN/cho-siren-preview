"""CHO-SIREN 创意工坊 AI 换装 — GPT Image 2.6 personal test service (Tencent Cloud SCF ap-singapore, Python 3.9).

2026-11-05 (用户: 在美国出差，自己测试游戏里的 GPT 换装). For the owner's own test build only: every
request must carry the test code (env TEST_CODE) in header X-Test-Code. Mainland players never reach this
service; the released game keeps the Guangzhou RunningHub service. The OpenAI key lives only in env OPENAI_API_KEY.

  POST /dress          {device, member, prompt, image (PNG base64)} -> {task: "o:<response id>"}
  GET  /dress/o:<id>   -> {status: running|done|failed, url?, error?}
  GET  /img/<id>       -> the finished PNG (unguessable response id; no test code so the game can download it)

OpenAI Responses API background mode does the waiting, so the function needs no storage or worker.
"""
import base64
import hmac
import struct
import zlib
import json
import os
import urllib.error
import urllib.parse
import urllib.request

API = "https://api.openai.com/v1/responses"
MODEL = os.environ.get("AI_DRESS_TEXT_MODEL", "gpt-5.4-mini")
IMAGE_MODEL = os.environ.get("AI_DRESS_IMAGE_MODEL", "gpt-image-2.6-flare")
QUALITY = os.environ.get("AI_DRESS_QUALITY", "medium")
TEMPLATE = ("Use the image_generation tool to edit this character image. Keep exactly the same girl: face, eyes, "
            "expression, hairstyle, hair colour, body and standing pose, same anime art style. Replace her clothes "
            "completely with a brand new idol outfit: {wish}. The new outfit must look clearly different from the "
            "old one, fashionable but not revealing. Full body, centred, plain pure green (#00FF00) background, "
            "no shadow, no floor, no text, no other objects.")
CORS = {"Access-Control-Allow-Origin": "*", "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
        "Access-Control-Allow-Headers": "Content-Type,X-Test-Code", "Content-Type": "application/json; charset=utf-8"}


def reply(code, body):
    return {"isBase64Encoded": False, "statusCode": code, "headers": CORS, "body": json.dumps(body, ensure_ascii=False)}


def openai(url, body=None):
    request = urllib.request.Request(url, data=json.dumps(body).encode() if body else None,
                                     method="POST" if body else "GET", headers={
        "Authorization": "Bearer " + os.environ["OPENAI_API_KEY"], "Content-Type": "application/json"})
    with urllib.request.urlopen(request, timeout=20) as response:
        return json.loads(response.read())


def authorised(event):
    headers = {k.lower(): v for k, v in (event.get("headers") or {}).items()}
    expected = os.environ.get("TEST_CODE", "")
    return bool(expected) and hmac.compare_digest(headers.get("x-test-code", ""), expected)


def safe_id(value):
    return value if value.startswith("resp_") and value.replace("_", "").isalnum() and len(value) < 100 else None


def submit(body):
    image = body.get("image") or ""
    prompt = (body.get("prompt") or "").strip()[:300]
    if not image or not prompt or len(image) > 8_000_000:
        return reply(400, {"error": "请求不完整"})
    result = openai(API, {
        "model": MODEL, "background": True, "store": True,
        "input": [{"role": "user", "content": [
            {"type": "input_text", "text": TEMPLATE.format(wish=prompt)},
            {"type": "input_image", "image_url": "data:image/png;base64," + image}]}],
        "tools": [{"type": "image_generation", "model": IMAGE_MODEL, "size": "1024x1536", "quality": QUALITY,
                   "output_format": "png", "action": "edit", "moderation": "low"}],
        "tool_choice": {"type": "image_generation"}})
    print("ai-dress submit", result.get("id"), result.get("status"))
    return reply(200, {"task": "o:" + result["id"], "remaining": -1})


def image_call(response):
    for item in response.get("output") or []:
        if item.get("type") == "image_generation_call":
            return item
    return None


def query(task, base):
    response_id = safe_id(task[2:]) if task.startswith("o:") else None
    if not response_id:
        return reply(400, {"error": "任务不存在"})
    response = openai(f"{API}/{response_id}")
    status = response.get("status")
    if status in ("queued", "in_progress"):
        return reply(200, {"status": "running"})
    call = image_call(response)
    if status == "completed" and call and call.get("result"):
        return reply(200, {"status": "done", "url": f"{base}/img/{response_id}"})
    error = json.dumps((call or {}).get("error") or response.get("error") or {})
    print("ai-dress failed", response_id, status, error[:300])
    friendly = "这套衣服没通过审核，换个风格试试" if "moderation" in error else "生成失败，换个描述再试试"
    return reply(200, {"status": "failed", "error": friendly})


def shrink_png(data, step=2):
    """Halve a non-interlaced 8-bit RGB/RGBA PNG (stdlib only): the function URL cuts responses near 1 MB."""
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        return data
    pos, idat, width = 8, [], 0
    while pos < len(data):
        length, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        if kind == b"IHDR":
            width, height, depth, ctype, _, _, interlace = struct.unpack(">IIBBBBB", body)
            if depth != 8 or ctype not in (2, 6) or interlace:
                return data
            bpp = 3 if ctype == 2 else 4
        elif kind == b"IDAT":
            idat.append(body)
        pos += 12 + length
    raw = zlib.decompress(b"".join(idat))
    stride = width * bpp
    prev = bytearray(stride)
    out = bytearray()
    for y in range(height):
        f = raw[y * (stride + 1)]
        line = bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        if f == 1:
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 255
        elif f == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 255
        elif f == 3:
            for i in range(stride):
                left = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 255
        elif f == 4:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                b = prev[i]
                c = prev[i - bpp] if i >= bpp else 0
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        prev = line
        if y % step == 0:
            out.append(0)
            for x in range(0, width, step):
                out += line[x * bpp:x * bpp + bpp]
    w2, h2 = (width + step - 1) // step, (height + step - 1) // step

    def chunk(kind, body):
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)

    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w2, h2, 8, 2 if bpp == 3 else 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(bytes(out), 9)) + chunk(b"IEND", b""))


def image(response_id):
    response_id = safe_id(response_id)
    call = image_call(openai(f"{API}/{response_id}")) if response_id else None
    if not call or not call.get("result"):
        return reply(404, {"error": "图片不存在"})
    return {"isBase64Encoded": True, "statusCode": 200,
            "headers": {"Content-Type": "image/png", "Access-Control-Allow-Origin": "*",
                        "Cache-Control": "private, max-age=3600"},
            "body": base64.b64encode(shrink_png(base64.b64decode(call["result"]))).decode()}


def main_handler(event, context):
    method = (event.get("httpMethod") or event.get("requestContext", {}).get("httpMethod") or "GET").upper()
    path = event.get("path") or event.get("rawPath") or "/"
    headers = {k.lower(): v for k, v in (event.get("headers") or {}).items()}
    base = os.environ.get("PUBLIC_URL") or "https://" + headers.get("host", "")
    if method == "OPTIONS":
        return reply(204, {})
    try:
        if method == "GET" and "/img/" in path:
            return image(urllib.parse.unquote(path.rsplit("/", 1)[-1]))
        if method == "GET" and path.rstrip("/") in ("", "/"):
            return reply(200, {"ok": True, "service": "cho-siren ai-dress gpt test"})
        if not authorised(event):
            return reply(403, {"error": "这是测试服务，未授权"})
        if method == "POST" and path.rstrip("/").endswith("/dress"):
            raw = event.get("body") or "{}"
            if event.get("isBase64Encoded"):
                raw = base64.b64decode(raw).decode()
            return submit(json.loads(raw))
        if method == "GET" and "/dress/" in path:
            return query(urllib.parse.unquote(path.rsplit("/", 1)[-1]), base)
        return reply(404, {"error": "not found"})
    except urllib.error.HTTPError as error:
        print("ai-dress openai http", error.code, error.read()[:300])
        return reply(502, {"error": "AI 服务暂时不可用，请稍后再试"})
    except Exception as error:  # never leak the key or a stack trace
        print("ai-dress error", type(error).__name__, str(error)[:200])
        return reply(500, {"error": "AI 服务出错了，请稍后再试"})
