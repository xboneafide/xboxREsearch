# Console DNS Allowlist / Blocklist Postures

**Scope:** methodology only. Describes the DNS/host allowlist and blocklist
postures used across the console research and what each is for. Public
service hostnames only; private addresses are written as `192.0.2.x`
(TEST-NET-1); substitute your own lab addressing. No device identifiers, no
account data, no capture material.

Related: [emudhra-mitm-chain.md](emudhra-mitm-chain.md) (the interception rig
these lists configure), [relay-splice-rule.md](relay-splice-rule.md) (why
XBL-signed endpoints must be raw-spliced), and
[`tools/mitm-listener/`](../tools/mitm-listener/) (file-driven update-check
responder).

## Summary

Every console session behind the interception rig runs one of a small number
of named postures. A posture is the complete host policy: which hostnames
resolve to the rig (allowlist), which are hard-dead (blocklist), which are
spliced raw to the real server (passthrough), and what the default is for a
host nobody has seen before. The postures differ in how much they let the
console reach and why.

Two invariants hold across **all** postures:

1. **Update surfaces are never allowed through unmodified.** OS-update
   delivery hosts are blackholed at DNS (`0.0.0.0`) *and* dead-ended at the
   proxy (`127.0.0.1:9`), checked in that order. A host in the blocklist
   stays blocked even if it also appears in the allowlist.
2. **The PKI/CTL/CRL-trust-update family is blocked in any posture where the
   forged-chain window matters.** `ctldl.windowsupdate.com` serves the
   per-connect `disallowedcertstl.cab` fetch that would distrusted the
   compromised intermediate the moment the console refreshes its store —
   closing its own interception window. This is the single most important
   blocklist entry.

## Posture table

| Posture | Console class | Purpose | Allowlist | Blocklist | Default for unknown host |
|---|---|---|---|---|---|
| Frozen offline-minimal | Console kept permanently below the update floor (offline research console) | Let the frozen console do offline/local work with just enough connectivity for device-plane visibility; no update surface ever | Connectivity gate + device auth + management/status endpoints only (~10 hosts) | **All** update/CDN families, PKI/CTL, telemetry, everything unobserved | Dead (`0.0.0.0`) |
| Capture allowlist (forged-chain window) | Console above the XSTS floor during a capture session | Full-plaintext interception of auth/licensing/store/social/achievement planes while protecting the forged-chain window | Wide: ~134 hosts across auth, licensing, store, social, stats, events | PKI/CTL (`ctldl.windowsupdate.com` etc.), OS-update CDNs, revocation responders, pure telemetry | Dead (`0.0.0.0`); extend from `dns.log` only |
| Passive observation | Any console whose traffic is being studied but not altered | Minimal interference: watch what the console does, intercept only the surfaces under study | Only the hosts being studied (a handful) | Update families (always), PKI/CTL if a forged chain is armed | Dead (`0.0.0.0`) |
| Open-content posture | A permitted-to-update console during install/commerce work | Let the console behave normally (updates allowed, forge disabled) while keeping metadata capture | Broad, plus content CDNs and update plane unblocked | PKI/CTL *may* stay blocked to preserve the window, or be opened if the window is being deliberately surrendered | Dead (`0.0.0.0`), extended liberally |

## Per-posture detail

### 1. Frozen offline-minimal (the Collateral Damage console posture)

