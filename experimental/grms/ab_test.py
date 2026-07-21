#!/usr/bin/env python3
"""A/B test: Compare mistral:7b vs gemma4:31b on WW2 scoring scenarios."""

import asyncio
import json
import os
import sys
import time
from pathlib import Path

# Force unbuffered output
sys.stdout.reconfigure(line_buffering=True)
os.environ["PYTHONUNBUFFERED"] = "1"

sys.path.insert(0, str(Path(__file__).parent))

import httpx as _httpx

from grms.llm.ollama_provider import OllamaProvider
from grms.models.events import GeopoliticalEvent
from grms.models.leader import LeaderProfile
from grms.models.scoring import KnownOutcome
from grms.services.leader_service import LeaderService
from grms.services.scoring_service import DIMENSION_WEIGHTS, TONE_SIMILARITY

# Patch OllamaProvider to use a longer timeout for A/B testing
_orig_generate = OllamaProvider.generate

async def _patched_generate(self, system_prompt: str, user_prompt: str) -> str:
    from grms.config import settings
    payload = {
        "model": self.model,
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": user_prompt},
        ],
        "stream": False,
        "options": {
            "temperature": settings.llm_temperature,
            "num_predict": 8192,
            **({"seed": settings.llm_seed} if settings.llm_seed is not None else {}),
        },
    }
    async with _httpx.AsyncClient(timeout=300.0) as client:
        resp = await client.post(f"{self.host}/api/chat", json=payload)
        resp.raise_for_status()
        data = resp.json()
        return data["message"]["content"]

OllamaProvider.generate = _patched_generate


MODELS = ["mistral:7b", "gemma4:31b"]
SCENARIOS_PATH = Path(__file__).parent / "grms" / "data" / "scoring" / "ww2_scenarios.json"


def score_prediction(response, known: KnownOutcome) -> dict:
    """Score a single prediction using the updated scoring logic."""
    scores = {}

    if known.escalation_risk is not None:
        diff = abs(response.escalation_risk - known.escalation_risk)
        scores["escalation_risk"] = max(0.0, 1.0 - diff)
        scores["esc_predicted"] = response.escalation_risk
        scores["esc_actual"] = known.escalation_risk

    if known.action_types:
        predicted_set = set(a.action_type for a in response.actions)
        known_set = set(known.action_types)
        tp = len(predicted_set & known_set)
        precision = tp / len(predicted_set) if predicted_set else 0.0
        recall = tp / len(known_set)
        scores["action_types"] = (2 * precision * recall / (precision + recall)) if (precision + recall) > 0 else 0.0
        scores["act_predicted"] = sorted(predicted_set)
        scores["act_actual"] = sorted(known_set)

    predicted_tone = response.verbal_response.tone
    if known.tone:
        if predicted_tone == known.tone:
            scores["tone"] = 1.0
        else:
            scores["tone"] = TONE_SIMILARITY.get((predicted_tone, known.tone), 0.0)
        scores["tone_predicted"] = predicted_tone
        scores["tone_actual"] = known.tone

    total_weight = sum(DIMENSION_WEIGHTS.get(d, 1.0) for d in scores if d in DIMENSION_WEIGHTS)
    scores["overall"] = sum(
        scores[d] * DIMENSION_WEIGHTS[d] for d in DIMENSION_WEIGHTS if d in scores
    ) / max(total_weight, 0.01)

    return scores


class ModelRunner:
    """Runs predictions with a specific model by patching the OllamaProvider."""

    def __init__(self, model_name: str):
        self.model_name = model_name
        self.service = LeaderService()
        self.service.provider = OllamaProvider()
        self.service.provider.model = model_name

    async def predict(self, leader: LeaderProfile, event: GeopoliticalEvent):
        return await self.service.predict(leader, event)

    async def warmup(self):
        """Load model into VRAM with a trivial request."""
        import httpx
        payload = {
            "model": self.model_name,
            "messages": [{"role": "user", "content": "Say OK"}],
            "stream": False,
            "options": {"temperature": 0.0, "num_predict": 5},
        }
        async with httpx.AsyncClient(timeout=300.0) as client:
            await client.post(f"{self.service.provider.host}/api/chat", json=payload)


