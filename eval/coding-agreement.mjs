#!/usr/bin/env node
// Agreement between skill coders on expert-coded Korean utterances (codebook ko-codebook-1).
//
//   node eval/coding-agreement.mjs                         # lexicon vs reference labels
//   WORKER_URL=https://… node eval/coding-agreement.mjs    # + LLM coder (Server /code)
//   node eval/coding-agreement.mjs --gold path.csv --floor # CI: fail if lexicon kappa drops
//
// Reports accuracy, Cohen's kappa, per-code precision/recall/F1, a confusion matrix and
// quality agreement (quadratic-weighted kappa on items where both coders chose the same code).
// Pairs: lexicon–A, llm–A, llm–lexicon and, when a second expert column is filled, A–B.
import { spawnSync } from "node:child_process";
import { readFileSync, writeFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const opt = (name, fallback) => {
  const i = args.indexOf(name);
  return i >= 0 && args[i + 1] ? args[i + 1] : fallback;
};
const goldPath = resolve(opt("--gold", join(root, "eval/coding/gold-ko.csv")));
const outDir = resolve(opt("--out", join(root, "eval/coding")));
const checkFloor = args.includes("--floor");
const workerUrl = (process.env.WORKER_URL || "").replace(/\/$/, "");

export const CODES = [
  "reflection_exploration", "reflection", "validation", "open_question", "closed_question",
  "why_question", "advice", "premature_reassurance", "neutral", "silence",
];

// ---- CSV (RFC 4180, as saved by Excel/Sheets) -------------------------------------------
export function parseCsv(text) {
  const rows = [];
  let row = [], field = "", quoted = false;
  const src = text.replace(/^﻿/, "");
  for (let i = 0; i < src.length; i++) {
    const ch = src[i];
    if (quoted) {
      if (ch === '"' && src[i + 1] === '"') { field += '"'; i++; }
      else if (ch === '"') quoted = false;
      else field += ch;
    } else if (ch === '"') quoted = true;
    else if (ch === ",") { row.push(field); field = ""; }
    else if (ch === "\n" || ch === "\r") {
      if (ch === "\r" && src[i + 1] === "\n") i++;
      row.push(field); field = "";
      if (row.some((v) => v !== "")) rows.push(row);
      row = [];
    } else field += ch;
  }
  if (field !== "" || row.length) { row.push(field); if (row.some((v) => v !== "")) rows.push(row); }
  const [header, ...body] = rows;
  return body.map((r) => Object.fromEntries(header.map((h, i) => [h.trim(), (r[i] ?? "").trim()])));
}

// ---- Metrics --------------------------------------------------------------------------
export function cohenKappa(a, b, labels = CODES) {
  const n = a.length;
  if (!n) return NaN;
  let agree = 0;
  const pa = Object.fromEntries(labels.map((l) => [l, 0])), pb = { ...pa };
  for (let i = 0; i < n; i++) {
    if (a[i] === b[i]) agree++;
    if (a[i] in pa) pa[a[i]]++;
    if (b[i] in pb) pb[b[i]]++;
  }
  const po = agree / n;
  const pe = labels.reduce((s, l) => s + (pa[l] / n) * (pb[l] / n), 0);
  return pe === 1 ? 1 : (po - pe) / (1 - pe);
}

export function weightedKappa(a, b, k = 4) {
  const n = a.length;
  if (n < 2) return NaN;
  const O = Array.from({ length: k }, () => Array(k).fill(0));
  const ra = Array(k).fill(0), rb = Array(k).fill(0);
  for (let i = 0; i < n; i++) { O[a[i]][b[i]]++; ra[a[i]]++; rb[b[i]]++; }
  let num = 0, den = 0;
  for (let i = 0; i < k; i++)
    for (let j = 0; j < k; j++) {
      const w = ((i - j) ** 2) / ((k - 1) ** 2);
      num += w * O[i][j];
      den += w * (ra[i] * rb[j]) / n;
    }
  return den === 0 ? 1 : 1 - num / den;
}

export function compare(reference, predicted, refQ, predQ) {
  const n = reference.length;
  const confusion = Object.fromEntries(CODES.map((r) => [r, Object.fromEntries(CODES.map((p) => [p, 0]))]));
  let agree = 0;
  for (let i = 0; i < n; i++) {
    if (confusion[reference[i]] && reference[i] in confusion && predicted[i] in confusion[reference[i]]) confusion[reference[i]][predicted[i]]++;
    if (reference[i] === predicted[i]) agree++;
  }
  const perCode = CODES.map((code) => {
    const tp = confusion[code][code];
    const support = CODES.reduce((s, p) => s + confusion[code][p], 0);
    const predictedCount = CODES.reduce((s, r) => s + confusion[r][code], 0);
    const precision = predictedCount ? tp / predictedCount : 0;
    const recall = support ? tp / support : 0;
    const f1 = precision + recall ? (2 * precision * recall) / (precision + recall) : 0;
    return { code, support, precision, recall, f1 };
  }).filter((r) => r.support > 0 || r.precision > 0);
  const same = [];
  for (let i = 0; i < n; i++) if (reference[i] === predicted[i] && refQ && predQ) same.push(i);
  const qualityKappa = refQ && predQ ? weightedKappa(same.map((i) => refQ[i]), same.map((i) => predQ[i])) : NaN;
  const macroF1 = perCode.filter((r) => r.support > 0).reduce((s, r) => s + r.f1, 0) / Math.max(1, perCode.filter((r) => r.support > 0).length);
  return { n, accuracy: agree / n, kappa: cohenKappa(reference, predicted), macroF1, qualityKappa, perCode, confusion };
}

// ---- Coders ---------------------------------------------------------------------------
function lexiconCodes(items) {
  const build = join(root, ".ci-build");
  const exe = join(build, "lexicon-coder.exe");
  mkdirSync(build, { recursive: true });
  const compile = spawnSync("mcs", [
    "-langversion:latest", "-nowarn:0414", "-out:" + exe,
    "Tools/ci/UnityStubs.cs", "Assets/Scripts/SkillLexicon.cs", "Assets/Scripts/CounselingResponseEvaluator.cs",
    "Assets/Scripts/RelationalModelWeights.cs", "Assets/Scripts/CounselingCodebook.cs", "Tools/ci/LexiconCoder.cs",
  ], { cwd: root, encoding: "utf8" });
  if (compile.status !== 0) throw Error("lexicon coder build failed\n" + compile.stdout + compile.stderr);
  const input = items.map((it) => it.id + "\t" + it.utterance.replace(/[\t\r\n]+/g, " ")).join("\n") + "\n";
  const run = spawnSync("mono", [exe], { input, encoding: "utf8", env: { ...process.env, LANG: "C.UTF-8" } });
  if (run.status !== 0) throw Error("lexicon coder failed\n" + run.stderr);
  const out = new Map(run.stdout.trim().split("\n").map((l) => { const [id, code, q] = l.split("\t"); return [id, { code, quality: Number(q) }]; }));
  return items.map((it) => out.get(it.id) || { code: "neutral", quality: 1 });
}

async function llmCodes(items) {
  const results = new Array(items.length);
  let next = 0;
  const worker = async () => {
    while (next < items.length) {
      const i = next++;
      const it = items[i];
      try {
        const r = await fetch(workerUrl + "/code", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ sessionId: "eval-" + it.id, caseId: it.case_id, clientLine: it.client_line, counselorUtterance: it.utterance || " " }),
        });
        const body = await r.json();
        results[i] = r.ok ? { code: body.code, quality: body.quality, rationale: body.rationale, confidence: body.confidence } : { code: "neutral", quality: 1, error: body.error || r.status };
      } catch (e) {
        results[i] = { code: "neutral", quality: 1, error: String(e) };
      }
    }
  };
  await Promise.all([worker(), worker(), worker()]);
  return results;
}

