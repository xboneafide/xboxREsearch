# Mount-Tree Mining of a Collection-Package Title: A Pre-Release Build of the GoldenEye 007 Remaster Found in a Collection-Package Dump

## Summary

A package-identity scan across an owned collection-package dump surfaced a
Microsoft-publisher package whose family name (`Microsoft.ProjectDiamond_...`)
does not match the collection shell it shipped alongside. Static mining of its
`Mount/` tree showed the package is not the Rare Replay collection shell at
all: it is a complete standalone **GoldenEye 007 (N64) remaster** package —
host executable shell, game plugin DLL, separate N64 emulator plugin
("Pearl"), a single N64 rom container image, a stat-ID table, and a
registry-hive game config blob.

The identity finding sharpened after the structural work: the package's
store ID matches the **released** GoldenEye 007 remaster (the lineage of
the cancelled 2008-era XBLA project, finally shipped by Microsoft in
January 2023; store listing: xbox.com/en-US/games/store/goldeneye-007/9n6639h7vgh4),
while the build itself is version 1.4.38647.0 — a
**2021-era pre-release build** that predates that release by well over a
year. So this is not cancelled content; it is a **pre-release snapshot of
a title that later shipped**, riding inside a different title's dump.

The headline analytical result stands: the build's entire
achievement/stat/event plumbing is fully baked in — a 2021-wave
statically-linked XSAPI build wired to a complete numeric stat vocabulary in a
plaintext CSV, with the game-side event names compiled into the plugin and
bridged to the host. Structurally, everything except the service-side
achievement catalog (unlock IDs, gamerscore) is present in the package.

This writeup follows the repo's methodology-first format. All analysis was
static and offline against files from a dump of owned content; no copyrighted
payload, rom contents, or binary text is reproduced here — structure only.

## Environment (generic)

- An Xbox One-class console with owned content, used only to extract owned
  packages; extracted package trees land on a PC for offline analysis.
- A PC-side static-analysis bench: a bucketed strings miner (ASCII/UTF-16
  pattern families), export-table and RTTI inspection, registry-hive
  structure viewers, and generic CSV/SQLite tooling.
- Read-only handling throughout: the package tree was treated as evidence;
  nothing in it was modified.

## Chain

1. **Package-identity scan** (see
   [`techniques/package-identity-fuzzing`](../../techniques/package-identity-fuzzing/README.md)).
   The publisher-namespace suffix used by Microsoft's Xbox packages is a
   fixed, high-precision marker. Scanning the collection-package dump's
   directory tree for the full package-name shape
   (`<Name>_<Version>_<Arch>_<ResourceId>_<PublisherId>`) lists every package
   present on the media, including any that are present-but-not-obvious —
   such as a second, first-party-named package sitting beside the expected
   collection shell.
2. **Family-name triage.** The collection shell has its own well-known
   family name. A *second* `Microsoft.*` family with a project-codename name
   (`ProjectDiamond`) is the anomaly: codename families on retail-signed
   first-party media almost always indicate a title that is not the label on
   the box.
3. **Mount-tree inventory.** Enumerate the package's `Mount/` directory
   structurally — executables, plugin DLLs, data/config files, asset
   containers — with type and role, not contents.
4. **Identity extraction from the config blob.** The game config ships as a
   registry-hive-format blob; hive *structure* parsing (key paths, value
   types, presence) confirms it carries the title's service identity
   (title id, service config id) without needing to quote values.
5. **Strings/RTTI mining of the shell and plugins.** Bucketed mining for
   telemetry-family markers (ETX/ETW, XSAPI classes, REST/RTA endpoint
   shapes, event-name namespaces), plus export-table walks of the plugin
   DLLs.
6. **Plaintext data-file mining.** The stat-ID table is a plaintext CSV;
   its structure (column shape, section markers, ID ranges, naming
   conventions) maps the whole game→service stat bridge without reproducing
   its contents.

## Findings (structural)

### The package is a complete standalone title package, not a collection component 

- Family-name shape, target device family, and manifest structure are
  indistinguishable from any shipped first-party Xbox One title.
- `Mount/` contains: one host executable (an XSAPI-statically-linked game
  shell), one game plugin DLL, one emulator plugin DLL, one N64 rom container
  image plus patch table, a stat table CSV, a registry-hive game config, and
  shader/asset trees.
- Build-tree breadcrumbs in debug paths place the source under a
  project-codename build tree with per-platform final-build subfolders and a
  `plugins\<game-plugin>` source directory — an ordinary console-plugin
  architecture, not collection-shell scaffolding.

### Architecture: host shell + game plugin + emulator plugin

- The emulator plugin exports only creation/destruction entry points — a
  clean emulator-core boundary. It carries no achievement or telemetry
  surface of its own.
