# Unwrapping Xbox One content keys: running CikExtractor on a retail console

## Summary

This writeup documents a research effort to extract per-title content
decryption keys (CIKs, "Content Intermediary Keys") from a retail Xbox One
console the author owns, using the publicly available **CikExtractor**
tool (upstream by LukeFZ). The extraction model is: derive the
console's device key, use it to unwrap the per-package CIKs that the
console's licensing layer stores sealed in the registry, and export them
for offline XVC package decryption on a PC.

Three independent problem classes were worked through:

1. **The .NET loader problem** — the tool's managed assemblies would not
   load from any on-console path; root cause is a filesystem
   execute-mapping restriction on the console OS, not the usual
   environment suspects. The working solution is byte-loading the
   assemblies fully in memory, which is in fact the same sanctioned
   technique the console's own deployment mechanism already uses.
2. **The license-format problem** — after the tool ran, it reported no
   device license. Decoding the on-disk license format showed why: the
   console's stored clip licenses use a *different* block format than
   the registry "SP license" blobs the tool consumes, and the console's
   internalization step (which converts one to the other) had never
   populated the registry store on this console.
3. **The provisioning problem** — the Device-type SP license that
   anchors device-key derivation is created only by the console's
   online licensing-service sync. Disc licenses are volume-bound and
   never persist to the clip store, so no purely offline route reaches
   device-key derivation on this console. The online license-refresh
   window is the remaining path; it is also the step that makes the
   entire one-session extraction plan (one sync, then permanently
   offline) work as designed.

Everything below is methodology from research on owned hardware and
owned licenses. No proprietary content, key material, or device
identifiers are reproduced.

## Environment

- **Console:** retail Xbox One-class console running a 25398-family
  SystemOS build, at an arbitrary shell (SYSTEM-equivalent PowerShell)
  reached through the author's own exploit chain on their own console.
- **Console runtime:** a bundled .NET runtime (6.0.32) deployed to the
  console's scratch volume, plus PowerShell.
- **PC side:** Windows, .NET 6 SDK for rebuilds, an offline hive parser
  (`Registry.dll`) for post-mortem analysis of exported hives.
- **Storage:** a FAT/exFAT thumb drive (`D:`) readable by both console
  and PC, used for tool staging, license captures, and hive exports.
- **Privacy posture:** all identifiers (serials, device IDs, account
  data, drive serials) are excluded from documentation. Where an
  identifier shape is illustrative it is written as an X-filled
  same-length placeholder marked `# redacted`.

## Tool overview

