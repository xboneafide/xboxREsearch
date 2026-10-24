# LLM decompilation models — survey

Scope: models worth trying against binary-investigation work of the kind
documented in this repo (console OS components, extracted game binaries,
event/licensing internals). Caveat up front: nearly all of these are trained
on x86/x86-64 + C compiled with gcc/clang at O0–O3. The binaries of interest
are x86-64 but MSVC-compiled, which none of the benchmarks cover — expect
degraded but still useful output. Legacy PowerPC binaries are entirely out
of distribution; these tools will not help there.

## Tier list (what to actually try)

1. **LLM4Binary / llm4decompile-9b-v2** (Hugging Face) — the practical
   default. x86 asm → C, end-to-end, trained on 2B tokens, 4096 ctx. The
   V2 series is the largest jump in the family. The family also ships
   `-Ref` variants that refine Ghidra's pseudo-C (+16% re-executability
   over end-to-end on HumanEval-Decompile; Ref-6.7B 52.7% vs raw Ghidra
   20.1%). Workflow that works: Ghidra headless disassemble/decompile →
   feed the pseudo-C or asm through the model (their demo.py shows the
   exact loop).
2. **SK2Decompile** (Tan et al., Sep 2025) — SOTA on HumanEval-class
   re-executability (~69%) via two-phase RL over structure + identifier
   recovery. Research code, not a packaged model — check availability
   before investing.
3. **SALT4Decompile** (arXiv 2509.14646) — SOTA on test-case pass rate
   (70.4% Decompile-Eval); abstracts assembly jumps into a source-level
   logic tree before LLM generation, and is the most robust of the bunch
   against obfuscation (still drops hard under obfuscation overall).
4. **Nova** (arXiv 2311.13721) — generative LLM for assembly with
   hierarchical attention + contrastive learning on optimization variance.
   Also useful for binary-similarity tasks (matching functions across
   builds — relevant when diffing update deltas between builds).
5. **Decompile-Bench** (NeurIPS 2025, code+data public) — 2M
   binary-source function pairs from real GitHub builds; fine-tuning on it
   beats older benchmarks by ~20% re-exec. The dataset to fine-tune a base
   code model on for a custom MSVC-tuned model; Decompile-Bench-Eval is
   the eval harness.
6. **CodeInverter Suite** (arXiv 2503.07215) — lightweight 1.3B/6.7B models
   + a CFG/data-mapping prompt method; designed for privacy-sensitive /
   local inference. Good fit when everything must run locally on modest
   hardware.
7. **DecLLM** (ACM 3728958) — not a model: an iterative repair loop around
   GPT-4-class models using recompile + runtime feedback as oracles; ~70%
   of non-recompilable Ghidra output becomes recompilable. Useful when
   decompiled code that actually builds is needed for analysis.
8. **SLaDe** (Armengol-Estape et al. 2023) — transformer + type inference,
   66% on ExeBench O3 x86. Older but principled on types.

Baselines for context: raw Ghidra ~20% re-exec; general-purpose chat LLMs
worse (GPT-4o ~16%, and low single digits on real-world ExeBench). Any
dedicated model crushes general chat models here.

## Practical recipe for dump analysis work

- Ghidra headless (with the appropriate loaders for the target platform)
  is the front end regardless of model — the Ref-variant models consume
  its pseudo-C.
- For event/licensing-style internals: the interesting functions are small
  and hot (token building, JSON event templates); per-function decompile +
  refine is the right granularity — whole-binary prompting blows the
  context window.
- Obfuscation (clearly present in some licensing code) kills all of them
  (>70% drop); do not spend time running LLM decompilation on packed or
  obfuscated functions.
- Outputs derived from copyrighted binaries stay out of this repo per
  policy — see [`tools/ghidra-passes/`](../tools/ghidra-passes/README.md).

## Sources

- https://huggingface.co/LLM4Binary/llm4decompile-9b-v2 (family + demo.py)
- LLM4Decompile paper: https://aclanthology.org/2024.emnlp-main.203.pdf
- SALT4Decompile: https://arxiv.org/abs/2509.14646
- Decompile-Bench: https://arxiv.org/abs/2505.12668
- CodeInverter: https://arxiv.org/abs/2503.07215
- Nova: https://arxiv.org/abs/2311.13721
- DecLLM: https://dl.acm.org/doi/10.1145/3728958
