# Title-Side Achievement / Event / Stat Mining from Extracted Xbox One Game Packages

Plaintext-asset-first methodology for mapping achievement, telemetry, and live-event surfaces across a 46-title corpus of owned, self-extracted game packages. Analysis-only: this documents what the evidence says the mechanism is.

## Summary

Over three sessions (2026-10-03 → 2026-10-22) we statically mined 46 dumped Xbox One titles plus three deep dives (a CryEngine shooter, a shared-engine loot shooter, a first-gen racing title, a live-service MMO, and a GOG PC build used as a negative control) to answer one question per title: **where does the achievement/event/stat pipeline live — in the package, in the OS service, or server-side only?**

Headline results:

- **A three-era taxonomy emerged from the corpus itself** (the campaign's main analytical result): XDK-era titles (2013–2018) carry ETW/ETX telemetry with XDP schema tags and achievements via the `Windows.Xbox.Achievements` OS service or XSAPI; transitional titles (2019–2021) use XSAPI REST/RTA and increasingly ship real in-exe event tables (one title carried 152 named gameplay events, another 21 named achievement handlers); native GDK titles (2021+) use the `XblEventsWriteInGameEvent` family. **Zero titles in the corpus shipped an in-game OneCollector pipeline** — the OneCollector transport work in the broader ecosystem map is validated by OS-side captures and cross-platform absences, not by the games themselves.
- **Plaintext assets decide most titles.** SQLite game databases, XML tuning configs inside ordinary zip containers, and plain string/RTTI scans of the binaries resolved the achievement surface for the overwhelming majority of the 46 titles. Decompilation was needed only twice in the whole campaign, and only for condition *classification* — never as the primary tool.
- **The binding conclusion (deepest dataset, one MMO):** the event-name → achievement binding lives **only in the title's service config**, never in any client artifact. The client ships a stat string-pool used for logging/lookup; hundreds of thousands of scanned package records produced zero additional stat strings. Public service-side achievement data confirmed this independently: requirements arrive as opaque single rules with no stat or event names.
- **Negative control worked as designed:** a PC (non-Store) build of a cross-platform action title shipped a Steamworks-only achievement layer — a full miner sweep and raw ASCII/UTF-16 scans found zero Xbox/XBL surface anywhere in the install, and the only "Xbox" byte sequences were shared-engine build labels. Confirms the methodology detects absence, not just presence.

## Environment (generic)

- An Xbox One console with owned discs / digital licenses, used only for extraction of owned content; extracted file trees land on a PC for analysis.
- A PC-side offline analysis bench: Python 3 tooling, `7z`/`unrar`, `zip`/SQLite tooling, and Ghidra (headless analyzer) for the occasional binary pass.
- A generic "bucketed strings miner" script: scans a title's binaries for ASCII/UTF-16 patterns bucketed into telemetry families (ETX/ETW pipeline markers, XDP schema tags, XSAPI/REST endpoint hints, RTTI, OneCollector, envelope shapes, session-lifecycle strings) and emits one JSON finding file per title.
- Public third-party sources used only for correlation of *published* achievement names/descriptions against binary-internal identifiers (a fan achievement-listing site; a public achievement service queried read-only for per-title achievement records).
- No network interaction with the titles' game servers beyond read-only service queries; no unlock tooling is documented here.

## Methodology — the asset-first funnel

The organizing rule: **exhaust plaintext and data-file surfaces before touching a decompiler.** Each stage is orders of magnitude cheaper than the next, and the next stage is only justified when the previous one provably lacks the conditions.

**Stage 1 — data files (SQLite, XML-in-zip, config trees).** Many Xbox-era engines ship their game DB as a plain SQLite file or XML tuning configs inside ordinary zip archives. Query these for achievement/event/stat tables, condition identifiers, and tuning knobs before scanning anything. A surprising fraction of titles resolve their entire event vocabulary here.

**Stage 2 — strings / RTTI mining.** Run the bucketed miner over the title's main binaries. RTTI type descriptors (`.?AV...` mangled names) enumerate the client's own class vocabulary — achievement managers, stat collectors, event dispatchers — even when symbols are stripped. Format strings (`ACHIEVEMENT_%02d`-style) reveal that identifiers exist without revealing them; named handler families (`Achievement_*`, `EVENT_*`, `eA_*`) reveal the actual vocabulary. Cross-tabulate symbol counts against publicly published achievement counts: an exact match (e.g. 74 vs 74) is strong evidence the in-binary set is complete.

**Stage 3 — archive/container parsing, only as needed.** Proprietary archives (custom compressed containers) get a quick magic-header sniff; if the layout is simple, derive the decompressor empirically. But track the cost honestly: on one title, ~120 of 810 blocks in one archive decoded against an inferred layout before per-block size semantics diverged — the correct call was "use the community GUI extractor if archive mining is ever needed again," not a week of container RE.

**Stage 4 — Ghidra headless, decompiler-last.** Only where data files lack the conditions: e.g. mapping in-binary achievement symbols to the engine's grant call path, or classifying per-stat code constructs. Even then, results are recorded as *structural findings* (which function family handles which concern), with any derived code artifacts kept out of the repo.

**Stage 5 — service-side cross-reference (read-only).** Pull the title's public achievement records (names, gamerscore, descriptions, requirement shapes) and join against binary-internal identifiers. Two independent confirmations both pointed the same way: requirements are opaque single-rule rows with no stat names; the binding is server-side.

A key funnel discipline: **post-patch re-scan to test the "data ships later" hypothesis.** One live-service client's static install yielded byte-identical stub event-framework configs after a full patch, with no per-event definitions and no `defs` tree anywhere in 16 GB — proving per-event definitions are delivered at runtime by the shard/account servers, not shipped. That single negative result redirected the whole capture plan away from static mining toward runtime observation.

## Campaign orchestration (46 titles)

Per-title loop, run in batch:

1. **Source the package** from the owned-media extraction pipeline (console-side extraction, or a previously validated archive of decrypted dumps). Extraction hygiene: download → extract → delete.
2. **Extract only Mount binaries + manifests** — never the full payload into the working tree.
3. **Mine** with the bucketed strings miner; emit `data/title-events/<slug>.json` (62 finding files over the campaign).
4. **Write a findings section** per title in the reference docs; correlate against published achievement lists where useful.
5. **Ledger everything** in a CSV: per-title status, one-word BLOCKED reason where applicable (e.g. main executable absent from the head dump; full archive too large for available disk).

Ledger result: **46 titles DONE, ~10 BLOCKED** with reasons recorded. Batch findings were committed in numbered reference documents so the campaign stayed auditable title-by-title rather than as one giant blob.

Two deep dives layered on top of the batch:

- **The MMO (end-to-end):** package identity captured from both binary and service; a kernel file-read filter on the Store install blocks reading the main exe, bypassed *analysis-only* by launching the game and carving the mapped image from a live process dump (a valid 63 MB PE) — method documented, artifact kept out of the repo. Miner found the native GDK event write entry point and a 1157-name in-exe event family; all public achievement records pulled read-only with continuation paging; hundreds of thousands of package records scanned, yielding zero additional stat strings — which is what established the service-side-binding conclusion.
- **The CryEngine shooter:** exe carries exactly the published achievement symbol count; grant path flows through engine flow-node hooks; no in-process achievements class → era-typical cloud/stats pipeline plus a proprietary telemetry collector. The write-call mapping was flagged as a Ghidra pass and left at the structural level.

## What worked

- **Bucketed strings mining as the single batch tool.** One generic script, one JSON per title, comparable findings across 46 unrelated engines. The per-bucket counts alone (etx/etw vs xdp vs xsapi vs playfab vs onecollector) were enough to place every title in the taxonomy.
- **RTTI when symbols are gone.** Mangled type descriptors gave complete achievement-manager and stats-pipeline class inventories on stripped binaries, including a Steamworks-only callback family that conclusively proved an Xbox layer's absence.
- **SQLite/zip-embedded game DBs.** Fastest possible stage-1 wins; several titles' full event vocabularies came from queries, not scans.
- **Count cross-checks.** In-binary symbol count vs published achievement count is a cheap, strong completeness test.
- **Negative results recorded as results.** Zero-OneCollector across 46 titles; zero XBL surface in the PC control build; zero stat strings in the scanned record classes; post-patch stub configs. These absences are the load-bearing findings.
- **Runtime-image carving to defeat read-protection of files we are entitled to read.** Launch-and-dump turned an "unreadable exe" into a normal static-analysis input.
- **Sub-classifying stat names by shape.** In the MMO's stat pool, display-style stat names and trigger-style stat names coexist as distinct members of name pairs — a structural observation that explains which member of a pair a service-side rule actually keys on, without needing any token-level procedures.

## What did not (exact errors)

- **`JsonReaderException: Unexpected end of content ... Path 'data', line 62`** — a templated JSON body failed parse. Root cause was a truncated template file ending mid-object with no closing brace: raw string substitution + strict parse means a missing final brace fails at exactly this error even when the other inputs are well-formed. Lesson: template tails matter; validate substituted output, not just the source template.
- **`POST .../update` → service code 49, "None of the submitted achievements may be updated in this fashion"** — definitive evidence that a subset of achievements is event-based rather than title-managed; the REST update route is categorically unavailable for that class. Recorded as a mechanism finding, not worked around.
- **`GET /achievements/{scid}` → HTTP 405** for both unsigned and signed requests, while the per-user `?titleId=` route works unsigned. Endpoint-shape quirk worth knowing; the path form is simply not offered.
- **Container parsing dead-end:** custom compressed archive blocks whose per-block compressed-size semantics did not match the inferred layout (many blocks failed against the C# reference rule). Full-archive extraction abandoned at ~30% decode on one file; correct outcome was to prefer existing GUI extractors over re-derivation.
- **Blocked-by-absence:** one title's head dump lacked its main release executable entirely (mechanism could not be inspected; identity + mechanism expectation recorded only). Another title's full archive (~42 GB encrypted package) exceeded available disk (~30 GB) and was not fetched.
- **OneCollector regex false positives:** 19 hits that were substring collisions on a short key name; cured by tightening the matcher. Batch tooling needs per-bucket precision checks, not just recall.

## Ethics / scope note (analysis-only, owned media)

- Every package analyzed came from media and entitlements we own: self-extracted from our own consoles and licenses, or from previously obtained decrypted dumps of titles we own. No DRM circumvention is offered here; where a read-protection filter was bypassed for analysis, it was by reading the memory image of a process we launched from software we licensed — and only to perform the static analysis this document describes.
- **This writeup is analysis-only.** It documents what the evidence says the mechanism is. It is explicitly **not unlock help**: no condition tables, no token-check sequences, no test ladders, no procedure to satisfy any achievement or event rule. Example findings are described structurally (e.g. "condition IDs mapped to string literals"; "display vs trigger stat-name pairs exist as distinct pool members") precisely so the mechanism is understandable without being operational.
- Third-party identities, console identifiers, device identifiers, serials, and account material are scrubbed throughout. Raw binary payload and decompilation artifacts are excluded from this repo by policy; derived, authored findings and tooling are what get committed.
- No game content is redistributed. Where community tooling existed for container extraction, it is acknowledged as upstream tooling, not reimplemented output.

## Next steps

1. **Runtime observation for server-authoritative titles.** For the live-service client whose event definitions are pushed at runtime: memory-carve the delivered definitions after they cross the parse point, diff across sessions/zones, and (if needed) place an instrumentation breakpoint at the wire→structure parse function identified by assert-string cross-reference. Zone-triggered on-demand content is the third capture surface.
2. **Finish the Ghidra-flagged structural mapping** for the CryEngine shooter's achievement write path (function-family level only; stays structural).
3. **Batch classifier v2.** The 62 per-title JSONs are now enough training data to auto-classify era + pipeline from miner output alone; make the taxonomy assignment programmatic and re-run over any newly acquired title.
4. **Close the ledger.** The ~10 BLOCKED titles each have a single documented reason; two are unblocked simply by disk space or a re-extraction of the correct executable.
5. **Publish the miner.** The bucketed strings miner is the reusable artifact of this campaign; generalize the bucket definitions (per-era signature sets) and commit it with the taxonomy doc as the methodology reference.
