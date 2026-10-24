# The OneCollector Event Transport Path and the Xbox Event-Tooling Ecosystem

*Analysis-only reference. This document describes mechanisms and ecosystem structure. It contains no unlock procedures, no token-manipulation instructions, and no tool usage walkthroughs. Event and transport evidence is cross-referenced with the 46-title static-mining corpus in [techniques/title-event-mining/](../../techniques/title-event-mining/README.md).*

## Summary

OneCollector is Microsoft's cross-platform telemetry ingestion service — the `events.data.microsoft.com` family that backs Visual Studio App Center, Office diagnostics, Windows telemetry, and Xbox gameplay events. In the Xbox context it is the PC/GDK-era transport for **event-based stats and achievements**: a title writes structured telemetry events, and cloud-side stat rules consume them. Because the transport accepts an ingestion-org API key rather than a per-user credential at the HTTP layer, the *transport itself* is open in a way the Xbox Live REST surface is not — a property that shaped an entire third-party tooling ecosystem around it.

Three findings anchor this writeup:

1. **The transport is org-key gated, not user-token gated at the HTTP layer.** The OneCollector `POST` endpoint used by Xbox event tooling carries static headers (a Microsoft org ingestion key, a `Client-Id` of `NO_AUTH`) — no per-user secret is required to *reach* the collector. Per-title validation happens afterward, in the Data Platform's stat-rule processing, where a ticket-bound events token and per-title criteria determine whether an event actually moves anything.
2. **Game binaries do not carry the pipeline.** Across a 46-title static-mining corpus, zero titles shipped an in-game OneCollector client — the transport evidence comes from OS-side captures and cross-platform absences, not from the games themselves (see [techniques/title-event-mining/](../../techniques/title-event-mining/README.md)).
3. **The tooling ecosystem converged on one shared format.** Multiple independent implementations (public and commercial) all post to the same collector with the same envelope shape and the same template-grammar interchange format — the event corpus, not the code, is the ecosystem's real asset.

## What OneCollector is (public Microsoft context)

OneCollector is the ingestion front end of Microsoft's internal telemetry pipeline, publicly reachable and publicly documented in SDK source:

- **The collector**: `v20.events.data.microsoft.com/OneCollector/1.0/` (v20 = current collector generation; v10 is the older one). Regional and tenant variants exist (`eu/us/au`-prefixed hosts, plus `mobile.`, `browser.`, `self.` tenants of the same service). For Xbox gameplay events, the pinned v20 host is effectively a constant across every implementation examined.
- **The client runtime**: Microsoft's universal telemetry client, EUTC, is open source at `microsoft/cpp_client_telemetry`. Titles built on the GDK Data Platform link its runtime (Microsoft.Applications.Events, "MAE") — decompiled title data models for a well-known platformer title expose the exact `ext` fields the real client emits (sandbox, device id, title id, event session id, epoch id), confirming the envelope contract from the producer side.
- **The header contract**: Microsoft's own App Center SDKs (e.g. `microsoft/appcenter-sdk-android`, `OneCollectorIngestion.java`) document the production header set — an `apikey` header bound to an ingestion org, a `Tickets` header carrying a JSON bag of ticket-key→token pairs, `Client-Version`, `Upload-Time`, and the x-json-stream content type. Tickets are a *standard Common Schema feature*: per-log tokens bound to registered token providers. There is nothing Xbox-specific about the mechanism; the Xbox-specific knowledge is which org key and which ticket key the events surface uses.
- **The backend**: ingestion feeds Microsoft's internal batch/telemetry platform (Cosmos/SCOPE lineage; Xbox's Asimov/XSPI dashboards). OneCollector is the front door only — there is no public "backend tool"; all cloud-side processing is opaque.

## Where it appears in the Xbox ecosystem (our findings)

### The transport path, generationally

Xbox event transport has three documented generations, all verifiable in Microsoft's own sources:

