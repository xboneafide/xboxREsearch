# Secondary Console Entry Path: Retail Developer Mode + CVE-2026-62735 (QuantumTunnelEx)

## Summary

The current preservation pipeline depends on a single entry chain: the
Collateral Damage game-script path, which is permanently build-frozen at
10.0.25398.4908 and tied to one specific console and its cached entry-app
license. This document assesses a **secondary** entry path: a publicly
catalogued Windows kernel vulnerability in `http.sys` — CVE-2026-62735,
community name "QuantumTunnelEx" — confirmed working by its public researcher
on a **retail** Xbox One in **retail Developer Mode**, yielding the same
end state (SYSTEM in SystemOS) on a post-Collateral-Damage OS build.

The conclusion is a *strategic* one, not an operational one: today this path
does not replace Collateral Damage, and on the frozen console it cannot even
be attempted. Its value is as a lifeboat — an independently documented,
publicly reproducible SYSTEM vector that survives the loss of the
game-script chain, with the same downstream toolchain and the same license
pipeline on the other side of it.

**Framing:** this writeup documents the path at both the feasibility level and
the operational-summary level. The exploit itself is publicly published (PoC
source, crash logs, and post-exploitation tooling by the public researcher);
publishing a *summary* of that public material here serves the preservation
use case — SYSTEM access on owned hardware is the foundation of the entire
pipeline. This document includes no locally authored exploit code; the
canonical sources for reproduction are the public repository and the CVE
record, linked at the end. What we add is the build matrix, the risk
analysis, and how this path plugs into the rest of this repo's pipeline.

## Motivation: the single-entry-chain risk

The active pipeline (see
[techniques/eraos-code-exec](../../techniques/eraos-code-exec/)) rests on
exactly one anchor: the Game Script UWP entry app and its cached license on
a console permanently frozen at 10.0.25398.4908. That anchor is brittle in
three stacked ways:

- **Build-frozen.** The entry chain was patched in 10.0.25398.4910 (July
  2024). Any forced or accidental update kills it, and the Xbox boot/update
  architecture is monotonic — there is no known path back to an older build.
- **Single-console.** The entry app and its license are tied to Console A.
  A second retail console cannot follow the same path unless it also holds
  the app, and the app is delisted and blacklisted — new ownership is not
  possible.
- **Unrecoverable.** Losing the entry app or its cached license (store-side
  revocation, console storage damage, a license-cache wipe) is permanent.
  There is no second copy and no way to re-acquire.

A secondary entry path matters for exactly those cases: a different console,
a different build epoch, or the loss of the primary anchor. Ideally the
secondary path is (a) publicly documented so it cannot silently rot, (b)
independent of any specific title or app license, and (c) reachable on a
*current* retail build so a console that has been updated is not lost.
CVE-2026-62735 scores on all three — with one significant dependency
(Developer Mode) that the primary chain does not have.

## Environment (generic)

- A retail Xbox One-class console (Durango). Series S/X have **not** been
  publicly tested with this vulnerability.
- An OS build in the publicly documented vulnerable window: the July 2026
  retail branch, specifically build 10.0.26100.8866
  (xb_flt_2607ge.260630-2200, July 2026), before the August 2026 Patch
  Tuesday servicing that patched the Windows-side variant.
- Retail **Developer Mode** activated on the console, having been online at
  least once (activation is a one-time online handshake).
- A separate PC on the same LAN for tooling, staging, and analysis; the
  console's route to the internet is strictly mediated, as throughout this
  project.
- No disc, no installed game, and no specific game license involved — the
  path is independent of the retail title layer.
- No devkit involvement: the public record shows the vulnerability is not
  exploitable on the dev-kit build tested by the researcher.

## Vulnerability overview (CVE level)

| Field | Value |
|---|---|
| CVE | CVE-2026-62735 ("QuantumTunnelEx") |
| Advisory | ZDI-26-536; Microsoft SRD catalog entry (see references) |
| Original discovery | Siyeon Wi, Pwn2Own Berlin 2026 |
| Component | `http.sys` (Windows kernel HTTP driver) |
| Class | Integer overflow in response-header size accounting → undersized nonpaged pool allocation → pool overflow |
| Confirmed on Xbox | Retail Xbox One, retail Developer Mode, build 10.0.26100.8866 |
| Result demonstrated publicly | SYSTEM in SystemOS; post-exploitation flash and VBI dumps published by the researcher |

