import json
import sqlite3
import threading
from contextlib import contextmanager
from datetime import datetime, timezone
from pathlib import Path


def now():
    return datetime.now(timezone.utc).isoformat()


def encode(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), sort_keys=True)


class Store:
    """A shared unit of work for domain effects and durable run receipts."""
    def __init__(self, path):
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.lock = threading.RLock()
        with self.transaction() as db:
            db.executescript("""
                CREATE TABLE IF NOT EXISTS runs (
                    id TEXT PRIMARY KEY, goal TEXT NOT NULL, model TEXT NOT NULL,
                    status TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL,
                    revision INTEGER NOT NULL, permissions TEXT NOT NULL,
                    proposal TEXT, result TEXT, error TEXT, tokens INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT, run_id TEXT NOT NULL,
                    at TEXT NOT NULL, kind TEXT NOT NULL, title TEXT NOT NULL, data TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS metadata (key TEXT PRIMARY KEY, value INTEGER NOT NULL);
                INSERT OR IGNORE INTO metadata VALUES ('revision', 1);
            """)

    @contextmanager
    def transaction(self):
        with self.lock:
            db = sqlite3.connect(self.path, timeout=10)
            db.row_factory = sqlite3.Row
            db.execute("PRAGMA foreign_keys = ON")
            try:
                db.execute("BEGIN IMMEDIATE")
                yield db
                db.commit()
            except BaseException:
                db.rollback()
                raise
            finally:
                db.close()

    def event(self, db, run_id, kind, title, data=None):
        db.execute("INSERT INTO events(run_id,at,kind,title,data) VALUES (?,?,?,?,?)",
                   (run_id, now(), kind, title, encode(data or {})))
        db.execute("UPDATE runs SET updated_at=? WHERE id=?", (now(), run_id))

    def get(self, run_id):
        with self.transaction() as db:
            return self.read_run(db, run_id)

    def read_run(self, db, run_id):
        row = db.execute("SELECT * FROM runs WHERE id=?", (run_id,)).fetchone()
        if row is None:
            raise KeyError("Ejecución no encontrada")
        result = self.decode(row)
        result["events"] = [{**dict(e), "data": json.loads(e["data"])} for e in db.execute(
            "SELECT * FROM events WHERE run_id=? ORDER BY id", (run_id,))]
        return result

    @staticmethod
    def decode(row):
        item = dict(row)
        for key in ("proposal", "result", "permissions"):
            item[key] = json.loads(item[key]) if item[key] else None
        return item

    def history(self):
        with self.transaction() as db:
            return [self.decode(r) for r in db.execute("SELECT * FROM runs ORDER BY created_at DESC LIMIT 50")]

    def recover(self):
        with self.transaction() as db:
            for row in db.execute("SELECT id FROM runs WHERE status='running'").fetchall():
                db.execute("UPDATE runs SET status='interrupted',error=? WHERE id=?",
                           ("El proceso se reinició. Inicia una nueva ejecución; no se repiten efectos.", row["id"]))
                self.event(db, row["id"], "error", "Ejecución interrumpida por reinicio")
