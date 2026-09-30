#!/usr/bin/env node
// Refit CounselCue's relational model (Assets/Scripts/RelationalModelWeights.cs) from data.
//
//   node eval/calibrate-relational.mjs --ratings eval/calibration/my-ratings.csv
//   node eval/calibrate-relational.mjs --transcripts eval/calibration/coded-sessions.csv
//   … add --write to save Assets/Resources/CounselCue/relational-weights.json
//   node eval/calibrate-relational.mjs --check   # CI: pipeline on the synthetic example
//
// Inputs (CSV, values 0–1, 0–100 or 1–7 are all accepted and rescaled to 0–1):
//  --ratings     one row per rated counselor turn: code, quality, safety_before,
//                guardedness_before, disclosure_before, safety_after, guardedness_after,
//                disclosure_after [, rater, case_id, id]. Experts rate the client state
//                before and after reading the counselor turn.
//  --transcripts AVP-style coded sessions: session_id, turn, code, quality, openness
//                [, safety, guardedness] where openness is the rated client openness AFTER
//                the turn; deltas come from consecutive turns of the same session.
//
// Method: per code, empirical-Bayes shrinkage toward the prior (weight n/(n+k), k=8) for the
// disclosure and safety deltas, a pooled quality slope, the guardedness coupling from a
// regression of Δguardedness on Δsafety, and a grid search for the optional safety gate.
// 5-fold cross-validation compares prior and fitted prediction error.
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parseCsv } from "./coding-agreement.mjs";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const opt = (n) => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : undefined; };
const SHRINK_K = 8;
const TYPICAL = { reflection_exploration: 3, reflection: 2, validation: 2, open_question: 2, neutral: 1, closed_question: 1, why_question: 1 };

// ---- Prior, read from the C# source so the two never drift -----------------------------
export function loadPrior() {
  const cs = readFileSync(join(root, "Assets/Scripts/RelationalModelWeights.cs"), "utf8");
  const book = readFileSync(join(root, "Assets/Scripts/CounselingCodebook.cs"), "utf8");
  const names = Object.fromEntries([...book.matchAll(/public const string (\w+) = "([a-z_]+)";/g)].map((m) => [m[1], m[2]]));
  const codes = [...cs.matchAll(/W\(CounselingCodebook\.(\w+),\s*(-?[\d.]+)f,\s*(-?[\d.]+)f,\s*(-?[\d.]+)f\)/g)].map((m) => ({
    code: names[m[1]], safety: Number(m[2]), qualitySlope: Number(m[3]), disclosure: Number(m[4]), observations: 0,
  }));
  const num = (field, fallback) => { const m = cs.match(new RegExp(`public float ${field} = (-?[\\d.]+)f;`)); return m ? Number(m[1]) : fallback; };
  const version = (cs.match(/public string version = "([^"]+)"/) || [])[1];
  if (codes.length < 10) throw Error("could not read the prior from RelationalModelWeights.cs");
  return { version, guardednessCoupling: num("guardednessCoupling", 0.65), deliveryDisclosureCoupling: num("deliveryDisclosureCoupling", 0.8), safetyGate: num("safetyGate", 0), codes };
}

// ---- Data -----------------------------------------------------------------------------
const scale = (v) => {
  const x = Number(v);
  if (!Number.isFinite(x)) return NaN;
  if (x > 7) return x / 100;          // 0–100
  if (x > 1) return (x - 1) / 6;      // 1–7 Likert
  return x;                           // 0–1
};

export function fromRatings(rows) {
  return rows.map((r) => ({
    code: r.code, quality: Number(r.quality),
    safety0: scale(r.safety_before), guard0: scale(r.guardedness_before), disc0: scale(r.disclosure_before),
    dSafety: scale(r.safety_after) - scale(r.safety_before),
    dGuard: scale(r.guardedness_after) - scale(r.guardedness_before),
    dDisc: scale(r.disclosure_after) - scale(r.disclosure_before),
  })).filter((o) => o.code && Number.isFinite(o.dDisc));
}

export function fromTranscripts(rows) {
  const bySession = new Map();
  for (const r of rows) {
    if (!bySession.has(r.session_id)) bySession.set(r.session_id, []);
    bySession.get(r.session_id).push(r);
  }
  const out = [];
  for (const turns of bySession.values()) {
    turns.sort((a, b) => Number(a.turn) - Number(b.turn));
    for (let i = 1; i < turns.length; i++) {
      const prev = turns[i - 1], cur = turns[i];
      const s0 = scale(prev.safety), s1 = scale(cur.safety), g0 = scale(prev.guardedness), g1 = scale(cur.guardedness);
      out.push({
        code: cur.code, quality: Number(cur.quality),
        safety0: Number.isFinite(s0) ? s0 : 0.4, guard0: g0, disc0: scale(prev.openness),
        dDisc: scale(cur.openness) - scale(prev.openness),
        dSafety: Number.isFinite(s1 - s0) ? s1 - s0 : NaN,
        dGuard: Number.isFinite(g1 - g0) ? g1 - g0 : NaN,
      });
    }
  }
  return out.filter((o) => o.code && Number.isFinite(o.dDisc));
}