CikExtractor (public upstream; the patched source accompanying this
writeup lives in this repo, retargeted from net10.0 to net6.0 to match the
console's bundled runtime) is a small Spectre.Console CLI with four
commands:

| Command | What it does |
|---|---|
| `dump` (default) | Load SP licenses (from the live registry or an exported hive with `-r`), derive the device key (or load it from `-d`), unwrap every packed content key, write `<guid>.cik` files |
| `export-hive` | Export the registry subtree holding the license blobs to a file |
| `export-params` | Export the device-key derivation inputs (SMBIOS tables, drive serial, encrypted license blob) as JSON |
| `derive` | Derive the device key offline from exported parameters |

Internal notes worth recording:

- **Registry access.** The upstream tool shelled out to `reg save`. On
  the console SystemOS `reg.exe` is not a reliable presence, so the
  vendored version P/Invokes `RegSaveKeyA` directly after acquiring
  `SeBackupPrivilege`. Two console-specific patches were needed:
  (a) `RegSaveKeyA` cannot overwrite an existing file — the temporary
  file pre-created by `GetTempFileName()` must be deleted first, or the
  export fails with `ERROR_FILE_EXISTS`; (b) the hive parser must
  handle both a Control-subtree export and a full SYSTEM hive (applying
  a `ControlSet001\Control\` key prefix) depending on what was exported.
- **Device-key derivation.** From the SP license's `EncryptedDeviceKey`
  blob, the tool reconstructs an AES key schedule by XOR-combining
  selected round-key words with fixed constants (a documented
  key-schedule XOR trick), then AES-ECB decrypts the 16-byte device key
  and sanity-checks the result by re-encrypting. All inputs are
  console-unique; the math itself is offline-friendly.
- **What the tool requires to say "yes":** the loaded license set must
  contain **exactly one** SP license with `LicenseType == Device`
  (enum: 0 Unknown, 1 App, 2 Lease, 3 Device, 4 Dev, 5 Lob, 6 Upgrade)
  carrying an `EncryptedDeviceKey` block. Zero of them → "No device
  license found"; more than one → "More than one device license
  found". Every other SP license's `PackedContentKeys`
  (GUID → AES-WRAP'd CIK) is then unwrapped with the derived device
  key.
- **Registry layout.** SP license blobs live under
  `HKLM\SYSTEM\ControlSet001\Control\{7746D80F-97E0-4E26-9543-26B41FC22F79}\{A25AE4F2-1B96-4CED-8007-AA30E9B1A218}`
  (the clip-storage root and license-blob-storage subkey; sibling GUID
  keys for content-key-id/policy/entry-id storage exist and are part of
  the same facility). These GUIDs are format constants, not device
  identifiers.

## Chain

The intended end-to-end pipeline (planned before any console step):

1. **Console, once, online (~seconds per title):** one license-sync
   window under a strict update-blocking posture — the licensing
   service activates all owned digital titles and internalizes their
   licenses into the registry SP store, including the Device license.
2. **Console, offline:** insert any owned discs (license activates
   instantly and offline); run the extractor (`dump`) or the
   offline-friendly pair (`export-hive` + `export-params`).
3. **Transfer:** hive + parameters + captured clip license files move
   to the PC over the thumb drive or a TCP file-transfer helper.
4. **PC, offline forever after:** `derive` the device key from
   exported parameters; `dump -r <hive>` unwraps every CIK; each CIK
   (16 bytes, per-package — not device-specific once unwrapped) feeds
   XVD/XVC tooling (`-cikfile`) to decrypt the corresponding encrypted
   package, which is fetched from the CDN by ContentId.

Key architectural fact: the packed-CIK layer is sealed with the
per-console device key, but the CIK itself is universal. The device key
is only the seal on the box; a CIK extracted once decrypts the same
package anywhere.

## What worked

- **.NET 6 retarget and build.** Upstream (net10.0) rebuilt cleanly for
  net6.0 with `<LangVersion>11</LangVersion>`; `--help` smoke-tested on
  the PC against .NET 6.
- **In-memory execution on the console (the loader fix).** The root
  cause of the load failure (below) is a filesystem execute-mapping
  restriction on console SystemOS. The working bypass is the pattern
  already used by the console's own deployment mechanism: read each
  assembly's bytes (`File.ReadAllBytes`), load with
  `[Reflection.Assembly]::Load(byte[])`, install an `AssemblyResolve`
  fallback, and invoke the entry point reflectively. A helper script
  wraps this (byte-loads every DLL, forwards `-AppArgs` to the tool).
  With it, the tool runs fully in-process: `--help` exits 0, and
  `dump` reaches live-registry license loading ("Registry loaded").
- **Registry hive export from the tool itself.** The `RegSaveKeyA`
  P/Invoke + `SeBackupPrivilege` patch works; hive export
  (`export-hive`) succeeded on console. A diagnostic script using the
  same technique produced the hive dumps later parsed on the PC.
- **Offline hive analysis on the PC.** Exported hives parse with the
  offline `Registry.dll`; searching UTF-16 key-name/data patterns gives
  definitive answers about whether the SP license store was ever
  populated (it was not — see below).
- **Clip license capture and decode.** The console's stored clip
  license files (XML, `LicenseProtocolVersion=5`, `clas/LicResp/v1`
  response shape) were captured en masse and decoded on the PC with a
  purpose-built parser. The format is now fully understood (below).
- **The error-code ladder as an oracle.** Console error codes
  (`0x8375xxxx` family = JSON/schema validation of a response;
  `0x8B1xxxxx` = licensing facility) were decoded against community
  tables and used to prove exactly which component rejected which
  step.

## What did not (exact errors)

### Loader failure — assemblies will not load from any console path

Symptom: running the tool via `dotnet <tool>.dll` (or any direct
assembly load) wedged or failed on every attempt. What was ruled out,
one by one:

- `DOTNET_MULTILEVEL_LOOKUP` unset (hostfxr wedges scanning a
  nonexistent global install location after resolving the local
  runtime) — set to 0 everywhere; load **still** failed with the value
  confirmed in the hostfxr trace.
- Alternate Data Streams / Zone.Identifier (mark-of-the-web) — clean.
- Folder/location — failed identically from the tool's own directory
  and a fresh copy elsewhere.
- File-creation method — files copied by MSBuild are equally
  unloadable; not a copy-method artifact.

Exact observed behaviors:

- pwsh `LoadFrom` on a third-party dependency DLL returns a phantom
  "path not found" even though the file demonstrably exists.
- The same phantom-miss load behavior explains a recurring, unrelated
  crash of the console's PowerShell itself ("Could not load
  <assembly>, Version=<x>"), which recovers when its own launcher
  re-materializes and byte-loads it — corroborating that the FS
  execute-mapping state decays and that byte-loading is the reliable
  path.

Root cause: console SystemOS blocks execute-mapping of files that were
not provisioned through its own deployment mechanism. In-memory
(`Assembly.Load(byte[])`) loading sidesteps mapping entirely and is the
sanctioned pattern.

### "No device license found" — tool logic, not the filesystem

After the loader fix, `dump` reached live-registry loading and then
failed at device-key derivation:

```
ERR: No device license found.
ERR: Failed to dump device key parameters.
```

Follow-up investigation established this is genuinely true of the
console state, not a lookup bug:

- The clip-storage registry root exists, but the license-blob subkey
  has never been created: **zero subkeys** under
  `{7746D80F-...}`, unchanged before and after a successful disc
  license load.
- All exported hives (SYSTEM, OSDATA, and the rest of a full regdump)
  were searched (proper 16-byte UTF-16 pattern) for both GUIDs — zero
  hits. `dump -r` on the exported hives reproduces the same error.
- `cikdiag -CopyTest` / `-MsbCopy` experiments (folder theory,
  creation-method theory) both disproven before the loader root cause
  was found.

### Clip license format ≠ registry SP license format

Decoding the captured clip license XML files (the tool's expected SP
blob format is structurally unrelated):

- Clip files are XML licenses (`LicenseProtocolVersion=5`,
  `clas/LicResp/v1`) containing an `SVLicense` (SocID, KeyId,
  CustomPolicies as base64-JSON — the same content the LicenseState
  dump exposes — plus package references) and a base64
  `SPLicenseBlock`.
- The `SPLicenseBlock` uses the **CLAS response block namespace**
  (block ids 0x14/0x15/0x18/0x1A/0x1B/0x1C/0x28…), **not** the SP
  registry block namespace the tool parses (0x1 EncryptedDeviceKey,
  0xC9 LicenseInformation, 0xCA PackedContentKeys, …).
- Scanning all captured clip XMLs (50 files): 50× GameContent,
  **0** Device-type entries, **0** EncryptedDeviceKey, **0** packed
  CIKs at the XML layer.

Conclusion: the console's licensing service is what transforms clip
licenses into registry SP blobs; on this console that internalization
has never run or never persisted. The registry store is empty because
the provisioning step never happened — not because the data was
deleted.

### Disc licenses are volume-bound; no offline transplant

- Loading a disc's license file from the optical drive (option:
  `LoadLicenseFile` on `O:\Licenses\<n>.xml`) succeeds while the disc
  is present.
- The same file copied to the thumb drive and loaded via the same API
  fails with **`0x8B100041`** (Xbox licensing facility). The loader
  demands the optical source for disc-class licenses.
- The base disc license **never persists** to the clip store: the
  captured clip files are byte-identical before and after a successful
  disc load (50 files, GameContent only).
- The LicenseState enumeration only lists LicenseState entries, so a
  base disc game never appears there (DLC only).

### Offline routes for the Device license — all dead

- Disc-insert activation: instant and offline, but volume-bound and
  non-persistent (above).
- Back-compat license import from supplied license files: works as
  designed for BC titles, but does not produce the Device-type SP
  license either.
- Hive/registry transplant between consoles: SP licenses are sealed to
  per-console device keys; two consoles are two separate sealed
  stores.

### One-bay constraint (context)

The in-game-script dumping path requires the host title running with
its own disc in the (single) tray, and ejecting a disc game's disc
mid-run suspends the process — so disc targets cannot be dumped via
the host, and no shell-level mount vector exists (the mount utility is
code-integrity-blocked; raw content paths resolve only from
game-context processes). This is why the CIK path matters: the disc's
only essential role is entitlement, which the CIK path replaces — the
package data itself comes from the CDN by ContentId.

## Next steps

1. **The online license-sync window is the unlock.** With the
   update-plane gated (DNS/DNAT/path-level interception with real
   auth/licensing traffic passed through — the configuration already
   proven elsewhere in this project), sign in and let the licensing
   service complete a clean sync. The service should internalize
   licenses into the registry SP store, including provisioning the
   Device license. Re-run the in-memory `dump`/`export-params`
   afterward and check the blob subkey for population.
2. **Validate the open firmware-generation question.** It is not yet
   established whether the registry SP-blob store is populated at all
   on 25398-family (Series-era) OS, or whether SP licenses moved
   elsewhere. The single decisive test is the clean sync above; if the
   blob key is still absent after a verified sync, the registry CIK
   path is likely dead on this OS generation and the research pivots
   to whatever store replaced it.
3. **Licensing-endpoint reachability.** A separate, unresolved
   environmental finding: the licensing endpoint's TLS fails from the
   research network even with no interception in the path
   ("The remote certificate is invalid according to the validation
   procedure"). Isolate with a different network path (hotspot test)
   before blaming the console or the tooling.
4. **If the sync works, the pipeline collapses to one session:**
   one online window → all owned licenses internalized → one
   extraction run → every owned title's CIK on disk → permanently
   offline decryption of packages sourced by ContentId thereafter.
5. **Housekeeping for the tooling:** the in-memory runner, the
   `RegSaveKeyA` patches, and the dual-path hive parse are the
   durable contributions; a self-contained publish
   (`build-singlefile`) remains prepared but unneeded while the
   in-memory loader works.

---

*Methodology writeup for a public research draft. Research conducted on
the author's own console, discs, and licenses. No key material, device
identifiers, or redistributable content is included.*
