# Skill-coding agreement

Reference: `eval/coding/gold-ko.csv` (label status: **draft**). Codebook ko-codebook-1. Generated 2026-09-30T15:59:08.778Z.

> ⚠️ The reference labels are a **draft seed set** written to exercise this pipeline. They are not expert codes, so these numbers are a regression signal, not validation evidence. Replace them with two trained coders (see `eval/coding/README.md`).

### lexicon vs A

n = 87 · accuracy 62.1% · Cohen's κ 0.57 · macro-F1 0.64 · quality κw (same code) 0.78

| code | support | precision | recall | F1 |
|---|---:|---:|---:|---:|
| reflection_exploration | 10 | 0.75 | 0.90 | 0.82 |
| reflection | 11 | 0.60 | 0.82 | 0.69 |
| validation | 7 | 0.63 | 0.71 | 0.67 |
| open_question | 15 | 0.73 | 0.53 | 0.62 |
| closed_question | 8 | 0.00 | 0.00 | 0.00 |
| why_question | 7 | 1.00 | 0.71 | 0.83 |
| advice | 10 | 1.00 | 0.60 | 0.75 |
| premature_reassurance | 8 | 1.00 | 0.50 | 0.67 |
| neutral | 9 | 0.25 | 0.67 | 0.36 |
| silence | 2 | 1.00 | 1.00 | 1.00 |

Confusion (rows = reference, columns = predicted):

| | reflection exploration | reflection | validation | open question | closed question | why question | advice | premature reassurance | neutral | silence |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| reflection exploration | 9 | · | · | 1 | · | · | · | · | · | · |
| reflection | · | 9 | 1 | · | · | · | · | · | 1 | · |
| validation | · | 2 | 5 | · | · | · | · | · | · | · |
| open question | 3 | · | · | 8 | · | · | · | · | 4 | · |
| closed question | · | 1 | · | · | · | · | · | · | 7 | · |
| why question | · | 2 | · | · | · | 5 | · | · | · | · |
| advice | · | · | · | · | · | · | 6 | · | 4 | · |
| premature reassurance | · | · | 1 | 1 | · | · | · | 4 | 2 | · |
| neutral | · | 1 | 1 | 1 | · | · | · | · | 6 | · |
| silence | · | · | · | · | · | · | · | · | · | 2 |

## Disagreements (33)

| id | utterance | reference | lexicon | llm |
|---|---|---|---|---|
| ko005 | 집이 조용할 때 그리움이 더 크게 밀려오시나 봐요. 그 시간을 어떻게 보내고 계신지 여쭤봐도 될까요? | reflection_exploration | open_question |  |
| ko011 | 자신이 약한 건 아닌지 스스로를 탓하게 되시는군요. | reflection | neutral |  |
| ko020 | 예민한 게 아니라, 그 상황에서 충분히 느낄 수 있는 감정이에요. | validation | reflection |  |
| ko025 | 그런 마음이 드는 게 무리가 아니에요. | validation | reflection |  |
| ko028 | 학교생활은 요즘 어때? | open_question | neutral |  |
| ko031 | 회의에서 어떤 순간이 가장 불편하게 느껴지나요? | open_question | reflection_exploration |  |
| ko032 | 편하게 이야기해 볼래? | open_question | neutral |  |
| ko035 | 회사 다닌 지는 몇 년 되셨어요? | closed_question | neutral |  |
| ko036 | 요즘 잠은 잘 주무세요? | closed_question | neutral |  |
| ko037 | 성적표 부모님께 보여드렸어? | closed_question | neutral |  |
| ko038 | 이직 제안을 받으신 적 있으세요? | closed_question | neutral |  |
| ko039 | 식사는 하셨어요? | closed_question | neutral |  |
| ko040 | 한국에 온 지 1년 넘으셨죠? | closed_question | neutral |  |
| ko041 | 불안하셨어요? | closed_question | reflection |  |
| ko042 | 친구는 있어? | closed_question | neutral |  |
| ko047 | 왜 예민하다고 느끼세요? | why_question | reflection |  |
| ko051 | 공부 계획표를 짜서 하루에 조금씩 해 봐. | advice | neutral |  |
| ko054 | 회의 전에 미리 자료를 읽어 가시면 도움이 될 거예요. | advice | neutral |  |
| ko056 | 일단 이직 준비부터 하시면 어떨까요? | advice | neutral |  |
| ko061 | 어떤 선택을 하셔도 잘 해내실 거예요. | premature_reassurance | open_question |  |
| ko062 | 곧 적응되실 거예요. | premature_reassurance | neutral |  |
| ko064 | 그 정도는 다들 겪는 일이야. | premature_reassurance | neutral |  |
| ko069 | 그렇군요. 계속 말씀하세요. | neutral | open_question |  |
| ko072 | 주변에서 '괜찮아질 거야'라고 말해서 오히려 더 답답하셨군요. | reflection | validation |  |
| ko073 | 엄마가 공부 좀 하라고 하셨을 때 어떤 기분이었어? | open_question | reflection_exploration |  |
| ko074 | 지금 느끼는 걸 편하게 말씀해 보세요. | open_question | reflection_exploration |  |
| ko075 | 솔직히 저는 잘 이해가 안 돼요. 그게 그렇게 힘든 일인가요? | neutral | reflection |  |
| ko076 | 책임감 때문에 망설여지시는군요. 그래도 결국 떠나시는 게 맞아요. | advice | neutral |  |
| ko077 | 외로우시죠. 하지만 곧 괜찮아지실 거예요. | premature_reassurance | validation |  |
| ko078 | 서운했구나. 그런데 왜 말을 안 했어? | why_question | reflection |  |
| ko081 | 가족분들은 뭐라고 하세요? | open_question | neutral |  |
| ko083 | 팀장님이 뭐라고 하셨는데요? | open_question | neutral |  |
| ko084 | 선생님한테 다 말해도 괜찮아. 부모님께는 네 동의 없이 얘기하지 않을게. | neutral | validation |  |