// ---- Model ----------------------------------------------------------------------------
const find = (w, code) => w.codes.find((c) => c.code === code) || w.codes.find((c) => c.code === "neutral");
export function predict(w, o) {
  const c = find(w, o.code);
  const q = Number.isFinite(o.quality) ? o.quality - (TYPICAL[o.code] ?? 0) : 0;
  let disc = c.disclosure;
  if (disc > 0 && w.safetyGate > 0) disc *= 1 - w.safetyGate + 2 * w.safetyGate * (o.safety0 ?? 0.4);
  const safety = c.safety + c.qualitySlope * q;
  return { dDisc: disc, dSafety: safety, dGuard: -safety * w.guardednessCoupling };
}

const mean = (xs) => xs.reduce((s, x) => s + x, 0) / xs.length;
const rmse = (w, data, key) => {
  const xs = data.filter((o) => Number.isFinite(o[key]));
  return xs.length ? Math.sqrt(mean(xs.map((o) => (predict(w, o)[key] - o[key]) ** 2))) : NaN;
};

export function fit(prior, data) {
  const w = structuredClone(prior);
  const round = (x) => Math.round(x * 1000) / 1000;
  for (const c of w.codes) {
    const obs = data.filter((o) => o.code === c.code);
    c.observations = obs.length;
    if (!obs.length) continue;
    const wt = obs.length / (obs.length + SHRINK_K);
    c.disclosure = round(wt * mean(obs.map((o) => o.dDisc)) + (1 - wt) * c.disclosure);
    const safetyObs = obs.filter((o) => Number.isFinite(o.dSafety));
    if (safetyObs.length) {
      const ws = safetyObs.length / (safetyObs.length + SHRINK_K);
      // Remove the quality effect before averaging so the intercept is at typical quality.
      const centred = safetyObs.map((o) => o.dSafety - c.qualitySlope * ((o.quality || 0) - (TYPICAL[c.code] ?? 0)));
      c.safety = round(ws * mean(centred) + (1 - ws) * c.safety);
    }
  }
  // Pooled quality slope (per code would overfit small samples): regress residual Δsafety on Δquality.
  const withQ = data.filter((o) => Number.isFinite(o.dSafety) && Number.isFinite(o.quality));
  if (withQ.length >= 20) {
    const pts = withQ.map((o) => ({ x: o.quality - (TYPICAL[o.code] ?? 0), y: o.dSafety - find(w, o.code).safety }));
    const sxx = pts.reduce((s, p) => s + p.x * p.x, 0);
    if (sxx > 0) {
      const slope = pts.reduce((s, p) => s + p.x * p.y, 0) / sxx;
      const n = pts.length, ws = n / (n + SHRINK_K * 4);
      for (const c of w.codes) c.qualitySlope = round(Math.max(0, ws * slope + (1 - ws) * c.qualitySlope));
    }
  }
  const pairs = data.filter((o) => Number.isFinite(o.dSafety) && Number.isFinite(o.dGuard));
  if (pairs.length >= 20) {
    const sxx = pairs.reduce((s, o) => s + o.dSafety ** 2, 0);
    if (sxx > 0) w.guardednessCoupling = round(Math.max(0, Math.min(1.5, -pairs.reduce((s, o) => s + o.dSafety * o.dGuard, 0) / sxx)));
  }
  let best = { gate: w.safetyGate, err: rmse(w, data, "dDisc") };
  for (let g = 0; g <= 0.8001; g += 0.1) {
    const trial = { ...w, safetyGate: round(g) };
    const err = rmse(trial, data, "dDisc");
    if (err < best.err - 1e-6) best = { gate: round(g), err };
  }
  w.safetyGate = best.gate;
  return w;
}

