# Console B — OSU hop-ladder links (offline USB updates)

## Preface

This is a **snapshot** of the worked hop-ladder link list for Console B
(device class: Xbox One X / Scorpio), verified against the live Microsoft
OSU1 package links on **2026-09-30**. Microsoft rotates the
`download.microsoft.com` GUID paths periodically, so individual links can
and do rot over time; treat any 404 as a signal to re-check the canonical
source rather than a dead package.

**Canonical living source:** the xboxoneresearch community wiki firmware
page, which tracks every published OSU1 build and link:
<https://xboxoneresearch.github.io/wiki/operating-system/firmware/>

Console context: source build **10.0.22000.4158** (Cobalt, xb_flt_2204co,
2022-04-06). Target: **10.0.26100.8866** — the highest build with the
http.sys CVE chain confirmed working. Do **not** ladder past it
(26100.9438 is past the fix). Branch families crossed, in order:
Cobalt → Nickel → Zinc → Germanium.

## The ladder

| Hop | From build | To build | Package link (OSU1.zip) | Branch note |
|---|---|---|---|---|
| 0 | — | 10.0.22000.4158 (2022-04-06) | <https://download.microsoft.com/download/0/c/2/0c2102e3-9799-4f09-a91f-b7626dc21904/OSU1.zip> | Cobalt baseline (re-flash to known state) |
| 1 | 22000.4158 | 10.0.22000.4558 (2022-05-10) | <https://download.microsoft.com/download/b/4/e/b4e74d42-f330-456b-8b17-97f0b77607d1/OSU1.zip> | Same Cobalt branch, warmup step |
| 2 | 22000.4558 | 10.0.22621.3680 (2023-03-15) | <https://download.microsoft.com/download/b/c/5/bc5b50c9-d765-456e-8ad8-043ed1d0f983/OSU1.zip> | **Cobalt → Nickel crossing**; alternates below |
| 2a | 22000.4558 | 10.0.22621.2864 (2022-12-06) | <https://download.microsoft.com/download/b/1/c/b1c09b2b-492a-4fac-9a74-bc88337b6212/OSU1.zip> | Nickel entry, earlier alternate |
| 2b | 22000.4558 | 10.0.22621.2859 (2022-11-22) | <https://download.microsoft.com/download/a/7/6/a7651f1c-b04b-4cb7-b42b-d1a376c610f3/OSU1.zip> | Nickel entry, earliest alternate |
| 3 | 22621.3680 | 10.0.22621.4912 (2023-06-08) | <https://download.microsoft.com/download/5/9/4/5940128f-4e22-4f26-ae45-95a2b6081d14/OSU1.zip> | Within Nickel (optional mid-step) |
| 3a | 22621.3680 | 10.0.22621.4918 (2023-07-17) | <https://download.microsoft.com/download/b/0/3/b0303aa2-7950-48cb-a747-60b5ad4ffb8b/OSU1.zip> | Within Nickel, later alternate |
| 4 | 22621.4912 | 10.0.25398.2538 (2023-10-19) | <https://download.microsoft.com/download/1/d/d/1dd3f392-84d8-48ed-915e-388b47fa565e/OSU1.zip> | **Nickel → Zinc crossing** |
| 4a | 22621.4912 | 10.0.25398.3864 (2024-03-13) | <https://download.microsoft.com/download/6/7/9/679356c4-842b-4fdc-aba9-1eb4fb3ea2c0/OSU1.zip> | Zinc fallback if 2538 package rejected (2923 has no link in the wiki) |
| 5 | 25398.2538 | 10.0.25398.4086 (2024-04-10) | <https://download.microsoft.com/download/a/f/2/af24936d-b743-4317-bc9a-f0de947c44e9/OSU1.zip> | Within Zinc (optional; Collateral-Damage-safe window) |
| 5a | 25398.4086 | 10.0.25398.4478 (2024-05-08) | <https://download.microsoft.com/download/5/a/8/5a8e862a-ec07-45ff-b72c-14d08fb90e41/OSU1.zip> | Within Zinc (optional) |
| 5b | (completeness) | 10.0.25398.4910 (2024-07-15) | <https://download.microsoft.com/download/a/7/1/a718f076-9262-4662-8598-0380b8ae0a79/OSU1.zip> | Patches Collateral Damage — not relevant to Console B |
| 5c | (completeness) | 10.0.25398.4911 (2024-08-13) | <https://download.microsoft.com/download/a/e/0/ae0cc41a-1eb0-4019-ae07-207e7a918b4e/OSU1.zip> | Within Zinc, late |
| 6 | 25398.4086 | 10.0.26100.1968 (2024-08-28) | <https://download.microsoft.com/download/f/e/c/fec3af63-9e99-4d2e-a14d-664099714dba/OSU1.zip> | **Zinc → Germanium crossing (24H2 bump)** |
| 7 | 26100.1968 | 10.0.26100.1969 (2024-09-06) | <https://download.microsoft.com/download/c/5/e/c5e47130-f27f-467a-9a74-5d564aa5ec46/OSU1.zip> | Within Germanium, late 2024 (optional) |
| 7a | — | 10.0.26100.2239 (2024-10-03) | <https://download.microsoft.com/download/4/8/f/48fd6e68-64b0-45d7-b52a-77ade26893b4/OSU1.zip> | Within Germanium (optional) |
| 7b | — | 10.0.26100.2499 (2024-10-31) | <https://download.microsoft.com/download/f/a/7/fa73ff8a-c29f-46fc-8252-1d2831142eef/OSU1.zip> | Within Germanium (optional) |
| 7c | — | 10.0.26100.2910 (2024-11-21) | <https://download.microsoft.com/download/2/c/7/2c787ac4-1ac9-4ca7-80da-192144f3bb86/OSU1.zip> | Within Germanium (optional) |
| 7d | — | 10.0.26100.2911 (2024-12-11) | <https://download.microsoft.com/download/3/c/9/3c9dd32d-eac4-46ac-a3b7-e71f4f5aceb8/OSU1.zip> | Within Germanium (optional) |
| 8 | — | 10.0.26100.2913 (2025-01-14) | <https://download.microsoft.com/download/1/f/d/1fdbf697-d904-4032-9619-38b7007f8318/OSU1.zip> | Within Germanium, 2025 (optional) |
| 8a | — | 10.0.26100.3362 (2025-02-19) | <https://download.microsoft.com/download/be88e7ef-6660-49ad-ba1a-ae55e52d95c7/OSU1.zip> | Within Germanium (optional) |
| 8b | — | 10.0.26100.4020 (2025-04-16) | <https://download.microsoft.com/download/32e40ecd-b7e1-402e-a000-70a28811e124/OSU1.zip> | Within Germanium (optional) |
| 8c | — | 10.0.26100.4026 (2025-04-28) | <https://download.microsoft.com/download/ad1691a8-77fd-4418-b3dc-1d20954c0a08/OSU1.zip> | Within Germanium (optional) |
| 8d | — | 10.0.26100.4855 (2025-06-19) | <https://download.microsoft.com/download/0be2ee12-ca7f-4849-9f96-9555136ee5d9/OSU1.zip> | Within Germanium (optional) |
| 9 | — | 10.0.26100.7010 (2026-02-18) | <https://download.microsoft.com/download/b96c4b48-e2b5-44d8-9438-c09bc0ed5340/OSU1.zip> | 2026 builds — target zone |
| 9a | — | 10.0.26100.7018 (2026-03-24) | <https://download.microsoft.com/download/e3b50dbc-bc0f-4295-9521-6ce163307115/OSU1.zip> | 2026 builds (optional mid-step) |
| 9b | — | 10.0.26100.7807 (2026-04-15) | <https://download.microsoft.com/download/a38bc834-5cfe-4601-aefe-0f55cdc01bd5/OSU1.zip> | 2026 builds (optional) |
| 9c | — | 10.0.26100.7815 (2026-04-21) | <https://download.microsoft.com/download/99f89310-4a4f-42a0-9c5e-2287d8435fc6/OSU1.zip> | 2026 builds (optional) |
| 9d | — | 10.0.26100.8064 (2026-05-06) | <https://download.microsoft.com/download/2ca5fdb5-8a0b-4054-af13-3b9aea0ca3b3/OSU1.zip> | 2026 builds (optional) |
| 9e | — | 10.0.26100.8556 (2026-06-10) | <https://download.microsoft.com/download/bbbd3c5e-f4ca-42ec-a88b-d6680ef2058b/OSU1.zip> | 2026 builds (optional) |
| 9f | — | 10.0.26100.8561 (2026-06-12) | <https://download.microsoft.com/download/528854b0-710d-41ba-a186-3cfc1356b2ba/OSU1.zip> | 2026 builds (optional) |
| 9g | — | 10.0.26100.8569 (2026-06-30) | <https://download.microsoft.com/download/83eef2b2-79b7-4667-a897-7ba046ce0ef5/OSU1.zip> | 2026 builds (optional) |
| **10** | 26100.7018 | **10.0.26100.8866 (2026-07-15)** | <https://download.microsoft.com/download/3c8e3aec-d059-44a2-9525-326a20e33bf2/OSU1.zip> | **STOP HERE** — final target; http.sys CVE chain confirmed working ("SystemOS Privilege Escalation", per wiki issue 142) |

