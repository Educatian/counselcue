#!/usr/bin/env bash
# Compiles the Unity-independent logic (skill detector, relational model) with Mono and runs
# their editor check suites. Usage: Tools/ci/run-csharp-checks.sh
set -euo pipefail
cd "$(dirname "$0")/../.."
out="$(mktemp -d)"
mcs -langversion:latest -nowarn:0414 -out:"$out/checks.exe" \
  Tools/ci/UnityStubs.cs Tools/ci/CheckRunner.cs \
  Assets/Scripts/SkillLexicon.cs \
  Assets/Scripts/CounselingResponseEvaluator.cs \
  Assets/Scripts/RelationalDeliveryModel.cs \
  Assets/Scripts/UiPhrasebook.cs \
  Assets/Editor/RelationalDeliveryModelChecks.cs \
  Assets/Editor/CounselingResponseEvaluatorChecks.cs
LANG=C.UTF-8 mono "$out/checks.exe"
