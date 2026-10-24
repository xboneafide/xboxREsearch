# Probing the EraOS/GameCore Content-Mount Services from a Retail SYSTEM Shell: Methodology, Findings, and Dead Ends

## Summary

This document records how the EraOS/GameCore content-mount surface was mapped
from a retail console's SystemOS shell — without ever executing an exploit
payload on it. The work answered three questions with offline reverse
engineering plus a small number of read-only on-console observations:

1. **Which services own the mount path.** The game-mount flow lives in
   `comsrvhost.exe`, a service inside the per-title EraOS/GameCore virtual
   machine that boots from each launched disc's embedded XVD — *not* a
   component of the console's SystemOS, despite an earlier framing that
   attributed it to the host. Every owned disc carries its own GameCore
   build, so the mount surface is a per-disc matrix, and discs can be ranked
   and analyzed entirely offline before any console interaction.
2. **What validates, and what does not.** Across GameCore builds spanning
   2013–2021, the game-mount path performs **zero content-type, signature, or
   path-sanity validation inside EraOS**. The boot XVD path arrives from
   SystemOS over an RPC/COM channel, is transformed by a build-path helper,
   and is mounted with the unchecked mount call — the content-type-checked
   variant (`XCrdMountContentType`) does not exist in *any* sampled era.
   EraOS XCrd contexts are likewise unauthenticated from inside the VM: any
   game process can create contexts and mount content, and this was true from
   the first (2013) builds, not a later regression.
3. **Where the real gate is.** The `LAUNCH_ERA_VM` ioctl payload — publicly
   documented as having "no fields validated" — contains **no XVD path
   fields at all**. The boot XVD path flows over the COM channel
   (`IGameOsManager.SetupAndLaunchGame`), and its source is the temp-XVD
   slot bookkeeping in SystemOS. This collapses the "craft the launch
   payload" vector and pins the remaining open gate on HostOS-side XCrd
   enforcement, not on EraOS.

All console-facing work was read-side launch-payload observation on owned,
air-gapped hardware; no update path was ever touched, and no exploit was
developed or released. This is surface mapping for game-preservation
research, not a packaged exploit.

## Environment (generic)

- A retail Xbox One-class console frozen at OS build 10.0.25398.4908,
  deliberately kept one build before the July 2024 system update that patched
  the public SystemOS entry chain. No developer mode; retail software only.
- **A stable SYSTEM-level shell in SystemOS**, obtained via published
  techniques (the delisted Game Script UWP entry chain plus its paired kernel
  read/write primitive, staged by a public loader framework). See
  [techniques/eraos-code-exec](../../techniques/eraos-code-exec/) for the
  access chain, the shell's survival rules, and the safety model.
- The console is air-gapped in its standing state; its only network peer is
  the analysis PC on the LAN. All console-facing steps here are read-side
  launch observation or offline; nothing in this probe can trigger an update
  flow, by construction.
- Analysis hosts: a Windows PC with Ghidra headless for decompilation, plus
  public XVD/XVC utilities for header reads.
- Sample material: GameCore binaries extracted from the embedded-XVD
  filesystems of owned discs (via community mount-style package dumps of the
  same titles, which ship the embedded GameOS filesystem and its registry
  hive in plaintext — no decryption needed). Every referenced title is one we
  own on disc or digitally; nothing here involves distributing content.

## Chain

The mapping chain, in order of execution:

1. **Establish the SystemOS shell** (already standing — see
   techniques/eraos-code-exec). Everything downstream needs only this for
   observation; the analysis itself is PC-side.
2. **Correct the service-location framing.** An earlier hypothesis placed
   the mount service in SystemOS. Decompile-driven work showed
   `comsrvhost.exe` runs inside the EraOS/GameCore VM booted from the
   launched disc's embedded XVD. The console's SystemOS (the host) merely
   launches that VM and supplies the launch payload. Consequence: the probe
   surface is **per-disc** — each owned disc is one GameCore version with its
   own copy of the mount service.
