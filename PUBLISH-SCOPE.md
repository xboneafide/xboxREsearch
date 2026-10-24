# Publish scope: what is included, what is excluded, and why

This repository is a cleaned, public-facing record of Xbox game-preservation
research. The rules below are enforced by `scripts/scrub_check.py` and by a
manual review pass before every commit. When in doubt, content stays out.

## Included

- **Methodology.** How each technique works end-to-end: the chain of steps,
  the tools involved, the protocol shapes observed, and the reasoning.
- **Findings and negative results.** What worked, what did not, and the exact
  error codes / behaviors observed. Negative results are most of the value.
- **Source code we wrote ourselves**, scrubbed of identifiers.
- **Patches** against third-party open-source tools we modified, with apply
  instructions.
- **Device context at a generic level.** Consoles are referred to by role
  (Console A, Console B) and class (console family, OS build family).
  Permitted example: "Console A is an Xbox One S-class retail console frozen
  at OS build 10.0.25398.4908." Forbidden: serials, console IDs, MACs.

## Excluded — never staged, never linked

- **Device identifiers.** Serial numbers, console IDs, device IDs, account
  IDs, machine GUIDs, MAC addresses, certificate serials, and any
  account-bound token. Redacted to `X`-filled strings of the same length as
  the original (e.g. `F4:1C:...` becomes `XX:XX:...`).
- **Personal identity.** Any real username, email, gamertag, or handle — ours
  or anyone else's. Third-party identification work is out of scope entirely.
- **Artifacts.** Registry hive dumps, flash dumps, license blobs, certificate
  files and keys, game packages and payloads, extracted copyrighted binaries
  or decompilation dumps. The repo may explain *how* such artifacts are
  produced (for example, how the eMudhra-based MITM chain was built) but never
  hosts them.
- **Network captures.** No capture files are checked in.
  `samples/` folders may contain short, hand-redacted text excerpts that show
  request/response *structure* — with all identifier and token values
  replaced — and nothing replayable.
- **Piracy-adjacent links.** No links to torrent trackers, magnet links,
  Internet Archive / Wayback Machine copies, or any other mirror or
  rehosting site. Package and document references point to official vendor
  sources and public community wikis only.

## Enforcement

- `scripts/scrub_check.py` runs over staged files before every commit: it
  blocks the artifact extensions/patterns above and flags identifier-shaped
  strings (GUIDs, hex token blobs, long digit strings) that lack a
  corresponding redaction marker.
- Everything in this repo passes through the scrub transform; nothing is
  copied wholesale from the private research repos.
