"""
Build the single-file SkySaga reference: docs/reference/index.html.

Sources, all inside this repo:
  docs/**/*.md                          articles (protocol, client internals, world format, ...)
  docs/reference/data/rpc_client.json   client side of every RPC, from Tools/re/rpc_extract.py
  docs/reference/data/rpc_observed.json field names/values the client logged, Tools/re/rpc_observe.py
  Servers/SkySaga.Game/PacketId.cs      ordinal -> name as the emulator knows it
  Servers/SkySaga.Game/Packets/*.cs     emulator packets: doc comment, fields, wire code
  Servers/SkySaga.Game/Managers/Player/PlayerConnection.cs   which client->server RPCs are handled

    python docs/reference/build.py

The output is self-contained (no network), so it opens straight from disk.
"""

import html
import json
import re
from pathlib import Path

import markdown

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs"
GAME = ROOT / "Servers" / "SkySaga.Game"
OUT = DOCS / "reference" / "index.html"

ARTICLE_ORDER = ["protocol", "client", "world", "server", "tools"]


# --------------------------------------------------------------------------- server side

def parse_packet_ids():
    ids = {}
    for m in re.finditer(r"^\s*(\w+)\s*=\s*(\d+),", (GAME / "PacketId.cs").read_text(encoding="utf-8-sig"), re.M):
        ids[m.group(1)] = int(m.group(2))
    return ids


def doc_comment_before(text, index):
    """The /// block directly above position `index` (class or member), as plain text."""
    lines = text[:index].rstrip().split("\n")
    block = []
    for line in reversed(lines):
        s = line.strip()
        if s.startswith("///"):
            block.append(s[3:][1:] if s[3:4] == " " else s[3:])
        elif s.startswith("[") or not s:
            if block:
                break
            continue
        else:
            break
    block.reverse()
    body = "\n".join(block)
    body = re.sub(r"</?summary>", "", body)
    body = re.sub(r'<(?:see|seealso) cref="([^"]+)"\s*/>', r"\1", body)
    body = re.sub(r"<c>(.*?)</c>", r"`\1`", body)
    body = re.sub(r'<param name="(\w+)">', r"\1: ", body)
    body = re.sub(r"</param>", "", body)
    return body.strip()


def extract_method(text, signature_regex):
    m = re.search(signature_regex, text)
    if not m:
        return None
    start = text.find("{", m.end() - 1)
    if start < 0:
        return None
    depth = 0
    for i in range(start, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                line_start = text.rfind("\n", 0, m.start()) + 1
                return dedent(text[line_start:i + 1])
    return None


def dedent(code):
    lines = code.split("\n")
    indent = min((len(l) - len(l.lstrip()) for l in lines if l.strip()), default=0)
    return "\n".join(l[indent:] for l in lines)


def parse_packets(packet_ids):
    """class name -> info, plus ordinal -> [class names]."""
    by_ordinal = {}
    classes = {}
    for path in sorted((GAME / "Packets").glob("*.cs")):
        text = path.read_text(encoding="utf-8-sig")
        cm = re.search(r"public\s+(?:static\s+)?(?:sealed\s+)?class\s+(\w+)", text)
        if not cm:
            continue
        name = cm.group(1)
        ordinals = set()
        for m in re.finditer(r"WritePacketId\(\s*PacketId\.(\w+)\s*\)", text):
            if m.group(1) in packet_ids:
                ordinals.add(packet_ids[m.group(1)])
        for m in re.finditer(r"const\s+(?:byte|ushort|int)\s+Ordinal\s*=\s*(\d+)", text):
            ordinals.add(int(m.group(1)))
        for m in re.finditer(r"WritePacketId\(\s*\(PacketId\)\s*(\d+)\s*\)", text):
            ordinals.add(int(m.group(1)))
        fields = []
        for m in re.finditer(r"^\s*public\s+(?:required\s+)?([\w<>\[\]?,. ]+?)\s+(\w+)\s*(?:;|=|\{)", text, re.M):
            ftype, fname = m.group(1).strip(), m.group(2)
            if ftype in ("class", "static", "BitStream Serialize", "bool") and fname in ("Handle",):
                continue
            if "(" in m.group(0) or fname in (name, "Serialize", "Handle"):
                continue
            fields.append({"type": ftype, "name": fname, "doc": doc_comment_before(text, m.start())})
        classes[name] = {
            "class": name,
            "file": str(path.relative_to(ROOT)).replace("\\", "/"),
            "doc": doc_comment_before(text, cm.start()),
            "fields": fields,
            "serialize": extract_method(text, r"public\s+BitStream\s+Serialize\s*\(\s*\)"),
            "handle": extract_method(text, r"public\s+static\s+bool\s+Handle\s*\("),
            "ordinals": sorted(ordinals),
        }
        for o in ordinals:
            by_ordinal.setdefault(o, []).append(name)
    return classes, by_ordinal


def parse_handled(packet_ids):
    """ordinal -> handler class, from the PlayerConnection dispatch switch."""
    text = (GAME / "Managers" / "Player" / "PlayerConnection.cs").read_text(encoding="utf-8-sig")
    handled = {}
    for m in re.finditer(r"PacketId\.(\w+)\s*=>\s*(\w+)\.Handle", text):
        if m.group(1) in packet_ids:
            handled[packet_ids[m.group(1)]] = m.group(2)
    for m in re.finditer(r"\(PacketId\)\s*(\d+)\s*=>\s*(\w+)\.Handle", text):
        handled[int(m.group(1))] = m.group(2)
    return handled


# --------------------------------------------------------------------------- assemble

def load_client():
    path = DOCS / "reference" / "data" / "rpc_client.json"
    if not path.exists():
        return {}
    data = json.loads(path.read_text(encoding="utf-8"))
    return {r["ordinal"]: r for r in data["rpcs"]}


def load_observed():
    path = DOCS / "reference" / "data" / "rpc_observed.json"
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))["rpcs"]


