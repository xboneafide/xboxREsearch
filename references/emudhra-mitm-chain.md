# The eMudhra MITM Chain — Building a Transparent Interception Rig for Xbox Consoles

**Scope:** methodology only. This document describes how the interception
chain was constructed and operated. It contains no certificate material, no
keys, no per-account configuration, and no capture data. Private addresses
are written as `192.0.2.x` (TEST-NET-1); substitute your own lab addressing.

## Summary

A 2026 disclosure of compromised private keys for several eMudhra/emSign
intermediate CA certificates (chaining to roots trusted by Microsoft and
Mozilla) made it possible to forge end-entity certificates that validate on
any TLS stack that has not received the post-disclosure trust-store update.
Consoles that are deliberately kept from OS updates never receive that
update, so their stock TLS stacks accept forged leaf certificates with **no
console-side modification** — no certificate install, no developer mode.

The rig built around this has four cooperating pieces:

1. **Forged-leaf TLS service** on :443, serving a leaf signed by a
   compromised eMudhra intermediate plus every candidate intermediate path.
2. **DNS authority** on :53 running in allowlist mode: hosts on the allowlist
   resolve to the rig; the blocklist (update/PKI/CTL families) resolves to
   `0.0.0.0`; everything else is dead by default.
3. **HTTP pass-through** on :80, with raw splicing for bulk content CDNs.
4. **A file-driven listener** ([`tools/mitm-listener/`](../../tools/mitm-listener/))
   for answer-from-file update-check responses.

The result is a *safer* online window: the console can reach auth and
licensing surfaces through the intercept while every system-update surface
is blackholed at both DNS and proxy layers, by construction.

> Note on tooling: mitmproxy upstream now ships the relevant options for
> serving this class of chain — see <https://mitmproxy.org>. No local
> patches to mitmproxy exist in this repo and none are needed; where the
> earlier sessions found mitmproxy's chain serving insufficient, the fix was
> a small standalone TLS relay that serves the exact chain handed to it.

## Components

| Component | Role | Notes |
|---|---|---|
| Forged leaf certificate | Terminates TLS for allowlisted hosts | Signed by the compromised intermediate's key; SAN set must include **wildcards from day one** (`*.xboxlive.com`, `*.live.com`, `*.microsoft.com`, `*.mp.microsoft.com`, plus the specific host families in use). A missing SAN looks exactly like pinning on the wire — see Lessons. |
| Full chain on the wire | Handshake acceptance | Serve every candidate path: leaf → intermediate (cross-signed) → root, plus the alternate cross-sign path. A console may validate through either. |
| TLS relay (:443) | Serves the chain, terminates, captures plaintext | Chosen over mitmproxy's built-in serving after chain-serving defects; a plain Python `ssl` server serving the full PEM is sufficient and predictable. |
| DNS responder (:53) | Allowlist-mode routing | Blocklist hosts → `0.0.0.0`; allowlist hosts → rig IP; everything else → `0.0.0.0`. AAAA answered as empty NOERROR, never None (some responder libraries crash on empty replies). |
| HTTP router (:80) | Plain-HTTP pass-through | Metadata flows through the proxy for capture; bulk content CDNs are raw-spliced at line speed (multi-GB downloads stall through a Python HTTP relay). |
| File-driven listener | Deterministic update-check answers | [`tools/mitm-listener/`](../../tools/mitm-listener/) answers gate-path requests from local control files and passes everything else through untouched. |
| Host lists | The actual security policy | Allowlist, blocklist, passthrough list — see Host lists below. |
| Forge scoping file | Per-client-IP control | Re-read per connection; lets the forge be armed for one console and disabled for another without restarting anything. |

## Build steps (generically reproducible)

### 1. Understand the trust precondition

The chain only works on consoles whose trust store still contains the
affected intermediates and which have not fetched a distrust update. Before
building anything, verify:

- The intermediates are present in the console's trust store (offline review
  of a cert-store export, or an empirical handshake test).
- The console does not enforce Certificate Transparency for the endpoints
  you intend to intercept (see Safety rules — the shell HTTP stack and the
  Chromium webview differ on this).
- Whether specific endpoints pin a non-eMudhra chain or use mTLS. Test
  per-endpoint; do not generalize from one endpoint's behavior.
- The console does not resolve DNS itself (no DoH, no hardcoded IPs). Watch
  query traffic on the rig to confirm.

### 2. Mint the forged leaf

