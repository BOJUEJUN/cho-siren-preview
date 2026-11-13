"""SQLite job store for the asset factory. One row per job (one asset the game needs); one row per attempt."""
import json
import sqlite3
import threading
import time
from pathlib import Path

SCHEMA = """
create table if not exists jobs (
  id integer primary key autoincrement,
  brief text not null,            -- brief id, e.g. lobby-idle
  target text not null,           -- what it is for, e.g. huansha
  params text not null,           -- json: brief params resolved for this job
  priority integer not null default 50,
  status text not null,           -- queued | running | review | passed | approved | rejected | gave_up | paused
  attempts integer not null default 0,
  max_attempts integer not null default 6,
  best_attempt integer,
  note text default '',
  created real not null,
  updated real not null
);
create table if not exists attempts (
  id integer primary key autoincrement,
  job_id integer not null,
  n integer not null,
  prompt text not null,
  seed integer,
  outputs text default '[]',      -- json list of file paths
  checks text default '{}',       -- json: numeric checks
  review text default '{}',       -- json: AI review {pass, score, problems, fix}
  score real,
  passed integer default 0,
  seconds real,
  created real not null
);
create table if not exists events (
  id integer primary key autoincrement, at real not null, job_id integer, text text not null
);
"""


class Store:
    def __init__(self, path: Path):
        path.parent.mkdir(parents=True, exist_ok=True)
        self.db = sqlite3.connect(str(path), check_same_thread=False)
        self.db.row_factory = sqlite3.Row
        self.lock = threading.RLock()
        with self.lock:
            self.db.executescript(SCHEMA)
            cols = {r[1] for r in self.db.execute("pragma table_info(attempts)").fetchall()}
            if "backend" not in cols:
                self.db.execute("alter table attempts add column backend text default ''")
            self.db.commit()

    def log(self, job_id, text):
        with self.lock:
            self.db.execute("insert into events(at, job_id, text) values(?,?,?)", (time.time(), job_id, text))
            self.db.commit()

    def add_job(self, brief, target, params, priority=50, max_attempts=6):
        now = time.time()
        with self.lock:
            cur = self.db.execute(
                "insert into jobs(brief,target,params,priority,status,max_attempts,created,updated) values(?,?,?,?,?,?,?,?)",
                (brief, target, json.dumps(params, ensure_ascii=False), priority, "queued", max_attempts, now, now))
            self.db.commit()
            return cur.lastrowid

    def has_open_job(self, brief, target):
        with self.lock:
            row = self.db.execute("select id from jobs where brief=? and target=? and status in ('queued','running','review','passed','approved')",
                                  (brief, target)).fetchone()
            return row is not None

    def next_job(self):
        with self.lock:
            row = self.db.execute("select * from jobs where status='queued' order by priority desc, id asc limit 1").fetchone()
            if row is None:
                return None
            self.db.execute("update jobs set status='running', updated=? where id=?", (time.time(), row["id"]))
            self.db.commit()
            return dict(row)

    def set_job(self, job_id, **fields):
        fields["updated"] = time.time()
        keys = ", ".join(f"{k}=?" for k in fields)
        with self.lock:
            self.db.execute(f"update jobs set {keys} where id=?", (*fields.values(), job_id))
            self.db.commit()

    def add_attempt(self, job_id, n, prompt, seed, backend=""):
        with self.lock:
            cur = self.db.execute("insert into attempts(job_id,n,prompt,seed,backend,created) values(?,?,?,?,?,?)",
                                  (job_id, n, prompt, seed, backend, time.time()))
            self.db.commit()
            return cur.lastrowid

    def set_attempt(self, attempt_id, **fields):
        for k in ("outputs", "checks", "review"):
            if k in fields and not isinstance(fields[k], str):
                fields[k] = json.dumps(fields[k], ensure_ascii=False)
        keys = ", ".join(f"{k}=?" for k in fields)
        with self.lock:
            self.db.execute(f"update attempts set {keys} where id=?", (*fields.values(), attempt_id))
            self.db.commit()

    def jobs(self, limit=200):
        with self.lock:
            rows = self.db.execute("select * from jobs order by updated desc limit ?", (limit,)).fetchall()
            return [dict(r) for r in rows]

    def job(self, job_id):
        with self.lock:
            row = self.db.execute("select * from jobs where id=?", (job_id,)).fetchone()
            return dict(row) if row else None

    def attempts(self, job_id):
        with self.lock:
            rows = self.db.execute("select * from attempts where job_id=? order by n", (job_id,)).fetchall()
            out = []
            for r in rows:
                d = dict(r)
                for k in ("outputs", "checks", "review"):
                    d[k] = json.loads(d[k] or ("[]" if k == "outputs" else "{}"))
                out.append(d)
            return out

    def events(self, limit=80):
        with self.lock:
            rows = self.db.execute("select * from events order by id desc limit ?", (limit,)).fetchall()
            return [dict(r) for r in rows]

    def backend_stats(self, brief=None):
        """Per (brief, backend): attempts, passed, average score and seconds — the model leaderboard."""
        q = ("select j.brief brief, a.backend backend, count(*) n, sum(a.passed) passed, avg(a.score) score, avg(a.seconds) seconds "
             "from attempts a join jobs j on j.id=a.job_id where a.score is not null")
        args = ()
        if brief:
            q += " and j.brief=?"
            args = (brief,)
        q += " group by j.brief, a.backend order by j.brief, passed*1.0/count(*) desc"
        with self.lock:
            return [dict(r) for r in self.db.execute(q, args).fetchall()]

    def counts(self):
        with self.lock:
            rows = self.db.execute("select status, count(*) n from jobs group by status").fetchall()
            return {r["status"]: r["n"] for r in rows}
