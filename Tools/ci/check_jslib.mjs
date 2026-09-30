// Verifies the WebGL jslib parses and exports every function the C# bridge imports.
import { readFileSync, readdirSync } from "node:fs";

const jslib = readFileSync("Assets/Plugins/WebGL/CounselCueWebBridge.jslib", "utf8");
// Every script may import from the jslib (the bridge, research export, …).
const bridge = readdirSync("Assets/Scripts").filter((f) => f.endsWith(".cs"))
  .map((f) => readFileSync("Assets/Scripts/" + f, "utf8")).join("\n");
let library;
new Function("mergeInto", "LibraryManager", jslib)((_, exports) => (library = exports), { library: {} });
const imported = [...bridge.matchAll(/extern void (\w+)\(/g)].map((m) => m[1]);
const missing = imported.filter((name) => typeof library?.[name] !== "function");
if (!imported.length || missing.length) {
  console.error("JSLIB_CHECK_FAIL missing:", missing.join(", ") || "(no DllImports found)");
  process.exit(1);
}
console.log(`JSLIB_CHECK_PASS exports=${Object.keys(library).length} imports=${imported.length}`);