## Notes per boundary crossing

- **Cobalt → Nickel (22000 → 22621).** Hop 2. Any listed 22621 build is a
  valid entry point; 2864 / 2859 (late 2022) and 3680 (2023-03) all had
  live Microsoft links at snapshot time. If one 404s, try the next in the
  table before re-checking the wiki.
- **Nickel → Zinc (22621 → 25398).** Hop 4. Preferred entry is
  25398.2538 (2023-10-19; GUID not listed in the wiki at snapshot time).
  If the console rejects the 2538 package, fall forward to 25398.3864
  (2024-03-13). 25398.2923 exists but had **no link** in the wiki at
  snapshot.
- **Zinc → Germanium (25398 → 26100, the 24H2 bump).** Hop 6. 26100.1968
  (2024-08-28) is the crossing build. Do not attempt a direct leap from
  Zinc into the 2026 26100 builds without landing on an early Germanium
  build first.
- **Ceiling boundary (26100.8866).** Hop 10 is the final target: the
  http.sys CVE chain (CVE-2026-62735, wiki issue 142) is confirmed
  working here. **Do not ladder to 26100.9438 or later** — per issue 142,
  the chain most likely does not work on the latest update, and 9438 had
  no wiki OSU link as of the snapshot.

### Minimal ladder (fewest downloads)

1. 22000.4558 — same-branch warmup
2. 22621.3680 — Nickel crossing
3. 25398.2538 — Zinc crossing (fallback: .3864)
4. 26100.1968 — Germanium/24H2 crossing
5. 26100.7018 — 2026-03
6. 26100.8866 — STOP (exploit ceiling)

Mid-steps between crossings are optional; include one only if a crossing
hop is rejected, then step to the nearest lower listed build.

## Related

- [techniques/osu-offline-updates/](../techniques/osu-offline-updates/)
  — how the offline USB OSU update flow is performed.
- xboxoneresearch wiki firmware page (canonical living link source):
  <https://xboxoneresearch.github.io/wiki/operating-system/firmware/>
