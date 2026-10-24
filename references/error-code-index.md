# Xbox Error Code Index

This index catalogs every Xbox-side error code encountered during the research:
console error screens, subsystem result codes, server faults, and WinInet /
TLS transport errors. It is a reference for reading logs and error screens
quickly; each entry links to the technique writeup in this repo where the
code was observed, reproduced, or decoded in context.

## Confidence tiers

- **Verified** — reproduced directly against a console or client under our
  own control; the trigger and the code were both observed in the same test.
- **Community-corroborated** — decoded from Microsoft support pages, archived
  syscall/error tables, or repair-community reports that agree across
  multiple independent sources; not (or not yet) reproduced here.
- **Inferred** — deduced from behavior (e.g. a code appearing only under one
  experimental condition); plausible but unconfirmed.

## Structure of the E-codes (console error screens)

For the `E1xx`-family screens the E-code **picks the subsystem** (E101 =
offline-system-update install path, software side; E100-family generally maps
to hardware; E106 = package generation mismatch; E190 = console-side DNS
cache ghosts seen during interception). The **middle sub-code discriminates**
the actual failure within that subsystem (e.g. `90070007` = invalid/corrupt
data at that stage), and a third facility code (typically from the `0x8B05`
SU-client family) names the overall verdict. `8B05000F` recurs alongside
E100/E101/E106 in nearly all offline-update failure reports, so the E-code
alone carries almost no diagnostic information — read the full triad.

## SU / system-update client family (0x8B05xxxx)

