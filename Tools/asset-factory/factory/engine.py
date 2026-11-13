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

from . import review, scanner
from .generate import Backends
from .integrate import integrate


class Engine:
    def __init__(self, root: Path, cfg: dict, store):
        self.root = root
        self.cfg = cfg
        self.store = store
        self.gen = Backends(cfg, root)
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
        self.hopeless_logged = set()
        self.paid_path = self.out / "paid.json"          # {backend: {allowed, budget_cny, spent_cny}}
        self.disabled_path = self.out / "disabled.json"  # {backend@brief: reason}
        # Jobs left "running" by a previous run (crash, reboot) go back to the queue; their attempts are kept.
        for j in self.store.jobs(100000):
            if j["status"] == "running":
                self.store.set_job(j["id"], status="queued")
        self.reload_briefs()

    # ------------------------------------------------------------------ briefs and backlog

    def reload_briefs(self):
        data = yaml.safe_load((self.root / "briefs.yaml").read_text(encoding="utf-8")) or {}
        self.briefs = {b["id"]: b for b in data.get("briefs", [])}
        # Targets accepted from the page's suggestions live in briefs.auto.yaml ({brief id: [targets]}).
        auto_path = self.root / "briefs.auto.yaml"
        auto = (yaml.safe_load(auto_path.read_text(encoding="utf-8")) or {}) if auto_path.exists() else {}
        for bid, targets in auto.items():
            if bid in self.briefs:
                self.briefs[bid].setdefault("targets", [])
                known = {self.target_key(self.briefs[bid], t) for t in self.briefs[bid]["targets"]}
                self.briefs[bid]["targets"] += [t for t in targets or [] if self.target_key(self.briefs[bid], t) not in known]
        self.top_up()

    def suggestions(self):
        return scanner.suggestions(self.game, self.briefs)

    def accept_suggestion(self, key):
        for s in self.suggestions():
            if s["key"] == key:
                auto_path = self.root / "briefs.auto.yaml"
                auto = (yaml.safe_load(auto_path.read_text(encoding="utf-8")) or {}) if auto_path.exists() else {}
                auto.setdefault(s["brief"], []).append(s["target"])
                auto_path.write_text(yaml.safe_dump(auto, allow_unicode=True, sort_keys=False), encoding="utf-8")
                self.store.log(None, f"加入任务：{s['title']} · {s['target'].get('id')}（{s['why']}）")
                self.reload_briefs()
                return True
        return False

    # ------------------------------------------------------------------ paid backends and switching off poor ones

    def _read(self, path):
        try:
            return json.loads(path.read_text(encoding="utf-8"))
        except Exception:
            return {}

    def _write(self, path, data):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")

    def paid(self):
        """Every paid backend with its price, 少爷's decision and the money spent so far."""
        state = self._read(self.paid_path)
        out = []
        for name, spec in self.gen.specs.items():
            if not spec.get("paid"):
                continue
            s = state.get(name, {})
            out.append({"name": name, "price_cny": spec.get("price_cny", 0), "note": spec.get("note", ""),
                        "allowed": bool(s.get("allowed")), "budget_cny": s.get("budget_cny", spec.get("budget_cny", 50)),
                        "spent_cny": round(s.get("spent_cny", 0.0), 2)})
        return out

    def set_paid(self, name, allowed, budget):
        state = self._read(self.paid_path)
        row = state.setdefault(name, {})
        row["allowed"] = bool(allowed)
        if budget is not None:
            row["budget_cny"] = float(budget)
        self._write(self.paid_path, state)
        self.store.log(None, f"付费模型 {name}：{'允许' if allowed else '不用'}（预算 ¥{row.get('budget_cny', 0)}）")

    def allowed(self, name, brief_id=None):
        if brief_id and f"{name}@{brief_id}" in self._read(self.disabled_path):
            return False
        spec = self.gen.spec(name)
        if not spec.get("paid"):
            return True
        row = self._read(self.paid_path).get(name, {})
        return bool(row.get("allowed")) and row.get("spent_cny", 0) + spec.get("price_cny", 0) <= row.get("budget_cny", spec.get("budget_cny", 50))

    def charge(self, name):
        spec = self.gen.spec(name)
        if spec.get("paid") and not self.cfg.get("mock"):
            state = self._read(self.paid_path)
            row = state.setdefault(name, {})
            row["spent_cny"] = row.get("spent_cny", 0.0) + float(spec.get("price_cny", 0))
            self._write(self.paid_path, state)

    def review_backend(self, brief, name):
        """Stop wasting the GPU (or money) on a model that keeps failing a brief while another does better."""
        stats = {r["backend"]: r for r in self.store.backend_stats(brief["id"])}
        me = stats.get(name)
        if not me or me["n"] < self.cfg.get("judge_after", 6):
            return
        rate = (me["passed"] or 0) / me["n"]
        best = max(((r["passed"] or 0) / r["n"] for k, r in stats.items() if k != name and r["n"] >= 3), default=None)
        if rate < self.cfg.get("min_pass_rate", .25) and best is not None and best > rate:
            off = self._read(self.disabled_path)
            off[f"{name}@{brief['id']}"] = f"通过率 {rate:.0%}，低于 {best:.0%} 的模型"
            self._write(self.disabled_path, off)
            self.store.log(None, f"{brief.get('title', brief['id'])}：{name} 通过率只有 {rate:.0%}，停用，省下算力给更好的模型")

    def pick_backend(self, brief):
        """Quality first (少爷: "不是说能跑就直接跑浪费算力的，要效果好的").
        - A model is only tried on a brief for `trial_attempts` (3) attempts; after that it keeps working there only if
          it is the best or within 10 points of the best pass rate. Losers stop getting work (and review_backend()
          switches them off for good).
        - Among the keepers the best one gets most attempts; the close runner-up gets 15% so the ranking stays honest."""
        names = self.gen.names_for(brief, lambda n: self.allowed(n, brief["id"]))
        if not names:
            raise RuntimeError(f"任务 {brief['id']} 现在没有可用的模型（付费的要少爷先允许）")
        trial = int(self.cfg.get("trial_attempts", 3))
        stats = {r["backend"]: r for r in self.store.backend_stats(brief["id"])}
        untried = [n for n in names if (stats.get(n) or {}).get("n", 0) < trial]
        if untried:
            return untried[0]
        rate = {n: (stats[n]["passed"] or 0) / max(1, stats[n]["n"]) for n in names}
        best = max(names, key=lambda n: (rate[n], stats[n]["score"] or 0))
        close = [n for n in names if n != best and rate[n] >= rate[best] - .10]
        if close and random.random() < .15:
            return random.choice(close)
        return best

    def brief_is_hopeless(self, brief):
        """If even the best model rarely passes a brief, stop spending GPU on it until a better model is added."""
        stats = self.store.backend_stats(brief["id"])
        tried = sum(r["n"] for r in stats)
        if tried < self.cfg.get("hopeless_after", 12):
            return False
        best = max(((r["passed"] or 0) / max(1, r["n"]) for r in stats), default=0)
        return best < self.cfg.get("min_brief_pass", .2)

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
            if self.brief_is_hopeless(brief):
                if brief["id"] not in self.hopeless_logged:
                    self.hopeless_logged.add(brief["id"])
                    self.store.log(None, f"{brief.get('title', brief['id'])}：现有模型通过率太低，先停，等更好的模型（模型页的雷达）")
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
            "image": str(ref_path) if ref and ref_path.exists() else None,
            "speaker": target.get("speaker", target.get("id", "")),
            "text": target.get("text", ""),
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
            backend = self.pick_backend(brief)
            attempt_id = self.store.add_attempt(job["id"], n, values["prompt"], seed, backend)
            self.store.set_job(job["id"], attempts=n)
            self.store.log(job["id"], f"{brief['title']} · {job['target']} 第 {n} 次生成（{backend}）")
            t0 = time.time()
            try:
                files = self.gen.run(backend, values, work, brief.get("kind", "video"))
            except Exception as e:
                self.store.set_attempt(attempt_id, checks={"problems": [f"生成失败：{e}"]}, seconds=time.time() - t0)
                self.store.log(job["id"], f"生成失败：{e}")
                time.sleep(5)
                continue
            seconds = time.time() - t0
            self.busy_seconds += seconds
            self.charge(backend)
            media = (".mp4", ".webm", ".mov", ".gif", ".wav", ".mp3", ".flac", ".ogg", ".png", ".jpg", ".jpeg", ".webp")
            main = next((f for f in files if f.lower().endswith(media)), files[0] if files else None)
            if main is None:
                self.store.set_attempt(attempt_id, checks={"problems": ["没有输出文件"]}, seconds=seconds)
                continue
            ok, checks = review.numeric_checks(Path(main), brief.get("checks", {}))
            sheet = review.contact_sheet(Path(main), ref_path, work / f"sheet-{n}.jpg")
            ai = None
            try:
                if Path(main).suffix.lower() in review.AUDIO:
                    # Ears, not eyes: voice lines are checked by transcription against the line they should say.
                    expect = str(brief.get("expect_text", "")).format(**json.loads(job["params"])) if brief.get("expect_text") else ""
                    heard = review.asr_check(self.cfg, Path(main), expect)
                    judged = review.ai_audio_review(self.cfg, Path(main), brief.get("rubric", ""), values["prompt"])
                    # Both ears must agree: the words are right (ASR) and it sounds good (audio judge).
                    parts = [p for p in (heard, judged) if p]
                    if parts:
                        ai = {"pass": all(p["pass"] for p in parts), "score": min(p["score"] for p in parts),
                              "problems": sum((p.get("problems") or [] for p in parts), []),
                              "fix_prompt": (judged or {}).get("fix_prompt")}
                else:
                    ai = review.ai_review(self.cfg, sheet, brief.get("rubric", ""), values["prompt"])
            except Exception as e:
                self.store.log(job["id"], f"AI 审核出错：{e}")
            score = ai["score"] if ai else (70.0 if ok else 30.0)
            passed = ok and (ai["pass"] if ai else True)
            self.store.set_attempt(attempt_id, outputs=[main, str(sheet)], checks=checks, review=ai or {},
                                   score=score, passed=int(passed), seconds=seconds)
            self.review_backend(brief, backend)
            if score > best_score:
                best, best_score = attempt_id, score
                self.store.set_job(job["id"], best_attempt=attempt_id)
            if passed:
                status = "passed" if ai else "review"
                self.store.set_job(job["id"], status=status)
                self.store.log(job["id"], f"通过（{score:.0f} 分）" + ("，等少爷验收" if status == "passed" else "，没配 AI 审核，等人工看"))
                return
            # Rework: the reviewer's rewritten prompt, plus the hard-check problems spelled out.
            problems = (checks.get("problems") or []) + ((ai or {}).get("problems") or [])
            self.store.log(job["id"], "返工：" + "；".join(problems[:4]))
            if ai and ai.get("fix_prompt") and ai["fix_prompt"] != values["prompt"]:
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
        # 少爷: "我点击验收之后就会合入游戏" — straight into the game checkout (or the code-needed inbox).
        result = {"where": "已导出", "files": [str(dest)]}
        brief = self.briefs.get(job["brief"])
        if brief is not None:
            try:
                result = integrate(self.game, self.root, brief, job, dest, self.cfg)
            except Exception as e:
                result = {"where": f"合入游戏失败：{e}（文件在 {dest}）", "files": [str(dest)]}
        self.store.set_job(job_id, status="approved", note=result["where"])
        self.store.log(job_id, f"少爷验收通过 · {result['where']}")
        self.top_up()
        return result

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
