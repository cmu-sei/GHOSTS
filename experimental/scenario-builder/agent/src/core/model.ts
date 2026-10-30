// The models the builder's API offers, as Pi models. Every one is reached through the API's
// pass-through at /api/model, which holds the keys: the agent never sees one.

import type { Model } from "@earendil-works/pi-ai";

export interface ModelInfo {
  id: string;
  contextWindow: number;
}

export async function listModels(apiBase: string, fetchFn: typeof fetch = (input, init) => fetch(input, init)): Promise<ModelInfo[]> {
  const url = `${apiBase.replace(/\/+$/, "")}/api/model/models`;
  const response = await fetchFn(url);
  if (!response.ok) throw new Error(`${url} answered HTTP ${response.status}`);
  const body = (await response.json()) as { data?: { id: string; context_window?: number }[] };
  return (body.data ?? []).map(m => ({ id: m.id, contextWindow: m.context_window ?? 32768 }));
}

/** Settings that hold up against vLLM, Ollama and LiteLLM alike (jeffort's DEFAULT_COMPAT). */
export function builderModel(apiBase: string, info: ModelInfo): Model<"openai-completions"> {
  return {
    id: info.id,
    name: info.id,
    api: "openai-completions",
    provider: "builder",
    baseUrl: `${apiBase.replace(/\/+$/, "")}/api/model`,
    reasoning: false,
    input: ["text"],
    cost: { input: 0, output: 0, cacheRead: 0, cacheWrite: 0 },
    contextWindow: info.contextWindow,
    maxTokens: 8192,
    compat: { supportsStore: false, supportsDeveloperRole: false, maxTokensField: "max_tokens" },
  };
}