- Extract the compromised intermediate's key material from the tooling that
  packages it (mitmproxy distributes the chain; see
  <https://mitmproxy.org>).
- Mint a leaf signed by that key with an ECDSA-compatible signature (the
  compromised intermediates in question are EC/P-384; the console's
  ClientHello offers ECDHE-ECDSA suites, so an EC leaf is satisfiable).
- **SAN list: wildcards from day one.** Cover every host family you might
  ever route (`*.xboxlive.com`, `*.live.com`, `*.microsoft.com`,
  `*.mp.microsoft.com`) plus specific hosts. Regenerating the leaf because a
  new host appeared costs a console-side failure that is indistinguishable
  from pinning.
- Verify the leaf's signature against the CA's real public key before
  deploying.

### 3. Serve the full chain

- Serve **every** candidate path on the wire: leaf + intermediate-by-root +
  root, and the alternate cross-signed path. Chain-serving defects were the
  single largest source of false "console rejects it" conclusions — a
  handshake that aborts mid-flight may simply mean a path piece was not
  served.
- Use a TLS server that serves exactly the chain you hand it. mitmproxy's
  upstream options now cover this class of chain (<https://mitmproxy.org>);
  a small standalone relay is equally valid and easier to reason about.
- Verify on a stock PC trust store first (a forged leaf must validate there
  without installing anything) before pointing any console at it.

### 4. DNS layer

- Run an allowlist-mode DNS responder on :53 pointed at from the console
  (router DNS override, or per-console DNS settings).
- Blocklist first, allowlist second: a host on the blocklist stays dead even
  if also allowlisted.
- Answer AAAA queries with an empty NOERROR answer, never an error/None
  reply.
- Log every query live. The DNS log is your discovery surface: any host the
  console asks for that is not allowlisted shows up immediately.

### 5. HTTP layer (:80)

- Plain-HTTP traffic (connectivity checks, some CDN roots) flows through an
  HTTP proxy for visibility.
- Raw-splice high-volume content CDNs directly to the real server: parse the
  first request line + Host header non-destructively (peek, do not consume),
  then splice the socket at line speed. Bulk downloads through a
  Python-level HTTP relay stall for hours.
- Provide a DoH-based upstream resolution path in the pass-through so the
  rig does not loop on its own DNS override.

### 6. File-driven listener for controlled answers

Deploy [`tools/mitm-listener/`](../../tools/mitm-listener/) where you need
deterministic, file-controlled responses (e.g. answering an update check
from a control file instead of the live service):

- Gate path family matched by request path; answered from local control
  files with configurable status/content-type/body/mode.
- Everything else passed through untouched, with upstream resolution via DoH
  so the listener bypasses its own DNS override.
- The console-facing certificate is supplied at startup (PFX).

### 7. Routing the console

- Point the console's DNS at the rig (same override mechanism as any
  network-spoof posture). Do not rely on router-wide redirection if other
  devices share the router — scope it.
- Scope the forge per client IP where possible: the forge file is re-read
  per connection, so arming/disarming a specific console needs no restart.
- Confirm the console considers itself online (its connectivity-gate
  endpoint must be allowlisted and answered 200) before testing anything
  else.

### 8. Verification discipline

- **A completed TLS handshake does not distinguish interception from raw
  splice — both complete.** Always verify by comparing the served
  certificate hash against the known forged leaf.
- Use literal-string matching when grepping logs (parenthesized regex
  groups in ERE silently match nothing).
- After **any** host-list edit, restart **every** rig component — each one
  caches its lists at startup. A component that loads the allowlist once at
  startup will keep refusing hosts added after its last restart while the
  DNS layer already resolves them. This mismatch masquerades as mysterious
  service breakage.
- Restarting the relay kills running games' network sessions silently (the
  game keeps running with a dead session; writes fail invisibly). Batch all
  config changes, restart each component once, then relaunch every running
  game.

## Host lists and passthrough discipline

The lists are the security policy. Maintain three:

**Allowlist** — hosts that resolve to the rig and are TLS-terminated for
capture. Populated empirically: watch the DNS log, add families as the
console asks for them (auth, title management, catalog, social, store,
commerce). Wildcard entries must be matched semantically, not as literal
strings — a matcher that treats `*.host` literally silently dead-ends every
wildcard entry.

**Blocklist** — hard-dead at DNS *and* dead-ended at the proxy (`127.0.0.1:9`).
Checked first. Contains, at minimum, the entire system-update and OS-delivery
CDN family and — critically — the **PKI/CTL channels**. A console behind the
rig must never be able to fetch an updated certificate trust list; the
per-connect CTL fetch from the Windows update CDN is exactly what would
distrust the compromised intermediate and close the window. Note that
OCSP/CRL fetches do *not* burn the window (only CTL/root-store fetches do),
but revocation responders for the forged chain should still be blocked so a
published revocation of the compromised intermediate cannot reach the
console (soft-fail keeps the chain working).

**Passthrough list** — hosts spliced raw to the real server (console sees
genuine certificates, rig has no visibility). Anything on this list is *not*
intercepted, by design:

- Endpoints that pin or require mTLS (a known title-auth endpoint aborted
  663 consecutive handshakes under every forged chain; raw splicing restored
  it).
- Endpoints behind CT-enforcing webviews (see Safety rules).
- Bulk content CDNs (throughput).
- Telemetry/event-upload channels that reject the forged chain — blocking
  them silently breaks dependent console behavior (achievement pops stopped
  landing until the channel was spliced and the queued flush delivered).

Default posture for an **unknown** host: dead (not on the allowlist). Never
add a host to the allowlist speculatively; add it when the DNS log shows the
console asking for it, and never add PKI/CTL/update-delivery hosts.

## Safety rules

