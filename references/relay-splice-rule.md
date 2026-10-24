# The Relay/Splice Rule — XBL-Signed Endpoints Must Be Raw-Spliced, Never Relayed

**Standing rule:** any endpoint whose requests carry the XBL `Signature` header
(XBL-Signed endpoints) must be **raw-spliced** at the network level — never
terminated, never re-emitted, never relayed by an HTTP-aware proxy. Endpoints
whose requests carry only MSA tickets relay transparently and may be
TLS-terminated for capture.

Full evidence: [techniques/update-check-mitm/](../techniques/update-check-mitm/)
(section "The relay variant — licensing, and why interception breaks
signatures") and [techniques/xvc-pc-acquisition/](../techniques/xvc-pc-acquisition/)
(section "What did not (exact errors)").

## Why — what breaks and the observed error cascade

The XBL signature covers the **exact request bytes**. Any alteration in
transit invalidates it server-side:

- header order or casing normalization,
- HTTP version handling / protocol translation,
- encoding differences,
- any rewriting by a well-meaning HTTP library inside the proxy.

A TLS-terminating relay re-serializes the request upstream by construction,
so it cannot avoid touching the byte stream. Once the signature is broken:

1. The server cannot derive the device session key from the signature.
2. Device claims arrive empty.
3. The server answers with the fault **`3023 DeviceEkbRequired`** (siblings
   `3043`, `3045` in the same family).
4. The client surfaces **`0x87e10bcf`** (`0xBCF` = 3023).

The critical insight from the PC-front capture work: the `3023`/`3043`/`3045`
faults observed "from the server" were **genuine responses to
signature-broken requests** — they looked like service breakage or a retired
endpoint but were entirely self-inflicted by the relay.

## Evidence that exonerated other causes

Two controls isolate the signature mechanism from every competing
hypothesis:

- **Replay / fake-device churn did not matter.** Hand-patched device
  identities (hex-only device usernames in the provision request) and full
  fresh-device randomization via new device registrations were both applied,
  confirmed server-side, and both still failed. The DeviceName/EKB claims are
  minted inside MSA's opaque ticket at device-registration time from hardware
  components a client does not control — no amount of identity churn on our
  side changes them, so identity was not the variable.
- **Removal of interception as a control test.** With *all* MITM apparatus
  removed from the path (hosts overrides, CA, listener, DNS), the previously
  failing titles launched and unpacked immediately afterward. The failures
  existed only while the relay was in the path; the same requests succeeded
  untouched. That is the exoneration: not the tokens, not the device
  identity, not the server — the relay's byte-level alteration.
- **The positive control came free.** During the same capture session,
  MSA-ticket-only licensing calls (lease renewals carrying no XBL signature)
  relayed through the identical listener with 200s and were captured in full.
  Same relay, same TLS termination, same endpoints family — only the
  signature-carrying calls broke.

## Architecture guidance

- **XBL-Signed hosts: raw splice at the SYN level.** Forward packets (or a
  raw TCP stream) without terminating TLS and without any TLS session on the
  PC. The PC must be a transparent path, not a party. Nothing on that path
  may re-serialize, re-order, normalize, or translate HTTP.
- **Unsigned hosts: TLS-terminating passthrough is fine.** Endpoints whose
  requests carry only MSA tickets (and responses that are not
  signature-checked — note that on the update-check path, unsigned *responses*
  were schema-rejected but never signature-rejected) can be terminated,
  captured, and re-emitted.
- **A pure TCP relay with no TLS termination is the only transparent posture
  for signature-carrying requests.** If the proxy opens a TLS session at
  either end, it is not a splice.
- **Maintain a passthrough-host list.** The listener keeps an explicit list of
  hosts that are spliced/passed through raw rather than terminated. Put every
  host known to receive XBL-Signed requests on it before enabling interception;
  treat an unknown host as splice-by-default until proven unsigned. Console-side,
  the licensing family in particular carries device-specific mTLS/certificate
  pinning and must never be terminated.

### Passthrough-host list (observed)

| Class | Hosts | Posture |
|---|---|---|
| XBL-Signed — splice, never terminate | `licensing.xboxlive.com` (device-specific mTLS / pinning), update-check hosts receiving `POST /IsSystemUpdateAvailable` (`update.xboxlive.com` family) with the `Signature` header | Raw splice at SYN level |
| MSA-ticket — relay/capture safe | `licensing.mp.microsoft.com` lease-renewal calls (`/v7.0/licenses/leases/renew`) and other MSA-ticket-only calls | TLS-terminating passthrough OK |
| Decision rule | Any host observed carrying the XBL `Signature` header | Move to splice list |

Where the meaning of a code in the cascade is needed, see
[error-code-index.md](error-code-index.md).

## Operational hygiene

- **Capture logs hold live tokens.** Capture output is machine-local,
  gitignored, and never committed, pushed, or shared. This includes full
  request/response dumps from the relay path.
- **Replay read-shaped calls freely.** Calls that only query state
  (e.g. collections/entitlement queries, update checks, manifest reads) can be
  replayed against live services without consequence — this is exactly how
  the update-check response schema was solved.
- **Treat write-shaped replays as state-mutating and dangerous.** Lease
  creation and root-license creation calls change server-side state; a
  replayed write can fail mid-creation and leave partial state behind. Never
  run write-shaped replays against live services for content you do not
  intend to license, and test on a throwaway identity first.
- **Capture mode degrades online state by design** (stale caches, service
  outages, wedge states such as `0x800705B4` timeouts). Sessions are
  capture-only; afterward, remove all interception, reboot, and verify fresh
  data before trusting the console's online behavior.
- **Never combine an update-advertising response mode with reachable CDNs.**
  The interception rig exists to keep update paths covered; a forged response
  that advertises an update must never be live while the console can reach a
  real update payload.
