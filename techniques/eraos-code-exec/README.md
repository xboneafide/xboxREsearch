# Retail EraOS Code Execution for Game Preservation: Methodology, Results, and Dead Ends

## Summary

This document describes, at a methodology level, how a retail Xbox One console
was brought to a SYSTEM-level shell in SystemOS using only publicly documented
techniques, what that access actually bought for game-preservation work
(license enumeration, hive capture, offline diagnosis of the console's update
gate), where the pipeline dead-ends (the SystemOS/EraOS VM boundary, and
licensing/entitlement surfaces that refuse anything but the genuine device
identity), and the safety model under which all of it was done. It deliberately
does not reproduce exploit steps beyond what is already public: the goal is to
record constraints, observed errors, and research conclusions, not to be a
recipe.

## Environment (generic)

- A retail Xbox One-class console on OS build 10.0.25398.4908 (June 2024) —
  deliberately one build before the 10.0.25398.4910 (July 2024) system update
  that patched the entry chain described below. The console is intentionally
  frozen there; updating it is forbidden (see Safety model).
- No developer mode. Retail-facing software only.
- A separate PC on the same LAN used as rehearsal, listener, and analysis
  machine. All tooling and data transfer is PC↔console over the local network;
  the console's route to the internet is strictly mediated (see Safety model).
- Tools used are all public: the Game Script UWP entry chain and its kernel
  read/write primitive, the associated multi-stage loader framework,
  SharpShell-style in-process PowerShell hosting, .NET runtime deployment to
  the console, XVD/XVC utilities, and ConnectedStorage container tooling.
- Titles referenced are ones we own (disc or digital license). This is
  preservation research on our own hardware and licenses; nothing here
  involves distributing content.

## Chain

The access chain, at the level of "which published pieces, in what order":

1. **Entry: the Game Script UWP app (SystemOS).** A Microsoft Store app
   (since delisted and blacklisted — ownership had to predate that) whose
   scripting surface exposed arbitrary memory read/write, which converts to
   arbitrary native code execution inside SystemOS. This is the published
   "Collateral Damage" chain (carrot_c4k3 & landaire, July 2024).
2. **Kernel read/write.** The same published chain pairs the SystemOS exec
   with a kernel read/write primitive (CVE-2024-30088), yielding SYSTEM-level
   access in SystemOS. A multi-stage loader framework staged the payload over
   the network from the PC; the loader also handles firewall and persistence
   conveniences.
3. **SystemOS shell.** With SYSTEM access, a full PowerShell environment is
   deployed to the console and driven in-process (SharpShell-style MSBuild
   hosting of .NET scripts). This is the working research shell: file access,
   registry, process control, WMI, .NET code, all from the PC over a socket.
4. **EraOS (the game VM) is a separate problem.** SystemOS sees only encrypted
   XVC content; decrypted package content exists only inside the per-title
   EraOS/GameCore VM. Every *published* EraOS code-exec method is the
   Lua-FFI save-modification pattern (LuaFFI-CE / OneDumpGame): a
   script-VM-bearing title loads attacker-placed script from its own save
   data, which then mounts and streams the target package. We surveyed the
   public exploit index end-to-end: no published EraOS service exploit exists;
   all other retail entries are SystemOS/HostOS class. The Lua path requires
   a "launcher" title — one owned game with a save-reachable embedded script
   VM (LuaJIT+FFI or similar).

The two halves connect differently depending on the goal: SystemOS access is
sufficient for license/regulatory/hive research and for offline diagnosis;
EraOS access (or the absence of it) is the wall for decrypted-content dumping.

## What worked

- **Stable SYSTEM-level SystemOS access** on build .4908, re-establishable
  from the Game Script entry at will (the entry app and its cached license
  are the permanent anchors — losing either is a permanent loss; see Safety
  model).
- **A usable PowerShell research shell**, with a learned set of survival rules
  (below) that makes long sessions reliable rather than fragile.