3. **Acquire the per-disc GameCore binaries offline.** Community mount-style
   dumps of owned titles contain, in plaintext: the GameCore registry hive
   (`era.vbi`, which carries the exact version string, e.g. `6.2.NNNN.0
   (xb_rel)`) and the entire embedded-XVD System32 filesystem including
   `comsrvhost.exe` and `xcrdapi.dll`. Version verification from `era.vbi`
   beats the public GameCore build tables (one owned title's verified build
   is absent from the table's 100+ entries — the table is incomplete).
4. **Decompile the mount services per era** (Ghidra headless) and diff the
   game-mount function across builds to isolate what hardening was added
   over time — and what was never added at all.
5. **Decode the launch payload** by decompiling the SystemOS ERA control
   service binary (`eracontrol.exe`, obtained from a public dumping toolkit)
   to reconstruct the exact `LAUNCH_ERA_VM` ioctl structure and answer where
   the boot XVD path actually originates.
6. **Observe the live path on console, read-only.** With the shell, verify
   the COM binding direction (GameOS-side service bound to a SystemOS
   endpoint) and the error/log surfaces — no payload crafting, no content
   substitution, no state changes.

## Findings (structural)

### The services

- **`comsrvhost.exe`** (GameOS/EraOS, per-disc): the mount broker. Binds an
  RPC endpoint (the "xbox-sra" surface) toward SystemOS and consumes launch
  data from it. Size ~47–69 KB depending on era; a small, stable, RPC-bound
  service — and **absent entirely** on the one Series-X-era (2024) GameCore
  sampled, which restructured the service out of the VM (see What did not).
- **`xcrdapi.dll`** (GameOS/EraOS, per-disc): the client-side XCrd library.
  Its exported `XCrdMount` is a bare RPC wrapper: it allocates a request
  sized from the caller's path, copies the raw path string in unmodified,
  sends it, and polls for the device-path response. Zero authentication,
  zero content-type validation, zero signature checks client-side.
- **`eracontrol.exe`** (SystemOS): builds and submits the `LAUNCH_ERA_VM`
  ioctl (`\\.\XVmCtrl`, custom ioctl `0x150100`) — a fixed header plus four
  variable strings, GUIDs, a temp-XCD slot number, policy words, and an
  event handle. Critically, **no XVD path fields** (see below).

### What validates

- **HostOS-side mount checks exist**: the host enforces its one-Title-XVC-
  at-a-time rule, and post-2017 hardening placed content-type enforcement
  questions on the HostOS XCrd side. The host is the gate; how strong the
  gate is remained open here (see What did not).
- **XVD info bits feed only a mount flag**: before mounting, the service
  queries XVD information; bits 2/4 of that information select only the
  READONLY mount flag. Information flow exists — but it chooses a flag, not
  a rejection.
- **The 2021-era build adds one small check**: a `GetLastError` inspection
  on the mount result plus one extra function parameter. That is the entire
  eight-year delta in validation depth (see the similarity numbers below).

### What does not (the unvalidated game-mount path)

The game-mount function, structurally identical across 2013→2021 builds:

```
XCrdBuildPath(partition, path)
  → XCrdOpenAdapter
  → XCrdQueryXvdInformation        (bits 2/4 → READONLY flag only)
  → XCrdMount                      (never XCrdMountContentType)
  → XCrdQueryDevicePath → symlink \??\g:
```

- **No content-type check.** `XCrdMountContentType` — the content-type-
  checked variant — has **zero hits (string and import) in any of the four
  sampled eras** (2013, 2014, 2018, 2021). The checked variant was never
  adopted for this path.
- **No signature check inside EraOS.** Nothing in the sampled mount functions
  verifies the boot XVD's signature. The 2013 ground-truth decompile is
  explicit: no content-type, no signature, no path sanity — the EraOS side
  blindly trusts the SystemOS-supplied path. After mounting, EraOS loads
  `G:\appdata.bin` from that content as its own `\REGISTRY\MACHINE\XBOX`
  registry hive.
- **No path sanity.** The path arrives as a Windows string over RPC and goes
  through unmodified; any XCrd path form passes through the client library.
- **Quantified stability.** Structural similarity of the game-mount function
  across eras: 2013 vs 2014 ≈ 0.99, 2013 vs 2018 ≈ 0.99 — unchanged for five
  years; 2013 vs 2021 ≈ 0.72, with the delta being the single
  `GetLastError` check noted above. **No content-type, signature, or
  path-sanity validation was added through 2021.**
- **One pre-mount step recorded** (new detail beyond the original finding):
  before mounting, the service opens `\\.\xvmmcli` and issues a device
  control (custom code `0x150044`), logged on failure as "PsmService:
  Failed (%x) to secure G:". This is a G:-securing ioctl, not a content
  check; the zero-validation conclusion stands with this step now recorded.

### What does not (unauthenticated XCrd contexts)

- Inside EraOS, **game processes can create XCrd contexts freely via
  `xcrdapi.dll` with no authentication** — verified against the modern-era
  behavior and confirmed in the 2013 decompile: the exported mount call was
  an unauthenticated bare RPC wrapper from day one. It was true in 2013, not
  a regression.
- Corollary (from the published dump pipeline this repo builds on): an
  in-VM process can unmount the running title itself (Content Type 1 =
  Title), which defeats the HostOS one-Title-at-a-time check, and then mount
  *another* Title package — the basis of the documented Lua-FFI dump method.
  No temp-XVD swapping, no SystemOS copying needed, and compatible with this
  console's OS build.

### Where the boot XVD path actually comes from (payload correction)

Full decompilation of the SystemOS ERA control service (hundreds of
functions reconstructed) produced the exact `LAUNCH_ERA_VM` ioctl layout:
a fixed header with flags (compat/policy bits, launch-mode mapping), a
max-title-memory value, four variable strings (two UTF-8 from HSTRINGs, two
UTF-16 from the era-params struct, each length-prefixed), a shutdown event
handle, policy words, two GUIDs (content and build), a temp-XCD slot number,
and two trailing parameters.

**The payload contains no XVD path fields.** The publicly documented
`bootXvdPath` / `altBootXvdPath` field names are **not part of this
ioctl**. The boot XVD path is passed later, over the COM channel: SystemOS
eracontrol → GameOS `IGameOsManager.SetupAndLaunchGame()`, which builds the
XVD path from the partition id and path, and mounts it (with zero EraOS-side
validation, per the above). Practical consequences:

- Crafting the launch ioctl alone does **not** redirect the boot mount. The
  "no fields validated" documentation is true but about fields that do not
  include the paths that matter.
- The path originates from SystemOS `AppPackageMountManager` bookkeeping —
  the temp-XVD slot name (e.g. `temp01`) plus the package mount location.
  The already-documented **temp-XVD swap** is therefore the correct
  injection point; a second route to the same point exists (with SystemOS
  exec + kernel read/write, the eracontrol process's in-memory UTF-16 path
  strings can be patched at launch time).
- The remaining unknown gate is **HostOS-side**: HostOS XCrd content-type
  enforcement on the temp slot / boot XVD, and any StartEraVm payload
  validation on the host. That gate is hosted by the console, not the disc,
  and is testable directly — it was not tested in this pass.

## What worked

- **Offline per-disc GameCore acquisition and versioning.** Embedded-XVD
  System32 trees plus `era.vbi` hive version strings extracted from
  community dumps of owned titles; ranged downloads suffice (the relevant
  files sit at the start of non-solid archives). Per-disc verification
  superseded the public GameCore build table, which is demonstrably
  incomplete.
- **Full decompilation of the mount services across four eras** (2013,
  2014, 2018, 2021 GameCore builds) and of the SystemOS ERA control
  service, with a quantified cross-era structural diff of the game-mount
  function.
- **Negative structural results with high confidence**: absence of
  `XCrdMountContentType` across all eras (string + import, zero hits);
  zero validation in the EraOS-side game-mount path; unauthenticated XCrd
  contexts confirmed from the earliest build.
- **The payload-structure decode** that falsified the launch-ioctl path-
  injection hypothesis before any console testing was spent on it — the
  single biggest time-saver of the pass.
- **A ranked probe plan derived from the data**: offline diff first (free,
  ranks discs by weakest mount service), then console-created-XVD
  reconnaissance (`xcrdutil` create flags + where `appdata.bin` lands in a
  created XVD — console-created XVDs are legitimately signed by the
  console's own XCrd, which would sidestep signature gates entirely if the
  user-data/appdata is controllable), then live substitution on the
  weakest-comsrvhost disc.

## What did not (exact errors)

This pass was deliberately offline/read-side, so the negative results are
mostly structural rather than runtime errors:

- **No live mount-substitution was executed.** Probe A (bootXvdPath/temp-XVD
  substitution on console) was planned and outcome-matrixed but not run;
  there are no observed rejection codes from HostOS or comsrvhost to report
  yet. The outcome matrix stands as the next experiment's acceptance
  criteria: (A1) VM boots on substitute content; (A2) HostOS rejects before
  comsrvhost — capture the exact code and decode it against the public
  syscall/error table; (A3) comsrvhost rejects — diff that era's service;
  (A4) mount succeeds but the VM dies on the substituted hive — still
  proves the mount primitive.
- **The launch-ioctl path-injection vector is dead on arrival**: the
  `LAUNCH_ERA_VM` payload has no path fields to inject (structural result,
  above). Any future work must go through the COM channel or the temp-XVD
  slot, not the ioctl.
- **The Scarlett-era ceiling.** The one 2024 Series-X-class GameCore sampled
  (GameOS on a 19041-era build) contains **no `comsrvhost.exe` at all** in
  its embedded System32 (only `xcrdapi.dll` / `xcrdutil.exe`) — the service
  was restructured out. Findings via this vector therefore apply to discs
  with GameCore builds through roughly 2021; the 2024-era mount path is a
  different, unmapped service topology.
- **Open, untested gates left standing** (explicit non-results):
  - Does HostOS XCrd enforce Title content-type on boot XVDs / temp slots
    (the post-2017 hardening question)? Unresolved.
  - Will a non-Title XVD mount as the game drive at the host? Unresolved.
  - Does comsrvhost tolerate a hive-only content (an `appdata.bin` with no
    game data)? Unresolved.
  - Which XVD types the console's own `xcrdutil` can create, and whether a
    created XVD's user-data/appdata is writable from SystemOS —
    reconnaissance not yet run.

## Links

- Shell context and access chain:
  [techniques/eraos-code-exec](../../techniques/eraos-code-exec/) — the
  SystemOS SYSTEM-shell methodology, its exact-observed-errors section, and
  the safety model (air-gapped standing state, update-path avoidance) that
  governed this probe as well.
- Related repo records (private research repo): our private mount-probe
  runbook (comsrvhost content-mount surface mapping) and the private EraOS
  code-execution / dump-path working document, which hold the
  underlying evidence trail; this document is their publish-facing distillation.
