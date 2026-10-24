# mitm-listener

PC-side MITM listener used during the Xbox update-check research: a Kestrel
HTTP/1.1 + HTTP/2 (h2) listener with ALPN that impersonates Xbox Live update
endpoints while leaving every other request fully functional.

## What it does

- **Gate path family** — requests whose path matches the update-check family
  (`IsSystemUpdateAvailable`, `IsUpdateAvailable`, `GetSpecificSystemVersion`,
  `/SLS/`) are answered from local control files instead of hitting the real
  service: `response.204` (force a hard 204), or a status/ctype/body/mode
  combination. `response.mode` switches behavior without a rebuild:
  `noupdate` (mirror of the real "already current" schema), `sameversion`
  (`UpdateAvailable:true` targeting the console's own build, no files),
  `mandatory` (same but `IsMandatory:true`). ContentId/VersionId values are
  echoed back from the request body.
- **Transparent passthrough** — everything else is relayed untouched to the
  real endpoint, with upstream IPs resolved via DoH against 1.1.1.1 so the
  listener bypasses its own LAN DNS override (self-loop guard). Enumeration,
  manifests, licenses, and profile sync all relay normally.
- **Capture logging** — full request/response logging to `capture.log` /
  `passthru.log`, licensing/collections requests and responses written to a
  `license-capture/` directory, and manifest dumps saved under `manifests/`.

## Build & run

Requires a .NET SDK matching the target framework in
[`mitm-listener.csproj`](mitm-listener.csproj) (`net10.0`).

```sh
dotnet build
dotnet run -- <path-to-server.pfx>
```

If no PFX path is passed, the tool looks for `mitm-pki/server2.pfx` relative
to the solution layout. The listener binds 443 (HTTPS, h2+http1 with client
certificates allowed) and 80.

## Warnings

- **Capture logs contain live auth tokens.** `capture.log`, `passthru.log`,
  `license-capture/`, and `manifests/` hold device tickets, XSTS tokens, and
  account-bearing request bodies. They must **never** be committed, pushed,
  or shared. The scrub gate in `../../scripts/scrub_check.py` blocks them by
  name — keep it that way.
- Only point a console you own at this listener. Never let it serve any
  traffic that could trigger a system update download.

## Research context

See [../../techniques/update-check-mitm/](../../techniques/update-check-mitm/)
for the surrounding research: why the gate exists, what responses were
captured, and what each mode demonstrates.