**Purpose.** A console frozen at a pre-disclosure build by design (kept
permanently below Microsoft's server-side minimum-OS floor) is brought
online only to observe its device-plane behavior and to clear stale update
state. It must never be able to download an OS update — that is both a hard
safety rule and the precondition for everything else about this console.

**Allowlist families (narrow by construction):**

- Connectivity gate: `xncsi.xboxlive.com`, `www.msftncsi.com`
  (+ `www.msftconnecttest.com`, `dns.msftncsi.com`, `ipv6.msftncsi.com` as
  observed).
- Device identity: `device.auth.xboxlive.com` (nonce + authenticate —
  accepted the forged chain on the frozen console).
- Management/status (device-plane, no user token required):
  `title.mgt.xboxlive.com` (`/titles/default/endpoints`),
  `settings-win.data.microsoft.com`, notification endpoints
  (`xnotify.xboxlive.com` family).
- The update-*check* host `update.xboxlive.com` — but **only** with the
  forged no-update response armed and scoped to this console's IP, so the
  fresh check swallows stale cached offers. This is the one deliberate
  exception to "no update surfaces"; it exists to *clear* update state, and
  it must never coexist with open content CDNs.

**Blocklist families (everything else, effectively):**

- All OS-update delivery: `xboxoneupdate.xboxlive.com`,
  `delivery.mp.microsoft.com`, `tlu.dl.delivery.mp.microsoft.com`,
  `fe3.delivery.mp.microsoft.com`, `download.windowsupdate.com`,
  `fg.download.windowsupdate.com`, `download.microsoft.com`,
  `wustat.windows.com`.
- The PKI/CTL family: `ctldl.windowsupdate.com` and siblings.
- Content CDNs (`packages.xboxlive.com`, `assets1/2.xboxlive.com`,
  `dlassets*`, delivery hosts) — doubly sinkholed: at the rig *and* at the
  router. A frozen console has nowhere to download an update from even if
  the rig misconfigures.
- Telemetry (`watson.telemetry.microsoft.com`), IPv6 tunnel
  (`xbox.ipv6.microsoft.com`).

**Passthrough rules.** Essentially none — this console is about the
device-plane only, and any host that must complete with a genuine
certificate (title-auth-class pinned endpoints) is either left dead or
raw-spliced if a session requires it. Bulk content is never reached by
design.

**Failure modes when misconfigured.**

- Update-check host left sinkholed with a stale cached offer present: the
  console surfaces a cached-offer error (`0x8B050084`-class) on connect.
  Fix: allow the check host with the forge scoped to this console, reboot.
- Content CDN accidentally opened (even one): the console is below the
  floor, so a real update offer *plus* an open CDN means it *can* download
  the update. This is the catastrophic failure for this posture. Content
  CDNs and OS-update CDNs are the same hosts and cannot be separated at DNS
  level — there is no "game downloads only" configuration.
- `0x8B050033` (`UPD_E_REQUIRED_UPDATE_NOT_AVAILABLE`) is the *expected
  steady state*, not a failure: server policy demands an update (signed,
  unforgeable) while the forged check says none exists.

### 2. Capture allowlist (the eMudhra-rig posture)

**Purpose.** Full-plaintext interception of a live console's user plane —
sign-in, licensing, store, social, achievements, events — during a capture
session, while guaranteeing the console can neither fetch an OS update nor
burn its own forged-chain window by refreshing its certificate trust list.

**Allowlist families (the working ~134-entry list, grouped):**

- Connectivity/NCSI gate (above).
- Auth: `device.auth.xboxlive.com`, `xsts.auth.xboxlive.com`,
  `user.auth.xboxlive.com`, `sisu.xboxlive.com`, `login.live.com`,
  `account.microsoft.com` (+ wildcard).
- Licensing/keys/grants: `licenses.xboxlive.com`, `xlicense.xboxlive.com`,
  `licensing.xboxlive.com`, `licensing.mp.microsoft.com`,
  `xkms.xboxlive.com`, `xgrant.xboxlive.com`, `xrap.xboxlive.com`,
  `rap-fd.xboxlive.com`, `privileges.xboxlive.com`, `title.mgt.xboxlive.com`,
  `user.mgt`, `device.mgt`, `instance.mgt`, `cert.mgt`.
- Store/catalog/commerce: `storeedgefd.dsx.mp.microsoft.com`,
  `displaycatalog.mp.microsoft.com`, `catalog.gamepass.com`, `plaid`,
  `eplists.xboxlive.com`, `pti.store.microsoft.com`, `silver.xboxservices.com`,
  `collections.mp.microsoft.com`.
- Profile/social/stats: `profile`, `userpresence`, `peoplehub`,
  `sessiondirectory`, `clubhub`, `titlehub`, `titleauth`, `titlestorage`,
  `userstats`, `msg`, `xblmessaging`, `comments`, `leaderboards`,
  `notifier`, `achievements`, `gamertag`, `xoobe`, `gamingconsent`,
  `settings.xboxlive.com`, `*.ui.xboxlive.com`, `*.gameservices.xboxlive.com`,
  `*.xboxservices.com`.
- Events/telemetry-carriers: `v10/v20.events.data.microsoft.com` (these
  carry the achievement event stream, not just telemetry),
  `watson.events.data.microsoft.com` (see blocklist note).
- Content/asset metadata: `packages.xboxlive.com` (install metadata),
  `assets1/2`, `dlassets*`, `images-eds-ssl`, `assets-srvs`, store images.
- Shell/config/flighting: `xflight.xboxlive.com`, `xconfig`-class hosts,
  `fs.microsoft.com`, `graph.microsoft.com`, `arc.msn.com`, UI deps
  (`www.microsoft.com`, `www.xboxlive.com`).
- Account-add family: `accounts.xboxlive.com`, `privacy.xboxlive.com`,
  `notify.xboxlive.com`, plus the MSA webview asset CDNs
  (`*.msftauth.net`, `*.msauth.net`, `*.azureedge.net`, `js.monitor.azure.com`)
  — though these webview hosts belong in **passthrough**, not the MITM list
  (see below).

**Blocklist families (checked first, hard-dead at DNS *and* proxy):**

- **PKI/CTL — mandatory.** `ctldl.windowsupdate.com` (the per-connect
  `disallowedcertstl.cab` / `authrootstl.cab` channel — this is the exact
  fetch that closes the forged-chain window), plus all
  `*.windowsupdate.com`, `download.microsoft.com`, `wustat.windows.com`.
- OS-update delivery: `xboxoneupdate.xboxlive.com`, the
  `*.dl.delivery.mp.microsoft.com` family.
- Revocation responders **for the forged chain**:
  `ocsp/crl.comodoca.com`, `c.lencr.org`. Soft-fail keeps the console
  working when revocation is unreachable; if the intermediate ever gets
  revoked, this block is what keeps the chain alive. Note: OCSP/CRL fetches
  for *genuine* certificates on spliced surfaces do **not** burn the window
  — `oneocsp.microsoft.com` / `crl2.microsoft.com` belong on the allowlist
  so spliced surfaces can complete.
- Pure telemetry: `watson.telemetry.microsoft.com` (the
  `*.events.data.microsoft.com` hosts stay open — they carry the
  achievement event stream).
- IPv6 tunnel: `xbox.ipv6.microsoft.com` (console works fine over IPv4).

**Passthrough rules (raw splice — console sees the genuine Microsoft
certificate, no capture):**

- `title.auth.xboxlive.com`: aborts under any forged chain (mTLS/pin or
  floor-gated) — spliced raw, verified end-to-end.
- Bulk content CDNs (`dlassets`, `dlassets-ssl`, `d1`, `d2`, `assets1`,
  `assets2`): raw-spliced at line speed at the :80 router. Multi-GB
  downloads stall through a Python HTTP relay; splicing fixes it, at the
  cost of not capturing the (plain-HTTP, per-title-encrypted) content bytes.
- `notifier.xboxlive.com`: chat reconnect storm (~1 conn/s) — spliced to
  stop capture pollution.
- **All MSA webview hosts** (`login/account/signup.live.com`,
  `logincdn`/`acctcdn` `.msftauth/.msauth`, the `azureedge` CDN,
  `js.monitor`): the console's Chromium-based webview enforces
  **Certificate Transparency** and silently rejects the forged chain (no
  SCTs), while the shell HTTP stack does not. Webview-originated traffic
  must be raw-passthrough or the surface dies silently. Rule of thumb: a UI
  surface that circles on-rig but works off-rig is webview-based — splice
  its hosts.
- `silver.xboxservices.com`: console-side validation shift made it abort
  every handshake against the forged chain; moved to passthrough.
- `watson/v10/v20.events.data.microsoft.com`: the event uploader rejects
  the forged chain (CT/pinning-class, not revocation — retested with all
  revocation responders open and still aborts). Achievement-event delivery
  must ride passthrough or unlocks only land after a later reboot-with-
  passthrough flush.

**Failure modes when misconfigured.**

- A missed PKI/CTL host: the console fetches a fresh CTL and permanently
  distrusts the compromised intermediate — the window closes for that
  console. There is no undo short of console-side trust-store surgery.
- Content CDN open while an OS update is offered: the console can download
  it (same hosts serve both). Only acceptable on a console explicitly
  permitted to update.
- Wildcard entries silently dead (an actual bug once shipped): `*.host`
  entries must be matched as suffixes, not literals — a literal match made
  every wildcard allowlist entry non-functional without any error.
- Store/UI breakage from sinkholed dependency hosts: "temporary network
  problem", circling shells, empty library UI. Trace each to the missing
  host in `dns.log` and extend the list.
- Relay refusing hosts the DNS already resolves to the rig: the TLS relay
  loads its host lists once at startup; a host added after its last restart
  is refused even though DNS answers. See Maintenance rules.

### 3. Passive observation

**Purpose.** Study a console's natural behavior with minimal interference.
The rig answers DNS for everything (or nothing) but terminates TLS only for
the specific hosts under study; everything else passes through untouched or
is answered honestly.

**Allowlist families:** only the hosts being studied that session — e.g.
the NCSI gate plus the one or two service families being profiled. Kept to
a handful of entries; the point is that anything the console asks for shows
up in the DNS query log first, and only studied hosts are promoted to the
allowlist.

**Blocklist families:** the update families are blackholed in *every*
passive session (this is the standing OTA safety rule, independent of the
experiment). The PKI/CTL family is additionally blocked whenever a forged
chain is armed anywhere on the segment — the CTL fetch is a per-connect
behavior, not per-posture.

**Passthrough rules:** default. Everything not explicitly allowlisted for
interception is passed raw; the console sees genuine certificates for all
unstudied surfaces.

**Failure modes when misconfigured.**

- Allowing too much "just in case": a passive session that leaks into a
  capture posture changes the console's behavior (and risks the window).
  Grow the list one host at a time from the DNS log, never speculatively.
- Forgetting the update blocklist because "nothing is being intercepted":
  the DNS responder is the OTA kill switch regardless of posture. It must
  be verified end-to-end before any console is pointed at the rig.

### 4. Open-content posture (permitted-to-update console)

**Purpose.** A console the operator has decided may take updates (e.g. a
floor-respecting console used for install/commerce capture). The forge is
disabled, content CDNs and the update plane are unblocked, and the console
behaves normally — while auth/licensing/social metadata still flows through
the rig for capture.

**Allowlist families:** the full capture list (posture 2) plus the update
plane (`update.xboxlive.com` with real responses, `packages.xboxlive.com`,
`assets1/2`, `dlassets*` open for real downloads, real update manifests
captured as they flow).

**Blocklist families:** PKI/CTL *may* remain blocked to preserve the
console's forged-chain window for future sessions — but if the console
takes the update anyway, the window closes and the CTL block becomes moot.
Decide deliberately: keep the block while the window is still wanted; open
it when the console's research value is exhausted.

**Passthrough rules:** as posture 2 (pinned endpoints, bulk CDNs, webview
family).

**Failure modes when misconfigured.**

- Forge left armed on this console while CDNs are open: the console is
  told "no update available" while policy says one is required and the
  bytes are reachable — contradiction states (`0x8B050033`-class) and a
  console stuck between states. Disable the forge before opening CDNs.
- Assuming the window survives: any successful update fetch (including
  fresh CTLs) permanently closes the forged-chain window on that console.
  Accept this explicitly before entering the posture.

## Maintenance rules

1. **Restart discipline.** Every rig component (`dns_redirect`, `tls_relay`,
   the :80 router, `mitmdump`) caches its host lists **once at startup**.
   After *any* list edit, restart **every** component before concluding a
   host is still blocked or newly allowed — a partial restart produces
   split-brain behavior (DNS resolves the host to the rig while the relay
   refuses it). Batch all config changes, restart each component once.
2. **Never restart while installs or game sessions are in flight.**
   Restarting the relay silently kills running games' network sessions
   (the game keeps running with a dead session; achievement writes fail
   invisibly). Restarting mid-download aborts the download and can push the
   installer into giving up (`0x8007045B`-class) after repeated kills.
3. **Verification is by certificate hash, not by handshake success.** A
   completed TLS handshake does not distinguish MITM termination from a raw
   splice — both complete. Compare the served certificate hash (SHA-256 of
   the peer cert bytes) against the known forged leaf. A host that
   "handshakes fine" may be silently spliced.
4. **Unknown hosts default to dead.** The default answer for any
   unlisted host is `0.0.0.0`. Discover missing hosts from the DNS query
   log (`dns.log` shows every query live), promote them one at a time, and
   **never** add a PKI/CTL or update-delivery host to the allowlist — the
   convenience of unbreaking a UI surface never justifies burning the
   window or risking an OTA.
5. **Wildcard entries must match as suffixes.** Test any new `*.host`
   entry against a real subdomain before trusting it; a literal-match bug
   in the host matcher makes wildcards silently non-functional.
6. **Verify the kill switch end-to-end before every session.** Point a
   test client at the rig, resolve an update host, confirm `0.0.0.0` at
   DNS and a dead-end at the proxy. A console must never be pointed at the
   rig with the blocklist unverified.
7. **Restore DNS to auto when a session ends** unless the posture is
   meant to persist — consoles left pointed at a powered-off rig lose all
   name resolution.
