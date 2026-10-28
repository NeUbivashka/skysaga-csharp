---
name: skysaga-wiki-writing
description: Write or update pages of the SkySaga emulator wiki (GitHub wiki of skysaga-csharp, local clone skysaga-wiki) after reversing or implementing something for the Alpha 10 / 36731 client. Use whenever an RPC, sync field, HTTP endpoint, GeoData table or game rule has been worked out, and before committing server changes that depend on it.
---

# Writing the SkySaga wiki

The wiki is the project's memory. Server code has no comments, so everything learned about the
client lives here. The test for a page: **someone who has only the wiki and the client data can
implement the feature without opening Ghidra again.**

The reference page for depth and structure is `Creature-AI.md`. New pages and sections should be
as detailed: every field, every constant, where it comes from, what the client does with it,
what the emulator does and what is still unknown.

## Where things go

The wiki is a separate git repository (GitHub `<repo>.wiki`); a local clone sits next to the
server repo as `skysaga-wiki`. Pages are flat Markdown files; `_Sidebar.md` lists them by group.

| Content | Page |
|---|---|
| Bit layout of packets, ids, primitives | `Packet-Format` |
| Handshake and RPCs 0–13, 73 | `Connection` |
| Every RPC: ordinal, wire id, name, link to its description | `RPC-Ordinals` |
| Payloads of RPCs the client sends | `Client-RPCs`, or the feature page |
| Payloads of RPCs the server sends | `Server-RPCs`, or the feature page |
| Entity components and their sync fields | `Entity-Sync` |
| HTTP API the client calls | `Authentication`, `Worlds-and-Travel`, `Social-Graph`, `Photos` |
| A game system (data, client behaviour, emulator rules) | its own page: `Creature-AI`, `Crafting`, `Jobs-and-XP`, `Health-and-Death`, `Perks`… |

A new page needs a line in `_Sidebar.md` and, when it fits, in `Home.md`.

### RPC-Ordinals rule

Every RPC that is documented anywhere gets its row in `RPC-Ordinals.md` turned into a link to that
description:

```markdown
| 90 | `E0` | [RPCAllowFriendToEditHomeIsland](Client-RPCs#90-allowfriendtoedithomeisland) |
```

RPCs that exist only as names, have dead senders or no client handler are listed as such too, so
nobody reverses them again.

Links use GitHub wiki anchors: the heading lower-cased, spaces to `-`, punctuation dropped
(`## 77 EntityPerformedAbility` → `#77-entityperformedability`, `## MapDefinition (8)` →
`#mapdefinition-8`).

## Page structure

Start with the build and a one-paragraph summary of what the page covers. Then, per feature, use
the same three parts that Creature-AI uses:

1. **Data.** The GeoData / Entities tables and fields involved, with real values from 36731 in a
   table (`| Item | Where | Value in 36731 |`). Name the generator and the server table it ends
   up in (`Tools/maps/make_creature_ai.py` → `Data/CreatureAi.txt`).
2. **In the client.** What the client sends or reads, field by field, and what it does with each
   value. Function addresses go here (`FUN_00A53410`, `0x01404C90`) so the next person can
   re-check them.
3. **In the emulator.** What the server does, as numbered rules. Any rule not taken from the
   client or the data is an **emulator choice** and says so ("How the original server timed the
   block is not known. The emulator's rules:").

A "Status in the emulator" section at the top (what works, what was tested in game and how) and a
"Not done yet" list at the bottom keep the page honest.

### RPC descriptions

One heading per RPC, `## <ordinal> <Name>` (or `### …` on a feature page), with the wire id.

- **Direction and when.** Who sends it, the caller chain, the trigger (button, input, timer).
- **Field table.** The payload as a table: order, field, bits, encoding, meaning.

  ```markdown
  | Field | Bits | Encoding |
  |---|---:|---|
  | friend | 1 + 128 | optional UUID (`FUN_007B0D70`) |
  | allow | 1 | bit |
  ```

- **Handling.** What the receiving side does with each field: handler function, side effects, UI.
- **Emulator.** How the server handles or builds it: class name, when it is sent, which choices
  are its own.
- **Unknowns.** What is not known yet, and what was tried. A failed attempt or a crash is also
  worth a line (for example "event 0x0B on a creature crashed the client").

### HTTP endpoints

Give the method, the URL format string as found in the client, the request body keys and the
response keys the client actually reads, with the JSON types it checks. Write down which
envelope works (`result` object or bare body) and which error codes the client shows.

## Style

- **Plain technical English.** Short sentences and a neutral tone. Use present tense for how the
  client works.
- **Tables for anything with more than two items** of the same shape: fields, values, mappings.
- **Real values only.** Numbers come from the client or GeoData, with their source. Never round
  a constant without saying so.
- **Tested.** Say "tested in game" only after the player has checked it, and say what was seen.
  Before that: "not yet checked in game".
- **Facts, not history.** No "I found", no dates, no commit hashes in running text. Exception: a
  short note on what the emulator used to do wrong, when it explains an old bug report.
- **Secrets.** Never paste passwords, tokens or personal data, not even from logs.

## Checklist

- [ ] Every field documented with bits, encoding and meaning.
- [ ] Client function addresses for the reader/writer and the handler.
- [ ] Emulator choices marked as such.
- [ ] RPC-Ordinals row linked.
- [ ] Links between pages (feature page ↔ Client-RPCs/Server-RPCs ↔ Entity-Sync).
- [ ] Sidebar updated for a new page.
- [ ] "Not done yet" updated.
