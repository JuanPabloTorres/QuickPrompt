from dataclasses import dataclass
from inspect import getmembers, signature
from typing import Literal, get_type_hints
import sqlite3
from pydantic import BaseModel


class ClarificationRequired(ValueError):
    """The host cannot establish a unique, source-grounded target."""


@dataclass(frozen=True)
class Invocation:
    db: sqlite3.Connection
    actor: str
    run_id: str


def capability(*, description: str, effect: Literal["read", "write"], permission: str):
    """Opt in a method. Undecorated methods are never discoverable."""
    if effect not in {"read", "write"} or not permission:
        raise ValueError("Invalid effect or permission")
    def decorate(fn):
        fn.__capability__ = {"description": description, "effect": effect, "permission": permission}
        return fn
    return decorate


class Registry:
    def __init__(self):
        self._items = {}

    def register(self, host):
        for name, fn in getmembers(host, callable):
            metadata = getattr(fn, "__capability__", None)
            if metadata is None:
                continue
            if name in {"agent_finish", "agent_clarify"}:
                raise ValueError(f"Reserved capability name: {name}")
            if name in self._items:
                raise ValueError(f"Duplicate capability: {name}")
            hints = get_type_hints(fn)
            inputs, outputs = hints.get("args"), hints.get("return")
            if list(signature(fn).parameters) != ["args", "ctx"]:
                raise TypeError("Capability signature must be (args: BaseModel, ctx: Invocation)")
            if not all(isinstance(t, type) and issubclass(t, BaseModel) for t in (inputs, outputs)):
                raise TypeError("Capabilities require Pydantic input and output contracts")
            self._items[name] = {**metadata, "fn": fn, "input": inputs, "output": outputs}
        return self

    def describe(self, permissions):
        return [{"name": name, "version": "1", **{k: item[k] for k in ("description", "effect", "permission")},
                 "approval_required": item["effect"] == "write",
                 "input_schema": item["input"].model_json_schema(),
                 "output_schema": item["output"].model_json_schema()}
                for name, item in self._items.items() if item["permission"] in permissions]

    def validate(self, name, arguments, permissions):
        item = self._items.get(name)
        if item is None or item["permission"] not in permissions:
            raise PermissionError("Capacidad inexistente o no autorizada")
        return item, item["input"].model_validate(arguments)

    def invoke(self, name, arguments, ctx, permissions):
        item, args = self.validate(name, arguments, permissions)
        return item["output"].model_validate(item["fn"](args, ctx)).model_dump(mode="json", by_alias=True)