1. **Vortex** (`vortex.data.microsoft.com/collect/v1`) — the XSAPI-era upload path, pinned in the open-source `microsoft/xbox-live-api` events service headers.
2. **Vortex-events** (`vortex-events.xboxlive.com`) — a per-relying-party token-and-signature path; the events relying party is documented in community-published XSTS relying-party tables and in console endpoint inventories.
3. **OneCollector/1.0** (`v20.events.data.microsoft.com/OneCollector/1.0/`) — the PC/GDK-era EUTC/Common Schema path: static org apikey plus a tickets header carrying an events-relying-party token.

The two-transport split is the structural insight: **console-native events flow through the Vortex family with per-RP XSTS tokens; PC/EUTC gameplay events flow through OneCollector with the org key + ticket bag.** Tooling in the ecosystem overwhelmingly implements the PC path.

### The envelope

Every Xbox gameplay event is the same OneCollector envelope (version 4.0): name `Microsoft.XboxLive.T<titleId>.<EventName>` (the documented event-name prefix contract in `xbox-live-api`), an org iKey, an `ext.utc` block (shell/session fields, page name, epoch, sequence), privacy block, OS block, and an app block — with the per-title property bag nested in the metadata `f`-tree. What varies per title is only the property bag and which `f`-tree paths the cloud stat rules read.

### What our corpus adds: the games don't carry it

The 46-title static-mining campaign (see [techniques/title-event-mining/](../../techniques/title-event-mining/README.md)) established the *absence* side of the picture:

- **Zero titles shipped an in-game OneCollector pipeline.** The corpus's three-era taxonomy — XDK-era ETW/ETX with XDP schema tags, transitional XSAPI REST/RTA, native GDK `XblEventsWriteInGameEvent` — never includes a bundled OneCollector client. On Xbox One, the console OS service is the producer; the game only calls the local event API.
- **Cross-platform absences as controls.** A PC (non-Store) negative-control build of a cross-platform title shipped a Steamworks-only achievement layer with zero Xbox surface — confirming the miner detects absence, not just presence.
- **The binding is service-side.** The event-name → achievement binding lives in the title's service config, never in any client artifact; a deep-dive MMO's package yielded zero additional stat strings across hundreds of thousands of scanned records, and its per-event definitions arrived at runtime from account servers, not from the install.