async def run_ab_test():
    with open(SCENARIOS_PATH) as f:
        scenarios = json.load(f)

    print(f"{'=' * 100}")
    print(f"A/B TEST: {MODELS[0]} vs {MODELS[1]}")
    print(f"Scenarios: {len(scenarios)} WW2 cases")
    print(f"{'=' * 100}\n")

    results = {model: [] for model in MODELS}
    timings = {model: [] for model in MODELS}

    # Run all scenarios for each model sequentially to avoid model swap overhead
    for model in MODELS:
        runner = ModelRunner(model)
        print(f"--- Warming up {model} ---")
        await runner.warmup()
        print(f"--- Running {model} on {len(scenarios)} scenarios ---\n")

        for i, scenario in enumerate(scenarios):
            label = scenario["label"]
            leader = LeaderProfile(**scenario["leader"])
            event = GeopoliticalEvent(**scenario["event"])
            known = KnownOutcome(**scenario["known_outcome"])

            t0 = time.time()
            try:
                response = await runner.predict(leader, event)
                elapsed = time.time() - t0
                scores = score_prediction(response, known)
                scores["success"] = True
                scores["elapsed"] = elapsed
                timings[model].append(elapsed)
            except Exception as e:
                elapsed = time.time() - t0
                scores = {"success": False, "error": str(e), "elapsed": elapsed, "overall": 0.0}
                timings[model].append(elapsed)

            results[model].append({"label": label, **scores})

            if scores.get("success"):
                print(f"  [{i+1:2d}/{len(scenarios)}] {label[:55]:57s} overall={scores['overall']:.3f}  esc={scores.get('escalation_risk', 0):.2f}  act={scores.get('action_types', 0):.2f}  tone={scores.get('tone', 0):.2f}  ({elapsed:.1f}s)")
            else:
                print(f"  [{i+1:2d}/{len(scenarios)}] {label[:55]:57s} FAILED: {scores.get('error', '')[:50]}  ({elapsed:.1f}s)")
        print()

    # Summary
    print(f"\n{'=' * 100}")
    print("SUMMARY")
    print(f"{'=' * 100}\n")

    header = f"{'Case':62s}"
    for model in MODELS:
        short = model.split(":")[0]
        header += f" | {short:>10s}"
    header += " |  Winner"
    print(header)
    print("-" * len(header))

    wins = {model: 0 for model in MODELS}
    ties = 0

    for i in range(len(scenarios)):
        label = scenarios[i]["label"][:60]
        row = f"{label:62s}"
        model_scores = []
        for model in MODELS:
            s = results[model][i]["overall"]
            model_scores.append(s)
            row += f" | {s:10.3f}"

        if abs(model_scores[0] - model_scores[1]) < 0.01:
            row += " |  tie"
            ties += 1
        elif model_scores[0] > model_scores[1]:
            row += f" |  {MODELS[0]}"
            wins[MODELS[0]] += 1
        else:
            row += f" |  {MODELS[1]}"
            wins[MODELS[1]] += 1
        print(row)

    print(f"\n{'─' * 60}")
    print("AGGREGATE METRICS")
    print(f"{'─' * 60}")

    for model in MODELS:
        successful = [r for r in results[model] if r.get("success")]
        avg_overall = sum(r["overall"] for r in successful) / max(len(successful), 1)
        avg_esc = sum(r.get("escalation_risk", 0) for r in successful) / max(len(successful), 1)
        avg_act = sum(r.get("action_types", 0) for r in successful) / max(len(successful), 1)
        avg_tone = sum(r.get("tone", 0) for r in successful) / max(len(successful), 1)
        avg_time = sum(timings[model]) / max(len(timings[model]), 1)
        below_50 = sum(1 for r in successful if r["overall"] < 0.5)

        print(f"\n  {model}:")
        print(f"    Overall:    {avg_overall:.3f}")
        print(f"    Escalation: {avg_esc:.3f}")
        print(f"    Actions:    {avg_act:.3f}")
        print(f"    Tone:       {avg_tone:.3f}")
        print(f"    Below 50%:  {below_50}/{len(successful)}")
        print(f"    Failures:   {len(results[model]) - len(successful)}/{len(scenarios)}")
        print(f"    Avg time:   {avg_time:.1f}s/prediction")

    print(f"\n{'─' * 60}")
    print(f"WINS: {MODELS[0]}={wins[MODELS[0]]}, {MODELS[1]}={wins[MODELS[1]]}, ties={ties}")
    print(f"{'─' * 60}")

    # Save detailed results
    output_path = Path(__file__).parent / "data" / "ab_test_results.json"
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with open(output_path, "w") as f:
        json.dump({"models": MODELS, "results": results, "timings": timings}, f, indent=2, default=str)
    print(f"\nDetailed results saved to: {output_path}")


if __name__ == "__main__":
    asyncio.run(run_ab_test())