- **License-state enumeration**: a full dump of cached entitlement state
  (device, open, and subscription-device license classes), cross-referenced
  against title lookup data — the foundation for all downstream licensing
  research.
- **Registry hive capture**: full hive dumps (OSDATA, SYSTEM, SOFTWARE, SAM)
  via `RegSaveKeyA` from the SYSTEM shell, transferred off-console and parsed
  on the PC. This is what cracked the update-gate mystery (below).
- **Offline diagnosis of the console's forced-update gate.** The console
  displayed a mandatory system-update screen despite the network answering
  "no update available." PC-side parsing of the OSDATA hive proved the gate
  decision was **cached state** (`HKLM\OSDATA\Update\Content\<ContentId>`
  with `LastResult` holding the exact on-screen error), not a live network
  decision — the console re-displays cached failure state regardless of the
  HTTP response it receives. The cache keys were provably deletable
  (`RegDeleteTreeW` returning success, verified by reopen), but deletion alone
  was a recreate-fail loop: the check re-runs and rewrites its own failure
  values.
- **Update-request response-schema research (PC-side only).** By replaying
  the console's captured request directly against the real update endpoint
  from the PC, the real response schemas were captured (mandatory-update and
  no-update shapes, field names, file lists). This validated response shapes
  without touching the console — an example of the PC-rehearsal discipline
  paying off.
- **PC-side package acquisition front**: console-only titles proved to be
  PC-downloadable end-to-end as *encrypted* XVC (catalog → manifest →
  unauthenticated, range-resumable CDN fetch), byte-verified against the CDN
  ETag. The missing piece for decryption is the per-package key, which is
  entangled with device licensing (see What did not).
- **EraOS attack-surface mapping (offline)**: per-disc GameCore builds can be
  read from package metadata and extracted from community package dumps;
  reverse engineering of the 2013-era GameCore mount services showed the
  game-mount path is SystemOS-supplied and unvalidated inside EraOS (no
  content-type, signature, or path-sanity checks client-side), and that EraOS
  XCrd contexts are unauthenticated — a durable, game-independent research
  lead even though no exploit was built on it.

## What did not (exact errors)

All error codes below are observed, on-console unless noted.

- **License-hidden content cannot be dumped keyless.** Mounting a title whose
  Game Pass lease had expired produced, consistently:
  `0x80070002` (mount failure), raw `FindFirstFileA` error 2, and VBI read
  `0x80070490` — all consistent with license-hidden content, not script or
  mount bugs. The stage-2 fallback script ran and reported cleanly; the
  conclusion is that keyless raw dump of digital content is impossible while
  the content is license-hidden. No amount of SystemOS access changes this.
- **The forced system-update gate (`0x83750009`).** Every attempt to fully go
  online or launch a title with a pending update hit the mandatory-update
  screen. Systematically ruled out live: DNS interception (traffic demonstrably
  hit the PC listener), CRL/OCSP handling, content-host denies, time service,
  three response formats (`IsUpdateAvailable:false` in the old field name →
  `0x83750009`; `204 No Content` → `0x8923000D`, a different code, proving the
  console parses our responses), and telemetry/temp-cache clearing. Root cause
  proven to be cached hive state (above).
- **Response-variant matrix on the update gate.** After matching the real
  no-update schema exactly (nulls, `UpdateType:0`, echoed incoming version),
  the gate error became `0x8B050033` — stable across reboots, CA re-install,
  and cache deletion. `UpdateAvailable:true` targeting the console's own build
  with an empty file list gave `0x8B05000C`. Interpretation: the update client
  treats any stale build as mandatory-on-check-in; the ring enrollment
  (`SystemUpdateGroup` set to a non-retail ring) is a suspect. The console
  demands an update regardless of the answer we give it. Also notable: during
  update attempts the console fetches system-update licenses for its *current*
  build — that call is not gated and answers correctly, so the network path
  itself is healthy.