// ---- Report ---------------------------------------------------------------------------
const pct = (v) => (Number.isFinite(v) ? (v * 100).toFixed(1) + "%" : "–");
const num = (v) => (Number.isFinite(v) ? v.toFixed(2) : "–");

function section(title, r) {
  const lines = [`### ${title}`, "", `n = ${r.n} · accuracy ${pct(r.accuracy)} · Cohen's κ ${num(r.kappa)} · macro-F1 ${num(r.macroF1)} · quality κw (same code) ${num(r.qualityKappa)}`, "",
    "| code | support | precision | recall | F1 |", "|---|---:|---:|---:|---:|",
    ...r.perCode.map((c) => `| ${c.code} | ${c.support} | ${num(c.precision)} | ${num(c.recall)} | ${num(c.f1)} |`), ""];
  const used = CODES.filter((c) => CODES.some((p) => r.confusion[c][p] || r.confusion[p][c]));
  lines.push("Confusion (rows = reference, columns = predicted):", "", "| | " + used.map((c) => c.replace(/_/g, " ")).join(" | ") + " |", "|---|" + used.map(() => "---:").join("|") + "|");
  for (const row of used) lines.push(`| ${row.replace(/_/g, " ")} | ` + used.map((c) => r.confusion[row][c] || "·").join(" | ") + " |");
  lines.push("");
  return lines.join("\n");
}

