# Package-Identity Fuzzing in the Microsoft Publisher Namespace (`8wekyb3d8bbwe`)

## Summary

Windows AppModel package full names end in a 13-character Crockford Base32
**PublisherId** derived from the publisher's code-signing certificate subject.
Microsoft Corporation's is a single fixed value, `8wekyb3d8bbwe` — every
package Microsoft publishes (Store, OS components, Xbox/Durango system and
game packages) ends in it, because Xbox uses the same Windows AppModel. That
constancy makes the suffix an extremely high-precision search marker: string
scanning arbitrary evidence (game dumps, update payloads, hive captures,
filesystem listings, service state directories) for
`<Name>_<Version>_<Arch>_<ResourceId>_8wekyb3d8bbwe` reliably surfaces
package identities that are present on media or in traces but not
necessarily installed or deployed. This document records the regexes used,
where they were applied, and an honest account of what the technique found
and where it under-delivered.

It is a preservation-research methodology note: everything below operates on
evidence the researcher already possesses. No distribution or acquisition
guidance is implied or provided.

## The technique

### Package full name shape

```
<Name>_<Version>_<Architecture>_<ResourceId>_<PublisherId>
```

- **Name** — developer-chosen; dots (and on Xbox dumps, hyphens) allowed.
- **Version** — typically `Major.Minor.Build.Revision` (four dot-separated
  numbers).
- **Architecture** — `x64`, `x86`, `arm`, `arm64`, `neutral`, `any`.
- **ResourceId** — usually **empty** (hence the double underscore in
  `x64__8wekyb3d8bbwe`), or a language/scale tag; always `~` for bundles.
- **PublisherId** — exactly 13 characters, Crockford Base32
  (`[a-hj-km-np-tv-z0-9]`: alphanumerics minus `I`, `L`, `O`, `U`),
  ordinal case-insensitive, stored lowercase in practice. It is a constant
  per publisher, not a per-package hash — SHA-256 over UTF-16LE of the
  Publisher string, first 8 bytes, Crockford-encoded. Microsoft's is
  `8wekyb3d8bbwe`; `cw5n1h2txyewy` (CN=Microsoft Windows) is the other
  marker worth watching.

### The regexes

**Strict (high precision, full package full name):**

```regex
[A-Za-z0-9.]+_\d+(\.\d+){3}_(x64|x86|arm64|arm|neutral|any)_(~|[A-Za-z0-9\-._]+|)_[a-hjkmnp-tv-z0-9]{13}
```

The ResourceId group allows empty — that is what matches the `__`
double-underscore form. The `{13}` on a Crockford-class alphabet does most
of the precision work: excluding I/L/O/U kills a large fraction of
accidental 13-character words.

**Loose (high recall, fuzz the suffix anywhere in a blob):**

```regex
_[A-Za-z0-9.]+_\d+(\.\d+){1,4}_{1,2}[a-hjkmnp-tv-z0-9]{13}\b
```

Tolerates 1–4 version parts and missing/empty ResourceId. Good for
first-pass sweeps where format discipline is unknown.

**Microsoft marker anchored (the practical shortcut):**

```regex
\b8wekyb3d8bbwe\b|\bcw5n1h2txyewy\b
```

Thirteen fixed characters is almost always enough on its own when the
question is "is anything Microsoft-packaged in here"; the full-name regex
then reconstructs the identity around the hit.

**Any-publisher Crockford-13 sanity class:**

```regex
[a-hjkmnp-tv-z0-9]{13}(?![a-hjkmnp-tv-z0-9])
```

Use only with version+architecture context on the left (the strict regex)
— alone it matches random 13-char tokens.

### Binary / UTF-16LE handling

Package names occur as UTF-16LE inside Xbox binaries, hive captures, and
update payloads (`x\0 6\0 4\0 _\0 _\0 8\0 w\0 …`). Ripgrep handles
ASCII/UTF-8 and BOM-marked UTF-16 automatically with `-a`; raw BOM-less
UTF-16LE needs an interleave pattern:

```bash
# ASCII/UTF-8 and BOM-autodetected UTF-16:
rg -a -i '[A-Za-z0-9.]+_\d+(\.\d+){3}_(x64|x86|arm64|neutral)_[a-hjkmnp-tv-z0-9]{13}'

# Raw BOM-less UTF-16LE:
rg -a '(?:[a-zA-Z0-9.]\x00){3,}_\x00(?:\d\x00)+(\.\x00(?:\d\x00)+){3}_\x00(?:x\x006\x004\x00|x\x008\x006\x00|a\x00r\x00m\x006\x004\x00)_\x00_\x00(?:8\x00w\x00e\x00k\x00y\x00b\x003\x00d\x008\x00b\x00b\x00w\x00e\x00)'

# Fixed-string shortcut in UTF-16 (13 chars, two known publisher IDs):
rg -a '8\x00w\x00e\x00k\x00y\x00b\x003\x00d\x008\x00b\x00b\x00w\x00e\x00'
rg -a 'cw\x005\x00n\x001\x00h\x002\x00t\x00x\x00y\x00e\x00w\x00y\x00'
```