export function crossValidate(prior, data, folds = 5) {
  const idx = data.map((_, i) => i);
  const acc = { prior: { dDisc: [], dSafety: [] }, fitted: { dDisc: [], dSafety: [] } };
  for (let f = 0; f < folds; f++) {
    const test = idx.filter((i) => i % folds === f).map((i) => data[i]);
    const train = idx.filter((i) => i % folds !== f).map((i) => data[i]);
    if (!test.length || !train.length) continue;
    const fitted = fit(prior, train);
    for (const key of ["dDisc", "dSafety"]) {
      acc.prior[key].push(rmse(prior, test, key));
      acc.fitted[key].push(rmse(fitted, test, key));
    }
  }
  const avg = (xs) => { const v = xs.filter(Number.isFinite); return v.length ? mean(v) : NaN; };
  return { prior: { dDisc: avg(acc.prior.dDisc), dSafety: avg(acc.prior.dSafety) }, fitted: { dDisc: avg(acc.fitted.dDisc), dSafety: avg(acc.fitted.dSafety) } };
}

function report(prior, fitted, data, cv, source) {
  const f = (x) => (Number.isFinite(x) ? x.toFixed(3) : "–");
  const lines = [
    "# Relational model calibration", "",
    `Source: \`${source}\` · ${data.length} observations · prior \`${prior.version}\` → \`${fitted.version}\``, "",
    `5-fold CV RMSE — disclosure Δ: prior ${f(cv.prior.dDisc)} → fitted ${f(cv.fitted.dDisc)}; safety Δ: prior ${f(cv.prior.dSafety)} → fitted ${f(cv.fitted.dSafety)}`, "",
    `Guardedness coupling ${f(prior.guardednessCoupling)} → ${f(fitted.guardednessCoupling)} · safety gate ${f(prior.safetyGate)} → ${f(fitted.safetyGate)}`, "",
    "| code | n | disclosure (prior → fit) | safety (prior → fit) | quality slope |", "|---|---:|---|---|---:|",
    ...fitted.codes.map((c) => { const p = prior.codes.find((x) => x.code === c.code); return `| ${c.code} | ${c.observations} | ${f(p.disclosure)} → ${f(c.disclosure)} | ${f(p.safety)} → ${f(c.safety)} | ${f(c.qualitySlope)} |`; }),
    "",
  ];
  const expl = fitted.codes.find((c) => c.code === "open_question").disclosure;
  const emp = Math.max(fitted.codes.find((c) => c.code === "reflection").disclosure, fitted.codes.find((c) => c.code === "validation").disclosure);
  lines.push(`Exploration / empathy ratio on disclosure: ${emp > 0 ? (expl / emp).toFixed(2) + "×" : "n/a"} (AVP reported ≈3×).`, "");
  return lines.join("\n");
}

async function main() {
  const prior = loadPrior();
  const check = args.includes("--check");
  const ratingsPath = opt("--ratings") || (check ? join(root, "eval/calibration/example-ratings.csv") : undefined);
  const transcriptsPath = opt("--transcripts");
  if (!ratingsPath && !transcriptsPath) {
    console.log("Usage: --ratings file.csv | --transcripts file.csv [--write] | --check");
    process.exit(2);
  }
  const source = ratingsPath || transcriptsPath;
  const rows = parseCsv(readFileSync(resolve(source), "utf8"));
  const data = ratingsPath ? fromRatings(rows) : fromTranscripts(rows);
  if (!data.length) throw Error("no usable observations in " + source);
  const fitted = fit(prior, data);
  fitted.version = `fit-${new Date().toISOString().slice(0, 10).replace(/-/g, "")}-n${data.length}`;
  fitted.source = `Fitted by eval/calibrate-relational.mjs from ${source.replace(root + "/", "")} (${data.length} observations), shrunk toward ${prior.version}.`;
  const cv = crossValidate(prior, data);
  const md = report(prior, fitted, data, cv, source.replace(root + "/", ""));
  console.log(md);
  const outDir = join(root, "eval/calibration");
  mkdirSync(outDir, { recursive: true });
  if (!check) writeFileSync(join(outDir, "last-fit.md"), md);

  if (check) {
    const expl = fitted.codes.find((c) => c.code === "open_question").disclosure;
    const refl = fitted.codes.find((c) => c.code === "reflection").disclosure;
    const ok = cv.fitted.dDisc < cv.prior.dDisc && expl > refl && prior.codes.length === 10;
    console.log(ok ? "CALIBRATION_PIPELINE_PASS" : "CALIBRATION_PIPELINE_FAIL");
    process.exit(ok ? 0 : 1);
  }
  if (args.includes("--write")) {
    const target = join(root, "Assets/Resources/CounselCue/relational-weights.json");
    mkdirSync(dirname(target), { recursive: true });
    writeFileSync(target, JSON.stringify(fitted, null, 2) + "\n");
    console.log("Wrote " + target.replace(root + "/", "") + " — Unity loads it on the next play; turn records store its version.");
  }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) main().catch((e) => { console.error(e.message); process.exit(1); });
