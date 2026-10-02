#!/usr/bin/env python3
"""Cross-checks that every counseling case is defined consistently across Unity content,
English UI text, the server persona prompt, and generated case assets.

Catches drift such as a case re-authored in Unity whose LLM persona still describes the old
client. Usage: python3 Tools/ci/check_case_consistency.py
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
factory = (ROOT / "Assets/Editor/CounselingContentFactory.cs").read_text(encoding="utf-8")
worker = (ROOT / "Server/CounselCue.EdgeWorker/src/index.js").read_text(encoding="utf-8")

specs = re.findall(r'new CaseSpec\("([^"]+)", "([^"]+)", "([^"]+)", "(\d+)세"', factory)
english_ids = set(re.findall(r'case "([a-z0-9-]+)":\s*\n\s*return English\(', factory))
personas = dict(re.findall(r'"([a-z0-9-]+-\d\d)":\s*`([^`]*)`', worker))
asset_ids = set()
for asset in (ROOT / "Assets/Data/Cases").glob("*.asset"):
    match = re.search(r"^\s*caseId:\s*(\S+)", asset.read_text(encoding="utf-8"), re.M)
    if match:
        asset_ids.add(match.group(1))

errors = []
spec_ids = [s[0] for s in specs]
if len(spec_ids) != 5:
    errors.append(f"expected 5 case specs, found {len(spec_ids)}")
for case_id, title, name, age in specs:
    if case_id not in english_ids:
        errors.append(f"{case_id}: no English UI text in CounselingContentFactory.EnglishFor")
    persona = personas.get(case_id)
    if persona is None:
        errors.append(f"{case_id}: no server persona in EdgeWorker PERSONAS")
        continue
    if name not in persona:
        errors.append(f"{case_id}: persona does not name the client {name}")
    if not re.search(rf"\b{age}\b", persona):
        errors.append(f"{case_id}: persona age does not match {age}")
    if case_id not in asset_ids:
        errors.append(f"{case_id}: no generated case asset in Assets/Data/Cases (rebuild the catalog)")
for extra in sorted(set(personas) - set(spec_ids)):
    errors.append(f"server persona {extra} has no Unity case")

# Scenario-specific facts that must survive re-authoring on both sides.
must_match = {
    "adolescent-pressure-01": [("스카프|히잡", r"headscarf|hijab"), ("무슬림", r"Muslim")],
    "older-bereavement-01": [("사별", r"spouse")],
    "international-belonging-01": [("유학생|대학원", r"graduate student")],
}
for case_id, pairs in must_match.items():
    unity_text = factory.split(f'new CaseSpec("{case_id}"', 1)[1].split("new CaseSpec(", 1)[0]
    persona = personas.get(case_id, "")
    for ko_pattern, en_pattern in pairs:
        if re.search(ko_pattern, unity_text) and not re.search(en_pattern, persona, re.I):
            errors.append(f"{case_id}: Unity case mentions /{ko_pattern}/ but persona lacks /{en_pattern}/")

if errors:
    print("CASE_CONSISTENCY_FAIL")
    for error in errors:
        print(" -", error)
    sys.exit(1)
print(f"CASE_CONSISTENCY_PASS cases={len(spec_ids)} personas={len(personas)} assets={len(asset_ids)}")