- The game plugin exports native-code table accessors (start/end pointers for
  both the main CPU and RSP recompiled-code tables): the N64 game code was
  recompiled to native code and shipped as lookup tables the host walks. This
  is a recompilation-based remaster, not an interpreter.
- Achievement/stat logic is compiled into the game plugin (a helper source
  unit is named in debug paths) and forwarded to the host executable — the
  plugin has no Xbox services imports of its own. All Xbox Live contact is
  concentrated in the host shell.

### Telemetry plumbing: static XSAPI, no ETX

- No ETX/ETW event-write imports, no trace-logging, no manifest-declared
  events anywhere in the three binaries — the same finding the 46-title
  corpus classifies as the *transitional* (2019–2021) era shape.
- The host statically links the Xbox Services API: achievement manager,
  stats manager, multiplayer manager, social/profile manager, context
  initialization, HTTP call layer, and real-time-activity subscriptions.
- Endpoint strings match the standard XSAPI surface: session directory,
  per-SCID stats user URIs, RTA websocket connect, recent-players and title
  surfaces. The static lib's user-agent string dates the build to a specific
  2021 XSAPI servicing wave — a useful relative-dating signal for a build
  that predates its eventual release.

### The stat bridge is fully embedded in plaintext

- A single CSV maps numeric stat IDs to names, sectioned by comment markers:
  a small global block, three parallel per-level blocks (one per difficulty),
  a per-level cheat-unlock block, a per-map multiplayer block, and a
  multiplayer weapon/scenario unlock block. The naming convention
  (`<level>_<difficulty>`, `<level>_unlock`, `<map>_multi`) is regular enough
  that the ranges alone reveal the game's full content list shape — twenty
  mission levels, the classic multiplayer map list, and the classic cheat
  roster, all present.
- Host-side write strings show the wire payloads are two tiny JSON shapes
  (add-value and set-max) keyed by stat ID, with structured error logging.
- The full bridge — numeric ID → stat name → JSON payload → XSAPI URI — is
  reconstructible from the package alone.

### Event vocabulary lives in the plugin, namespaced, bridge-delivered

- The game plugin carries a game-namespaced event-template family: per-level
  completion and fastest-time templates (level+difficulty parameterized),
  cheat-unlock-requirement templates, and a set of literal award names
  matching the original game's end-of-round awards, plus multiplayer-match
  completion events parameterized by scenario variants, special mission
  events, and kill/shot counter families.
- These are *not* OS events: with no ETX surface, they travel the
  plugin→host bridge and exit through XSAPI. The vocabulary is embedded; the
  achievement bindings (which event unlocks which achievement) are not in the
  package.

### What is NOT in the package

- The achievement catalog (unlock IDs, gamerscore, requirement shapes) —
  service-side/Store-managed, as the broader corpus predicts.
- Any runtime telemetry pipeline — the 2021 wave talks directly to XSAPI
  services; nothing else ships.

## What worked

- Package-identity scanning as the discovery mechanism: the codename package
  was surfaced by a generic namespace regex, not by knowing to look for it.
- Family-name triage: "second first-party codename family in a collection
  package" is a cheap, reliable hidden-package flag — whether that package
  turns out to be cancelled or a pre-release build of a later release.
- Plaintext-first mining: the CSV stat table and string pools alone resolved
  the entire stat/event architecture — no decompilation was needed at all
  for this title.
- Structural hive parsing to confirm identity data presence without quoting
  identity values.
- The statically-linked XSAPI user-agent as a dating signal for a
  pre-release build.

## What did not

- The event names embedded in the plugin do not bind to achievements inside
  the package — the binding is service-side, so an offline analysis of the
  package can describe the vocabulary but not the unlock conditions.
- The emulator core's export surface is minimal by design; any deeper
  emulator behavior (timing, HLE specifics) is not reachable from export
  tables and was out of scope.
- The rom container was inventoried but not analyzed; rom contents are out
  of scope for this writeup by rule.

## Preservation note

This package is a preservation case study in miniature, in two ways.

First, the build itself: a complete, structurally self-describing
**pre-release snapshot** (1.4.38647.0, 2021) of a remaster that ultimately
shipped in January 2023 — shell, recompiled game code, emulator core, rom
container, stat table, config — everything except the service-side catalog
that Microsoft's servers hold. Pre-release builds of major releases are
rarely preserved deliberately; this one survived by accident, and documents
what the game looked like eighteen months before launch (including a stat
roster that may have changed between this build and ship).

Second, the discovery mechanism: the package was found inside a *different
title's* dump by nothing more than a package-name regex. That is the
standing argument in this repo for treating **collection-package dumps as
first-class preservation objects**: non-obvious packages — cancelled
content, pre-release builds, staged titles — ride along inside shipped
packages and dumps, and package-identity scanning is the low-cost pass that
finds them. Nothing about this analysis required distribution of any
payload; the structural findings are the value, and the payload stays
exactly where it is.
