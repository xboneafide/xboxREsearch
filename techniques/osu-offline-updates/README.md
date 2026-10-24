# Offline System Update (OSU) as the sanctioned build-movement mechanism on Xbox One-family consoles

*Public research draft — methodology and observations only. Written from work on privately owned consoles, on private media, with licensed content. Nothing here is distributed or reproduced from copyrighted material; no update packages, binaries, or images are included or hosted in this document. Build numbers, error codes, and file names are cited as observed factual data points.*

## Summary

The Offline System Update (OSU) USB path is the only mechanism we treat as sanctioned for moving a console's OS build: it is a fully offline, user-initiated reflash from a USB stick, and it never triggers or invites an over-the-network (OTA) system update. This writeup documents an end-to-end ladder climb between branch families (Cobalt → Nickel → Zinc → Germanium/24H2) performed entirely via USB OSU, the decode of the `E101 90070007 8B05000F` failure that started the effort, and the `consoles.txt` current-build trick used to read a console's OS build at cold boot — including from a console stuck before first-run setup, where no dashboard exists to read the build from. The central finding: the E101 failure we hit on a very-old-to-very-new single hop was not package corruption but a **version-relationship validation** — the updater checks the gap between the current OS and the package, and a multi-branch jump in one hop is rejected. Splitting the movement into small hops, one branch boundary at a time, succeeded on every crossing.

## Environment (generic)

- **Console A** — One-family console, device class "Scorpio-generation". Frozen at build **10.0.25398.4908** by design; it is *not* part of any ladder work and is only mentioned here as context for why build ceilings matter. (Stating the build number is not an identifier.)
- **Console B** — One-family console, same device class as Console A. The OSU ladder test subject. Started on build **10.0.22000.4158** (branch string beginning `xb_flt_22…`, 2022-04), was stuck before first-run setup (OOBE) at the start of the work, so its build could not be read through any dashboard UI.
- A PC with the ability to format an NTFS USB stick, write files to it, and read files back off it.
- A USB stick formatted NTFS (stick brand changes are a documented community fix for finicky sticks; we did not need one, but note it as a mechanical variable).
- Per-version OSU1 packages sourced from the Xbox One Research community wiki's firmware table, which indexes per-build Microsoft-hosted `OSU1.zip` packages (official `download.microsoft.com` links; an archive mirror exists for the 2013–2014 era). Links rot as Microsoft rotates them — always re-check the wiki table for a live link before assuming a build is unobtainable.

**Safety posture (stated explicitly):** every procedure below is USB-offline only. At no point was a network system update triggered, accepted, scheduled, or allowed to download. Wherever the console's own online update check appeared during this work, it was refused or blocked (see "What did not"). Per-console rule of this research: the USB OSU path remains the *only* sanctioned build-movement mechanism, and any target build reachable only over the network is not taken until a USB package for it exists and has been evaluated. Additional standing caution: every OSU attempt carries a failed-update error-loop risk — do not retry repeatedly from a cold state, and treat hardware-class errors (E100-family) as a stop sign, not a retry prompt.

## Chain (as actually performed)