- **MSA/licensing endpoint dead-ends (PC side).** The licensing content
  endpoint returned fault **3043 `DeviceNameNotHexBinary`** ("The Device Name
  claim in the provided authorization token was mal-formed") for every
  client variant tried (third-party tooling device identities, fresh
  randomized device identity, XSTS tokens under multiple identity types).
  Live interception of the *real* Windows Gaming Services client then showed
  the real device ticket also receives 3043 on that endpoint — the endpoint is
  effectively retired for new-license acquisition. New-license creation was
  observed to fail server-side (`CreateUserRootLicense →
  GetOSEkbForDeviceAsync → ArgumentNullException`, HTTP 500) without the OS
  EKB device claim, which is hardware-attested and only minted by the genuine
  OS stack. Unsigned request tricks fail outright: `0x80048820 Invalid STS
  request`.
- **PC Gaming Services licensing breakage (incidental).** During the same
  work the bench PC's own first-launch licensing broke
  (`0x87e10bcf` = `DeviceEkbRequired`). Cause not proven; the same OS-EKB
  hardware-attestation dependency is the suspected common factor. Recorded
  here as an honest incident log entry: the intercept/replay work may have
  contributed, and no fix was found short of the disc path.
- **Shell fragility classes** (recurring, with known recoveries):
  - `pwsh` crashes with `Could not load Newtonsoft.Json 13.0.0.0` on stale or
    incomplete materializations of the PowerShell deployment; recovery is
    re-running the materialization routine, not debugging the shell.
  - .NET tooling wedges scanning `C:\Program Files\dotnet` when
    `DOTNET_MULTILEVEL_LOOKUP` is unset after resolving `D:\dotnet` — the
    multilevel-lookup environment fix is mandatory on every session.
- **Structural wall — the VM boundary.** SystemOS exec plus kernel read/write
  cannot see decrypted XVC content; only an EraOS game process can. No
  published path crosses that boundary except the per-title script-VM pattern,
  and our owned collection triaged to **zero** usable launchers: engine
  verdicts across the disc collection (ForzaTech, Frostbite, Northlight, IW,
  Apex, MT Framework, REDengine 3, proprietary Bungie/Hudson engines) show no
  save-reachable script VM. Automated triage of dumped packages for
  `luaopen_`/LuaJIT/FFI and other VM markers (Squirrel, AngelScript, embedded
  Python) produced only false positives (ambient soundbank names matching
  "squirrel"); every hit must be read in context.
- **Dead public paths.** External VBI Loading (user-content VM binary at
  boot) was patched in July 2017 — dead on this build class. The original
  temp-XVD mount swap variant of the Lua pipeline is likewise not applicable
  to this build; only the unauthenticated-XCrd-mount approach is
  build-compatible (verified against the .4478/.4908/.4909 build family).
- **launcher-runbook.csv was produced as a planning artifact only** — its
  step rows were never filled in; the eraos reference document's runbook
  section is the real plan of record.

### Shell survival rules (recorded because they cost real dead shells to learn)

- Run scripts **in-process** (`& script.ps1`), never by spawning a child
  PowerShell; the child resolves to a different install and crashes at startup,
  taking the session with it.
- **Never wait on child processes.** Launch with redirected stdin from an
  empty file (turns any interactive prompt into instant EOF), redirected
  output to files, no wait; poll later. A wedged child hangs the session, and
  Ctrl+C kills everything on this socket-based shell.
- Never execute a `.dll` directly — the console pops an app-install prompt.
- Redirected console output is silent; read the log file rather than waiting
  on the console.
- For anything possibly-hanging, use the hang-proof probe pattern: a nested
  runspace invoked asynchronously with a hard timeout and abandon-on-timeout,
  so no probe can ever require an interrupt.

## Safety model

These rules are hard and precede every experiment; they exist because the
console is effectively unrecoverable as a research platform if they are
violated. **One system update, or one lost entry-app license, is a permanent,
flat loss — there is no re-exploitation path today.**

- **Never trigger OTA system updates.** No update download, no
  reboot-into-update flow, no "harmless" update banner accepted — assume every
  banner is a one-way door. Every script or probe is read first and confirmed
  incapable of kicking off an update before it is run.
- **Console A is frozen at 10.0.25398.4908.** The next build (.4910, July
  2024) patches the entry chain; every subsequent build is past it. The
  console is ~2 years behind current and must stay there. Any drift in OS
  build = full stop, offline, reassess.
- **Default state = proven-safe configuration:** the frozen build, update
  endpoints blocked at two independent layers, no listeners or redirects left
  active. Experiments happen only after this state is verified, and the state
  is restored after.
- **Safety invariant checked after every console-facing step:** OS build
  unchanged → entry app present → dashboard functional → no update prompts
  accepted. Any drift stops everything.
- **PC-side rehearsal first.** Anything testable on the PC (listeners,
  schemas, parsers, fuzz targets) is tested there. The console never sees
  untested changes.
- **One approved action at a time**, each with explicit intent, a ready undo,
  and a post-check.
- **MITM apparatus is a PC-side research tool** and only touches the console
  when demonstrably necessary and rehearsed; intercept modes degrade the
  console's online state by design. When interception is required (e.g. to
  keep the console's update checks answered while online), it must be the
  full tested configuration — DNS rules, DNAT fallbacks, running listener with
  a path-based gate, and per-session CA — and every redirect must be scoped to
  exact hosts, because shared IPs silently poison unrelated services (auth
  hosts are never redirected anywhere but the real service).
- **Recovery procedure if anything wedges:** remove all PC-side interference,
  restore the proven blocklist, cold boot, verify health before anything else.
- **PII/secrets hygiene:** hive dumps, captures, and console logs contain
  device identifiers and account secrets; they are processed on disk but
  never committed, pushed, or included in documents — only extracted findings
  are.

## Open questions

- **Is the update gate driven by ring enrollment?** The console is enrolled in
  a non-retail update ring; whether the forced-update behavior (or the
  `0x8B050033` result class) can be moved off by a service-adoption change is
  untested. Any test touching the update surface is gated on the safety model.
- **Does HostOS enforce Title content-type on boot XVDs post-2017?** The
  2013-era GameCore's mount path is proven unvalidated inside EraOS; the open
  half is HostOS-side. If a console-created XVD (legitimately signed by the
  console's own crypto) can serve as boot content with attacker-influenced
  user data, that is a game-independent EraOS foothold with no script VM.
- **Modern-build diff:** does the same unvalidated mount path survive into
  current GameCore builds? Requires extracting a modern title's embedded
  GameCore for the comparison.
- **Launcher scarcity:** is there any owned title in the broader pool with a
  save-reachable embedded VM (id Tech lineage, LIGHT/RAGE-derived builds,
  casual-title Lua UIs are the cheap remaining checks)? If the collection
  triages empty, a known-good launcher title on the secondary console is the
  pragmatic route.
- **EraOS service bugs in old GameCore:** none are public; the 2013/2014
  builds on disk are the RE queue. A single bug in a shared launch-era
  GameCore would cover several titles at once.
- **The OS-EKB wall:** new-license acquisition (on either PC or console
  fronts) appears to require a hardware-attested device claim only the
  genuine OS stack produces. Either driving the genuine client's own
  acquisition path, or the disc-activation path, are the only live routes.
- **Consequence analysis of the cached-gate state:** the console's own check
  logic recomputes failure state into the hive on every Go Online. Whether any
  observable write pattern (as opposed to response crafting) can move
  `LastState`/`LastIntState` is unknown, and testing it flirts with the
  update surface — treat as low priority under the safety model.

---

*Research framing: this is documentation of private preservation research on
our own consoles, discs, and licenses, using only publicly documented
techniques. It describes which published chain was used, which constraints
apply, and what breaks — it is not a reproduction guide for the entry exploit
itself, and nothing here is distributed.*
