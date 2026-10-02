#!/usr/bin/env bash
# Wraps Dashboard/dashboard.html (a page fragment, also publishable as-is) into the WebGL
# template so it deploys at <site>/dashboard/ next to the simulator.
set -euo pipefail
cd "$(dirname "$0")/.."
out=Assets/WebGLTemplates/CounselCue/dashboard/index.html
mkdir -p "$(dirname "$out")"
{
  printf '<!DOCTYPE html>\n<html lang="ko">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">\n<!-- Built from Dashboard/dashboard.html by Tools/build-dashboard.sh; edit the source, not this file. -->\n</head>\n<body>\n'
  cat Dashboard/dashboard.html
  printf '\n</body>\n</html>\n'
} > "$out"
echo "Wrote $out"