At summary level: the legacy `http.sys` response path on the console
computes a total header size from attacker-influenced values, that sum
wraps, a small buffer is allocated against the wrapped total, and the real
(multi-megabyte) header data is then written past it. The public researcher
demonstrated this converting to a controlled pool-overflow primitive and,
via pool grooming, to kernel read access and a SYSTEM token — on the retail
console itself. The patch adds integer-overflow guards to the affected
routines; the legacy code path is the one reachable on Xbox One SystemOS.

For the full root-cause walk-through (the header-sum overflow expression,
the patched routine names from the Diaphora diff, and the pool-feng-shui
progression), see [`references/quantumtunnelex-analysis.md`](../../references/quantumtunnelex-analysis.md)
and the public PoC repository linked at the end.

## Operational summary (from the public record)

Everything in this section is a condensation of **publicly published
material** — the researcher's PoC repository, the community wiki issue, and
the independent analysis write-ups. It is reproduced here at summary level
for preservation use: the intent is SYSTEM access on owned hardware to run
the licensing/CIK pipeline documented across this repo.

### Prerequisites

| Requirement | Detail |
|---|---|
| Hardware | Retail Xbox One (Durango). Series S/X have **not** been publicly tested. |
| OS build | 10.0.26100.8866 (`xb_flt_2607ge.260630-2200`, 2026-07-15), or another build in the vulnerable window before the August 2026 Patch Tuesday servicing. |
| Developer Mode | Standard retail Developer Mode, activated and online at least once. |
| Tooling | The public cross-compiled PoC binary for x64 SystemOS, deployable via SSH / Dev Portal / UWP sideload. |

What is *not* needed is the striking part: no Collateral Damage, no Game
Script app, no Lua injection, no disc, no active license, no installed game,
no devkit (the dev-kit build tested is already patched), and no prior kernel
access — the vulnerability itself *is* the kernel access.

### Entry-point flow

```
Retail Xbox One on a vulnerable build
        │
        ▼
Developer Mode (already activated)
        │
        ▼
SSH or Dev Portal → push the public PoC binary to the console
        │
        ▼
Run the PoC binary on console (usermode)
        │   ├── spawns a local HTTP server (server role)
        │   └── spawns a client thread GET /poc/ to localhost
        │       on receive, crafts HTTP_RESPONSE_V2 with ~69,993 oversized headers
        ▼
DeviceIoControl(queue, 0x12403F, crafted response, ...)
        │
        ▼
http.sys kernel driver (legacy response path)
        │   header-size sum wraps → tiny 0x2000 nonpaged buffer
        │   allocated → full header data written out of bounds
        ▼
Named-pipe pool grooming (overflow corrupts the next chunk's Flink)
        │
        ▼
Arbitrary kernel read → token swap → NT AUTHORITY\SYSTEM shell
```

### The trigger, phase by phase

The PoC acts as **both** HTTP client and server on the same machine —
the entire exchange stays on local loopback:

| Phase | Action |
|---|---|
| Setup | `HttpInitialize` → `HttpCreateRequestQueue` → `HttpCreateServerSession` → `HttpCreateUrlGroup` → add a localhost URL |
| Client | Sends `GET /poc/` to the local queue |
| Response crafting | Builds an `HTTP_RESPONSE_V2` whose header groups are sized to overflow the sum: ~69,993 header entries across the well-known header names, with per-entry sizes chosen so the total wraps |
| Delivery | `DeviceIoControl(0x12403F)` submits the crafted response into `http.sys` |
| Result | Controlled nonpaged-pool overflow → named-pipe `DATA_QUEUE_ENTRY` corruption → arbitrary kernel read → SYSTEM |

### Post-exploitation toolchain (public, same author)

Once at SYSTEM, the publicly published tooling covers the preservation
needs directly:

| Tool | Use |
|---|---|
| `QuantumTunnelEx` (public repo) | Flash/XBFS dumping at SYSTEM: raw NAND image, or individual FlashFs files (cert keys, `system.vbi`) |
| `xcrdutil` | XVD mount/extract at SYSTEM (used to pull `system.vbi`) |
| `very_based_interface` (`vbidump`) | VBI parser — extracts SystemOS kernel binaries from the dumped VBI for offline analysis |
| `CikExtractor` / this repo's patches | The licensing/CIK pipeline — identical downstream of SYSTEM regardless of entry path |

### License-refresh feasibility on this path

With SYSTEM on a vulnerable build, the license-refresh loop is
structurally identical to the Collateral Damage pipeline — the only
difference is how SYSTEM was reached:

1. Stay online through the MITM-gated network posture (update surfaces
   blocked, auth/licensing passed — see
   [references/console-dns-allowlists.md](../../references/console-dns-allowlists.md)).
2. Sign in and install-then-cancel each owned title — forcing the licensing
   service to write the `Device`-type SP license and packed CIKs into
   registry clip storage.