async function main() {
  const rows = parseCsv(readFileSync(goldPath, "utf8"));
  const items = rows.filter((r) => CODES.includes(r.coder_a_code));
  if (!items.length) throw Error("no coded rows in " + goldPath);
  const refCodes = items.map((r) => r.coder_a_code);
  const refQ = items.map((r) => Math.max(0, Math.min(3, Number(r.coder_a_quality) || 0)));
  const status = [...new Set(items.map((r) => r.label_status || "unknown"))].join(", ");

  const report = { gold: goldPath.replace(root + "/", ""), labelStatus: status, generatedUtc: new Date().toISOString(), pairs: {} };
  const lex = lexiconCodes(items);
  report.pairs["lexicon_vs_A"] = compare(refCodes, lex.map((x) => x.code), refQ, lex.map((x) => x.quality));

  const second = items.filter((r) => CODES.includes(r.coder_b_code));
  if (second.length) {
    report.pairs["A_vs_B"] = compare(second.map((r) => r.coder_a_code), second.map((r) => r.coder_b_code),
      second.map((r) => Number(r.coder_a_quality) || 0), second.map((r) => Number(r.coder_b_quality) || 0));
  }

  let llm = null;
  if (workerUrl) {
    llm = await llmCodes(items);
    report.pairs["llm_vs_A"] = compare(refCodes, llm.map((x) => x.code), refQ, llm.map((x) => x.quality));
    report.pairs["llm_vs_lexicon"] = compare(lex.map((x) => x.code), llm.map((x) => x.code), lex.map((x) => x.quality), llm.map((x) => x.quality));
    report.llmErrors = llm.filter((x) => x.error).length;
  }

  report.disagreements = items.map((it, i) => ({
    id: it.id, utterance: it.utterance, reference: refCodes[i], lexicon: lex[i].code, llm: llm ? llm[i].code : undefined,
    llmRationale: llm ? llm[i].rationale : undefined,
  })).filter((d) => d.lexicon !== d.reference || (d.llm && d.llm !== d.reference));

  const md = [
    "# Skill-coding agreement", "",
    `Reference: \`${report.gold}\` (label status: **${status}**). Codebook ko-codebook-1. Generated ${report.generatedUtc}.`, "",
    status.includes("draft")
      ? "> ⚠️ The reference labels are a **draft seed set** written to exercise this pipeline. They are not expert codes, so these numbers are a regression signal, not validation evidence. Replace them with two trained coders (see `eval/coding/README.md`)."
      : "", "",
    ...Object.entries(report.pairs).map(([name, r]) => section(name.replace(/_/g, " "), r)),
    `## Disagreements (${report.disagreements.length})`, "",
    "| id | utterance | reference | lexicon | llm |", "|---|---|---|---|---|",
    ...report.disagreements.map((d) => `| ${d.id} | ${d.utterance.replace(/\|/g, "／") || "(empty)"} | ${d.reference} | ${d.lexicon} | ${d.llm ?? ""} |`), "",
  ].join("\n");
  mkdirSync(outDir, { recursive: true });
  writeFileSync(join(outDir, "report.md"), md);
  writeFileSync(join(outDir, "report.json"), JSON.stringify(report, null, 2));
  for (const [name, r] of Object.entries(report.pairs))
    console.log(`${name.padEnd(16)} n=${r.n} acc=${pct(r.accuracy)} kappa=${num(r.kappa)} macroF1=${num(r.macroF1)}`);

  if (checkFloor) {
    const floorPath = join(root, "eval/coding/floor.json");
    const floor = existsSync(floorPath) ? JSON.parse(readFileSync(floorPath, "utf8")) : { lexiconKappa: 0 };
    const k = report.pairs.lexicon_vs_A.kappa;
    if (!(k >= floor.lexiconKappa)) {
      console.log(`CODING_AGREEMENT_FAIL lexicon kappa ${num(k)} < floor ${floor.lexiconKappa}`);
      process.exit(1);
    }
    console.log(`CODING_AGREEMENT_PASS lexicon kappa ${num(k)} >= floor ${floor.lexiconKappa}`);
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) main().catch((e) => { console.error(e.message); process.exit(1); });
