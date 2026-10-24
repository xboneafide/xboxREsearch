# Xbox Disc vs. Digital License Models: Volume-Bound Disc Licenses, the Missing Device License, and the One-Bay Problem

## Summary

On a Development Mode-adjacent retail System OS with a research shell, the
licensing model for owned discs and digital entitlements was mapped end-to-end.
Disc-class base licenses were found to be bound to the optical source at load
time: a successfully loaded disc license refuses to load when the identical
file is presented from fixed storage, with facility error `0x8B100041`, and the
base license never persists to the console's clip store. Separately, the
registry SP-license blob store used for device-key derivation was proven empty
across the live registry and all exported hives — the console licensing service
never internalized its CLiP-format licenses into SP blobs, so device-key
derivation fails with `No device license found`. Together these results mean
disc content cannot be dumped via a shell host holding its own disc (the
"one-bay problem"), and the only identified route to device-key provisioning is
a controlled online window in which the licensing service performs its sync.

## Environment

All work was performed on consoles the researcher owns, in a private lab. Device
details are described generically:

- **Console A** — retail Xbox One-family console, System OS build 10.0.25398.x
  (mid-2024-era), permanently maintained offline or behind a fully controlled
  network path. Access is via a SYSTEM-level remote shell obtained through a
  published game-script entry chain, with tooling staged on a USB drive mounted
  as `D:\`. This console holds the installed exploit host game.
- **Console B** — second retail console of the same family, used for
  comparison of license-state enumeration and network experiments.
- PC-side tooling: .NET 6 rebuilds of public open-source utilities
  (CikExtractor, XVDTool), PowerShell analysis scripts, and a local MITM
  listener used only in separately documented network sessions (out of scope
  here except where it explains licensing-service behavior).

No titles were redistributed; every package and license discussed corresponds
to a disc or digital entitlement owned by the researcher.

## Chain

The following is the end-to-end sequence as actually performed, on Console A
unless noted:

1. **Establish the SYSTEM shell offline.** Boot the exploit host game, run the
   game-script entry chain to a SYSTEM PowerShell context, and stage the
   session script (`run2.ps1`) plus the .NET 6 runtime from `D:\`. All
   PowerShell is run in-process (no child `pwsh`) per shell-survival rules.
2. **Insert a disc and let the license land.** With the console fully offline,
   insert a retail disc and wait for the game tile on Home. The disc's license
   is loaded into the live licensing state at this point.
3. **Dump the encrypted package.** Mount the disc's content volume and copy the
   encrypted XVC package(s) to `D:\` for later PC-side processing. Eject and
   repeat per disc.
4. **Enumerate the license state.** Export the console's license-state
   document (`D:\LicenseState.txt`) and classify every entitlement by kind.
   Result: 43 `Device`, 2 `Open`, 2 `SubscriptionDevice` entitlements. Note
   that `Device` here is the LicenseState kind for content entitlements, not
   the SP-license `Device` type used by device-key derivation (LicenseType 3).
5. **Copy the CLiP license files.** Export the license clip files
   (`D:\Licenses\*`, mirrors of `S:\clip` on console) — 50 XML files.
6. **Attempt device-key derivation offline.** Run the CIK extraction tool's
   `dump` command against the live registry. It loads the registry, then fails:
   `ERR: No device license found.` / `ERR: Failed to dump device key
   parameters.`
7. **Decode the CLiP license format.** Parse the 50 clip XMLs with a PC-side
   parser: they are XML licenses (LicenseProtocolVersion 5, `clas/LicResp/v1`)
   containing an SVLicense element ( SocID, KeyId, CustomPolicies as
   base64-JSON, packages) plus a base64 SPLicenseBlock using the CLAS response
   block namespace — *not* the SP-license registry block namespace. All 50 are
   GameContent type; zero Device-type licenses, zero EncryptedDeviceKey
   entries, zero packed CIKs exist at the XML layer.
8. **Patch and rebuild the extraction tool.** Patch the open-source CikExtractor
   locally (RegSaveKeyA P/Invoke with SeBackupPrivilege instead of a `reg.exe`
   shell-out; pre-delete before export; dual-path hive parsing) and rebuild for
   net6. Local commits only; nothing pushed upstream.
9. **Prove the SP blob store is empty everywhere.** Enumerate the clip-storage
   registry root live (exists, zero subkeys — the SP blob key is never created),
   both before and after a successful disc-license load. Grep all five exported
   hives from an earlier registry dump for both storage GUIDs (as UTF-16):
   zero hits. Run the tool's `dump -r` against the exported SYSTEM and OSDATA
   hives: same `No device license found`.
10. **Test disc-license transplant.** Load the disc's license file in place
    (optical source present): success. Copy the identical file to fixed storage
    (`D:\LicensesFromDisc\`) and invoke the same licensing API
    (`LoadLicenseFile` via a compiled helper): failure `0x8B100041` (Xbox
    licensing facility 0x8B1). Diff the clip store before/after the successful
    in-place load: identical — 50 files, GameContent only. The base disc
    license exists only on the optical media.
11. **Map the shell-level mount options.** Attempt direct volume access:
    `xcrdutil.exe` is code-integrity blocked (signature verification), and a
    raw open of the content volume path from the shell returns
    `ERROR_INVALID_NAME (123)` — content paths resolve only from game-context
    processes.
12. **Test the launch-then-swap idea.** Ejecting a disc game's disc mid-run
    suspends the process, killing any in-game script host. Concluded: no
    offline route exists to dump a second disc while the exploit host's own
    disc occupies the single tray.

## What Worked

- **CLiP license format decode.** The clip XMLs are fully parseable: SVLicense
  wrapper + base64 SPLicenseBlock in the CLAS response block namespace, with
  LicenseState entries recoverable from CustomPolicies. A PC-side parser
  (`parse-splicenses.ps1`) scans the whole clip store.
- **Complete negative proof of SP-blob internalization.** Live registry
  enumeration, five exported hives, and offline hive parsing all agree: the SP
  blob store (where the Device-type SP license with EncryptedDeviceKey would
  live) has never been populated on this console. The licensing service's
  internalization step is the only thing that creates it.
- **Tool hardening.** CikExtractor patched and rebuilt to run in-process under
  SystemOS (native hive export, dual-path parse), with its exact requirement
  decoded from source: exactly one SP license with LicenseType == Device
  containing EncryptedDeviceKey; zero gives `No device license found`, more
  than one gives the "more than one" error.
- **License-state classification.** All owned entitlements enumerated and
  classified (43 Device / 2 Open / 2 SubscriptionDevice), with a reliable
  pattern for extracting XVC package identifiers for cross-reference against
  a title-lookup table.
- **Clean in-place disc-license load.** With the disc present, the licensing
  API accepts the disc license — confirming the loader itself works and that
  the failure is source-binding, not file corruption.

## What Did Not (with exact error codes)

- **Device-key derivation offline** — `ERR: No device license found. ERR:
  Failed to dump device key parameters.` The console has no Device-type SP
  license in registry clip storage; the licensing service has never performed
  its internalization sync on this console.
- **Disc-license transplant from fixed storage** — `0x8B100041` (licensing
  facility 0x8B1). The loader requires the optical source for disc-class
  licenses. Anti-piracy by design; not a filesystem or API-usage problem.
- **Persistent base disc license** — none. Clip store before/after a
  successful disc load is byte-identical (50 GameContent files); the base game
  license never lands in `S:\clip`. The in-shell license list only enumerates
  LicenseState entries, so base disc games never appear (DLC only).
- **`xcrdutil.exe` from the shell** — code-integrity blocked ("cannot verify
  digital signature"), both directly and via `cmd /c`. Other system binaries
  run; this one is walled.
- **Raw content-volume open from the shell** — `ERROR_INVALID_NAME (123)`.
  `XUC:` content paths resolve only from game-context processes; the path
  context, not the API, is the blocker.
- **Launch-then-swap dumping** — ejecting the host game's disc mid-run
  suspends the process and stops its script host. Not viable.
- **Background (scheduled-task) reverse shell** — connects then dies when the
  msbuild parent exits and tears down the socket. Not a persistence mechanism
  as built.
- **WMI SMBIOS probe** — `root\WMI` connects but `MSSmBios_RawSMBiosTables`
  returns zero rows (OneCore has no SMBIOS provider), so hardware identity
  inputs for device-key derivation cannot be read this way.

## Open Questions / Next Steps

- **Does an online licensing-service sync populate the Device SP license?**
  The working hypothesis is yes: a controlled online window (update-gated,
  MITM-audited, per the repo's safety model) should let the licensing service
  internalize CLiP licenses into SP blobs and provision the Device license.
  After that window, re-run the CIK dump offline and verify the blob store is
  populated.
- **Offline import path.** The community-documented "adding games to an
  offline console" technique imports license files into clip storage —
  potentially the same internalization problem from a different angle. The
  referenced wiki page is access-blocked from this network; obtain and evaluate
  the procedure details.
- **What exactly satisfies the Device-license lookup beyond LicenseType?**
  Source review says exactly one LicenseType-3 SP license with
  EncryptedDeviceKey; whether the service will re-provision it on a console
  whose entire blob store is empty (vs. merely refreshing an existing one) is
  untested.
- **Disc entitlement via the digitize route.** A separately documented
  disc-to-digital protocol converts a pressed disc into a digital entitlement
  server-side (the disc's BCA region is the anti-forgery anchor). If a
  disc-class license can be replaced by a digital entitlement, the volume-bound
  restriction becomes irrelevant for that title. Whether the resulting digital
  license internalizes normally on an online window is the key follow-up.
- **Persistent shell for long sessions.** The background-shell teardown needs
  a fix (payload retained for debugging); long licensing-sync observation
  sessions will need it or an equivalent.
- **Cross-console comparison.** Confirm on Console B whether the SP blob store
  is populated on a console that has completed a normal online licensing sync,
  to validate the internalization hypothesis directly.

---

*Methodology note: this document describes research on hardware, discs, and
licenses owned by the researcher, conducted entirely offline or under a
controlled network path. No packages, keys, or license blobs are included
here; identifiers are redacted per the repo's scrub rules.*
