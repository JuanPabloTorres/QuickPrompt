"""Offline prerequisite/import probe. Never starts the worker or calls inference."""
import importlib
import importlib.metadata
import json
from pathlib import Path
import sys


def inspect(root):
    failures = []
    if sys.version_info < (3, 11):
        failures.append("PYTHON_VERSION")
    lock = root / "runtime" / "requirements-inference.lock.txt"
    for line in lock.read_text(encoding="utf-8-sig").splitlines():
        if not line.strip() or line.startswith("#"):
            continue
        name, separator, required = line.partition("==")
        if not separator or not name or not required:
            failures.append("INVALID_REQUIREMENTS_LOCK")
            continue
        try:
            if importlib.metadata.version(name) != required:
                failures.append("DEPENDENCY_VERSION:" + name)
        except importlib.metadata.PackageNotFoundError:
            failures.append("DEPENDENCY_MISSING:" + name)
    if not failures:
        sys.path.insert(0, str(root / "runtime" / "sdk"))
        for name in ("adaptive_core.host", "adaptive_core.inference", "pydantic_ai", "jsonschema", "httpx2"):
            try:
                module = importlib.import_module(name)
                if name.startswith("adaptive_core.") and not Path(module.__file__).resolve().is_relative_to((root / "runtime" / "sdk").resolve()):
                    failures.append("SDK_IMPORT_OUTSIDE_PACKAGE")
            except Exception:
                # Provider/import exceptions can contain local paths. Emit fixed codes only.
                failures.append("IMPORT_FAILED:" + name)
    return {"schema": 1, "ready": not failures, "codes": failures, "inferenceVerified": False}


if __name__ == "__main__":
    try:
        result = inspect(Path(__file__).resolve().parent)
    except Exception:
        result = {"schema": 1, "ready": False, "codes": ["PREREQUISITE_PROBE_FAILED"], "inferenceVerified": False}
    print(json.dumps(result, separators=(",", ":")))
    raise SystemExit(0 if result["ready"] else 2)
