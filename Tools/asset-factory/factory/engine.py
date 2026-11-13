"""The production loop: take the most important queued job, generate, check, AI-review, and either keep it or rework
the prompt from the reviewer's feedback and try again. When the queue runs dry it tops itself up from the briefs'
`variants` targets, so the GPU keeps working until every asset has enough passed versions."""
import json
import random
import shutil
import threading
import time
import traceback
from pathlib import Path

import yaml

from . import review
from .generate import make_generator


class Engine:
    def __init__(self, root: Path, cfg: dict, store):
        self.root = root
        self.cfg = cfg
        self.store = store
        self.gen = make_generator(cfg, root)
        def under_root(value, default):
            p = Path(value or default).expanduser()
            return p if p.is_absolute() else (root / p).resolve()
        self.game = under_root(cfg.get("game_repo"), "../..")
        self.out = under_root(cfg.get("output_dir"), "output")
        self.export = under_root(cfg.get("export_dir"), "export")
        self.paused = bool(cfg.get("start_paused"))
        self.current = []          # jobs being worked on (one per worker)
        self.busy_seconds = 0.0
        self.samples = []
        self.started = time.time()
        self.briefs = {}
        # Jobs left "running" by a previous run (crash, reboot) go back to the queue; their attempts are kept.
        for j in self.store.jobs(100000):
            if j["status"] == "running":
                self.store.set_job(j["id"], status="queued")
        self.reload_briefs()

    # ------------------------------------------------------------------ briefs and backlog

    def reload_briefs(self):
        data = yaml.safe_load((self.root / "briefs.yaml").read_text(encoding="utf-8")) or {}
        self.briefs = {b["id"]: b for b in data.get("briefs", [])}
        self.top_up()

    @staticmethod
    def target_key(brief, target):
        return target.get("id") or target.get("race") or json.dumps(target, ensure_ascii=False, sort_keys=True)

    def passed_count(self, brief_id, key):
        return sum(1 for j in self.store.jobs(5000) if j["brief"] == brief_id and j["target"] == key and j["status"] in ("passed", "approved"))

    def top_up(self):
        """Queue one job for every target that is short of passed versions and has nothing in flight."""
        for brief in self.briefs.values():
            if brief.get("enabled", True) is False:
                continue
            for target in brief.get("targets", []):
                key = self.target_key(brief, target)
                if self.passed_count(brief["id"], key) >= brief.get("variants", 1):
                    continue
                if any(j["brief"] == brief["id"] and j["target"] == key and j["status"] in ("queued", "running", "review")
                       for j in self.store.jobs(5000)):
                    continue
                # A target that keeps failing waits for a person (edit its brief, then 重新读取任务).
                if sum(1 for j in self.store.jobs(5000) if j["brief"] == brief["id"] and j["target"] == key and j["status"] == "gave_up") >= 3:
                    continue
                self.store.add_job(brief["id"], key, target, brief.get("priority", 50), brief.get("max_attempts", 6))

    # ------------------------------------------------------------------ one job

    def resolve(self, brief, target):
        def sub(text):
            return str(text).format(**target) if text else text
        ref = sub(brief.get("reference", ""))
        ref_path = Path(ref) if Path(ref).is_absolute() else self.game / ref
        values = {
            "prompt": sub(brief["prompt"]),
            "negative": sub(brief.get("negative", "")),
            "width": brief.get("size", [720, 1280])[0],
            "height": brief.get("size", [720, 1280])[1],
            "frames": brief.get("frames", 49),
            "fps": brief.get("fps", 24),
            "image": str(ref_path) if ref_path.exists() else None,
        }
        if brief.get("last_frame") == "same":
            values["last_image"] = values["image"]
        return values, ref_path

    def run_job(self, job):
        brief = self.briefs.get(job["brief"])
        if brief is None:
            self.store.set_job(job["id"], status="paused", note="brief 已删除")
            return
        target = json.loads(job["params"])
        values, ref_path = self.resolve(brief, target)
        if values["image"] is None and brief.get("reference"):
            self.store.set_job(job["id"], status="gave_up", note=f"找不到参考图 {ref_path}")
            self.store.log(job["id"], f"缺参考图 {ref_path}")
            return
        if job.get("note"):
            values["prompt"] += ", " + job["note"]
        work = self.out / brief["id"] / job["target"] / f"job{job['id']}"
        work.mkdir(parents=True, exist_ok=True)
        best, best_score = None, -1.0
        n = job["attempts"]
        while n < job["max_attempts"] and not self.paused:
            n += 1
            seed = random.randint(1, 2**31 - 1)
            values["seed"] = seed
            attempt_id = self.store.add_attempt(job["id"], n, values["prompt"], seed)
            self.store.set_job(job["id"], attempts=n)
            self.store.log(job["id"], f"{brief['title']} · {job['target']} 第 {n} 次生成")
            t0 = time.time()
            try:
                files = self.gen.run(brief["workflow"], values, work)
            except Exception as e:
                self.store.set_attempt(attempt_id, checks={"problems": [f"生成失败：{e}"]}, seconds=time.time() - t0)
                self.store.log(job["id"], f"生成失败：{e}")
                time.sleep(5)
                continue
            seconds = time.time() - t0
            self.busy_seconds += seconds
            main = next((f for f in files if f.lower().endswith((".mp4", ".webm", ".mov", ".gif"))), files[0] if files else None)
            if main is None:
                self.store.set_attempt(attempt_id, checks={"problems": ["没有输出文件"]}, seconds=seconds)
                continue
            ok, checks = review.numeric_checks(Path(main), brief.get("checks", {}))
            sheet = review.contact_sheet(Path(main), ref_path, work / f"sheet-{n}.jpg")
            ai = None
            try:
                ai = review.ai_review(self.cfg, sheet, brief.get("rubric", ""), values["prompt"])
            except Exception as e:
                self.store.log(job["id"], f"AI 审核出错：{e}")
            score = ai["score"] if ai else (70.0 if ok else 30.0)
            passed = ok and (ai["pass"] if ai else True)
            self.store.set_attempt(attempt_id, outputs=[main, str(sheet)], checks=checks, review=ai or {},
                                   score=score, passed=int(passed), seconds=seconds)
            if score > best_score:
                best, best_score = attempt_id, score
                self.store.set_job(job["id"], best_attempt=attempt_id)
            if passed:
                status = "passed" if ai else "review"
                self.store.set_job(job["id"], status=status)
                self.store.log(job["id"], f"通过（{score:.0f} 分）" + ("，等少爷确认" if status == "passed" else "，没配 AI 审核，等人工看"))
                return
            # Rework: the reviewer's rewritten prompt, plus the hard-check problems spelled out.
            problems = (checks.get("problems") or []) + ((ai or {}).get("problems") or [])
            self.store.log(job["id"], "返工：" + "；".join(problems[:4]))
            if ai and ai.get("fix_prompt"):
                values["prompt"] = ai["fix_prompt"]
            elif problems:
                values["prompt"] = values["prompt"] + ", " + "fix: " + "; ".join(problems[:3])
        if self.paused:
            self.store.set_job(job["id"], status="queued")
        else:
            self.store.set_job(job["id"], status="gave_up", note=f"{job['max_attempts']} 次都没过，最好 {best_score:.0f} 分")
            self.store.log(job["id"], "多次返工仍未通过，先放着，换下一个")

    # ------------------------------------------------------------------ approve / reject from the web page

    def approve(self, job_id):
        job = self.store.job(job_id)
        attempts = {a["id"]: a for a in self.store.attempts(job_id)}
        best = attempts.get(job["best_attempt"]) if job else None
        if not best or not best["outputs"]:
            return None
        src = Path(best["outputs"][0])
        dest_dir = self.export / job["brief"]
        dest_dir.mkdir(parents=True, exist_ok=True)
        dest = dest_dir / f"{job['target']}-job{job_id}{src.suffix}"
        shutil.copy2(src, dest)
        self.store.set_job(job_id, status="approved")
        self.store.log(job_id, f"少爷通过 → {dest}")
        self.top_up()
        return str(dest)

    def reject(self, job_id, note):
        job = self.store.job(job_id)
        if not job:
            return
        self.store.set_job(job_id, status="rejected", note=note or "")
        brief = self.briefs.get(job["brief"])
        if brief:
            new_id = self.store.add_job(job["brief"], job["target"], json.loads(job["params"]), job["priority"] + 5, brief.get("max_attempts", 6))
            if note:
                self.store.set_job(new_id, note=note)
        self.store.log(job_id, f"少爷退回：{note}")

    # ------------------------------------------------------------------ loop

    def utilisation(self):
        """Share of the last hour the GPU had work in ComfyUI's queue (sampled every 5 s)."""
        if self.samples:
            return sum(self.samples) / len(self.samples)
        wall = max(1.0, time.time() - self.started)
        return min(1.0, self.busy_seconds / wall)

    def sample(self):
        while True:
            busy = self.gen.busy()
            if busy >= 0 and not self.cfg.get("mock"):
                self.samples.append(1 if busy > 0 else 0)
                del self.samples[:-720]
            time.sleep(5)

    def loop(self):
        while True:
            if self.paused:
                time.sleep(2)
                continue
            job = self.store.next_job()
            if job is None:
                self.top_up()
                job = self.store.next_job()
            if job is None:
                time.sleep(5)
                continue
            self.current.append(job)
            try:
                self.run_job(job)
            except Exception:
                self.store.set_job(job["id"], status="queued")
                self.store.log(job["id"], "出错：" + traceback.format_exc()[-400:])
                time.sleep(10)
            finally:
                self.current.remove(job)

    def start(self):
        # Two workers by default: while one job is being checked and AI-reviewed, the other's generation is already
        # queued in ComfyUI, so the GPU goes straight from one clip to the next instead of waiting for the review.
        for _ in range(max(1, int(self.cfg.get("workers", 2)))):
            threading.Thread(target=self.loop, daemon=True).start()
        threading.Thread(target=self.sample, daemon=True).start()
