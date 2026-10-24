# QuantumTunnelEx (CVE-2026-62735): the Xbox-side Entry Vector, in Depth

## Summary

CVE-2026-62735 — community name **QuantumTunnelEx** — is a 2026 `http.sys`
kernel vulnerability (integer overflow in response-header size accounting)
that was independently reproduced on **retail Xbox One hardware** by a
public researcher, yielding `NT AUTHORITY\SYSTEM` in SystemOS on a
post-Collateral-Damage build (10.0.26100.8866, July 2026). The companion
writeup [techniques/devmode-secondary-entry](../../techniques/devmode-secondary-entry/README.md)
assesses it as a *feasible secondary entry path*. This reference document
goes deeper: the vulnerability class as documented in the public record,
the build matrix across Windows and Xbox branches, the retail-vs-devkit
split, why the legacy `http.sys` code path is the one that matters on
SystemOS, the open question of presence on the 25398 branch, and the
public record's published artifacts.

**Scope note:** this document stays at the analysis level. It contains no
reproduction steps, no PoC code, and no memory-layout detail beyond what
the public record states in summary. Reproduction detail belongs to the
public CVE record and the researcher's own disclosures (see References).

## Vulnerability overview

| Field | Value |
|---|---|
| CVE | CVE-2026-62735 ("QuantumTunnelEx") |
| Advisory | ZDI-26-536; Microsoft SRD catalog entry |
| Original discovery | Siyeon Wi, Pwn2Own Berlin 2026 |
| Independent Xbox reproducer | Public researcher (GitHub org `xbdevv`) |
| Component | `http.sys` (Windows kernel HTTP driver) |
| Class | Integer overflow in header-size accounting → undersized nonpaged pool allocation → controlled pool overflow |
| Windows patch | August 2026 Patch Tuesday |
| Confirmed on Xbox | Retail Xbox One (Durango), retail Developer Mode, build 10.0.26100.8866 |
| Public result | SYSTEM in SystemOS; flash and VBI dumps published |

At the class level: the legacy `http.sys` response path sums several
header-byte contributions into a single total. The attacker-controlled
contribution can wrap the sum, so the driver allocates a small nonpaged
pool buffer sized against the wrapped total and then writes the *real*
(multi-megabyte) header data past it. The public record describes this
converting into a controlled nonpaged-pool overflow, and via pool grooming
against named-pipe allocations, into kernel read access and a SYSTEM token.

The Windows-side patch adds integer-overflow guards to the affected size
computation and response-construction routines. Crucially for Xbox, the
**legacy** code path — the pre-refactor routines — is the one reachable on
Xbox One SystemOS, so the patched *modern* path is not the surface that
matters on console; the question is whether the legacy routines carry the
bug on a given console build (see Build matrix and Open questions).

## Build matrix

Consolidated from the public record and our own private source reports:

| Build | Branch / servicing | Hardware | Collateral Damage (≤ .4908) | CVE-2026-62735 | Notes |
|---|---|---|---|---|---|
| 10.0.25398.4908 | xb_flt_2405ge, June 2024 | Retail Xbox One | **Works** (our frozen epoch) | Unknown — likely present, untested | Our console sits here |
| 10.0.25398.4910 | xb_flt_2405ge, July 2024 | Retail Xbox One | **Patched** | Unknown | The build that kills the primary chain on any update |
| 10.0.26100.6214 | (dev-kit branch) | Xbox Dev Kit | n/a | **Not exploitable** (per public record) | Dev-kit builds documented as not vulnerable |
| 10.0.26100.8866 | xb_flt_2607ge.260630-2200, 2026-07-15 | Retail Xbox One, Dev Mode | **Patched** | **Exploitable — PoC confirmed** | The researcher's confirmed build |

Two structural facts fall out of the matrix:

1. **The two epochs do not overlap on one console.** Xbox update
   architecture is monotonic: reaching the confirmed-vulnerable July 2026
   build from 10.0.25398.4908 forces passing through 10.0.25398.4910,
   which permanently patches Collateral Damage. Adopting QuantumTunnelEx
   on our console means accepting the primary chain's death first.
