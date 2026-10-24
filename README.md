# xbone-deep-analysis

Public-facing writeup of Xbox-era game-preservation research conducted on
consoles, discs, and licenses we own. Private research; nothing here is
distribution tooling. The goal of this repository is to document **what we
tried, what worked, what did not, and why** — one technique per folder under
`techniques/`, each with a README describing the full chain.

## Disclaimers

- **Ownership.** All research described here was performed on consoles,
  discs, and licenses the researcher personally owns. Nothing was acquired,
  distributed, or shared; no copyrighted content — game packages, binaries,
  assets, keys, certificates, or dumps — is included in or hosted by this
  repository. The repo documents *methodology and findings*, never
  artifacts.
- **Reverse engineering.** Everything here is independent interoperability
  and preservation research: static analysis, protocol observation on
  networks we control, and analysis of software and media we legitimately
  possess. It is not affiliated with, endorsed by, or connected to Microsoft
  or any game publisher or platform holder.
- **Not a piracy guide.** This is not a guide to obtaining games you do not
  own. Techniques that touch licensing or package decryption are documented
  because they are the preservation path for software that is otherwise
  unrecoverable when storefronts and servers shut down. Every acquisition
  step described was performed against titles the researcher owns.
- **Not a cheating guide.** Achievement, event, and stats research is
  strictly **analysis-only**: it documents what the evidence says a
  mechanism *is*. It contains no unlock procedures, no condition tables, no
  token checks to run, and nothing operational for manipulating a live
  service — and we ask that it not be used that way.
- **Not a warranty.** Everything here is "as documented on our hardware on
  the dates noted." Builds, endpoints, and service behaviors change; error
  codes carry confidence labels for a reason. Console updates are
  irreversible; do not experiment on hardware you cannot afford to lose.
- **Trademarks.** Xbox and product/game names are trademarks of their
  respective owners and are used here solely for identification.
- **AI assistance.** This repository was researched and written with the
  assistance of an AI agent (Hermes Agent by Nous Research, running a GLM
  Flash model from Z.ai) under direct human direction. All experiments,
  console operations, and findings are the researcher's own; the agent
  assisted with documentation, tooling, and analysis, and every published
  word passed human review.

## Consoles used

All consoles are retail units we own, referred to by role throughout this
repo. No serials or identifiers — the build numbers are the published data
points that matter for reproducing the findings.

| Role | Device class | Notable OS build | Posture |
|---|---|---|---|
| Console A | Xbox One-family retail | 10.0.25398.4908 (May-2024 train, `xb_flt_2405`) | **Frozen by design** — the published game-script entry chain is patched one build later (.4910); it never updates and its entry app + cached license are the primary anchor |
| Console B | Xbox One-family retail (Scorpio-class) | 10.0.22000.4158 (Apr-2022 train) → 10.0.26100.8866 (Jul-2026 train) via offline USB OSU hop ladder | The OSU ladder subject; deliberately kept at the last build where the documented SystemOS privilege-escalation chain is known to work (26100.9438-class is believed past it, unverified) |
| Console C | Xbox One-family retail | 10.0.19041.1927 (`rs_xbox_release_2004`, Apr-2020 train) | The bench console: past first-run setup with a working dashboard, far below the entry-chain patch line — used to iterate the licensing/dump flow without risking Console A's state |
| Console D | Xbox Series X retail | 10.0.26100.9445 (self-reported via update check), fully updated | The only console **permitted to update** — its forged-chain interception window closes by design when it does. First console in the lab with the complete user plane captured through the rig, and the console where the disc-to-digital claim flow was recorded end-to-end |

Build numbers are stated because they are experimental variables, not
identifiers; everything else about the units (serials, IDs, network
addresses) is excluded per `PUBLISH-SCOPE.md`.

## Ground rules (enforced, see `PUBLISH-SCOPE.md`)

- No serial numbers, device IDs, account identifiers, usernames, or emails.
  Device identifiers are replaced with `X`-filled strings of the same length.
