# Disc-to-Digital ("digitize"): turning a pressed disc into a server-side entitlement

## Summary

Disc-to-Digital (DTD, internally `disctodigital/digitize`) is the Xbox One / Series
flow where the console takes a pressed retail disc and asks the commerce service to
grant the matching digital entitlement to the signed-in account. Captured live,
end-to-end, via the eMudhra MITM rig: the flow is a single request to
`plaid.xboxservices.com` carrying three pieces of disc-derived data, of which the
**BCA region** (the burned, low-level anti-piracy band pressed into the disc near
the outer edge) is the anti-forgery anchor. The server validates the BCA blob
against its own decode, maps the disc's package GUID to the *current* storefront
productId, and grants a new collection entitlement — after which the console
re-syncs its collection and pulls install metadata. Research framing: observation
of our **own discs, own accounts, own licenses**; nothing here is a forgery path,
and nothing here concerns distribution of content.

## Environment (generic)

- Retail Xbox Series X console, fully updated (10.0.26100.9445-class,
  self-reported via its own update check), on a home LAN — referred to as
  Console D in this repo's console table.
- eMudhra MITM chain on the local network segment: console's DNS pointed at the
  rig; rig resolves public hosts (with an OTA-blackhole list returning `0.0.0.0`
  for update endpoints), terminates the console's TLS with a locally trusted
  chain, and logs every request/response pair per connection.
- Public IPs seen on the wire are listed here as documentation-prefix examples
  (`192.0.2.x` only); all console-side addresses are private LAN addresses.
- No console state was modified beyond a normal disc insert and a normal
  entitlement claim; captures were mirrored to a private backup outside the repo.

## Chain

1. **Console inserts disc, resolves `plaid.xboxservices.com`** (Xbox commerce /
   franchise service) through the rig's DNS. The rig is already in the
   certificate trust path, so the console happily establishes TLS with it.
2. **Claim request.** `POST /XboxOneFD/disctodigital/digitize` with a compact
   JSON body:
   ```json
   {
     "BcaData": "XXXX # redacted",
     "MediaId": "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX # redacted",
     "storeIds": [],
     "xboxProductIds": ["XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX # redacted"]
   }
   ```
   - `BcaData` — base64 opaque blob read from the physical disc's BCA region.
     This is the disc-embedded authorization data and the core of the flow.
   - `MediaId` — per-disc media identifier from the disc metadata.
   - `xboxProductIds` — candidate title identity (the legacy/package GUID)
     taken from the disc metadata.
3. **Server-side validation and grant.** The service decodes the BCA blob into
   a structured server-side record, binds it to the signed-in account, and maps
   the disc identity to the current storefront product:
   ```json
   {
     "status": 1,
     "bcaId": "X:X:XXX:X:XXXXX:XXXXXXX:XXXXXXXXXX # redacted",
     "entitlements": [
       {"productId": "PRODUCTIDCAT", "skuId": "0001",
        "title": null, "isNewGrant": true}
     ],
     "message": null,
     "error": null
   }
   ```
   Note the granted commerce `productId` is **not** the package GUID from the
   request — the server maps disc identity to the live storefront product.
   `isNewGrant: true` marks a first-time claim; a repeat claim of the same disc
   would return the already-existing entitlement instead.
4. **Follow-on resync (same wire window).**
   - `POST /v7.0/collections/query` burst against `collections.mp.microsoft.com`
     — inventory re-sync; the new entitlement shows up in the account's
     collection with purchaser identity and a `satisfiedBy` chain linking it
     back to the disc claim.
   - `GET /v7.0/products/lookup?fieldsTemplate=InstallAgent&...` on
     `displaycatalog.mp.microsoft.com` — install metadata for the granted
     productId.
   - titlehub titlerecord refresh — the title appears in the console's game
     list as a digital license.

## What worked

- The full claim succeeded end-to-end on an owned, pressed retail disc: disc
  inserted → digitize request → `status: 1`, `isNewGrant: true` → entitlement
  visible in the collection → install metadata pulled → title playable as a
  digital license.
- **BCA is the whole anti-forgery anchor.** The server will not grant without
  a valid `BcaData`, and the blob is bound to the pressed disc — it is not
  derivable from metadata, not present in any download, and is read directly
  off the disc surface by the drive.
- The entitlement chain is auditable on the wire: collections query shows
  `satisfiedBy` connecting the digital grant to the disc claim, so the
  provenance of every DTD entitlement is reconstructable from the captures.
- A MITM can **observe and replay** a real exchange perfectly, but cannot mint
  a claim for a disc it does not physically possess — same trust boundary the
  licensing service enforces. Observation + replay of real grants only.
- The digitize endpoint is the *only* DTD surface on the wire. The companion
  plaid help-article fetches (`/xboxonefd/content/...` → 404) are cosmetic and
  have no bearing on the grant.

## What did not (exact errors)

- An earlier attempt (2026-10-22 session) with a different owned disc of a
  program region variant **failed**: no grant was returned. The full exchange
  is captured, but the root cause has **not yet been mined from the payloads**
  — no error code or failure body has been extracted yet. It is known from the
  program rules that DTD covers Xbox One / Series titles, so the failure is
  specific to that disc / title / service check, not to program scope.
  Comparison of the failed vs successful request bodies (BcaData shape,
  MediaId, xboxProductIds mapping) is the obvious next mining target.
- The plaid help-article endpoints 404 (`/xboxonefd/content/...`): expected
  and harmless, but easy to misread as a DTD service problem when watching the
  connection log.
- Not a failure of DTD itself, but a rig lesson from the same window: the MITM
  components cache their host lists at startup, so a host that was added to the
  allowlist mid-session stays blocked until that component is restarted. A
  blocked host in the logs does not mean the service is rejecting the claim.

## Next steps

- Mine the 2026-10-22 failed-claim payloads for the exact server error and diff
  request shape against the successful 2026-10-23 claim (field-by-field).
- Replay a captured successful exchange to confirm replay-only semantics
  (expected: entitlement already granted / no `isNewGrant`).
- Attempt claims across more owned discs to map which title/region variants
  are covered by DTD versus rejected, and whether the failure surfaces as an
  error body or a silent no-grant.
- Document the BCA → `bcaId` field structure (the colon-separated server-side
  decode) across several discs to build a decode table.
- Record whether the same claim succeeds with the rig **down** (open internet)
  to bound exactly what the MITM adds — per the captures, the claim itself
  should work without observation.
