# Relational model calibration

The client's relational state (safety, guardedness, willingness to disclose) changes each turn
by per-code amounts in `Assets/Scripts/RelationalModelWeights.cs`. The shipped values are a
**prior** (`avp-prior-1`), not a fit:

- Empathy (reflection, validation) mainly builds **safety**.
- Exploration (open questions) mainly opens **disclosure**, at about **3×** a single empathy
  component. This follows Chen et al. (2026), *The Empirically Grounded Adaptive Virtual
  Patient for Psychotherapy Training*, whose dynamics model was fit to 2,000+ hours of therapy
  transcripts. Reflection plus exploration combines both.
- Advice and premature reassurance lower both.

## Refit from Korean data

1. **Expert ratings** (`--ratings`). For each counselor turn, raters score the client state
   before and after reading it (0–1, 0–100 or 1–7). Columns: `code, quality, safety_before,
   guardedness_before, disclosure_before, safety_after, guardedness_after, disclosure_after`
   (optional `id, rater, case_id`). `code` uses ko-codebook-1; code it by hand or with the LLM
   coder.
2. **Coded transcripts** (`--transcripts`, the AVP approach). Real or role-played sessions,
   one row per counselor turn: `session_id, turn, code, quality, openness` (optional `safety,
   guardedness`), where `openness` is the rated client openness in the reply that follows.

```
node eval/calibrate-relational.mjs --ratings my-ratings.csv            # report only
node eval/calibrate-relational.mjs --ratings my-ratings.csv --write    # also writes
    Assets/Resources/CounselCue/relational-weights.json (Unity loads it at start)
```

The fit shrinks each code toward the prior by n/(n+8), pools the quality slope, estimates the
guardedness coupling and the optional safety gate, and reports 5-fold cross-validated error for
prior vs. fit. Codes without data keep their prior values. Every turn record stores
`relationalModelVersion`, so analyses can separate sessions run under different weights.

`example-ratings.csv` is **synthetic** (a made-up ground truth plus noise). It only exercises
the pipeline in CI (`--check`); do not ship weights fitted to it.