def build_rpcs():
    packet_ids = parse_packet_ids()
    id_names = {v: k for k, v in packet_ids.items()}
    classes, by_ordinal = parse_packets(packet_ids)
    handled = parse_handled(packet_ids)
    client = load_client()
    observed = load_observed()

    rpcs = []
    for ordinal in sorted(set(id_names) | set(client)):
        c = client.get(ordinal, {})
        name = (c.get("name") or ("RPC" + id_names.get(ordinal, f"Unknown{ordinal}")))
        short = name[3:] if name.startswith("RPC") else name
        sends, recvs = c.get("send", []), c.get("receive", [])
        if sends and not recvs:
            direction = "C2S"
        elif recvs and not sends:
            direction = "S2C"
        elif sends and recvs:
            direction = "both"
        else:
            direction = "?"
        server_classes = [classes[n] for n in by_ordinal.get(ordinal, [])]
        handler_cls = handled.get(ordinal)
        if handler_cls and handler_cls in classes and classes[handler_cls] not in server_classes:
            server_classes.append(classes[handler_cls])
        if direction == "?":
            if handler_cls:
                direction = "C2S"
            elif any(s["serialize"] for s in server_classes):
                direction = "S2C"

        if direction in ("C2S",):
            status = "handled" if handler_cls else "unhandled"
        elif direction == "S2C":
            status = "implemented" if any(s["serialize"] for s in server_classes) else "missing"
        elif direction == "both":
            status = "handled" if handler_cls else "partial"
        else:
            status = "unknown"

        rpcs.append({
            "ordinal": ordinal, "name": name, "short": short, "direction": direction, "status": status,
            "client": c, "server": server_classes, "handler": handler_cls, "observed": observed.get(name),
        })
    return rpcs


# --------------------------------------------------------------------------- render

MD = markdown.Markdown(extensions=["tables", "fenced_code", "toc", "sane_lists"])


def md(text):
    MD.reset()
    return MD.convert(text or "")


def esc(s):
    return html.escape(str(s))


def slug(s):
    return re.sub(r"[^a-z0-9]+", "-", s.lower()).strip("-")


def load_articles():
    articles = []
    for path in sorted(DOCS.rglob("*.md")):
        rel = path.relative_to(DOCS)
        if rel.parts[0] == "reference":
            continue
        text = path.read_text(encoding="utf-8")
        title_m = re.search(r"^#\s+(.+)$", text, re.M)
        title = title_m.group(1).strip() if title_m else path.stem
        section = rel.parts[0] if len(rel.parts) > 1 else "general"
        articles.append({"id": "doc-" + slug(str(rel.with_suffix(""))), "title": title,
                         "section": section, "path": str(rel).replace("\\", "/"), "html": md(text)})
    order = {s: i for i, s in enumerate(ARTICLE_ORDER)}
    articles.sort(key=lambda a: (order.get(a["section"], 99), a["path"]))
    return articles


