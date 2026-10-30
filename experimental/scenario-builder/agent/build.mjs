// Bundles the agent for the browser with esbuild (jeffort's `node build.mjs web`):
//
//   node build.mjs      # dist/web/agent.js and agent.d.ts, for the builder UI's side panel
//
// The evaluation runs the core straight from source under Node, so it needs no build.

import { build } from "esbuild";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";

// The agent only speaks openai-completions, and imports pi-ai's API for it alone. Another
// vendor's SDK in the bundle means something imported a provider, which brings them all.
function onlyOpenAISdk() {
  return {
    name: "only-openai-sdk",
    setup(b) {
      b.onResolve({ filter: /^(@anthropic-ai\/sdk|@google\/genai|@mistralai\/mistralai|@aws-sdk\/)/ }, a => ({
        errors: [{ text: `${a.path} would be bundled, imported by ${a.importer}. Import pi-ai's openai-completions API, not a provider.` }],
      }));
    },
  };
}

mkdirSync("dist/web", { recursive: true });
const result = await build({
  bundle: true,
  platform: "browser",
  target: "es2022",
  format: "esm",
  sourcemap: true,
  logLevel: "warning",
  entryPoints: ["src/web/index.ts"],
  outfile: "dist/web/agent.js",
  plugins: [onlyOpenAISdk()],
  metafile: true,
});
writeFileSync("dist/web/meta.json", JSON.stringify(result.metafile));
writeFileSync("dist/web/agent.d.ts", `${readFileSync("src/web/types.ts", "utf8")}
export declare const createWebAgent: CreateWebAgent;
`);
