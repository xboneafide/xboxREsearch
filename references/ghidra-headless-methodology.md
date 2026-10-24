# Ghidra Headless Pass Methodology — the "Decompiler-Last" Campaign Pattern

## Summary

How multi-pass Ghidra headless analysis was used across a large corpus of
statically analyzed binaries without letting decompilation dominate the
cost. The organizing rule, stated in
[`techniques/title-event-mining/`](../techniques/title-event-mining/README.md):
**exhaust plaintext and data-file surfaces before touching a decompiler.**
When decompilation *is* justified, it is run as short, purpose-built,
single-question passes — not one giant interactive project. This document
records the pass structure, what each pass class yields, and the practical
lessons from running this pattern over dozens of binaries.

## Environment (generic)

- Ghidra headless analyzer (`analyzeHeadless`) on a PC analysis bench;
  scripts are Jython post-scripts (see [`tools/ghidra-passes/`](../tools/ghidra-passes/README.md)).
- One throwaway Ghidra project per batch, deleted after; scripts are kept,
  projects are not.
- Outputs are plaintext C corpora written to a scratch directory, consumed
  by grep, diff, and LLM tooling downstream. Per repo policy the corpora
  derived from copyrighted binaries are never committed anywhere.

## The pass structure

Each pass is a single script answering **one question**. Chaining questions
into one mega-script was repeatedly the wrong call — a pass that answers
one thing can be re-run cheaply when the question changes.

1. **Inventory pass** (no decompiler): enumerate functions, sizes, imports,
   and string anchors. Answers "what is this binary and where is the region
   of interest" in seconds.
2. **Anchor-string pass** (`anchor_string_diff.py` pattern): locate a
   string/import family (e.g. a mount API, an achievement event family),
   enumerate every function referencing it, decompile *only those*. This is
   the highest-yield pass in the whole methodology — most targeted questions
   die here, cheaply.
3. **Full-sweep pass** (`full_decomp.py` / `small_binary_full_sweep.py`):
   only when the binary is small enough that a full decompile is cheaper
   than another targeted pass (tens of KB), or when a grep over the whole
   corpus beats another iteration. Small OS components are the sweet spot;
   full game executables are not.
4. **Post-pass analysis** — outside Ghidra: grep the corpus for the
   question's vocabulary, diff corpora between builds, and (optionally) feed
   selected functions to a decompilation-focused LLM for refinement
   (see [`llm-decompilation-models.md`](llm-decompilation-models.md)).

## What worked

- **One question per pass.** Re-running a pass with a changed needle costs
  seconds; re-running a mega-script costs the whole binary.
- **Anchor-string driven partial decompile** as the default second pass.
  On a 47–69 KB service component, anchoring on the mount-API string family
  cut the decompile set to a handful of functions while answering the whole
  question.
- **Full sweeps for small binaries only.** Small service/era components
  (tens of KB) decompile completely in minutes and reward whole-corpus
  grep; multi-MB game executables never did.
- **Corpora as diff inputs.** Decompiling the *same component* from two
  builds and diffing the C corpora localized behavioral changes faster
  than any interactive session.
- **LLM refinement at function granularity.** Feeding single small
  functions' pseudo-C through a decompilation-specialized model (rather
  than whole-binary prompts) is the granularity that actually works within
  context limits — see the model survey reference for which models and the
  obfuscation caveat.
- **Throwaway projects.** Headless projects are disposable; the scripts and
  corpora carry all the value. Nothing dries up because a project file is
  stale.

## What did not

- **Interactive Ghidra sessions did not survive batch scale.** Click-driven
  analysis of 46 unrelated engines does not compose; everything that worked
  was scriptable and re-runnable.
- **LLM decompilation on obfuscated code** dropped harder than 70% across
  every model tried — obfuscated functions were excluded from LLM passes
  after confirming the drop twice.
- **Whole-binary LLM prompting** blew context limits and produced worse
  per-function output than passing the same functions individually.
- **Benchmarks do not transfer directly to these binaries**: the public
  decompilation models are trained on gcc/clang x86 output, while the
  binaries of interest are MSVC-compiled — expect degraded but still useful
  refinement (details and model list in
  [`llm-decompilation-models.md`](llm-decompilation-models.md)).
- **Container formats as a decompilation target** was repeatedly the wrong
  tool: proprietary archive layouts were better handled by empirical
  layout inference outside Ghidra (see the title-event-mining writeup's
  container dead-end).

## Scope note

This document and the scripts under [`tools/ghidra-passes/`](../tools/ghidra-passes/)
are generic methodology. The title-specific pass scripts stay in the
private research repos, and per repo policy no decompiled corpus derived
from a copyrighted binary is committed here.

## Related

- [`techniques/title-event-mining/`](../techniques/title-event-mining/) — the campaign this pattern came from.
- [`techniques/eraos-mount-probe/`](../techniques/eraos-mount-probe/) — the anchor-string pass applied to mount services.
- [`references/llm-decompilation-models.md`](llm-decompilation-models.md) — the model survey.
