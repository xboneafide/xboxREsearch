# PC-Side Acquisition of Owned-Title XVC Packages — Store → CDN → CIK Pipeline

**Research framing:** This writeup documents private, own-media preservation work
only. Everything described was done against titles the researcher **owns** — digital
entitlements on the researcher's own account, used with their own entitlements, and
discs they own. No distribution, no sharing, no acquisition of titles not owned.
The goal is an offline, byte-perfect copy of media already purchased, plus the
per-title content keys needed to read it after the storefront dies.

## Summary

An owned, console-only XVC title can be acquired end-to-end from a PC **without the
console at all**, as far as the encrypted package: public catalog lookup →
authenticated `GetBasePackage` → unauthenticated CDN download → MD5-verified
byte-perfect XVC on disk. What the PC **cannot** currently do is unwrap the per-title
CIK through the PC licensing surface: the legacy `/licenses/content` endpoint rejects
every PC-authenticatable identity with a canned fault tied to an OS-attested device
claim (EKB) that only the real Windows GamingServices stack produces. The proven
fallback for the CIK is the console disc-activation route, because the CIK itself is
per-package and universal — the device key is only the wrapping, not the content.

## Environment (generic)

- A Windows PC (referred to below as "the PC"), ordinary retail Windows.
- A Microsoft account holding the entitlement for the target title — the
  researcher's own account, used with their own entitlements. Where an
  authenticated tool is needed, it authenticates as that account via the standard
  device-code flow; nothing here involves any other account.
- Open-source research tooling implementing the Xbox Live auth chain (xal-rs
  lineage) and the XVC toolchain (XVDTool). Names and builds are the researcher's
  own local tooling; no upstream project is implicated by this writeup.
- A working network path to Microsoft's store, catalog, licensing, and CDN
  endpoints. No MITM is required for the acquisition chain itself; interception was
  used only to study the real client's licensing traffic (see What did not).

## Chain

### 1. Store catalog lookup → ContentId (unauthenticated)

Query the store SDK's product resolution endpoint for the title's store product ID:

```
https://storesdk.dsx.mp.microsoft.com/v8.0/Sdk/products/contentId
    ?market=US&locale=en-US&languages=neutral,en-US
    &deviceFamily=Windows.Xbox&productIds=<STORE-ID>
```

- `deviceFamily=Windows.Xbox` returns the package `ContentId` (a GUID), package
  format (XVC), architecture, and SKU list (full vs. trial).
- Re-running the same query with `deviceFamily=Windows.Desktop` returning an
  **empty** `ContentIds` array is the definitive test that the title is
  console-only: no Play Anywhere build exists, so there is no plaintext
  `C:\XboxGames` install route and the XVC route is mandatory.
- Cross-check with the display catalog
  (`displaycatalog.mp.microsoft.com/v7.0/products/<STORE-ID>`):
  `DisplaySkuAvailabilities[].Sku.Properties.Packages[]` carries the same
  `ContentId` plus `PackageFormat` and arch.

### 2. Fresh XBL tokenchain (the researcher's own account)

The `GetBasePackage` call requires an **XBL3.0 XSTS token signed for the relying
party `http://update.xboxlive.com`**. Raw XBL3.0 headers without the RP signing get
401/403. The working flow:

- Run the auth CLI from the xal-rs tooling with `--flow device-code` against a
  token-store file. If a stored refresh token is still alive, it refreshes
  silently and non-interactively; no user interaction is needed.
- A tool from the same tree (`packagespc`) then performs the **signed** request:
  it signs the request with the xal signer machinery and presents the XSTS token
  scoped to the update RP.

### 3. GetBasePackage → CDN URL construction

POST to the PC-side package endpoint:

```
https://packagespc.xboxlive.com/GetBasePackage/<CONTENT-ID>
```

(The console-side sibling is `packages.xboxlive.com`.) Notably, this endpoint
**answers for console-only titles** when called with a PC-scoped, signed XSTS
token — the PC client can resolve packages for titles with no Windows device
family in the catalog.

Response fields that matter:

| Field | Use |
|---|---|
| `VersionId` | The exact package version to fetch |
| `CdnRootPaths` / `BackgroundCdnRootPaths` | CDN base URL(s) |
| `RelativeUrl` | Per-file path appended to the root |
| `FileSize` | Expected byte count (for verification) |

The download URL is simply `CdnRootPaths + RelativeUrl`. **No authentication** is
required on the CDN — the URL is bearer-enough. The host supports HTTP `Range`
requests (206 partial content) for resume and parallelism; a 256-byte range probe
is the cheap reachability check before committing to a multi-GB pull.