def fn_link(entry):
    if not entry:
        return ""
    return f'<code>{esc(entry.get("function") or "?")}</code> <span class="addr">{esc(entry.get("address") or "")}</span>'


def fields_flat(schema, prefix=""):
    for k, node in schema.items():
        yield prefix + k
        yield from fields_flat(node.get("fields", {}), prefix + k + ".")
        yield from fields_flat(node.get("items", {}), prefix + k + "[].")


def schema_rows(schema, prefix=""):
    for k, node in schema.items():
        t = "/".join(node["types"])
        if node.get("item_types"):
            t += " of " + "/".join(node["item_types"])
        yield f'<tr><td><code>{esc(prefix + k)}</code></td><td>{esc(t)}</td></tr>'
        if node.get("fields"):
            yield from schema_rows(node["fields"], prefix + k + ".")
        if node.get("items"):
            yield from schema_rows(node["items"], prefix + k + "[].")


def pretty(text):
    try:
        return json.dumps(json.loads(text), indent=2)
    except Exception:
        return text


def render_rpc(r):
    c = r["client"]
    parts = [f'<section class="rpc" id="rpc-{r["ordinal"]}" data-search="{esc((r["name"] + " " + str(r["ordinal"]) + " " + " ".join(fields_flat((r.get("observed") or {}).get("schema", {})))).lower())}" '
             f'data-dir="{r["direction"]}" data-status="{r["status"]}">']
    parts.append(f'<h3><span class="ord">#{r["ordinal"]}</span> {esc(r["name"])} '
                 f'<span class="badge dir-{r["direction"]}">{r["direction"]}</span> '
                 f'<span class="badge st-{r["status"]}">{r["status"]}</span></h3>')
    rows = [f'<tr><th>Ordinal</th><td>{r["ordinal"]} (0x{r["ordinal"]:X})</td></tr>']
    for label, key in (("Client sender", "send"), ("Client receive stub", "receive")):
        if c.get(key):
            rows.append(f"<tr><th>{label}</th><td>{'<br>'.join(fn_link(e) for e in c[key])}</td></tr>")
    if r["handler"]:
        rows.append(f'<tr><th>Emulator handler</th><td><code>{esc(r["handler"])}.Handle</code></td></tr>')
    for s in r["server"]:
        rows.append(f'<tr><th>Emulator class</th><td><code>{esc(s["class"])}</code> <span class="addr">{esc(s["file"])}</span></td></tr>')
    parts.append('<table class="kv">' + "".join(rows) + "</table>")
    ob = r.get("observed")
    if ob:
        parts.append(f'<h4>Fields as the client logs them <span class="addr">seen {ob["count"]}&times; ({", ".join(ob["directions"])})</span></h4>')
        if ob["schema"]:
            parts.append('<table class="fields"><thead><tr><th>Field</th><th>JSON type</th></tr></thead><tbody>'
                         + "".join(schema_rows(ob["schema"])) + "</tbody></table>")
        else:
            parts.append('<p class="muted">Empty payload.</p>')
        if ob.get("latest_example") and ob["latest_example"] != "{}":
            parts.append(f'<details><summary>Example payload</summary><pre><code>{esc(pretty(ob["latest_example"]))}</code></pre></details>')
    for s in r["server"]:
        if s["doc"]:
            parts.append(f'<div class="doc">{md(s["doc"])}</div>')
        if s["fields"]:
            frows = "".join(f'<tr><td><code>{esc(f["name"])}</code></td><td><code>{esc(f["type"])}</code></td><td>{md(f["doc"]) if f["doc"] else ""}</td></tr>'
                            for f in s["fields"])
            parts.append(f'<table class="fields"><thead><tr><th>Field</th><th>Type</th><th>Notes</th></tr></thead><tbody>{frows}</tbody></table>')
        for label, key in (("Serialize (server &rarr; client wire format)", "serialize"), ("Handle (client &rarr; server parser)", "handle")):
            if s[key]:
                parts.append(f'<details><summary>{label}</summary><pre><code>{esc(s[key])}</code></pre></details>')
    if not r["server"] and not ob:
        parts.append('<p class="muted">Not analysed yet.</p>')
    parts.append("</section>")
    return "\n".join(parts)


