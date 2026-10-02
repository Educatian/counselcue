# Skill-coding agreement

CounselCue codes each counselor turn with one code from **ko-codebook-1**
(`Assets/Scripts/CounselingCodebook.cs`, mirrored in `Server/CounselCue.EdgeWorker/src/index.js`):

| code | 한국어 | quality range |
|---|---|---|
| reflection_exploration | 감정 반영 + 탐색 | 2–3 |
| reflection | 감정 반영 | 1–3 |
| validation | 공감적 반응 (타당화) | 1–3 |
| open_question | 개방형 질문 | 1–3 |
| closed_question | 닫힌 질문 | 0–1 |
| why_question | '왜' 질문 | 0–1 |
| advice | 성급한 조언 | 0–1 |
| premature_reassurance | 성급한 안심 | 0–1 |
| neutral | 중립 반응 | 0–2 |
| silence | 침묵 | 0 |

Decision order and quality rubric are the ones in the coder prompt (`CODER_SYSTEM` in the
worker). Two coders live side by side: the **LLM coder** (`POST /code`, used whenever it answers
with confidence ≥ 0.5) and the **lexicon** (offline fallback). Every turn record stores both
(`skillCode`, `codingSource`, `lexiconCode`), so field data also yields LLM–lexicon agreement.

## Reference labels

`gold-ko.csv` holds 87 utterances across the five cases. Its labels are a **draft seed set**
(`label_status = draft`) written to exercise the pipeline, including boundary cases (quoted
reassurance, in-session invitations vs. advice, reflection followed by advice). They are not
expert codes, and agreement against them is a regression signal only.

To produce validation evidence:

1. Two trained coders (e.g. counseling faculty or supervised doctoral students) read this
   codebook and practise on 15 items not in the set.
2. Each codes every row independently and blind to the other and to the system
   (`coder_a_*`, `coder_b_*`; use `expert-template.csv` for new items). Set `label_status` to
   `expert`.
3. Run `node eval/coding-agreement.mjs`. The `A vs B` section is inter-rater reliability;
   aim for κ ≥ 0.70 before treating the set as a reference, then adjudicate disagreements
   into `coder_a_*`.
4. Run with the deployed worker to score the LLM coder:
   `WORKER_URL=https://<worker> node eval/coding-agreement.mjs`.
   Report κ and per-code F1 for `llm vs A` and `lexicon vs A` together with the model id.

## Files

- `report.md`, `report.json` — last run (accuracy, Cohen's κ, per-code precision/recall/F1,
  confusion matrix, quadratic-weighted κ on quality, and every disagreement).
- `floor.json` — CI fails if the lexicon's κ against the reference falls below this.
