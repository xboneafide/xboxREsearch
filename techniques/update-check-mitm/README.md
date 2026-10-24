# Intercepting Xbox Console Update-Check and Licensing Traffic: Protocol Capture, Spoofed Gate, TLS Trust Break, and the Relay Resolution

> Research writeup describing work performed on privately owned consoles, discs, and
> licenses on a private lab network. Methodology and findings only; nothing here is
> distributed. All addresses below are documentation-range examples (192.0.2.x), all
> protocol values are hand-redacted (X-filled), and no key/cert material appears in
> this document.

## Summary

This writeup documents a MITM apparatus that stood between a console and its
update/licensing services, what it revealed about the update-check protocol, where the
approach worked, where it hit hard walls, and why the final answer for
signature-carrying endpoints was not an intercept at all but a plain TCP relay.

Five results, in order:

1. **The console trusted a private CA immediately.** No certificate pinning was
   encountered on update hosts; the machine-wide cert store accepted our CA on the
   first try. The store write is virtualized (not visible in the standard registry
   location) and does not survive reboot — the CA must be reinstalled each session.
2. **The update-check protocol was captured in full**: an HTTP/2 `POST` with an
   XBL3.0 device token, an ES256 request-body signature, and a compact JSON body.
3. **The forced-update gate (`0x83750009`) is not a network decision.** It is the
   console re-displaying a cached failure from the OSDATA registry hive; HTTP responses
   only feed a recompute loop. The response *schema* was eventually solved by PC-side
   replay against the real server — the field name is `UpdateAvailable`, not
   `IsUpdateAvailable`.
4. **The licensing endpoint broke TLS at Microsoft's edge** — not our fault, not our
   CA — which parked the licensing MITM plan and led to a passthrough-relay design.
5. **The decisive rule of the whole project**: endpoints whose requests carry an XBL
   `Signature` header cannot survive a relay or proxy, because the signature covers the
   exact request bytes. MSA-ticket endpoints relay transparently; XBL-Signed endpoints
   must be raw-spliced or left alone.

## Environment

Generic, from public/lab parts only:

- **Interception runtime**: stock [mitmproxy](https://mitmproxy.org) (upstream,
  unmodified — the eMudhra certificate-generation options used here are
  upstream features) for exploratory captures, plus a small purpose-built
  Kestrel listener (below) where scripted/file-driven responses were needed.

- **Console**: retail Xbox One, firmware 10.0.25398.4908 (June 2024 era), already
  running a trusted research shell (kernel exploit → SYSTEM). Capable of installing
  files, running PowerShell, and writing registry.
- **PC**: Windows 11 host on the same LAN, .NET 10 SDK, OpenSSL (Win64 build),
  PowerShell 5.1/7.
- **Router/gateway**: UniFi-class with local DNS records, firewall policy table, and
  destination NAT.
- **All interception is inbound to the PC on :443**; the console is otherwise
  firewall-blocked from the internet except through the PC.

## Chain

### 1. PKI setup — `gen-mitm-ca.ps1`

A small PC-side OpenSSL wrapper generates the lab CA and a wildcarded server
certificate for the intercepted hostnames, emitting a passwordless PKCS#12 for
`SslStream`. Two OpenSSL-on-Git-Bash gotchas are worth recording:

- `MSYS_NO_PATHCONV=1` is required before `-subj` or MSYS mangles the subject path.
- SANs must be written into a config file, not passed inline.

The server cert carries a `*.xboxlive.com` wildcard SAN — a `0x80072F8F`
(cert date/CA error) on a *non-overridden* host was the tell that a specific
hostname was missing from the SAN list. Never quote or reuse key material;
in the source repo the whole PKI directory is gitignored.

### 2. Listener — `tools/mitm-listener`

A Kestrel-based listener speaking both HTTP/1.1 and h2. PowerShell 5.1's `SslStream`
cannot do ALPN, so strict clients stall after the handshake with it — modern .NET
negotiates ALPN natively, which is why this is a .NET host rather than a PS script.

Design (from `Program.cs` header, quoted):