Together this means: OneCollector transport knowledge cannot be recovered from game packages. It is observable only in OS-side traffic (the console's own event uploads) or in the ecosystem's shared corpora — which is exactly how the tooling ecosystem got built.

### Where validation actually happens

The transport's openness is bounded by what happens after ingestion:

- **Ticket/relying-party binding.** A standard XAUTH token gets a `200 OK` from the collector and is then silently ignored — the events surface expects a token minted for the events relying party (identifiable by its certificate thumbprint in the JWE header, documented in the ecosystem's own code). This silent no-op is the documented explanation for most "it didn't work" reports.
- **Stat-rule processing.** Cloud stat rules (SUM/MAX/MIN/REPLACE semantics) evaluate the event's fields. Some stats are "protected" — the documented analog of the class an external replay cannot move.
- **Per-title criteria.** The mapping from an achievement's requirement to the internal progression index is per-title knowledge and is the main thing the ecosystem's corpora store.
- **Session/correlation gating (per-title, not general).** The corpus data itself shows placeholder session identifiers in the overwhelming majority of stored templates (they work — the rules don't read them), with literal captured session values in a small minority of titles. "Session fields block unlocks" is *not* a general property of the system; it is a per-title configuration question, and some reported failures are better explained by wrong-RP tokens, unmapped criteria, or documented event-platform outages than by session validation.
- **Title-server-gated titles.** At least one well-known live-service title's unlocks run through the game's own server entirely (the client posts to a custom game server, which then calls back into Xbox services). For that class, the Xbox-side event path is not the decision-maker at all — which is why such titles are simply absent from the event corpora.

## The tooling ecosystem map (structural)

The ecosystem map CSV in the private source repo catalogues the actors.
Structurally, the ecosystem consists of the following **families** — described here by role, with third-party community identities removed; named projects are public open-source repositories only:

### 1. The event-corpus family (the ecosystem's core asset)

- A public **per-title event template database** (JSON, one file per title id, plus a master data file) covering 81 titles — the shared, PR-maintained knowledge base of captured envelopes, template holes, and per-achievement criteria mappings. Roughly three-quarters of its titles are marked fully supported. The template grammar is small: literal replacement, integer/float ranges, and NT-FILETIME-style timestamp fills.
- A public **title database** (SQLite + JSON) mapping ~12,000 titles to ids and title-based-achievement flags, used for lookup.
- A public **title-id lookup API** (third-party web service) mapping title ids to names, service config ids, and package family names.
- A community-maintained **"incomplete games" tracker** — effectively the public TODO list of titles not yet mapped.

### 2. The unlocker tool family

- The **reference open-source implementation** (C#): the first public tool to implement event-based replay; its documentation file is the de-facto spec for the payload format, and its events distribution branch is the source of the corpus above.
- At least two independent **reimplementations** (a Rust clean-room port with an Android/mobile shell, and a commercial closed-source tool): both consume the same template format and post to the same collector. Byte-comparison of their bundled corpora shows they ship essentially the *same* event database — the differentiator is client-side tooling (token acquisition strategy, rate limiting, bulk planning), not event knowledge.
- The **template-discovery tooling**: the Rust port ships a criterion-discovery script that renders a template across candidate values and diffs the account's achievement state — the automated form of criteria mapping. Single-field templates are discoverable this way; multi-field, session-correlated templates are the recognized boundary of the craft.
- A **fork landscape**: the reference tool carries dozens of forks, of which a small number of active maintainers pushed the frontier (notably an automated events-token acquisition branch — process-memory scanning of a Microsoft first-party title to harvest a fresh events-RP token — and token-minting experiments). A fork-maintained writeup also documents resold rips of the tool by unrelated resellers — a black-market node of the map.

### 3. The token/auth tooling family

- **Auth libraries** (public, general-purpose): open-source Xbox authentication stacks (MSA → device/user/XSTS flows) that underpin everything, plus a full open-source PC Xbox client reimplementation that also documents the console's endpoint table (and the licensing/packages surface, adjacent but out of scope here).
- **Token acquisition strategies** differ by tool: capture from a running first-party title's process memory, ETW capture from the Xbox app, or direct minting via the WAM/GDK broker. These are implementation differences on the same requirement: a token for the events relying party.

### 4. The commercial service family (three business models)

- **Self-service paid tools/plugins** (encrypted plugin distributions with community support channels).
- **Manual credential services** — storefronts whose stated model is logging into the buyer's account and performing unlocks manually (a credential-based service, categorically different from telemetry replay).
- **Resold rips** of the open tool, documented by the fork community itself.

### 5. The analysis/reference family

- **Official Microsoft sources** used by everyone: the open-source telemetry client, App Center SDKs (the documented header/ticket contract), `xbox-live-api` (event-name prefix, upload URLs), GDK Data Platform docs, and the official trace-analysis tools.
- **Decompiled title corpora** on GitHub (AssemblyCSharp-style repos, IL2CPP interop dumps, full C++ recreations): these expose producer-side event names, measurement shapes, and criteria-id spaces — the raw material for new per-title knowledge.
- **Community knowledge surfaces**: achievement-community forum threads (documenting real event-platform outages and chronic flakiness on pre-2018 event-based titles) and reverse-proxy mirrors making the ecosystem's code visible in other regions.

### Ecosystem invariants

- **One endpoint, one format.** Every implementation examined — public or commercial — posts to the same collector host with the same header set and the same template grammar. The template format is the ecosystem's de-facto interchange standard; criteria mappings are portable across all tools.
- **The corpus is the moat.** Captured event knowledge exists nowhere else publicly; code-search for distinctive captured-event strings hits only the corpus maintainer's repositories. Anyone can reimplement the transport; nobody but the corpus maintainers has published the captured data.
- **Knowledge asymmetry.** Microsoft's public docs describe the stat-rule → cloud-unlock model but not the events-RP ticket contract, the org key, or the silent wrong-RP no-op. Those live only in ecosystem code and captured corpora.

## What this means for preservation

- **Event-based achievements are service-side state.** No disc dump, no extracted package, and no binary contains the achievement binding or the event vocabulary for these titles. When Microsoft eventually retires the ingestion or stat-rule backend for a title, that title's event-based achievements become unrecoverable by any client-side means. Our corpus finding — zero in-package OneCollector across 46 titles — is the measured proof.
- **The ecosystem's shared corpora are, unintentionally, preservation artifacts.** The 81-title template database is the only public record of how a generation of titles' event surfaces were configured. It exists because a tooling community captured live traffic — nothing in the official ecosystem (docs, SDKs, or packages) documents per-title event vocabularies.
- **Capture-now economics.** The templates could only be produced while the titles' online components were live and events were flowing. Titles outside the corpora — including titles whose communities never captured them — have no template record and no non-runtime way to produce one. This is the same runtime-delivery finding the title-mining campaign hit from the other direction (per-event definitions delivered by account servers, not shipped).
- **Auth surfaces churn.** The three-generation transport history (Vortex → vortex-events → OneCollector) shows the pipeline is not stable infrastructure. Any preservation strategy premised on the transport should expect it to change.
- **Clean boundary for our own work.** Everything in this document is mechanism description. Our own contribution is the negative/structural evidence: the 46-title corpus taxonomy, the two-transport split, and the service-side-binding conclusion. Operational details of the tooling ecosystem — token acquisition, criteria fuzzing procedures, rate discipline — are deliberately out of scope here and documented (where at all) in the source repo's internal notes.

## What was verified / inferred

**Verified against public source or our own measurements:**

- OneCollector endpoint, envelope shape, and header contract (official SDK sources: App Center `OneCollectorIngestion`, `cpp_client_telemetry`, `xbox-live-api`).
- The org-key + `NO_AUTH` client-id header set, as implemented consistently across multiple public repositories.
- The events-RP ticket binding and the silent wrong-RP no-op, documented in the open-source implementation's own code comments.
- Template grammar and corpus statistics (81 titles, replacement-type counts, session-placeholder distribution), measured directly from the public corpus files.
- Byte-level equivalence of the commercial tool's bundled event database with the public corpus (diffed from decompiled distributions).
- Zero in-package OneCollector across the 46-title corpus (our own static mining; negative control validated).
- The three-generation transport lineage, pinned in Microsoft's open-source XSAPI and in console endpoint inventories.

**Inferred (labeled as such):**

- That most "event unlock failed" community reports are wrong-RP tokens, unmapped criteria, or platform flakiness rather than session validation — inferred from the corpus's placeholder-session distribution (613 of 657 parseable blobs) plus documented outage threads; not proven by a failing-title diff.
- That the ecosystem's closed commercial corpora add nothing beyond the public one — inferred from the byte-identity finding on the one closed tool we could decompile; other closed services' data was never examined.
- That certain live-service titles are title-server-gated — supported by one direct statement from a tool developer plus the title's absence from the corpus; not independently verified.

## Related documents

- **[techniques/title-event-mining/](../../techniques/title-event-mining/README.md)** — the 46-title corpus evidence: the three-era taxonomy, the zero-OneCollector finding, the service-side-binding conclusion, and the negative-control methodology.
- Source material (private research repo): our raw research notes on the
  OneCollector path, the ecosystem-map data (identities redacted in this
  writeup), and the event-tooling search/census notes.
