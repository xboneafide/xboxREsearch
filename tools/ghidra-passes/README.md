# Ghidra headless pass scripts

Reusable Ghidra (headless or Script Manager) post-scripts used throughout
the decompilation passes documented in this repo. Generic only — the
title-specific passes stay in the private research repos.

**Note on lint:** these scripts run inside Ghidra's script environment, which
injects globals (`currentProgram`, `getScriptArgs()`, `findBytes()`,
`getFunctionContaining()`, ...). A Python linter will flag them as
undefined; that is expected and harmless. Run them via:

```
analyzeHeadless <project_dir> <project_name> -import <binary> -postScript full_decomp.py <tag> [outdir]
```

(or `-process` against an already-imported program; scripts are also
loadable in the GUI Script Manager under the `Analysis` category).

## Scripts

- **`full_decomp.py`** — decompile every function in the program into one
  C corpus file for offline grep/LLM consumption. Args: `<tag> [outdir]`.
- **`small_binary_full_sweep.py`** — same idea, tuned for small binaries
  (tens-of-KB OS components): longer per-function timeout, one output file
  per program named after the program. Arg: `[outdir]`.
- **`anchor_string_diff.py`** — targeted partial decompile: find all
  occurrences of a configurable needle-string family (ASCII) in the binary,
  decompile every function referencing them, fall back to a full sweep when
  references don't resolve. This is the "diff two builds' service component"
  workhorse — point `NEEDLES` at the API/string family under investigation.
  Args: `<tag> [outdir]`.

## Methodology context

See [`references/ghidra-headless-methodology.md`](../../references/ghidra-headless-methodology.md)
for how these passes fit into the asset-first analysis funnel, and
[`references/llm-decompilation-models.md`](../../references/llm-decompilation-models.md)
for the model survey that informed the LLM-refinement stage.

Per repo policy the outputs of these scripts (decompiled C corpora of
copyrighted binaries) are **not** committed anywhere public — the scripts
are, the dumps are not.
