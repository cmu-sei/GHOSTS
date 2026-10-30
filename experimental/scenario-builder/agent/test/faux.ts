// pi-ai's faux provider standing in for the model server: a test scripts its replies with
// faux.setResponses, and passes `stream` as the session's streamFn (jeffort's test/faux.ts).

import { createModels, fauxProvider } from "@earendil-works/pi-ai";
import type { StreamFn } from "@earendil-works/pi-agent-core";

export function fauxModel() {
  const faux = fauxProvider();
  const models = createModels();
  models.setProvider(faux.provider);
  // The model keeps its own id; only where the call goes changes.
  const stream: StreamFn = (model, context, options) =>
    models.streamSimple({ ...model, provider: faux.provider.id, api: faux.api, compat: undefined }, context, options);
  return { faux, stream };
}