- Devices are discussed generically (e.g. "Console A, an Xbox One X-class
  dev-adjacent retail console").
- No binaries, keys, certificates, dumps, capture files, or copyrighted
  content. Methodology only — the repo shows *how* artifacts were produced,
  never the artifacts.
- Network-capture files are never checked in; `samples/` folders hold only
  hand-redacted text excerpts showing protocol shape.
- Code here is MIT-licensed (`LICENSE-CODE`); prose is CC-BY-4.0
  (`LICENSE-DOCS`).

## Layout

- `techniques/<name>/` — one technique per folder: goal, chain, environment,
  what worked, what did not, failure codes, what we would try next.
- `tools/` — reusable source-only tooling (build it yourself).
- `patches/` — patches against third-party upstreams, with apply instructions.
- `references/` — methodology-only reference documents.
- `scripts/` — the redaction/scrub pipeline used to prepare this repo
  (`scrub_check.py`), so the transform itself is auditable.

## Techniques index

- `techniques/disc-license-models/` — disc vs digital license models: volume-bound disc licenses, the missing device license, the one-bay problem.
- `techniques/cik-extraction/` — per-title content-key extraction with CikExtractor: loader root cause, clip license format decode, the provisioning gap.
- `techniques/update-check-mitm/` — update-check/licensing interception: full protocol capture, spoofed gate, TLS trust break at the edge, and the relay/signature rule.
- `techniques/osu-offline-updates/` — offline USB OSU as the sanctioned build-movement mechanism: E101 decode, hop ladders, the consoles.txt build oracle.
- `techniques/eraos-code-exec/` — retail SystemOS code-exec methodology via the published entry chain: shell survival, the EraOS VM boundary, safety model.
- `techniques/title-event-mining/` — achievement/event/stat mining across a 46-title corpus: the asset-first funnel, analysis-only framing.
- `techniques/xvc-pc-acquisition/` — PC-side acquisition of owned-title XVC packages: store -> CDN -> CIK, and the licensing dead ends.
- `techniques/package-identity-fuzzing/` — package-identity fuzzing in the Microsoft publisher namespace.
- `tools/mitm-listener/` — the Kestrel HTTP/1.1+h2 listener: file-driven gate responses, transparent DoH-resolved passthrough, capture logging.
- `patches/` — format-patch exports of our local changes against the CikExtractor and xal-rs upstreams, with apply instructions.
- `techniques/disc-to-digital-digitize/` — the disc-to-digital claim flow: BCA-anchored, captured end-to-end on the wire.
- `techniques/devmode-secondary-entry/` — secondary console entry path via retail Developer Mode + CVE-2026-62735: feasibility and risk posture, no reproduction recipe.
- `techniques/onecollector-event-path/` — the OneCollector telemetry/event transport and the tooling ecosystem around it (analysis-only).
- `techniques/eraos-mount-probe/` — mapping the EraOS/GameCore content-mount surface from the shell: what validates, what doesn't.
- `techniques/goldeneye-mount-mining/` — the cancelled GoldenEye 007 remaster found in a collection package: structure-level mining and preservation value.
- `references/console-b-osu-ladder-links.md` — the worked OSU hop-ladder link snapshot (link-rot warning included).
- `references/console-dns-allowlists.md` — the per-console DNS/host-list postures and their failure modes.
- `references/quantumtunnelex-analysis.md` — CVE-2026-62735 analysis: build matrix, retail/devkit split, open questions.
- `references/emudhra-mitm-chain.md` — how the interception rig is built and operated, with its safety rules.
- `tools/ghidra-passes/` — generic Ghidra headless pass scripts (full sweep, anchor-string diff) + methodology pointer.
- `references/ghidra-headless-methodology.md` — the decompiler-last pass pattern and its lessons.
- `references/llm-decompilation-models.md` — survey of decompilation-focused LLMs and the practical refinement recipe.
- `references/error-code-index.md` — every Xbox-side error code from the research, with confidence tiers.
- `references/relay-splice-rule.md` — the standing XBL-Signed-endpoint splice rule.