Python, both encodings in one pass:

```python
import re
PAT = re.compile(r'[A-Za-z0-9.]+_\d+(?:\.\d+){3}_(?:x64|x86|arm64|arm|neutral|any)_(?:~|[A-Za-z0-9\-._]*?)_[a-hjkmnp-tv-z0-9]{13}')
W16 = re.compile(rb'(?:[\x20-\x7e]\x00){4,}')  # find UTF-16LE strings, then run PAT on decoded
def scan(data: bytes):
    for m in PAT.finditer(data.decode('latin-1', 'ignore')):
        yield m.group(0)
    for m in W16.finditer(data):
        s = m.group(0).decode('utf-16-le', 'ignore')
        yield from (x.group(0) for x in PAT.finditer(s))
```

## Where applied

- **Installed-package evidence on a research PC** — package-identity
  strings recovered from Windows service state directories under the
  per-service `Packages\<PublisherFamily>` tree (e.g. the gaming-services
  identity's `LocalState` cache, where per-title license state and package
  configs land only for titles the service knows about). Identities
  enumerated here define the "installed/known" baseline.
- **Store/service traces and captures** — request/response and config
  artifacts that embed package full names; the loose regex was used for
  first-pass sweeps, the strict one for harvesting.
- **Game dumps and update payloads** — raw binary scans (ASCII + UTF-16LE)
  for identities embedded in executables, manifests, and payload blobs.
- **Filesystem listings of Xbox titles** — directory and manifest naming
  follows `<Name>_<…>_x64__8wekyb3d8bbwe`, so the marker doubles as a
  manifest/skimmer filter over large listing dumps.

## What worked

- The **fixed 13-character publisher marker** is the single best filter in
  practice: no false positives observed on real evidence, near-zero cost,
  and works identically in UTF-16LE interleave form. Every downstream
  extraction was seeded with it.
- The **strict regex** reconstructs complete package full names from
  partial context (logs, config fragments, mid-stream binary hits) with
  high precision; the Crockford alphabet exclusion is doing real work —
  almost no false positives across mixed binary corpora.
- **Recall confirmed**: scans consistently surfaced identities for packages
  that were *referenced or staged* in the evidence (titles present in
  license-state caches and manifests) but not deployed/installed — exactly
  the hidden/undeployed-identity signal the technique is meant to provide
  for preservation research: a title's identity in a trace proves it
  existed in the ecosystem even when the package itself is gone.
- **Bundle/scale variants and empty ResourceId** both appear and both
  match, because the pattern treats ResourceId as optional.

## What did not

- The **loose recall pattern** produced false positives on long versioned
  strings and identifiers; it is only useful as a first-pass sweep and
  must be re-filtered with the strict pattern.
- **Unsigned/developer packages are invisible** to the whole approach:
  they use a special OID-style publisher marker rather than a Crockford
  PublisherId, so any identity not signed with a real publisher
  certificate is a blind spot. Third-party (non-Microsoft) identities are
  caught only if the generic Crockford-13 class is used — with the
  Microsoft anchor alone, store-signed third-party titles under review
  can be missed.
- **Name charset gaps**: Xbox dump names can contain hyphens; the original
  strict pattern allowed only dots, and hyphenated names were missed until
  `-` was added to the Name class. Version depth also varies — the strict
  4-part assumption fails on 1–3-part versions in some traces (the loose
  pattern's `{1,4}` exists for this reason).
- **Case sensitivity** burned one pass: the patterns are lowercase-only
  and PublisherId comparison is only conventionally lowercase; a `-i`
  flag is mandatory for safety.
- The technique enumerates **identities, not packages**: a hit proves the
  identity string existed in the evidence, not that content is recoverable
  or licensable. Identity presence was necessary but nowhere sufficient
  for any of the downstream reconstruction goals.

## Next steps

- Add hyphen (and underscore) to the Name character class and re-run
  strict-pattern harvests over prior evidence to recover missed identities.
- Extend the harvest to generic Crockford-13 (any publisher), not just the
  two Microsoft markers, so store-signed third-party identities in traces
  are enumerated too; dedupe against the installed-package baseline.
- Build a small harvester tool in-repo (the Python scanner above as a CLI)
  that walks a directory tree, applies both encodings, and emits a
  deduplicated CSV of identities with source file and offset — findings
  doc, not raw captures, so it stays committable.
- Cross-reference surfaced undeployed identities against ecosystem
  metadata sources to classify which are real shipped titles vs. internal
  staging identities.

## Reference

- Microsoft, "An overview of Package Identity in Windows apps"
  (learn.microsoft.com, windows-apps/desktop/modernize; also mirrored in
  MicrosoftDocs/windows-dev-docs).
- Community derivation of PublisherId (SHA-256 / Crockford, first 8
  bytes): stackoverflow.com/questions/21568483.
