// Verifies the WebGL jslib parses and exports every function the C# bridge imports.
import { readFileSync } from "node:fs";

const jslib = readFileSync("Assets/Plugins/WebGL/CounselCueWebBridge.jslib", "utf8");
const bridge = readFileSync("Assets/Scripts/CounselCueWebBridge.cs", "utf8");
let library;
new Function("mergeInto", "LibraryManager", jslib)((_, exports) => (library = exports), { library: {} });
const imported = [...bridge.matchAll(/extern void (\w+)\(/g)].map((m) => m[1]);
const missing = imported.filter((name) => typeof library?.[name] !== "function");
if (!imported.length || missing.length) {
  console.error("JSLIB_CHECK_FAIL missing:", missing.join(", ") || "(no DllImports found)");
  process.exit(1);
}
console.log(`JSLIB_CHECK_PASS exports=${Object.keys(library).length} imports=${imported.length}`);
