"""Web page + API for the asset factory.  Run:  python -m factory.app  (from Tools/asset-factory)"""
import os
from pathlib import Path

import uvicorn
import yaml
from fastapi import Body, FastAPI, HTTPException
from fastapi.responses import FileResponse, HTMLResponse

from .engine import Engine
from .store import Store

ROOT = Path(__file__).resolve().parent.parent
CFG_PATH = Path(os.environ.get("FACTORY_CONFIG", ROOT / "config.yaml"))
if not CFG_PATH.exists():
    CFG_PATH = ROOT / "config.example.yaml"
CFG = yaml.safe_load(CFG_PATH.read_text(encoding="utf-8")) or {}
_OUT = Path(CFG.get("output_dir") or "output")
STORE = Store(Path(CFG.get("db") or ((_OUT if _OUT.is_absolute() else ROOT / _OUT) / "factory.db")))
ENGINE = Engine(ROOT, CFG, STORE)
app = FastAPI(title="CHO-SIREN 资源工厂")


@app.get("/", response_class=HTMLResponse)
def index():
    return (ROOT / "factory" / "static" / "index.html").read_text(encoding="utf-8")


@app.get("/api/state")
def state():
    jobs = STORE.jobs(300)
    briefs = {b: {"title": v.get("title", b), "variants": v.get("variants", 1)} for b, v in ENGINE.briefs.items()}
    return {
        "paused": ENGINE.paused,
        "current": ENGINE.current,
        "utilisation": round(ENGINE.utilisation(), 3),
        "gpu": ENGINE.gen.gpu(),
        "comfy_queue": ENGINE.gen.busy(),
        "counts": STORE.counts(),
        "jobs": jobs,
        "briefs": briefs,
        "events": STORE.events(60),
        "mock": bool(CFG.get("mock")),
        "reviewer": (CFG.get("reviewer") or {}).get("model") if (CFG.get("reviewer") or {}).get("base_url") else None,
    }


@app.get("/api/job/{job_id}")
def job(job_id: int):
    j = STORE.job(job_id)
    if not j:
        raise HTTPException(404)
    return {"job": j, "attempts": STORE.attempts(job_id)}


@app.post("/api/job/{job_id}/approve")
def approve(job_id: int):
    path = ENGINE.approve(job_id)
    if not path:
        raise HTTPException(400, "这个任务还没有可用的结果")
    return {"exported": path}


@app.post("/api/job/{job_id}/reject")
def reject(job_id: int, body: dict = Body(default={})):
    ENGINE.reject(job_id, (body or {}).get("note", ""))
    return {"ok": True}


@app.post("/api/job/{job_id}/retry")
def retry(job_id: int):
    STORE.set_job(job_id, status="queued", attempts=0)
    return {"ok": True}


@app.post("/api/pause")
def pause(body: dict = Body(default={})):
    ENGINE.paused = bool((body or {}).get("paused", not ENGINE.paused))
    return {"paused": ENGINE.paused}


@app.post("/api/reload")
def reload_briefs():
    ENGINE.reload_briefs()
    return {"briefs": list(ENGINE.briefs)}


@app.get("/file")
def file(path: str):
    p = Path(path).resolve()
    allowed = [ENGINE.out.resolve(), ENGINE.export.resolve(), ENGINE.game.resolve()]
    if not any(str(p).startswith(str(a)) for a in allowed) or not p.exists():
        raise HTTPException(404)
    return FileResponse(p)


def main():
    ENGINE.start()
    uvicorn.run(app, host=CFG.get("host", "127.0.0.1"), port=int(CFG.get("port", 8790)), log_level="warning")


if __name__ == "__main__":
    main()