3. Run the CIK extraction tooling to dump the device key and per-title CIKs.
4. Decrypt on the PC with XVD tooling.

Two caveats carried over from the general analysis: SYSTEM alone does not
create licenses (it only exposes what the licensing service has written —
the internalization still requires the online window), and the additive
flash-dump capability should be used to archive the console's full flash
state *before* any risky online operation.

## Feasibility assessment

**Entry-path independence: high.** No game, no disc, no license, no store
app. The trigger is a usermode process on the console talking to the local
kernel driver. This is the decisive advantage over the primary chain — it
does not depend on the retail title layer at all, so it survives title
revocations and license losses that would kill Collateral Damage.

**Build-window risk: the defining constraint.** The vulnerability is
confirmed on 10.0.26100.8866 and patched in August 2026 servicing. The
useful window is therefore "post-July-2026 retail build, pre-August-2026
patch." Worse, from our position: Console A is frozen two years *behind*
that window, and Xbox update architecture is monotonic — reaching the
vulnerable build requires passing through 10.0.25398.4910, which kills
Collateral Damage. The two paths cannot coexist on one console today.
Reaching the July 2026 branch means accepting the primary chain's death on
that console first.

Whether the legacy `http.sys` path exists (and is vulnerable) on the older
25398 branch is an open question — the code is from the same era and the
legacy path is the reachable one on SystemOS, but this has not been tested
and is not confirmed by any public source. If true, the picture improves
dramatically: both chains could coexist on the frozen console. This is the
single highest-value open question.

**Dev Mode dependency: medium risk.** The confirmed public PoC requires
retail Developer Mode. That has three consequences:

1. It is a *prerequisite, not a bypass* — a console without Dev Mode
   activated cannot use this path as publicly documented.
2. Dev Mode activation is a one-time online handshake. If Microsoft ever
   gates or retires retail Dev Mode activation, new activations stop, and
   existing consoles keep whatever they already have. A console activated
   today retains the surface; one never activated may never get it.
3. Dev Mode is itself a Microsoft-controlled surface: it could be patched,
   gated behind newer endpoints, or restricted by policy on future builds.
   Any migration plan that ends "update, re-enable Dev Mode, exploit"
   assumes step 2 keeps working indefinitely — that is an assumption, not a
   guarantee.

**Tooling maturity: high.** The public researcher demonstrated the full arc
on the exact build class in question — entry, SYSTEM, flash dump, kernel
binary extraction — using a coherent public toolchain (PoC, flash dumper,
VBI unpacker). The engineering risk is not "does the exploit exist" but
"port/cross-compile the published PoC to our toolchain and console,"
rated low-to-medium in the public record.

**Post-exploitation continuity: excellent.** Everything downstream of
SYSTEM is identical to the Collateral Damage pipeline — the licensing
internalization flow, registry clip storage, CIK extraction, XVD tooling.
The only difference is how SYSTEM was reached. The flash dumper is purely
additive: a full XBFS/NAND archive before risky online operations reduces
brick risk, which the primary chain does not offer.

**Net assessment:** feasible as a documented fallback with two genuine
risks — the build-window reachability problem (monotonic updates force
sacrificing the primary chain to reach it) and the Dev Mode dependency
(a Microsoft-controlled activation surface). It is not a drop-in
replacement; it is a lifeboat with a boarding cost.

## What was verified

- The vulnerability is publicly catalogued (CVE-2026-62735, ZDI-26-536)
  and independently reproduced on retail hardware by the public researcher.
- The confirmed retail build (10.0.26100.8866, July 2026), the Dev Mode
  prerequisite, and the resulting SYSTEM-level access are stated in the
  public record (xboxoneresearch wiki issue and the researcher's published
  artifacts).
- The patched dev-kit build is documented as **not** exploitable — the
  retail-only character of the path is part of the public record.
- The post-exploitation toolchain (flash dump, cert-key extraction, VBI
  unpacking of SystemOS kernel binaries) is demonstrated in the public
  artifacts at SYSTEM level on the same build.
- Our own console's position relative to the vulnerable window, and the
  monotonic-update obstacle between the two epochs, are established from
  the build matrix compiled in this repo's source reports.
- Downstream pipeline equivalence (license refresh, CIK extraction) is
  structurally identical below SYSTEM, per our own primary-chain
  documentation.

## What was not

- **Not tested on our console.** Console A sits on 10.0.25398.4908, outside
  the confirmed vulnerable window, and must not be updated (Safety model).
  Nothing here has been executed on our hardware.