2. **The retail/devkit split is part of the public record.** The dev-kit
   build tested by the researcher was not exploitable — a different code
   path or legacy-path state. The entry surface this document describes
   therefore exists on *retail* Developer Mode consoles, not devkits.

## Why it matters for Xbox preservation

- **It decouples SYSTEM access from the retail title layer.** No disc, no
  installed game, no store-app license — the entry is a local usermode
  process talking to a kernel driver. Every risk that threatens the
  Collateral Damage anchor (title delisting, license-cache loss, app
  revocation) simply does not apply.
- **It is a lifeboat for the post-July-2024 epoch.** If a forced update,
  an accidental update, or a future second console ever lands on a build
  past 10.0.25398.4910, the primary chain is gone. QuantumTunnelEx is the
  only publicly documented, independently reproduced SYSTEM vector on the
  July 2026 retail branch.
- **Downstream continuity is total.** Everything after SYSTEM is the same
  pipeline we already run: license internalization via install-and-cancel,
  registry clip storage, CIK extraction, XVD tooling. The only difference
  is how SYSTEM was reached. The public post-exploitation toolchain is
  additionally **additive**: its flash dumper can archive the full XBFS /
  NAND state (raw dump) before risky online operations — a brick-risk
  reduction the primary chain does not offer — and its VBI unpacker
  extracts SystemOS kernel images for offline analysis.
- **It is publicly anchored, so it cannot silently rot.** A CVE number, a
  ZDI advisory, a community wiki thread, and a public artifact archive
  mean the technique's existence and its build window are verifiable
  forever, independent of this repo.

The cost side is equally concrete: the Dev Mode prerequisite is a
Microsoft-controlled activation surface (one-time online handshake,
potentially gated on future builds), and the build-window reachability
problem above means it is not a drop-in replacement today. The full
trade-off analysis is in
[techniques/devmode-secondary-entry](../../techniques/devmode-secondary-entry/README.md).

## Open questions

1. **25398-branch presence (highest value).** Does the legacy `http.sys`
   path on the 25398 branch — the code from the same era, and the reachable
   path on SystemOS — carry the same bug? If yes, both chains could
   coexist on the frozen console with no update at all, which changes the
   risk posture entirely. Unverified by any public source; the action is
   source-level analysis of the console's own `http.sys` against the
   public patch description.
2. **Trigger surface without Developer Mode.** The confirmed PoC requires
   retail Dev Mode. Whether a comparable usermode trigger exists in the
   retail Game OS without Dev Mode is unknown.
3. **Dev Mode longevity.** Whether Dev Mode activation and functionality
   survive on post-July-2026 builds is unknown; any migration plan is
   conditioned on this assumption.
4. **Licensing stack on the July 2026 branch.** Whether clip → registry
   internalization still works the same way on 10.0.26100.8866 is
   unverified; the researcher's published kernel/VBI dumps from that
   build are the right first artifact to inspect.
5. **Offline operation.** The trigger is local loopback in principle and
   may work with networking fully mediated, but this is unconfirmed.
6. **Series S/X reachability.** Publicly untested; not material to this
   project (we hold Xbox One-class hardware) but relevant to the wider
   preservation community.
7. **Offline update path around 10.0.25398.4910.** Believed not to exist
   (monotonic boot-slot enforcement), but it bounds every migration plan
   and is worth keeping on the list.

## References

- Microsoft Security Response Center — CVE-2026-62735:
  <https://msrc.microsoft.com/update-guide/vulnerability/CVE-2026-62735>
- Zero Day Initiative advisory ZDI-26-536:
  <https://www.zerodayinitiative.com/advisories/ZDI-26-536/>
- xboxoneresearch community wiki — retail Xbox One confirmation issue:
  <https://github.com/xboxoneresearch/wiki/issues/142>
- Public researcher artifact archive (GitHub org `xbdevv`): the PoC /
  crash-record repository (`https.sys`), the flash dumper
  (`QuantumTunnelEx`), and the VBI parser (`very_based_interface`):
  <https://github.com/xbdevv>
- Feasibility writeup (companion): 
  [techniques/devmode-secondary-entry/README.md](../../techniques/devmode-secondary-entry/README.md)
- Source reports (internal, private research repo): our own CVE entry-point
  runbook and full vulnerability analysis documents.