CSS = """
:root { --bg:#fbfbfa; --fg:#1d1d1f; --muted:#6b6b70; --line:#e3e3e0; --card:#fff; --code:#f2f2ef; --accent:#2f6fdf;
        --c2s:#1f7a4d; --s2c:#7a3fb5; --both:#a06000; --ok:#1f7a4d; --warn:#b06000; --bad:#c0392b; }
@media (prefers-color-scheme: dark) { :root:not([data-theme="light"]) {
        --bg:#151517; --fg:#e8e8ea; --muted:#9a9aa2; --line:#2c2c30; --card:#1c1c1f; --code:#232327; --accent:#6aa0ff;
        --c2s:#4cc38a; --s2c:#b88cf0; --both:#e0a040; --ok:#4cc38a; --warn:#e0a040; --bad:#ff6b5e; } }
* { box-sizing:border-box } body { margin:0; background:var(--bg); color:var(--fg); font:15px/1.55 system-ui,-apple-system,Segoe UI,sans-serif }
#side { position:fixed; top:0; left:0; bottom:0; width:300px; overflow:auto; border-right:1px solid var(--line); padding:16px; background:var(--card) }
#main { margin-left:300px; padding:24px 40px; max-width:1100px }
@media (max-width:800px) { #side { position:static; width:auto; height:auto; border-right:0; border-bottom:1px solid var(--line) } #main { margin-left:0; padding:16px } }
h1 { font-size:24px; margin:0 0 4px } h4 { margin:14px 0 4px; font-size:14px } h2 { margin-top:40px; border-bottom:1px solid var(--line); padding-bottom:6px } h3 { margin:0 0 10px; font-size:17px }
a { color:var(--accent); text-decoration:none } a:hover { text-decoration:underline }
code, pre { font-family:ui-monospace,Consolas,monospace; font-size:13px } code { background:var(--code); padding:1px 4px; border-radius:4px }
pre { background:var(--code); padding:12px; border-radius:6px; overflow:auto } pre code { background:none; padding:0 }
table { border-collapse:collapse; margin:8px 0; width:100% } th, td { border:1px solid var(--line); padding:5px 8px; text-align:left; vertical-align:top }
table.kv th { width:190px; color:var(--muted); font-weight:500 }
.rpc { background:var(--card); border:1px solid var(--line); border-radius:8px; padding:16px; margin:14px 0 }
.ord { color:var(--muted); font-weight:500 } .addr { color:var(--muted); font-size:12px } .muted { color:var(--muted) }
.badge { font-size:11px; padding:2px 7px; border-radius:10px; border:1px solid currentColor; vertical-align:middle; font-weight:600 }
.dir-C2S { color:var(--c2s) } .dir-S2C { color:var(--s2c) } .dir-both { color:var(--both) } .dir-\\? { color:var(--muted) }
.st-handled, .st-implemented { color:var(--ok) } .st-partial { color:var(--warn) } .st-unhandled, .st-missing { color:var(--bad) } .st-unknown { color:var(--muted) }
#side input { width:100%; padding:7px 9px; border:1px solid var(--line); border-radius:6px; background:var(--bg); color:var(--fg); margin-bottom:8px }
#side select { width:49%; padding:4px; background:var(--bg); color:var(--fg); border:1px solid var(--line); border-radius:6px }
#side ul { list-style:none; padding:0; margin:6px 0 14px } #side li { margin:2px 0; font-size:13px } #side .sec { font-weight:600; margin-top:12px; text-transform:uppercase; font-size:11px; color:var(--muted) }
#rpclist li { white-space:nowrap; overflow:hidden; text-overflow:ellipsis }
.stats td { text-align:right } .stats td:first-child { text-align:left }
details summary { cursor:pointer; color:var(--accent); margin:6px 0 }
"""

