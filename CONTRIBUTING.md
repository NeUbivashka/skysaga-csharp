# Contributing

Thanks for helping bring SkySaga back.

## Workflow

1. Fork the repository and create a branch: `feat/<scope>` or `fix/<scope>`.
2. Make your change. Keep pull requests small and focused on one thing.
3. Make sure it builds: `dotnet build Servers/Beta1/Servers.sln` (and `Servers/Alpha10` if you touched it).
4. Open a pull request against `main` and fill in the template. A maintainer (see `.github/CODEOWNERS`) reviews and merges.

For bigger changes (new subsystems, refactors, protocol changes) open an issue or discussion first.

## Ground rules

- **This is a fork of [EDITzDev/SkySaga](https://github.com/EDITzDev/SkySaga).** Keep the original structure; changes should be additive or fixes.
- **The wire protocol is king.** Packet ordinals, bit layouts and field order must match what the real client expects. Do not change a packet format without verifying it against the client, and say in the PR which client build you tested with.
- `Servers/Alpha10` is frozen — new work goes to `Servers/Beta1` unless the change is Alpha-specific.
- Do not commit build output (`bin/`, `obj/`), archives (`.zip`, `.rar`, `.7z`), certificates/keys, secrets, or personal data (real IPs, account files).
- Do not commit proprietary game files beyond what is already in `Bundled/` (no client executables, `.pak`/`.bnk` assets, etc.).
- Write clear commit messages in English.

## Commit email

Use your GitHub noreply address so your email is not published:
`git config user.email <id>+<login>@users.noreply.github.com` (find it in GitHub → Settings → Emails).