1. **Attempted a single-hop USB OSU from a very old build toward a much newer package.** Prepared an NTFS stick with the `$SystemUpdate` folder at the stick root and the OSU1 package files *inside* that folder. Ran the OSU from the console's recovery/offline-update flow. Result: error screen `E101 90070007 8B05000F` (see decode below).
2. **Ruled out package integrity as the cause.** Cross-referenced the error with community reports of the *identical* code occurring in the opposite direction (a package *older* than the console's OS also producing `E101 90070007 8B05000F`). Conclusion: validation compares the version relationship between current OS and package, not just package bytes.
3. **Read Console B's exact build without any dashboard**, using the `consoles.txt` trick (see below): full power off (hold the power button ~10 s, briefly unplug power), insert the NTFS stick with `$SystemUpdate\consoles.txt` empty, boot, wait a minute at the stuck OOBE screen, power off, read the file on the PC. It now contained the current system update build in plaintext: **10.0.22000.4158**.
4. **Built a hop ladder from the community firmware table.** From 22000.4158 to the target zone (26100.x), crossing each branch boundary as its own hop:
   1. 22000.4158 → **22000.4558** (same Cobalt branch, small warm-up step)
   2. → **22621.3680** (Cobalt → Nickel branch crossing; several Nickel builds had live links — 22621.2859/2864/3680/4912/4918 — pick the nearest, fall back down-list if a hop is rejected)
   3. → **25398.2538** (Nickel → Zinc crossing; fallback to 25398.3864 if rejected)
   4. → **26100.1968** (Zinc → Germanium / the 24H2 bump — historically the hardest boundary)
   5. → **26100.7018** (within Germanium, 2026)
   6. → **26100.8866** (stop point — see "What worked")
5. **Executed the ladder one hop at a time**, with verification after every hop: apply OSU from USB → boot → record the new build via `consoles.txt` → confirm the console is healthy → only then proceed to the next hop. Each hop's package was fetched ahead of time from the wiki table's Microsoft links and pinned to disk, since links are known to rotate.
6. **Stopped at a known ceiling rather than continuing upward.** The ladder deliberately ends at 26100.8866: a later mandatory build (26100.9438-class) exists, but the research target on this console (a documented SystemOS privilege-escalation chain, confirmed working on 8866 by the community wiki) is believed patched on newer builds — unverified, but the risk of overshooting is irreversible, so we did not climb past the last build where the chain is known to work.

## The `consoles.txt` current-build trick

Documented on the Xbox One Research wiki's NTFS-USB-overrides page. Placing an **empty text file named `consoles.txt`** inside a `$SystemUpdate\` folder at the root of an NTFS USB stick makes the console **write its current system update build version into that file, in plaintext, at cold boot**. Properties that make it broadly useful:

- It is a **boot-time** override, handled during the console's boot routine — no network, no dashboard, no dev-mode certificate required.
- Output is unencrypted plaintext, readable on any PC.
- It works even from a console **stuck at OOBE / "Something went wrong" screens**, where no dashboard exists to read the build from — which is exactly the situation after a failed or interrupted OSU, and the situation in which you most need to know the exact build before choosing the next package.

Procedure (as performed):

1. Full power off: hold the console power button ~10 s until shutdown; briefly unplug power.
2. NTFS stick with `$SystemUpdate\` folder at root and an **empty** `consoles.txt` inside the folder.
3. Boot with the stick inserted; give it a minute at whatever screen it parks on (OOBE in our case); power off again.
4. Read `consoles.txt` on the PC — the current system update build is now in the file.

This made it possible to (a) record Console B's true starting build before designing the ladder, and (b) verify the build after every hop, before allowing the next one.

## Update-package structure as observed

- **Folder layout is strict:** the `$SystemUpdate` folder must sit at the **root** of the NTFS stick, with the update files **inside** the folder. Files loose at stick root are simply not seen by the One-era updater, which presents as a no-update / E101-class failure rather than a helpful message. If copies exist both at root and inside the folder, the folder copy is the one used and root copies are ignored — harmless but wasteful, and worth avoiding because two versions of a package on one stick is an easy mistake to make.
- **One package family per console generation:** One / One S / One X (the "Scorpio" class included) all consume the XB1-family `OSU1.zip` package with the same procedure. The Series X|S OSU package is a different generation and will not validate on a One-family console (generation mismatch presents as E106-class / validation errors).
- **Package metadata lives outside the package on the console side:** the console's flash carries `update.cfg` / `update2.cfg` records listing the *installed* SystemOS and GameOS builds plus a branch string; `update2.cfg` is empty when no second (staged) build is present. Nothing newer was ever found staged in flash after our OSU operations — USB OSU installs commit; they do not leave a staged copy. (We confirmed this by reading those flash records on a different console of ours via its flash-filesystem device interface; a failed OSU likewise left no partial/staged build behind.)
- **Validation is version-aware, not just integrity-aware** (the core E101 lesson): the same error triad appears for a package *older* than the current OS and — per our experiment — for a package *too far newer* in one hop. The updater trusts intermediate hops.

## E101 90070007 8B05000F — decode

Observed on screen after the failed single-hop attempt. Community-corroborated across Microsoft support pages and repair communities:

- **`E101`** — failure in the offline-system-update **install path** (software side: the updater ran and the package was rejected). The E100-family generally maps to hardware concerns, but E101 specifically indicates the software/update path; the discriminating information is the middle sub-code. `8B05000F` recurs alongside E100/E101/E106 in nearly all OSU failure reports.
- **`8B05000F`** — generic "update package validation/install failed". Microsoft's own support page for it says only "we're investigating"; community consensus is package validation.
- **`90070007`** — stage sub-code in the `0x80070007`-class family: invalid/corrupt data — the package failed verification *at that stage*. Sibling sub-codes seen in the wild at different stages: `90060007`, `900B0007`, `0000070C`.

**Key data point:** community reports show the *exact same triad* when the offered OSU1 package was **older** than the console's OS. Combined with our result (package much newer, same triad), the conclusion is that validation checks the **version relationship between the current OS and the package** — not merely package integrity. A multi-branch jump in a single hop is rejected the same way a backwards package is.

## What worked

- **The hop ladder, end to end.** Console B climbed from 10.0.22000.4158 (2022, Cobalt) to a 26100.8866-class build (2026, Germanium/24H2) entirely via USB OSU, crossing three branch boundaries (Cobalt→Nickel, Nickel→Zinc, Zinc→Germanium) plus the 24H2 bump without a single rejection once hops were kept small.
- **`consoles.txt` as a build oracle.** Worked from a console with no dashboard, stuck at OOBE, fully offline, at cold boot, no dev-mode access, output in plaintext. It both established the starting build (10.0.22000.4158) and served as the per-hop verification mechanism.
- **Strict `$SystemUpdate` folder layout.** Package inside the folder at NTFS stick root, every time, first try.
- **Pinning packages.** Downloading each hop's OSU1 from the wiki table's Microsoft links *before* starting the ladder avoided any mid-ladder link rot.
- **Per-hop verification discipline.** Apply → boot → read build via `consoles.txt` → verify health → next hop. No hop ever left the console in a state we could not recover from.
- **Stopping at a known ceiling.** The 26100.8866 stop point was chosen from community-verified exploit-survival data, not aspiration — see below for why the next build was deliberately not taken.

## What did not work (exact error codes)

- **`E101 90070007 8B05000F`** — the original single-hop attempt, very old build → newest available package. Cause: version-jump validation, not corruption. Fixed by the ladder.
- **`0x80072EFD`** — WinInet `WININET_E_CANNOT_CONNECT` (TCP-level connect failure), seen at the OOBE/connectivity step while the console was under our network-blocking posture. Not a package problem: the OOBE flow demands a connectivity check before/at OSU selection and the console could not phone home. Community decode family: 72EFD = connect failure.
- **`0x80072EE2`** — WinInet `WININET_E_TIMEOUT` (connect + retry timeout), the next failure in the same flow under the same posture. Same meaning: "no route", not validation.
- **`0x80072F8F`** — WinInet TLS security error (certificate validation failed) at OOBE on a direct connection. Two candidate causes, checked in order: (1) TLS interception in the path — a console that has never completed OOBE has no shell to install a custom CA into, so interception is unavailable pre-shell and, if present, *causes* this error; (2) the console clock — a console that has been offline/powered down for a long time fails every certificate on validity windows. Mitigations: plain DNS/firewall blocklisting instead of interception, and allowing NTP (UDP 123 time endpoints) or setting the clock manually at the region/locale step.
- **OOBE without connectivity did not complete.** An NCSI-spoof experiment (DNS-redirecting the connectivity-probe host to a local server) got the probe answered but the flow still ended in the timeouts above — the OOBE connectivity gate is deeper than the NCSI probe. On the builds we handled, no offline-OOBE path was offered.
- **The mandatory-update gate family, observed but refused:** `0x8B0500B6` ("mandatory update could not be applied" — this screen does offer *Retry / Check network / Skip update and stay offline / Power off*; the skip option is the sanctioned offline exit), then `0x8B050084 0x00000000 0x00000201` ("update required but could not be obtained", sub-code `0x201` = download stage — this screen offers only try again / check network / turn off). Also seen in related testing: `0x8B050033`, `0x8B05000C`, `0x8923000D`, `0x83750009`. Every one of these states was treated as a stop-and-refuse, never as a reason to grant network updates.

## Open questions / next steps

1. **Is the newest build (26100.9438-class) actually past the privilege-escalation patch?** Community sources say the chain in question "most likely" does not work on the latest update, but this is *unverified* — no public confirmation exists for that specific build. If a future check confirms it still works, a USB package for that build (once the wiki table lists one) would make the final hop takeable under our safety posture. Until then, we do not climb past 8866.
2. **Exact validation rule for "too large" jumps.** We know a multi-branch single hop fails with E101 90070007 8B05000F and same-branch small steps succeed, but the precise distance/threshold (build delta? branch boundary? manifest field?) is uncharacterized. Systematic single-hop probes from a sacrificial known-state console would pin it down — each probe carries failed-update risk, so this needs careful design.
3. **Same-build reflash semantics.** Whether re-flashing the *current* build's OSU1 package from the recovery/troubleshooter flow clears enforced-update state is community-precedented but untested here; rejection is expected to be benign (E101-class, not a brick), since the flow does not proceed without package validation.
4. **OSU-from-OOBE-troubleshooter vs. OSU-from-recovery.** Microsoft documents running OSU from the "Something went wrong" / OOBE troubleshooter screens; we used that path, but the exact differences in preconditions and connectivity-check behavior versus other recovery entry points deserve a comparison writeup.
5. **Sub-code taxonomy.** The stage sub-codes (`90070007`, `90060007`, `900B0007`, `0000070C`) clearly index *where* validation failed, but no public mapping of stage ordinal → meaning exists. Collecting community reports keyed by sub-code could reconstruct it.
6. **`consoles.txt` scope.** We used it for build reads; whether the same override mechanism supports other boot-time behaviors on modern builds is undocumented and worth probing read-only.
7. **Link-rotation cadence.** Per-build Microsoft OSU1 links rot as Microsoft rotates them. Pinning before a ladder is now standard practice for us; a snapshot/mirror discipline for the community wiki table would be a useful contribution.

---

*Again on posture: nothing in this document describes or performs an over-the-network system update; the USB offline path is used exclusively, and network update surfaces were refused or blocked throughout. No update packages or binaries are included, linked in redistributable form, or hosted by this repository — per-build package locations are maintained by the community wiki referenced above, from official vendor links.*