| Code | Meaning | Confidence | Discussed in |
|---|---|---|---|
| `0x8B050033` | SU client's own result code for a *correct* no-update mirror response; persisted across reboot, CA reinstall, OSDATA cache deletion, and a LiveSettings update-group write | Verified | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x8B05000C` | `UpdateAvailable:true` targeting the console's own build with an empty `Files[]` — "yes, but nothing to fetch" | Verified | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x8B050084` | "Update required but could not be obtained/downloaded" screen; sub-code `0x201` = download stage. Screen offers only try again / check network / power off — no skip option | Community-corroborated | [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |
| `0x8B0500B6` | "Mandatory update could not be applied" screen; offers Retry / Check network / **Skip update and stay offline** / Power off — the skip option is the sanctioned offline exit | Community-corroborated | [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |
| `0x8B050066` | Appears in the baseline OSDATA hive on content GUIDs that check normally — benign/normal-operation code | Inferred | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x8B05000F` | Generic "update package validation/install failed" (Microsoft support page says only "we're investigating"; community consensus: package validation). Recurs with E100/E101/E106 | Community-corroborated | [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |

## JSON / response-schema family (0x8375xxxx)

WEB_E-family decode table (archived syscall/error tables; the console as
oracle confirms the family on sight):

| Code | Meaning | Confidence | Discussed in |
|---|---|---|---|
| `0x83750009` | "JSON value not found" — body *parsed*, required key missing. Root cause of the forced-update gate: wrong response field name (`IsUpdateAvailable` vs the real `UpdateAvailable`), then cached OSDATA hive state kept the gate up regardless of responses | Verified | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x83750001` | WEB_E family: unsupported format | Community-corroborated | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x83750002` | WEB_E family: invalid XML | Community-corroborated | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x83750003` | WEB_E family: missing element | Community-corroborated | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x83750004` | WEB_E family: missing attribute | Community-corroborated | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x83750005` | WEB_E family: unexpected content | Community-corroborated | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x83750006` | WEB_E family: content too large | Community-corroborated | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x83750007` | WEB_E family: invalid JSON string | Community-corroborated | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x83750008` | WEB_E family: invalid JSON number | Community-corroborated | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |

## Licensing facility (0x8B1xxxxx)

| Code | Meaning | Confidence | Discussed in |
|---|---|---|---|
| `0x8B100041` | Disc-class license is volume-bound: a disc license that loads successfully in place fails with this code when the identical file is presented from fixed storage. The loader demands the optical source (anti-piracy by design); the base disc license never persists to the clip store | Verified | [techniques/cik-extraction/](../techniques/cik-extraction/), [techniques/disc-license-models/](../techniques/disc-license-models/) |

## XAL / HC auth HTTP-client family (0x89xxxxxx)

| Code | Meaning | Confidence | Discussed in |
|---|---|---|---|
| `0x8923000D` | Response to a `204 No Content` from the update-check endpoint — XAL/HC (Xbox Live auth HTTP-client) family. Its appearance instead of the schema-family code proved the console parses our responses; the gate decision does not come from the network | Verified | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |

## WinInet connect / TLS family (0x80072xxx)

| Code | Meaning | Confidence | Discussed in |
|---|---|---|---|
| `0x80072EFD` | `WININET_E_CANNOT_CONNECT` — TCP-level connect failure. Seen at OOBE/check-in under an update-blocking posture: "no route", not a validation error | Verified | [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |
| `0x80072EE2` | `WININET_E_TIMEOUT` — connect + retry timeout; the next failure in the same flow. Same reading: "no route", not validation | Verified | [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |
| `0x80072F0C` | `ERROR_INTERNET_SEC_CERT_REV_FAILED` — certificate revocation check failed. The console checks CRL/OCSP endpoints before trusting any Microsoft CDN; blocking those hosts fails genuine-certificate downloads | Verified | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x80072F76` | Bad HTTP response format (empty/invalid bodies, early race) | Verified | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x80072F8F` | WinInet TLS security error — certificate validation failed. Two proven triggers: hostname not in the SAN list (pre-wildcard `*.xboxlive.com`), and the virtualized Root store being wiped on reboot (the lab CA must be reinstalled after every reboot) | Verified | [techniques/update-check-mitm/](../techniques/update-check-mitm/), [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |
| `0x80072746` | Seen together with `0x80072F0C` when the console is still blocked from CRL/OCSP endpoints — same remediation (allow those hosts) | Community-corroborated | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |

## WinInet / OS-level siblings (0x8007xxxx, non-72xx)

| Code | Meaning | Confidence | Discussed in |
|---|---|---|---|
| `0x80070002` | Mount failure of a title whose Game Pass lease expired — consistent with license-hidden content, not script bugs | Verified | [techniques/eraos-code-exec/](../techniques/eraos-code-exec/) |
| `0x80070490` | VBI read failure on the same license-hidden content (raw `FindFirstFileA` error 2 alongside it) | Verified | [techniques/eraos-code-exec/](../techniques/eraos-code-exec/) |
| `0x80070007` | Invalid/corrupt data — the class behind the OSU stage sub-code `90070007` | Community-corroborated | [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |
| `0x800705B4` | Operation timeout — observed during capture-mode interception wedging the console into stale-cache/service-outage states | Verified | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |

## MSA / STS

| Code | Meaning | Confidence | Discussed in |
|---|---|---|---|
| `0x80048820` | `Invalid STS request` — MSA now rejects unsigned RST requests outright; the old "omit `<Signature>` for plaintext tickets" trick is dead | Verified | [techniques/xvc-pc-acquisition/](../techniques/xvc-pc-acquisition/) |

## XBL licensing / EKB fault family (HTTP faults and HRESULT surface)

| Code | Meaning | Confidence | Discussed in |
|---|---|---|---|
| `3023` `DeviceEkbRequired` | Server fault: the server cannot derive the device session key, so device claims arrive empty and an OS-EKB-attested device credential is required. On XBL-Signed requests this is the observable end of a **signature invalidated in transit**; on genuine clients it means the ticket carries no registered OS EKB | Verified | [techniques/xvc-pc-acquisition/](../techniques/xvc-pc-acquisition/), [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `3043` `DeviceNameNotHexBinary` | The Device Name claim in the authorization token is malformed (not a hex representation of a binary blob). The licensing `/content` endpoint returns it to every comers-tested identity — including a verbatim replay of the real client's device ticket — so the endpoint is effectively retired for new-license acquisition | Verified | [techniques/xvc-pc-acquisition/](../techniques/xvc-pc-acquisition/) |
| `3045` | Observed in the same licensing-fault family; genuine server response to signature-broken requests, not service breakage. Exact fault name not established | Inferred | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |
| `0x87e10bcf` | Client-visible HRESULT = `0xBCF` = 3023 `DeviceEkbRequired`. The runtime blocks at XVC "unpacking" and dies with this code when the device credential carries no registered EKB (brand-new content fails while titles with pre-existing keys keep working) | Verified | [techniques/xvc-pc-acquisition/](../techniques/xvc-pc-acquisition/) |

## Console error-screen triads (E-code family)

| Code | Meaning | Confidence | Discussed in |
|---|---|---|---|
| `E101` | Failure in the offline-system-update **install path** (software side: the updater ran and the package was rejected). The discriminating information is the middle sub-code | Community-corroborated | [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |
| `90070007` (stage sub-code) | `0x80070007`-class: invalid/corrupt data — the package failed verification at that stage. Siblings seen in the wild at other stages: `90060007`, `900B0007`, `0000070C` | Community-corroborated | [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |
| `E101 90070007 8B05000F` | The complete observed triad: version-relationship validation failure (package too far newer than the current OS in one hop — *and*, per community reports, also when the package is *older*). Not package corruption; intermediate hops are trusted | Verified | [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |
| `E100` | Hardware-territory family of the same updater stack (treat as a stop sign, not a retry prompt) | Community-corroborated | [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |
| `E106` | OSU generation mismatch: a Series X\|S package will not validate on a One-family console (and vice versa) | Community-corroborated | [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |
| `E190` | Console-side DNS cache ghosts during interception: device.auth / user.mgt failed on the console while resolving correctly upstream — stale console cache entries, not a routing failure | Verified | [techniques/update-check-mitm/](../techniques/update-check-mitm/) |

## Screen-state helper codes

| Code | Meaning | Confidence | Discussed in |
|---|---|---|---|
| `0x00000201` | Sub-code on the `0x8B050084` screen: download stage | Community-corroborated | [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |
| `0x00000000` | Companion slot on the same screen; no additional decode established | Inferred | [techniques/osu-offline-updates/](../techniques/osu-offline-updates/) |

## Reading order (practical)

1. Identify the **family** from the prefix (E1xx screen / 8B05 SU client /
   8375 schema / 8B1 licensing / 8923 XAL-HC / 80072xx WinInet / 87e1 client
   surface of a licensing fault).
2. For E1xx screens, the E-code names only the subsystem — decode the middle
   sub-code before drawing any conclusion.
3. For 0x8375 schema failures, the trailing digit is the exact parse failure.
4. Cross-check against the linked technique writeup before acting; several
   codes here were proven to be *cached console state re-displayed*, not live
   network decisions.