1. **Never allow a real update payload through the intercept.** This is the
   prime directive. The rig exists as an OTA kill switch: update domains are
   blackholed at DNS *and* dead-ended at the proxy, deliberately broad
   ("better to break content downloads than let an update through"). A
   forged response that advertises an update must never be live while the
   console can reach a real update payload.
   **Known hazard:** content CDNs (package/asset hosts) serve both game
   installs and OS updates and cannot be separated at the DNS level. If a
   console is permitted to take updates, it can — keep such consoles away
   from the armed forge, or accept that its window closes when it updates.
2. **Auth hosts are never redirected.** Endpoints in the auth plane that
   pin, require mTLS, or sit behind CT-enforcing webviews go on the
   passthrough list, never the allowlist. Do not "try harder" with different
   chains against a pinned host — splice it.
3. **PKI/CTL channels stay blocked.** The per-connect CTL fetch is the burn
   channel for the entire technique. `ctldl.windowsupdate.com` and the
   root-store fetch family must never resolve.
4. **No console-side modification.** The whole point of the technique is
   that it needs no certificate install, no debug mode, no console change.
   If you find yourself wanting to modify the console, you have left this
   technique.
5. **Verify interception, not just handshakes.** Compare served-certificate
   hashes against the forged leaf. Handshake success alone cannot tell you
   whether you captured anything.
6. **Restart discipline.** Every component caches its host lists at startup;
   after any list edit restart every component (once, batched), and never
   restart mid-download or mid-game-session.
7. **Capture hygiene.** Plaintext captures contain live tokens, account
   data, and third-party message content. They are machine-local,
   gitignored, and never committed, pushed, or included in any document.
   Only findings, not captures, leave the lab.
8. **Console recovery after capture sessions.** Restore the console's DNS
   to automatic when the session ends. Know the two steady states:
   - A console above the service floor returns to full normal function the
     moment it is off the rig (verified: every surface that circled on-rig
     worked immediately after DNS restore).
   - A console below the floor behind the armed forge sits in a
     must-update-but-can't state (`0x8B050033`) — benign and stable, but the
     forged no-update answer contradicts server policy. To return such a
     console to normal (non-research) use, disable the forge and restore
     auto-DNS; it will offer the update again (declinable manually) and its
     service errors become truthful.
   Never leave a console pointed at the rig unattended, and never leave a
   stale cached update offer uncleared if the console is coming off the rig
   (a forge-mediated fresh check clears it while armed).
9. **CT is a live boundary, not a theoretical one.** The console's
   Chromium-based webview enforces Certificate Transparency and silently
   rejects a publicly-chained cert with no SCTs, while the shell HTTP stack
   accepts the same cert. Shell-originated requests: interceptable.
   Webview-originated requests: must be raw-passthrough. When a UI surface
   circles on-rig but works off-rig, splice its host family.

## Lessons that cost a session each

- **Never conclude "pinning" until the same certificate has been accepted on
  at least one endpoint AND the SAN list provably covers the failing
  endpoint.** A SAN gap and a pin are indistinguishable on the wire (abort
  right after the server certificate flight). Mint wildcards from day one.
- **Chain-serving defects look like console rejection.** Two separate
  false conclusions (an entire console declared dead for the technique) were
  traced to the rig not serving needed chain pieces. Serve every candidate
  path and test on a stock PC store first.
- **Wildcard matching must be semantic.** Literal string matching of
  `*.host` entries silently dead-ends them all; every wildcard entry is
  dead until fixed, with no error anywhere.
- **Every component caches its lists.** One edit, all components restarted,
  every time.
- **The XSTS minimum-OS floor is unforgeable.** Consoles below the floor get
  signed-JWT rejections (`system_update_required`) at token issuance for
  every relying party except the update service. A MITM observes this but
  cannot forge past it; it shapes what a session can capture (device plane
  and update plane only, below the floor; full user plane above it).
- **Event-upload channels may reject forged chains outright** (CT or
  pinning-class, not revocation — verified even with all revocation
  responders open). Keep them on passthrough; blocking them silently stops
  real-time achievement pops and shifts them to post-reboot flushes.

## What the chain was used for

- **Update-check interception and response control** — capturing the real
  update-check request/response shapes, then answering checks from local
  control files via the file-driven listener. Full write-up:
  [`techniques/update-check-mitm/`](../../techniques/update-check-mitm/).
- **Disc-to-digital digitization** — the claim-exchange flow for converting
  disc ownership to digital entitlements was captured end-to-end through the
  intercept. Full write-up:
  [`techniques/disc-to-digital-digitize/`](../../techniques/disc-to-digital-digitize/).
- **Safe online windows** on frozen consoles: device-plane auth, endpoint
  configuration, catalog, and social flows captured while every update
  surface was unreachable by construction.
- **License lifecycle observation** — lease checkout, callback-with-WNS
  revocation channel, and removal shapes captured live.
- **XKMS key-provisioning protocol shape** — the per-boot key-material
  channel was captured (protocol only; both legs are device-encrypted, so
  the wire yields the protocol, not usable keys).

Related standing rule: [relay-splice-rule.md](relay-splice-rule.md) — which
endpoints may be terminated and which must be raw-spliced, and why.