JS = """
const q = document.getElementById('q'), fd = document.getElementById('fdir'), fs = document.getElementById('fst');
function apply() {
  const t = q.value.trim().toLowerCase(), d = fd.value, s = fs.value;
  document.querySelectorAll('section.rpc').forEach(el => {
    const ok = (!t || el.dataset.search.includes(t)) && (!d || el.dataset.dir === d) && (!s || el.dataset.status === s);
    el.style.display = ok ? '' : 'none';
    const li = document.querySelector('#rpclist li[data-o="' + el.id + '"]'); if (li) li.style.display = ok ? '' : 'none';
  });
  try { localStorage.setItem('ref-filter', JSON.stringify([q.value, d, s])); } catch (e) {}
}
try { const v = JSON.parse(localStorage.getItem('ref-filter') || 'null'); if (v) { [q.value, fd.value, fs.value] = v; } } catch (e) {}
[q, fd, fs].forEach(e => e.addEventListener('input', apply)); apply();
"""


def render(articles, rpcs):
    from collections import Counter
    st = Counter((r["direction"], r["status"]) for r in rpcs)
    dirs = ["C2S", "S2C", "both", "?"]
    statuses = ["handled", "implemented", "partial", "unhandled", "missing", "unknown"]
    stat_rows = "".join(
        f"<tr><td>{d}</td>" + "".join(f"<td>{st.get((d, s), '') or ''}</td>" for s in statuses) + f"<td>{sum(v for (dd, _), v in st.items() if dd == d)}</td></tr>"
        for d in dirs)

    side = ['<h1>SkySaga</h1><div class="muted">Reference &middot; client 36731</div>',
            '<div class="sec">Articles</div><ul>']
    last = None
    for a in articles:
        if a["section"] != last:
            side.append(f'</ul><div class="sec">{esc(a["section"])}</div><ul>')
            last = a["section"]
        side.append(f'<li><a href="#{a["id"]}">{esc(a["title"])}</a></li>')
    side.append('</ul><div class="sec">RPCs</div>'
                '<input id="q" placeholder="Filter: name, ordinal, field">'
                '<select id="fdir"><option value="">any direction</option>' + "".join(f'<option>{d}</option>' for d in dirs) + '</select> '
                '<select id="fst"><option value="">any status</option>' + "".join(f'<option>{s}</option>' for s in statuses) + '</select>'
                '<ul id="rpclist">')
    for r in rpcs:
        side.append(f'<li data-o="rpc-{r["ordinal"]}"><a href="#rpc-{r["ordinal"]}">{r["ordinal"]} {esc(r["short"])}</a></li>')
    side.append("</ul>")

    main = ['<h1>SkySaga reverse-engineering reference</h1>',
            '<p class="muted">Generated by <code>docs/reference/build.py</code> from the repository. '
            'Client addresses are SkySagaU.exe build 36731 (image base 0x400000, no ASLR). '
            'Claims in articles are marked <b>[code]</b> (read from disassembly), <b>[run]</b> (observed live), <b>[guess]</b>.</p>',
            '<h2 id="status">RPC coverage</h2>',
            '<p>Direction: <b>C2S</b> client&rarr;server, <b>S2C</b> server&rarr;client, from which client stub calls the RPC stats hook '
            '<code>FUN_00BB3E50(dir, ordinal, bits, json)</code>. Status is what the emulator does with it.</p>',
            '<table class="stats"><thead><tr><th>Direction</th>' + "".join(f"<th>{s}</th>" for s in statuses) + '<th>total</th></tr></thead>'
            f'<tbody>{stat_rows}</tbody></table>']
    for a in articles:
        main.append(f'<h2 id="{a["id"]}">{esc(a["title"])} <span class="addr">docs/{esc(a["path"])}</span></h2>')
        main.append(re.sub(r"<h1[^>]*>.*?</h1>", "", a["html"], count=1, flags=re.S))
    main.append('<h2 id="rpcs">RPC reference</h2>')
    main.extend(render_rpc(r) for r in rpcs)

    return f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>SkySaga Reference</title><style>{CSS}</style></head>
<body><nav id="side">{''.join(side)}</nav><main id="main">{''.join(main)}</main><script>{JS}</script></body></html>
"""


def main():
    articles = load_articles()
    rpcs = build_rpcs()
    OUT.write_text(render(articles, rpcs), encoding="utf-8")
    print(f"wrote {OUT}: {len(articles)} articles, {len(rpcs)} RPCs")


if __name__ == "__main__":
    main()
