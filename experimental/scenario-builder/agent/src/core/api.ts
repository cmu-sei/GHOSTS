// The builder API as the agent and the evaluation call it. The agent's client always sends
// X-Via: agent: the API's authority rules hang on it, and the model can't set headers.

export type Who = "stated" | "proposed";
export type Via = "person" | "agent";
export type AnswerType = "text" | "choice" | "list";

export interface QuestionView {
  id: string;
  stage: number;
  text: string;
  why: string;
  recommended?: string;
  answer: AnswerType;
  options?: string[];
}

export interface AnsweredView {
  id: string;
  stage: number;
  value: unknown;
  who: Who;
  by: string;
  via: Via;
  reason?: string;
  time: string;
}

export interface Finding {
  severity: "stop" | "warning";
  code: string;
  message: string;
  /** The stage it is about: the open one, or a closed one whose approval went stale. */
  stage?: number;
}

export interface Gate {
  passes: boolean;
  approval?: string;
  approved: boolean;
  findings: Finding[];
}

export interface Progress {
  kindId: string;
  stage: number;
  stageTitle: string;
  lastStage: boolean;
  gate: Gate;
  open: QuestionView[];
  answered: AnsweredView[];
  draftsDue: string[];
  /** The owner handed every remaining decision to the agent, gates included. */
  delegated?: boolean;
  /** The context the designer gave: pasted text, fetched pages, documents. */
  sources?: SourceSummary[];
}

export interface SourceSummary {
  id: number;
  name: string;
  kind: "text" | "url" | "file";
  characters: number;
}

export interface PassageView {
  source: { id: number; name: string };
  passage: number;
  of: number;
  text: string;
}

export interface ProposalView {
  id: number;
  questionId: string;
  before: unknown;
  after: unknown;
  reason: string;
  proposedBy: string;
  status: "pending" | "applied" | "discarded";
}

export interface DecisionBody {
  value: unknown;
  who?: Who;
  reason?: string;
  quote?: string;
}

/** A refusal or failure, with the API's code and its message, which is written as an instruction. */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;

  constructor(status: number, code: string, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
  }
}

export interface BuilderApiOptions {
  /** "agent" sends X-Via: agent. The person's calls send nothing. */
  via?: Via;
  /** Sent as X-Forwarded-User, as an auth proxy would. */
  user?: string;
  fetch?: typeof fetch;
}

export class BuilderApi {
  private readonly base: string;
  private readonly headers: Record<string, string>;
  private readonly fetchFn: typeof fetch;

  constructor(baseUrl: string, options: BuilderApiOptions = {}) {
    this.base = baseUrl.replace(/\/+$/, "");
    this.headers = {
      "content-type": "application/json",
      ...(options.via === "agent" ? { "x-via": "agent" } : {}),
      ...(options.user ? { "x-forwarded-user": options.user } : {}),
    };
    // Called unbound, a browser's fetch throws "Illegal invocation".
    this.fetchFn = options.fetch ?? ((input, init) => fetch(input, init));
  }

  progress(scenarioId: number): Promise<Progress> {
    return this.call("GET", `/scenarios/${scenarioId}/progress`);
  }

  kind(kindId: string): Promise<any> {
    return this.call("GET", `/kinds/${encodeURIComponent(kindId)}`);
  }

  putDecision(scenarioId: number, questionId: string, body: DecisionBody): Promise<AnsweredView> {
    return this.call("PUT", `/scenarios/${scenarioId}/decisions/${encodeURIComponent(questionId)}`, body);
  }

  propose(scenarioId: number, body: { questionId: string; value: unknown; reason: string }): Promise<ProposalView> {
    return this.call("POST", `/scenarios/${scenarioId}/proposals`, body);
  }

  proposals(scenarioId: number, status?: ProposalView["status"]): Promise<ProposalView[]> {
    return this.call("GET", `/scenarios/${scenarioId}/proposals${status ? `?status=${status}` : ""}`);
  }

  searchSources(scenarioId: number, query: string): Promise<PassageView[]> {
    return this.call("GET", `/scenarios/${scenarioId}/sources/search?q=${encodeURIComponent(query)}`);
  }

  async putDraft(scenarioId: number, field: string, text: string): Promise<void> {
    await this.call("PUT", `/scenarios/${scenarioId}/drafts/${encodeURIComponent(field)}`, { text });
  }

  advance(scenarioId: number): Promise<{ stage: number }> {
    return this.call("POST", `/scenarios/${scenarioId}/advance`);
  }

  async approve(scenarioId: number, stage: number): Promise<void> {
    await this.call("POST", `/scenarios/${scenarioId}/gates/${stage}/approve`);
  }

  apply(scenarioId: number, proposalId: number): Promise<ProposalView> {
    return this.call("POST", `/scenarios/${scenarioId}/proposals/${proposalId}/apply`);
  }

  discard(scenarioId: number, proposalId: number): Promise<ProposalView> {
    return this.call("POST", `/scenarios/${scenarioId}/proposals/${proposalId}/discard`);
  }

  createScenario(name: string, kindId: string): Promise<{ id: number }> {
    return this.call("POST", "/scenarios", { name, kindId });
  }

  pull(scenarioId: number): Promise<Record<string, any>> {
    return this.call("GET", `/scenarios/${scenarioId}`);
  }

  private async call<T>(method: string, path: string, body?: unknown): Promise<T> {
    let response: Response;
    try {
      response = await this.fetchFn(`${this.base}/api${path}`, {
        method,
        headers: this.headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
    } catch (e) {
      throw new ApiError(0, "UNREACHABLE", `Can't reach the Scenario Builder API at ${this.base}: ${(e as Error).message}`);
    }
    const text = await response.text();
    let data: any;
    try {
      data = text ? JSON.parse(text) : undefined;
    } catch {
      data = undefined;
    }
    if (!response.ok)
      throw new ApiError(response.status, data?.code ?? `HTTP_${response.status}`, data?.message ?? `The API answered HTTP ${response.status}.`);
    return data as T;
  }
}
