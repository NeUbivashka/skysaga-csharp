"""
Harvest RPC field structures from the client's own engine log into
docs/reference/data/rpc_observed.json (merged, so every play session adds to it).

With Tools/log/enable_engine_log.py running, the client logs every RPC it sends or receives as
decoded JSON, with the real field names - which are not in the static exe (they live in the
virtualized code and only exist at run time):

    [RPC-CALL][INFO] - RPCClientReadyToSync, {"primaryDataCentreID":""}          client -> server
    [RPC-HandleReceive][INFO] - RPCSetClientLocalPlayerEntity, {"playerEntityID":4}  server -> client

For each RPC this keeps the direction, a merged schema (field -> JSON type, nested), how often
it was seen, and the first and latest example payloads.

    python rpc_observe.py [engine.log ...]     # default: the client's engine.log
"""

import hashlib
import json
import re
import sys
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs" / "reference" / "data" / "rpc_observed.json"
DEFAULT_LOG = Path(r"D:\codya\SkySaga\Alpha v10 36731 fixed\Client\engine.log")
LINE = re.compile(r"\[(RPC-CALL|RPC-HandleReceive)\]\[\w+\] - (RPC\w+), (.*)$")
MAX_EXAMPLE = 4000


def jtype(v):
    if isinstance(v, bool):
        return "bool"
    if isinstance(v, int):
        return "int"
    if isinstance(v, float):
        return "float"
    if isinstance(v, str):
        return "string"
    if v is None:
        return "null"
    if isinstance(v, list):
        return "array"
    return "object"


def merge_schema(schema, value):
    """schema: {field: {"types": [...], "fields": {...}, "items": {...}}}"""
    if not isinstance(value, dict):
        return schema
    for k, v in value.items():
        node = schema.setdefault(k, {"types": []})
        t = jtype(v)
        if t not in node["types"]:
            node["types"].append(t)
        if isinstance(v, dict):
            merge_schema(node.setdefault("fields", {}), v)
        elif isinstance(v, list):
            for item in v:
                if isinstance(item, dict):
                    merge_schema(node.setdefault("items", {}), item)
                else:
                    it = node.setdefault("item_types", [])
                    if jtype(item) not in it:
                        it.append(jtype(item))
    return schema


def parse_payload(text):
    text = text.strip()
    try:
        return json.loads(text), True
    except json.JSONDecodeError:
        # Long payloads may be cut by the log buffer; keep the text for the record.
        return text, False


def main():
    logs = [Path(p) for p in sys.argv[1:]] or [DEFAULT_LOG]
    data = json.loads(OUT.read_text(encoding="utf-8")) if OUT.exists() else {"rpcs": {}}
    rpcs = data["rpcs"]
    now = datetime.now().isoformat(timespec="seconds")
    seen = 0
    for log in logs:
        if not log.exists():
            print(f"skip {log}: not found")
            continue
        text = log.read_text(encoding="utf-8", errors="replace")
        digest = hashlib.sha1(text.encode("utf-8")).hexdigest()
        if digest in data.setdefault("processed", []):
            print(f"skip {log}: already harvested")
            continue
        data["processed"].append(digest)
        for raw in text.splitlines():
            m = LINE.search(raw)
            if not m:
                continue
            kind, name, payload = m.groups()
            value, ok = parse_payload(payload)
            entry = rpcs.setdefault(name, {"directions": [], "count": 0, "schema": {}, "first_example": None,
                                           "latest_example": None, "parse_errors": 0})
            direction = "C2S" if kind == "RPC-CALL" else "S2C"
            if direction not in entry["directions"]:
                entry["directions"].append(direction)
            entry["count"] += 1
            if ok:
                merge_schema(entry["schema"], value)
                example = json.dumps(value)[:MAX_EXAMPLE]
                entry["first_example"] = entry["first_example"] or example
                entry["latest_example"] = example
            else:
                entry["parse_errors"] += 1
                entry["latest_raw"] = payload[:MAX_EXAMPLE]
            entry["last_seen"] = now
            seen += 1
    data["updated"] = now
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(data, indent=1, sort_keys=True), encoding="utf-8")
    print(f"{seen} RPC log lines -> {OUT} ({len(rpcs)} RPCs known)")


if __name__ == "__main__":
    main()
