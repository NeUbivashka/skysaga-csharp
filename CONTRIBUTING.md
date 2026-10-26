# Contributing

Thanks for helping bring SkySaga back. This page says what we need from you so that a change can
be reviewed and merged, and so that the knowledge behind it is not lost.

## What we work on

- The target is the **Alpha 10 client, build 36731** (alpha 10.6.6). All work goes to
  `Servers/Alpha10`. `Servers/Beta1` (38328) is no longer maintained; do not send changes for it.
- The server must behave the way the real 36731 client expects. The client binary is the
  reference, not other emulators, old forks or guesses.
- Game data comes from the client's own data (`Bundled/36731`, GeoData). Tables in
  `Servers/Alpha10/SkySaga.Game/Data/` are generated from it by the scripts in `Tools/`.

## Ground rules

1. **Verify against the client.** Packet ordinals, bit widths, encodings and field order must be
   checked against client 36731: its code (Ghidra / disassembly), its live memory, or captured
   packets. Encodings inherited from upstream [EDITzDev/SkySaga](https://github.com/EDITzDev/SkySaga)
   were written for an earlier build (Alpha 3); treat them as unverified until you check them.
2. **No invented values.** Every constant (speed, range, damage, timer, limit, id) must come from
   the client code or the game data. If the client does not give a value and the server must still
   pick one, say so in the PR and in the wiki as an emulator choice.
3. **Fix the cause, not the symptom.** No workarounds: no re-sending packets "to refresh" the
   client, no toggling state to hide a glitch, no hard-coded exceptions for one map or one item.
   If the client misbehaves, find what it expects.
4. **No comments in the code.** The reasoning, wire formats and client addresses go to the
   [wiki](../../wiki). Code should read on its own: clear names, small methods.
5. **Match the surrounding code**: naming, file layout, idioms. One packet per file in `Packets/`,
   one sync component per file in `Components/`.
6. **Everything in English**: code, commit messages, wiki pages, issues and PRs.
7. **Keep the fork's structure.** Changes are additive or fixes.

## Documentation is part of the change

A change that adds or corrects protocol or game behaviour is not finished until the wiki
describes it. Write it so that someone could **implement the feature from the wiki alone**,
without reading our code. A field table on its own is not enough. Cover:

- **Trigger**: what the player does and what the client does, with the client functions
  (`FUN_00XXXXXX`), UI event or action names, and data names.
- **Wire layout**: every field in order, its width in bits, its encoding (full-width u32, ranged
  max M, ranged index, offset and scale), and where its value comes from.
- **An example**: real bytes from a capture, or bytes computed from the layout, labelled as such.
- **Server behaviour**: what the server must check, change and send back (RPCs, synced component
  fields, entity add or remove), in which order, and what the client does with it.
- **Limits and edge cases**: clamps, maximum counts, what happens on bad input.
- **Confidence**: mark clearly what is confirmed in the client, what was only observed, and what is
  an emulator choice or still unknown.

Wiki rules:

- Only confirmed facts are stated as facts. Interpretations are labelled.
- Every documented RPC gets a link in its row of [RPC Ordinals](../../wiki/RPC-Ordinals).
- Addresses are for `SkySagaU.exe` build 36731.

If you cannot edit the wiki, put the text in the PR description and a maintainer will add it.

## Workflow

1. For a new subsystem, a refactor or a protocol change, open an issue first
   (**Feature / research task**) so that work is not duplicated.
2. Fork the repository and create a branch: `feat/<scope>` or `fix/<scope>`.
3. Keep a pull request small and about one thing.
4. Build: `dotnet build Servers/Alpha10/Servers.sln`.
5. Test with the real 36731 client and describe what you did and saw.
6. Open a pull request against `main` and fill in the template. A maintainer (see
   `.github/CODEOWNERS`) reviews and merges.

## What a pull request must contain

- **What and why**: the behaviour before and after.
- **Evidence**: client functions and addresses you read, captured bytes, data fields used. For a
  packet change, the bit layout and how you confirmed it.
- **How you tested**: client build, map, steps, and the result (server log lines, screenshot).
- **Wiki**: the link to the updated page, or the text to add.
- **Open questions**: anything you assumed or could not confirm.

## Reporting a bug

Use the **Bug report** template and include:

- client build (36731) and server commit;
- the map and the exact steps to reproduce, expected and actual result;
- the game server console output around the problem;
- for a client crash: the error text and the minidump from `%LOCALAPPDATA%\CrashDumps`
  (`SkySaga*.dmp`), or at least the crash address;
- remove IP addresses, account names and passwords before posting.

## Do not commit

- Build output (`bin/`, `obj/`), archives (`.zip`, `.rar`, `.7z`), certificates, keys, secrets.
- Personal data: real IP addresses, account files, logs with account names.
- Proprietary game files beyond what is already in `Bundled/` (no client executables, `.pak` or
  `.bnk` assets).

## Commits

- Clear messages in English: what changed, in the imperative or as a short statement.
- Use your GitHub noreply address so your email is not published:
  `git config user.email <id>+<login>@users.noreply.github.com` (GitHub → Settings → Emails).