Important negative result: `GetBasePackage` does **not** carry the content key.
The response shows an empty `KeyBlob` and `LicenseUsageType: 0`. It resolves
paths and sizes only; the CIK lives in a separate licensing/escrow flow (§5).

### 4. MSIXVC/XVC download (multi-GB, verifiable)

- Use the **native Windows** `C:\Windows\System32\curl.exe` for the full pull.
  The MSYS/git-bash curl aborts large streamed writes with exit 23 (write error)
  even to valid, spacious directories — the failure is in the MSYS I/O layer, not
  the CDN. The native binary streams the same URL cleanly.
- Flags that survived a ~19 GB pull: `-sL --retry 8 --retry-delay 5
  --retry-all-errors -C -` (resume on retry), run as a background job.
- Integrity: the CDN's `Content-MD5` header (and the first hash of the `ETag`
  string) is the file MD5. `certutil -hashfile <file> MD5` must match
  byte-for-byte. Verified in practice: a 19,149,148,160-byte XVC downloaded with
  a matching MD5 — byte-perfect on first pull.

### 5. CIK-based decryption (keys from the researcher's own entitlements)

The per-title content key (CIK) is what turns the encrypted XVC into files:

1. `XVDTool -i <package.xvc>` → prints the key GUID embedded in the package.
2. Match that GUID to the previously extracted `<guid>.cik` file (produced
   earlier on the console side by the CIK-extraction tooling against the
   researcher's own licenses).
3. `XVDTool -nd -eu -cik <guid> -cikfile <guid>.cik <package.xvc>` decrypts;
   add `-xf <outdir>` to extract files, or use the streaming variant for lazy
   reads over the multi-GB package.

Why cross-device works: the CIK is **per-package and universal** (plain
AES-128; the same bytes on any machine). The device key is only the AES-WRAP
*seal* on the CIK inside the license blob — which device unwrapped it is
irrelevant to the final content decryption.

### 6. DRM memory-carve bypass for Xbox PC installs (analysis access)

For PC-installed Xbox/Microsoft Store titles whose kernel DRM filter blocks
on-disk reads of the main executable (reads of `game/client/*.exe` are denied by
the Xbox-PC kernel filter while DLLs pass), the documented bypass is:

1. Launch the title normally (its own entitlement licenses it).
2. While the process is live, call `MiniDumpWriteDump` on the PID and capture a
   full memory dump including `Memory64List`.
3. Carve the mapped image (a valid PE, ~63 MB in the reference case) from the
   dump stream. The image is fully readable because it is mapped, not on disk.

Caveat learned in practice: child/worker processes spawned by the title dump
their payloads to `%TEMP%` — watch disk space during capture. This yields
analysis artifacts for strings/achievement-surface mining; it is not a
redistribution path and none is implied.

## What worked

- **Catalog resolution for a console-only title from the PC**, with the
  `Windows.Desktop` empty-result test proving no Play Anywhere plaintext route.
- **Signed `GetBasePackage` for a console-only title using a PC-scoped XSTS
  token** (RP `http://update.xboxlive.com`) — the PC can resolve package
  metadata (VersionId, CDN roots, file sizes) for titles it has no Windows SKU of.
- **Unauthenticated, range-resumable CDN download of the full encrypted XVC**
  (19.1 GB) with a byte-for-byte MD5 match against the CDN's own `Content-MD5`/ETag.
- **Native curl over MSYS curl** for large streamed writes.
- **The auth chain itself**: device-code flow with silent refresh-token renewal;
  request signing for the update RP.
- **PC MITM of the real Windows GamingServices licensing traffic** (hosts
  override + private CA + a patched capturing listener): MSA-ticket endpoints
  (`/v7.0/licenses/leases/renew`) relay transparently and were captured in full,
  proving lease issuance returns a complete License XML (binding, renewal period,
  custom policies) for the real client.
- **Deterministic CIK fallback** (proven architecture): disc → console license
  activation → CIK extraction on the researcher's own console → universal CIK →
  `XVDTool` decrypts the CDN-downloaded XVC on the PC.

## What did not (exact errors)

- **`licensing.mp.microsoft.com/v7.0/licenses/content` rejects every
  PC-authenticatable identity.** With xodus-minted device tickets, with the real
  Windows GamingServices device ticket replayed verbatim, and with XBL3.0 XSTS
  tokens for any relying party, the endpoint returns:
  **fault 3043 `DeviceNameNotHexBinary`** — "The Device Name claim in the provided
  authorization token was mal-formed (not a hex representation of a binary blob)."
  The endpoint 200s on `/licenses/renew` with the same ticket, so this is a
  canned rejection of the `/content` path itself: it is effectively retired, and
  any client-side CIK flow targeting it is targeting a dead endpoint.
- **New-license acquisition requires an OS EKB claim.** Replaying
  `leases/renew` with a lease URI for never-leased content returned
  `InternalServerError` with the server stack
  `CreateUserRootLicense → GetOSEkbForDeviceAsync(DeviceClaims) →
  Convert.FromBase64String(null)`. The OS EKB claim in `DeviceClaims` is
  Windows-hardware-attested (secure-boot/TPM identity) and only exists in
  tickets minted by the real GamingServices/TokenBroker stack — third-party
  auth flows (any device type) and hand-minted device credentials do not
  produce it. A device credential with no registered EKB cannot be key-licensed
  for brand-new MSIXVC content: the runtime blocks at XVC "unpacking" and dies
  with **0x87e10bcf** (= 0xBCF = 3023 `DeviceEkbRequired`). Games with
  pre-existing keys on the device keep working; this is consistent with the
  observed split (older licensed titles launch fine, brand-new content fails).
- **The unsigned-RST trick is dead.** Older documentation claims omitting the
  `<Signature>` element in the MSA RST returns plaintext tickets; MSA now
  rejects unsigned requests outright with **0x80048820 (Invalid STS request)**.
- **Hand-patched device identities do not fix 3043.** Two concrete falsified
  fixes, verified as falsified: forcing a hex-only device username in the
  provision request (patch applied; still 3043), and full fresh-device identity
  randomization via a new device registration (new device confirmed server-side;
  still 3043). The DeviceName claim is minted *inside* MSA's opaque compact
  ticket at device-registration time, from hardware components a PC client does
  not control (candidate omitted/placeholder components identified: MAC
  address, disk identifiers, several numeric component IDs, and TPM info — one
  of which likely feeds DeviceName).
- **User-bound SPLicenseBlock unwrapping is unsolved.** Roaming/Online leases
  issued via `leases/renew` carry a small (~274-byte) SPLicenseBlock *without*
  an `EncryptedDeviceKey`; existing parsers reject it (`no ClepSignState`).
  Hand-parsing shows len-prefixed TLV fields (package family name UTF-16,
  device identifier, a 34-byte wrapped key, timestamps). What seals a
  User-bound block's packed keys — and therefore how to unwrap it — is open.
  Unwrapping it would require GamingServices' local device/user key material
  (DPAPI territory), which is out of scope for this front.
- **XBL-signed endpoints cannot be MITM-relayed.** During the capture session,
  requests carrying the XBL `Signature` header broke when relayed — the
  signature covers the exact request bytes, and any alteration (header
  order/casing, HTTP-version handling, encoding) invalidates it server-side,
  cascading into empty DeviceClaims and 3023 `DeviceEkbRequired`. MSA-ticket
  endpoints relay fine. Rule: XBL-signed endpoints must be raw-spliced, never
  relayed.
- **PC-minted XSTS collections queries return empty inventories** even for an
  account with live leases; the reliable entitlement inventory came from
  console-side captures. Treat PC-token collections queries as unreliable.
- **TokenBroker `DeviceTicket` DPAPI decryption fails** with all entropy
  candidates tried from both user and SYSTEM contexts (8-byte TB header
  present). Not needed given the successful leases/renew capture.

## Next steps

1. **Disc-path CIK (recommended, deterministic):** run the console
   disc-activation → CIK-extraction route against the researcher's own disc and
   entitlements, export the per-title CIK, and decrypt the CDN-pulled XVC on the
   PC with `XVDTool`. This completes the chain with no PC-side licensing work.
2. **Reverse GamingServices' own new-license acquisition path** — it holds the
   OS-EKB-attested credential. Likely surfaces: its gRPC/IPC interface, or
   driving the `xgameruntime` DLLs directly, rather than forging tickets.
   Any such work is done only against titles the researcher owns.
3. **If PC-native CIK issuance is ever required:** the only untested repair
   candidate is a VM with vTPM 2.0 so GamingServices re-provisions with TPM
   material (verify with a control launch before trusting it).
4. **Tooling hygiene:** MSIXVC2 (Series-era) packages are not supported by the
   current XVC toolchain (open upstream issue); MSIXVC1/XVC only. Feed
   `ContentId`+`VersionId` pairs from captured manifests to the package
   downloader for automated re-pulls.
5. **Capture-safety rules already learned:** replay read-shaped calls
   (`collections/query`) freely; treat write-shaped replays (lease creation,
   root-license creation) as state-mutating and never run them against live
   services for content the researcher does not intend to license. XBL-signed
   endpoints: splice, don't relay.