```csharp
//   /IsSystemUpdateAvailable (and update-check family) -> file-driven responses (gate key)
//   EVERYTHING ELSE -> transparent passthru to the real endpoint via DoH(1.1.1.1)-resolved
//   upstream IPs (dodges our own DNS override). Console stays fully functional:
//   enumeration, manifests, licenses, profile sync all relay untouched.
```

Key behaviors:

- **File-driven responses**: the response body/status/content-type for the gate path
  live in the PKI directory as small files (`response.body`, `.status`, `.ctype`,
  a `.204` marker) and later a `response.mode` selector (`noupdate` / `sameversion` /
  `mandatory`). Edit a file, next request uses it — no rebuilds mid-experiment.
- **Self-loop guard**: upstream resolution uses DoH to a public resolver so the
  listener's own outbound requests don't recurse through the LAN DNS override.
- **Capture**: requests and passthrough traffic are logged; manifests are dumped to a
  subdirectory. Capture logs contain live auth tokens — gitignored, never committed,
  machine-local only.

### 3. Console config — DNS, DNAT, and the trust install

- `ca-install.ps1` transfers the CA certificate to the console (option G file
  transfer) and installs it into the machine-wide Root store, removing any prior test
  CA and verifying read-back.
- **DNS override**: router-level local DNS records pointed the update-family
  hostnames (update, updatepc, update-cdn, packages, and later the licensing/auth
  family) at the PC (example: `192.0.2.10`).
- **Hardcoded-IP fallback defeated with destination NAT**: some endpoints are
  hardcoded or fall back to literal IPs. Two DNAT policies on the router rewrite those
  destinations to `192.0.2.10:443` for the console's source address. Because NAT
  happens *before* firewall evaluation, the console-side deny policy for update hosts
  never fires — verified by the policy's block counters going quiet in the router
  syslog. The deny policy stays enabled as a fallback.
- **IPv6 is closed naturally**: no AAAA records were ever published for the
  intercepted hosts, so the console's v6 attempts fail cosmeticly; the v6 tunnel
  endpoint is kept firewall-blocked so v6 can't bypass the IPv4 intercept.
- Console-side DNS caching produced ghost mismatches: stale cached entries made some
  auth hosts fail on the console while resolving correctly at the router. A
  network toggle (off/on) forces re-resolution.

### 4. Capture — the update-check protocol

The console's own check, captured complete (illustrative, all values X-filled):

```http
# redacted — illustrative excerpt, every value X-filled
POST /IsSystemUpdateAvailable HTTP/2        ; alpn=h2, TLS 1.3
Host: update.xboxlive.com
Authorization: XBL3.0 XXXXXXXXXX            ; device XSTS token, XUID stripped
Signature: XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX ; 64-byte ES256 request-body signature (xkms-issued key)
ms-cv: XXXXXXXXXXXX                         ; correlator, changes per request

{"UpdateMode":X,"ContentId":"XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX",
 "VersionId":"X.X.XXXX.XXXX.XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX"}
```

A `/IsUpdateAvailable/Batch` variant was also observed. The endpoint list matches the
publicly documented Xbox update endpoints.

**CA trust result**: the console completed the full TLS handshake against our CA on
the first attempt — no pinning on the update hosts. Store write is virtualized: the
cert is not visible under the standard `SystemCertificates` registry path, and it is
wiped on reboot (`0x80072F8F` after any reboot means the CA store is gone —
reinstall before anything else).

### 5. Analysis — the error ladder and the gate

The console is an excellent oracle. Each malformed response yields a distinct code,
naming the failure class:

| Response given | Error on screen | Meaning |
|---|---|---|
| empty/invalid body | `0x80072F76` | bad HTTP response format |
| `{}` / wrong-schema JSON | `0x83750009` | "JSON value not found" — body *parsed*, key missing |
| well-formed but wrong field name (`IsUpdateAvailable`) | `0x83750009` | schema mismatch, the long-running blocker |
| `204 No Content` | `0x8923000D` | XAL/HC auth HTTP-client family — proves the console parses our response |
| exact mirrored no-update schema | `0x8B050033` | SU client's own result code |
| `UpdateAvailable:true` with empty `Files[]` | `0x8B05000C` | SU client, "yes but nothing to fetch" |
| after reboot, no CA installed | `0x80072F8F` | TLS trust error, expected |

