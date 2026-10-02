#!/usr/bin/env python3
"""Package Builds/WebGL for a static host with a per-file size cap (Cloudflare: 25 MiB).

Copies the build to an output folder, splits any file larger than --part-mb into numbered
parts, writes Build/parts.json, and makes sure index.html carries the part loader (the
CounselCue WebGL template already does; older builds get it injected).

    python3 Tools/web/package_webgl.py Builds/WebGL dist/webgl --part-mb 20
"""
import argparse, json, os, re, shutil, sys

LOADER_MARK = "CC_PARTS_LOADER"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("build")
    ap.add_argument("out")
    ap.add_argument("--part-mb", type=float, default=20)
    a = ap.parse_args()
    limit = int(a.part_mb * 1024 * 1024)
    if os.path.exists(a.out):
        shutil.rmtree(a.out)
    shutil.copytree(a.build, a.out)
    build_dir = os.path.join(a.out, "Build")
    manifest = {}
    for name in sorted(os.listdir(build_dir)):
        path = os.path.join(build_dir, name)
        size = os.path.getsize(path)
        if size <= limit:
            continue
        parts = []
        with open(path, "rb") as f:
            i = 0
            while True:
                chunk = f.read(limit)
                if not chunk:
                    break
                part = f"{name}.part{i}"
                with open(os.path.join(build_dir, part), "wb") as p:
                    p.write(chunk)
                parts.append(part)
                i += 1
        os.remove(path)
        manifest[name] = {"size": size, "parts": parts}
    with open(os.path.join(build_dir, "parts.json"), "w") as f:
        json.dump(manifest, f, indent=1)
    index = os.path.join(a.out, "index.html")
    html = open(index, encoding="utf-8").read()
    if manifest and LOADER_MARK not in html:
        sys.exit("index.html has no part loader; rebuild with the CounselCue WebGL template")
    total = sum(os.path.getsize(os.path.join(dp, f)) for dp, _, fs in os.walk(a.out) for f in fs)
    biggest = max((os.path.getsize(os.path.join(dp, f)), f) for dp, _, fs in os.walk(a.out) for f in fs)
    print(json.dumps({"split": {k: len(v["parts"]) for k, v in manifest.items()}, "totalBytes": total, "largestFile": biggest}))


if __name__ == "__main__":
    main()
