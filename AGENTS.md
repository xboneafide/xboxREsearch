# AGENTS.md — xbone-deep-analysis

## What this repo is

The **public-facing** writeup of Xbox game-preservation research. It is the
clean twin of the private research repos (the private research repo is the
single source of truth for the research itself; its location is private
infrastructure and is never named in public documents). Every
technique tried gets
its own folder with a README describing the full chain — what worked, what
did not (with exact error codes), and what we would try next. Negative
results are the point; keep them.

This repo is designed to be safe if it leaks. Everything here is
methodology, findings, and source code — never artifacts.

## Hard rules

1. **The scrub gate is law.** `scripts/scrub_check.py` runs as a pre-commit
   hook (`.githooks/pre-commit`, `core.hooksPath` is set). If it fails, fix
   the file (redact with same-length `X` fill + a `# redacted` marker) or
   extend `scripts/scrub_allowlist.txt` — only for *format constants and
   public upstream identifiers*, never for real device/account data. Never
   weaken the gate or commit with `--no-verify`.
2. **Never check in artifacts.** No registry hives, flash dumps, license
   blobs, certificates/keys, token files, capture logs (pcap/pcapng/etl/
   capture.log), game packages/binaries, or decompilation dumps of
   copyrighted binaries. The `.gitignore` blocks them — do not weaken it or
   force-add. `samples/` folders may hold short hand-redacted text excerpts
   showing protocol shape only (all values X-filled), never replayable data.
3. **Never check in identities.** No real usernames, emails, gamertags,
   handles, device IDs, serials, console IDs, or account data — ours or
   anyone else's. Devices are described by role + class only ("Console A, an
   Xbox One-class retail console frozen at 10.0.25398.4908" — build numbers
   are fine, they are not identifiers). Third-party community members are
   never named; public open-source projects are (GitHub links to public
   upstreams are fine).
4. **Never push without being asked.** The remote is our self-hosted Git
   server (its address is private infrastructure — never name it in public
   documents). Commit early and often locally; the human pushes.
5. **Never push to GitHub unprompted.** Public mirroring to GitHub happens
   only as an explicit, user-directed step, and only via the sanitized
   single-commit publish flow (fresh stage, fresh commit authored as the
   public identity — never a mirror of this repo's history, whose commit
   metadata carries the private identity).
6. **Nothing is copied wholesale from private repos.** Every document here
   is freshly written prose derived from the private sources. Private repo
   paths must not appear in committed files (write "private analysis notes"
   instead).
7. **Do not weaken the safety framing.** Techniques touching update surfaces
   must state the offline-USB-OSU-only posture; achievement/event work is
   analysis-only (mechanism, not unlock help); exploit material is limited
   to what the public record already publishes, framed for preservation on
   owned hardware.

## Layout

```
techniques/<name>/   one technique per folder: Summary, Environment (generic),
                     Chain, What worked, What did not (exact errors), Next steps
tools/               source-only tooling we wrote (build instructions included)
patches/             git format-patch exports vs third-party upstreams,
                     with BASE-SHA provenance and apply commands (see patches/README.md)
references/          methodology-only reference docs (error-code index, MITM chain
                     build, DNS postures, vulnerability analyses)
scripts/             the scrub gate itself
```

## Conventions

- **Technique README structure**: Title → Summary (3–5 sentences) →
  Environment (generic devices only) → Chain (numbered, as actually
  performed) → What worked → What did not (exact error codes) → Open
  questions / next steps. Professional, plain, no hype.
- **Error codes** are factual data points and always included; confidence
  tiers (verified / community-corroborated / inferred) are defined in
  `references/error-code-index.md`.
- **Cross-link** between folders (relative links, e.g.
  `../../techniques/update-check-mitm/`). Update the Techniques index in the
  root `README.md` when a folder lands.
- **Commits**: one folder/topic per commit, descriptive message. The gate
  runs on every commit.
- **Licensing**: code is MIT (`LICENSE-CODE`), prose is CC-BY-4.0
  (`LICENSE-DOCS`).

## Source of truth

The private research repo's master guide (its docs are the
authoritative record of the research). When this repo and that guide
disagree, the private guide wins —
fix this repo. Do not modify the private guide from here without being asked.