- **Presence on the 25398 branch is unknown.** Whether the legacy
  `http.sys` path on the older branch carries the same bug is unverified —
  the highest-value open question (see above).
- **Dev Mode availability on Console A.** The primary-chain console was
  configured without Developer Mode. Whether Dev Mode can be activated on
  it *now*, at its frozen build, is untested — and activation may have
  build-dependent server-side requirements.
- **Dev Mode on post-July-2026 builds.** Whether activation and
  functionality remain available on future retail builds is unknown; the
  public record does not answer it.
- **Series S/X reachability.** Unlikely to matter for this project (we hold
  Xbox One-class hardware), but publicly untested.
- **Offline operation.** The trigger is local loopback in principle and may
  work with networking fully mediated, but this is unconfirmed.
- **End-to-end license refresh on the new build.** Whether the July 2026
  branch's licensing stack still uses the same clip/CIK internalization
  path is unverified; the kernel/VBI dumps from the vulnerable build are
  the right first artifact to inspect for that.

## Safety model

The safety model for this path is the same one governing the primary
technique — read it in full at
[techniques/eraos-code-exec](../../techniques/eraos-code-exec/). The rules
that bear directly on this path:

- **Never trigger OTA system updates.** This path's biggest temptation is
  also its biggest hazard: reaching the vulnerable build means updating,
  and updating is forbidden without explicit, deliberate decision. Any
  probe, script, or tool that could plausibly kick off an update is read
  first and confirmed safe or not run.
- **Preserve the primary anchor.** Console A stays frozen at
  10.0.25398.4908. Losing the entry app or its cached license is
  unrecoverable; nothing in this path's evaluation may put that at risk.
- **Back up before touching.** Flash, hive, and settings captures are
  archived before any state-changing operation. The additive flash-dumping
  capability of the post-exploitation toolchain exists precisely for this.
- **Own hardware, own licenses.** This is preservation research on consoles
  and entitlements we own. Nothing is distributed; nothing leaves the
  research environment except analysis findings.
- **Private data never leaves the workspace.** Hive dumps, flash captures,
  and console logs are PII/secrets — processed locally, never committed,
  never included in output documents. Identity data for third parties is
  likewise out of scope and out of the repo.

## Next steps

1. **Determine whether the legacy `http.sys` path on the 25398 branch is
   vulnerable** — source-code-level analysis against the public patch
   description and diff of the affected routines. If yes, the frozen
   console gains a second, app-independent SYSTEM vector without any
   update, which changes the risk posture entirely.
2. **Assess Dev Mode activation on Console A at its current build** —
   documentation-level first (what the activation handshake requires on
   25398-era builds), runtime testing only if it cannot disturb the
   primary anchor.
3. **Acquire and archive the public artifacts** (PoC repository, flash
   dumper, VBI unpacker) into the private mirror, before they can be
   taken down — the public record itself flags takedown risk.
4. **Inspect the published kernel/VBI dumps from the vulnerable build** for
   the licensing-stack question: does the July 2026 branch still use the
   clip → registry internalization path our CIK pipeline depends on.
5. **Maintain the migration plan** (update → Dev Mode → exploit → resume
   pipeline) as a documented, gated last resort — explicitly conditioned
   on Dev Mode still being activatable at that future date, and explicitly
   requiring the loss of the primary chain on that console as an accepted,
   irreversible cost.
6. **Track the patch horizon.** The August 2026 servicing marks the end of
   the confirmed window. Any console we might migrate must be assessed
   against what is actually still reachable on the update path at the time
   of migration — the window is not static.

## References

- Microsoft Security Response Center — CVE-2026-62735:
  <https://msrc.microsoft.com/update-guide/vulnerability/CVE-2026-62735>
- Zero Day Initiative advisory ZDI-26-536:
  <https://www.zerodayinitiative.com/advisories/ZDI-26-536/>
- xboxoneresearch community wiki — retail Xbox One confirmation thread:
  <https://github.com/xboxoneresearch/wiki/issues/142>
- Public researcher artifacts (`xbdevv` org on GitHub):
  - PoC source and crash logs: <https://github.com/xbdevv/https.sys>
  - Flash dumper: <https://github.com/xbdevv/QuantumTunnelEx>
  - VBI parser (`vbidump`): <https://github.com/xbdevv/very_based_interface>
- Independent analysis write-up: <https://hackmd.io/@nhh/Hy6Oem7_Me>
- Deeper analysis in this repo:
  [references/quantumtunnelex-analysis.md](../../references/quantumtunnelex-analysis.md)
- Primary technique and safety model:
  [techniques/eraos-code-exec/README.md](../../techniques/eraos-code-exec/README.md)
