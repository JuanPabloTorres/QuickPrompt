import hashlib
import json
import threading
import time
import uuid
from datetime import datetime, timezone
from pydantic import BaseModel, ConfigDict, Field, ValidationError
from typing import Literal
from .capabilities import Invocation, ClarificationRequired
from .provider import ProviderError
from .store import encode, now


class Call(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)
    kind: Literal["call"]
    tool: str = Field(min_length=1, max_length=100)
    arguments: dict


class Message(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)
    kind: Literal["finish", "clarify"]
    message: str = Field(min_length=1, max_length=4000)


class AgentRuntime:
    def __init__(self, store, registry, provider, *, permissions, max_steps=6, max_seconds=240, today=None, read_completion=None):
        self.store, self.registry, self.provider = store, registry, provider
        self.permissions = frozenset(permissions)
        self.max_steps, self.max_seconds = max_steps, max_seconds
        self.today = today or (lambda: datetime.now(timezone.utc).date().isoformat())
        # Optional trusted host postcondition; no model-provided callback or authority.
        # It must be pure and return a validated BaseModel, or None while incomplete.
        self.read_completion = read_completion
        self.worker_lock = threading.Lock()

    def create(self, goal, model, *, background=True):
        if not self.worker_lock.acquire(blocking=False):
            raise ValueError("El modelo está atendiendo otra misión. Espera a que termine.")
        try:
            run_id = uuid.uuid4().hex
            with self.store.transaction() as db:
                revision = db.execute("SELECT value FROM metadata WHERE key='revision'").fetchone()[0]
                db.execute("INSERT INTO runs(id,goal,model,status,created_at,updated_at,revision,permissions) VALUES (?,?,?,'running',?,?,?,?)",
                           (run_id, goal, model, now(), now(), revision, encode(sorted(self.permissions))))
                self.store.event(db, run_id, "context", "Objetivo recibido", {"goal": goal, "revision": revision})
                self.store.event(db, run_id, "discovery", "Capacidades autorizadas descubiertas",
                                 {"tools": [x["name"] for x in self.registry.describe(self.permissions)]})
            if background:
                threading.Thread(target=self._work_and_release, args=(run_id,), daemon=True).start()
            else:
                self._work_and_release(run_id)
            return run_id
        except BaseException:
            if self.worker_lock.locked():
                self.worker_lock.release()
            raise

    def _work_and_release(self, run_id):
        try:
            self._work(run_id)
        finally:
            self.worker_lock.release()

    def _work(self, run_id):
        observations, started = [], time.monotonic()
        try:
            run = self.store.get(run_id)
            for step in range(self.max_steps):
                with self.store.transaction() as db:
                    if db.execute("SELECT status FROM runs WHERE id=?", (run_id,)).fetchone()[0] != "running":
                        return
                    if time.monotonic() - started >= self.max_seconds:
                        raise ValueError("Se alcanzó el límite de tiempo de la misión")
                    self.store.event(db, run_id, "model", "Consultando modelo local", {"step": step + 1, "model": run["model"]})
                decision, metrics = self.provider.decide(model=run["model"], goal=run["goal"],
                    catalog=self.registry.describe(run["permissions"]), observations=observations,
                    date=self.today())
                with self.store.transaction() as db:
                    if db.execute("SELECT status FROM runs WHERE id=?", (run_id,)).fetchone()[0] != "running":
                        return  # A late inference can never resurrect a cancelled run.
                    if time.monotonic() - started >= self.max_seconds:
                        raise ValueError("Se alcanzó el límite de tiempo de la misión")
                    db.execute("UPDATE runs SET tokens=tokens+? WHERE id=?", (metrics["tokens"], run_id))
                    self.store.event(db, run_id, "decision", "Decisión estructurada recibida", {**metrics, "proposal": decision})
                # Persist the actual model decision even if later validation rolls back.
                with self.store.transaction() as db:
                    if db.execute("SELECT status FROM runs WHERE id=?", (run_id,)).fetchone()[0] != "running":
                        return
                    if isinstance(decision, dict) and decision.get("kind") in {"finish", "clarify"}:
                        message = Message.model_validate(decision)
                        if message.kind == 'finish' and self.read_completion is not None:
                            raise ValueError('El anfitrión todavía no verificó el resultado solicitado; no se declara completado.')
                        status = "completed" if message.kind == "finish" else "needs_input"
                        db.execute("UPDATE runs SET status=?,result=? WHERE id=?", (status, encode({"message": message.message}), run_id))
                        self.store.event(db, run_id, "result", "Consulta completada" if status == "completed" else "Se necesita aclaración",
                                         {"message": message.message, "source": "model", "verified_effects": 0})
                        return
                    call = Call.model_validate(decision)
                    item, args = self.registry.validate(call.tool, call.arguments, run["permissions"])
                    canonical = args.model_dump(mode="json", by_alias=True)
                    if item["effect"] == "write":
                        proposal = {"tool": call.tool, "arguments": canonical, "revision": run["revision"]}
                        proposal["digest"] = hashlib.sha256(encode(proposal).encode()).hexdigest()
                        db.execute("UPDATE runs SET status='awaiting_approval',proposal=? WHERE id=?", (encode(proposal), run_id))
                        self.store.event(db, run_id, "approval", "Borrador listo para tu revisión", proposal)
                        return
                    result = self.registry.invoke(call.tool, canonical, Invocation(db, "local-user", run_id), run["permissions"])
                    receipt = {"tool": call.tool, "arguments": canonical, "result": result}
                    observations.append(receipt)
                    self.store.event(db, run_id, "tool", "Lectura verificada · " + call.tool, receipt)
                # The read receipt is committed before evaluating host postconditions.
                # A rejected verifier preserves what actually happened. No extra model
                # event or inference is fabricated for deterministic completion.
                if self.read_completion is not None:
                    completed = self.read_completion(run['goal'], json.loads(encode(observations)))
                    if completed is not None:
                        if not isinstance(completed, BaseModel):
                            raise ValueError('La verificación del anfitrión requiere un resultado tipado.')
                        payload = completed.model_dump(mode='json', by_alias=True)
                        with self.store.transaction() as db:
                            if db.execute('SELECT status FROM runs WHERE id=?', (run_id,)).fetchone()[0] != 'running':
                                return
                            if time.monotonic() - started >= self.max_seconds:
                                raise ValueError('Se alcanzó el límite de tiempo de la misión')
                            db.execute("UPDATE runs SET status='completed',result=? WHERE id=?", (encode(payload), run_id))
                            self.store.event(db, run_id, 'result', 'Resultado verificado por la aplicación',
                                {'source': 'host_postcondition', 'verified_reads': len(observations), 'verified_effects': 0, 'result': payload})
                        return
            raise ValueError("Se alcanzó el límite de pasos; no se ejecutan más acciones")
        except Exception as exc:
            safe = str(exc) if isinstance(exc, (ValueError, PermissionError, ProviderError)) and not isinstance(exc, ValidationError) else "Contrato o ejecución inválidos. No se completó la misión."
            with self.store.transaction() as db:
                if db.execute("SELECT status FROM runs WHERE id=?", (run_id,)).fetchone()[0] == "running":
                    if isinstance(exc, ClarificationRequired):
                        db.execute("UPDATE runs SET status='needs_input',result=? WHERE id=?", (encode({"message": safe[:500]}), run_id))
                        self.store.event(db, run_id, "result", "La aplicación requiere una referencia verificable", {"message": safe[:500], "source": "domain"})
                    else:
                        db.execute("UPDATE runs SET status='failed',error=? WHERE id=?", (safe[:500], run_id))
                        details = {"error": safe[:500]}
                        if isinstance(exc, ValidationError):
                            details["contract_errors"] = exc.errors(include_input=False, include_context=False, include_url=False)
                        self.store.event(db, run_id, "error", "Misión detenida", details)

    def cancel(self, run_id):
        with self.store.transaction() as db:
            row = db.execute("SELECT status FROM runs WHERE id=?", (run_id,)).fetchone()
            if not row:
                raise KeyError("Ejecución no encontrada")
            if row[0] not in {"running", "awaiting_approval"}:
                raise ValueError("La ejecución ya está cerrada")
            db.execute("UPDATE runs SET status='cancelled' WHERE id=?", (run_id,))
            self.store.event(db, run_id, "cancel", "Cancelada sin ejecutar nuevas escrituras")
        return self.store.get(run_id)

    def approve(self, run_id, digest):
        with self.store.transaction() as db:
            row = db.execute("SELECT * FROM runs WHERE id=?", (run_id,)).fetchone()
            if not row:
                raise KeyError("Ejecución no encontrada")
            run = self.store.decode(row)
            if run["status"] == "completed" and run["proposal"] and run["proposal"]["digest"] == digest:
                return self.store.read_run(db, run_id)  # Idempotent retry of the exact approved artifact.
            if run["status"] != "awaiting_approval" or not run["proposal"] or run["proposal"]["digest"] != digest:
                raise ValueError("La aprobación no corresponde a una propuesta pendiente")
            proposal = run["proposal"]
            if db.execute("SELECT value FROM metadata WHERE key='revision'").fetchone()[0] != proposal["revision"]:
                raise ValueError("Los datos cambiaron. Cancela esta propuesta e inicia una misión nueva.")
            granted = self.permissions.intersection(run["permissions"])
            item, _ = self.registry.validate(proposal["tool"], proposal["arguments"], granted)
            if item["effect"] != "write":
                raise ValueError("La propuesta no es una escritura")
            result = self.registry.invoke(proposal["tool"], proposal["arguments"], Invocation(db, "local-user", run_id), granted)
            db.execute("UPDATE metadata SET value=value+1 WHERE key='revision'")
            db.execute("UPDATE runs SET status='completed',result=? WHERE id=?", (encode(result), run_id))
            self.store.event(db, run_id, "saved", "Escritura aprobada, guardada y verificada", result)
        return self.store.get(run_id)