The full `0x8375000x` family (1=unsupported format, 2=invalid XML, 3=missing element,
4=missing attr, 5=unexpected content, 6=too large, 7=invalid JSON string,
8=invalid JSON number, 9=JSON value not found) is documented in archived syscall/error
tables and decodes every schema failure on sight.

**The gate itself is not the network.** Hive forensics (a `RegSaveKeyA` dump parsed
PC-side) proved the console caches per-ContentId update state under
`HKLM\OSDATA\Update\Content\<ContentId>` — `LastState`, `LastIntState`,
`LastUpdateFlags`, and `LastResult` holding the *exact error code displayed on
screen*. Deleting the key just triggers a recompute loop: the console re-runs the
check and rewrites its own failure values. Pre-seeding "healthy" values is likewise
overwritten. Staged-update theory was ruled out by reading flash (`update.cfg`
listed only installed builds; `update2.cfg` was empty).

**The schema win came from PC-side replay**: replaying the console's captured request
(the token was still valid) against the real server with `curl --resolve` produced
the genuine 47KB mandatory response and a 329KB-class no-update response. The real
field is `UpdateAvailable` (no `Is` prefix), with `TargetVersionId`, `Files[]`,
`IsMandatory`, `EstimatedDownloadSize`, `UpdateType`, and the request's `VersionId`
echoed back as `IncomingVersionId`. The old `IsUpdateAvailable` field name was the
original source of the `0x83750009` gate. Notably, none of our unsigned responses
were ever signature-*rejected* — only schema-rejected — so response signing appears
unenforced on this path.

**Spoofed-gate pass**: with a working (if schema-incomplete) response live, the
console reached "all services available" while fully spoofed — sign-in succeeded,
dashboard online. Capture mode degrades later syncs (stale caches, service outages on
title launch), so sessions are capture-only and the console is reset afterward.

**During `UpdateAvailable:true` attempts** the console also POSTs
`GetSystemUpdateLicenses` (requesting the updater package, current build,
`licenseProtocolVersion:5`) — not gated, answered by the real server with a signed
license container. With no-update responses it never makes that call.

### 6. The relay variant — licensing, and why interception breaks signatures

The licensing plan was passthrough rather than forging: forward the console's license
request to the real endpoint, return the genuine Microsoft-signed license. Two walls:

**Wall 1 — TLS broken at Microsoft's edge.** The licensing endpoint failed
certificate validation from this network *with no MITM in the path* — "the remote
certificate is invalid according to the validation procedure" straight from the PC to
the real service. Not our CA, not our DNS. Suspects: ISP middlebox, regional CDN edge
serving a bad chain, or a Microsoft-side issue. Isolation test (different network)
was the planned discriminator. The licensing MITM was parked on this basis; console
recovered to fully healthy operation with all interception removed.

**Wall 2 — the signature covers exact request bytes.** This is the decisive finding
of the later PC-front work (live intercept of the Windows licensing service on the
same PC, hosts-override + lab CA + the same listener, patched to dump full
request/response for the licensing host):

- **MSA-ticket-only calls relayed transparently** — lease-renewal calls carrying no
  XBL signature passed through the relay with 200s, fully captured.
- **Calls carrying the XBL `Signature` header broke through the relay** — the
  signature covers the exact request bytes; any alteration (header order or casing,
  HTTP version handling, encoding differences) invalidates it server-side. The server
  cannot derive the device session key → empty device claims →
  `3023 DeviceEkbRequired` → client surfaces `0x87e10bcf`. The 3023/3043/3045 faults
  observed "from the server" were genuine responses to signature-broken requests, not
  service breakage.

The incident that established this (all interception removed as a control test, and
the previously failing titles launched and unpacked immediately afterward) produced
the standing rule:

> **XBL-Signed endpoints must be raw-spliced, never relayed** — the same reason the
> console rig keeps a passthrough host list. MSA-ticket endpoints relay fine and are
> freely capturable.

Incident log also yielded a replay-safety lesson: replaying *write-shaped* calls
(first-lease creation, root-license creation) against a live licensing service can
fail mid-creation and is state-mutating. Replay read-shaped calls freely; treat
write-shaped replays as dangerous and test on a throwaway identity first.

