# Patches

Local modifications we maintain against third-party open-source upstreams,
exported as `git format-patch` files. Each directory lists the upstream
repository and the exact upstream commit SHA the patches apply against
(`BASE-SHA.txt`), so the diff can be reproduced regardless of where upstream
has moved since.

Upstream projects are credited and linked; the patches were authored locally.

## cikextractor

Upstream: https://github.com/LukeFZ/CikExtractor
Base: `580ed7ddbe625f464e165736a37e8a2e9f16a7f0` (see `BASE-SHA.txt`)

- `0001-local-RegSaveKeyA-P-Invoke-fix-full-SYSTEM-hi.patch` —
  replaces the `reg.exe` shell-out with a direct `RegSaveKeyA` P/Invoke
  (after acquiring `SeBackupPrivilege`), deletes the pre-created temp file
  before export (`RegSaveKeyA` cannot overwrite), and adds a full-SYSTEM-hive
  parse fallback so the tool works against both a Control-subtree export and
  a complete SYSTEM hive. Motivation and context:
  `techniques/cik-extraction/`.

Apply:

```
git clone https://github.com/LukeFZ/CikExtractor
cd CikExtractor
git checkout 580ed7ddbe625f464e165736a37e8a2e9f16a7f0
git am <path-to>/patches/cikextractor/0001-*.patch
```

## xal-rs

Upstream: https://github.com/xodus-gaming/xal-rs
Base: `6aa2420f8f0d5e6f7460ff13f399037583a942f4`

| Patch | What it does |
|---|---|
| `0001` | sisu `device_token`/`sandbox` become optional (retail `/authorize` omits them); webview redirect matches the full redirect base URL, not just the scheme |
| `0002` | adds the `ach_pull` example: signed XBL v4 achievement pull with continuation paging (SCID path 405s; the per-user titleId query route is the working form) |
| `0003` | adds the `packagespc` example: signed `GetBasePackage` request for PC-scoped XSTS tokens |
| `0004` | adds the license-request example (licreq) (signed POST against the licensing RP with XSTS-claims dump); default client identity switched to Win32/GRTS so fresh provisionings carry a hex DeviceName claim |
| `0005-uncommitted-examples-lib.patch` | one-line follow-up: explicit `CLIENT_WINDOWS()` in the example helper (apply last, with `git apply`) |

Apply:

```
git clone https://github.com/xodus-gaming/xal-rs
cd xal-rs
git checkout 6aa2420f8f0d5e6f7460ff13f399037583a942f4
git am <path-to>/patches/xal-rs/0001-*.patch 0002-*.patch 0003-*.patch 0004-*.patch
git apply <path-to>/patches/xal-rs/0005-uncommitted-examples-lib.patch
```

Usage context for the xal-rs examples lives in
`techniques/xvc-pc-acquisition/` and `techniques/title-event-mining/`.
Account identifiers in the examples are redacted placeholders; supply your
own token store at runtime.