## What worked

- Lab CA trusted by the console's TLS stack immediately; no pinning on update hosts.
- DNS override + DNAT covering both DNS-resolved and hardcoded-IP endpoints, with the
  firewall-avoidance trick (DNAT before firewall eval) verified in syslog.
- Full capture of the update-check request protocol (HTTP/2, XBL3.0 token, ES256
  body signature, JSON body).
- Console fully spoofed online (sign-in + dashboard) under a partial-spoof listener.
- Passthrough of content-update manifests (real 98KB/49KB manifests relayed intact).
- PC-side replay of a captured request against the real server — solved the response
  schema (`UpdateAvailable`, not `IsUpdateAvailable`).
- Hive forensics pinpointing the forced-update gate as cached registry state, not a
  network decision.
- Transparent relay of MSA-ticket-only licensing calls with full capture.

## What did not (exact error codes)

| Code | Context |
|---|---|
| `0x83750009` | forced-update gate; original cause was the wrong response field name (`IsUpdateAvailable` vs `UpdateAvailable`); then cached hive state kept the gate up regardless of responses |
| `0x8923000D` | response to a `204 No Content` — XAL/HC auth HTTP-client family |
| `0x8B050033` | console's own result code for a correct no-update mirror (persisted across reboot, cache deletion, and a LiveSettings update-group write) |
| `0x8B05000C` | `UpdateAvailable:true` with no files to fetch |
| `0x80072F8F` | TLS trust error: hostname not in SAN (pre-wildcard), and CA store wiped by reboot |
| `0x80072F76` | bad HTTP response format (empty-body race) |
| `0x8B100041` | (adjacent work) disc-license volume-bound failure when loading from a different volume |
| `0x87e10bcf` / `3023 DeviceEkbRequired` | relayed XBL-Signed licensing calls — signature invalidated in transit |
| `3043 DeviceNameNotHexBinary` | licensing `/content` endpoint rejects all comers; effectively retired |

Unsolved at park time: whether the SU client will ever accept a no-update answer —
every correct-schema attempt still yielded `0x8B050033`. Update-group enrollment
(the console sat in an Insider ring value) is the leading suspect but unproven.

## Notes on the relay approach

- A pure TCP relay (no TLS termination, no header rewriting) is the only transparent
  posture for signature-carrying requests. Anything that touches the byte stream —
  including header reordering, case normalization, or protocol-version translation by
  a well-meaning HTTP library — invalidates the XBL signature.
- The listener's passthrough path *is* a man-in-the-middle on the licensing family
  (it terminates TLS and re-issues upstream), which is exactly why it broke
  XBL-Signed calls while passing MSA-ticket calls. A true raw splice (SYN-level
  forwarding, no TLS session on the PC) is the correct architecture for signed hosts;
  the listener retains a passthrough-host list for precisely this reason.
- Operational hygiene: capture logs contain live auth tokens — machine-local,
  gitignored, never committed or shared. Console recovery after capture sessions is
  mandatory (capture mode degrades online state by design): remove interception,
  reboot, verify fresh data.
- Never allow the console to reach a real update payload through any intercept — the
  interception rig exists to keep update paths covered, and a response mode that
  advertises an update must never be combined with reachable CDNs.

## Next steps

1. Retest the no-update gate with a *raw-spliced* path for the SU client — if the
   `0x8B050033` persists on a splice-clean chain, the remaining variable is the
   Insider-ring enrollment value; establish the retail group spelling and whether a
   live registry edit changes effective enrollment.
2. Hotspot/isolation test to classify the licensing TLS failure at the edge
   (ISP vs regional CDN vs Microsoft-side).
3. Build the raw-splice forwarder for XBL-Signed hosts and re-run the licensing
   acquisition chain with free PC egress.
4. For PC-front licensing: the remaining gap is the OS EKB device claim, which only
   the real Windows licensing stack mints — either drive that stack's own IPC surface
   or use the deterministic disc-license path.
5. Package the error-decode table and the relay/splice rule into the repo's reference
   docs so future sessions don't re-derive either.
