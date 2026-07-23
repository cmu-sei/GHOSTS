"""Batch scoring service - runs predictions against known outcomes and scores accuracy.

Batch runs still produce aggregate metrics (RMSE, bias, accuracy) for the scoring_runs
table, but each individual case also creates/updates a SavedPrediction with its score.
"""

import logging
import math

from grms.config import settings
from grms.db import persist_run
from grms.models.scoring import (
    AblationCaseResult,
    AblationResult,
    BatchRunResult,
    CaseResult,
    KnownOutcome,
    RunMetadata,
    ScoreDimension,
    ScoringCase,
)
from grms.prompt_hash import compute_prompt_hash
from grms.services.leader_service import LeaderService

logger = logging.getLogger("grms.scoring_service")

TONE_SIMILARITY = {
    ("measured", "conciliatory"): 0.7,
    ("conciliatory", "measured"): 0.7,
    ("defiant", "threatening"): 0.6,
    ("threatening", "defiant"): 0.6,
    ("measured", "dismissive"): 0.5,
    ("dismissive", "measured"): 0.5,
    ("conciliatory", "dismissive"): 0.4,
    ("dismissive", "conciliatory"): 0.4,
    ("defiant", "measured"): 0.3,
    ("measured", "defiant"): 0.3,
    ("threatening", "measured"): 0.3,
    ("measured", "threatening"): 0.3,
    ("threatening", "dismissive"): 0.3,
    ("dismissive", "threatening"): 0.3,
    ("defiant", "dismissive"): 0.2,
    ("dismissive", "defiant"): 0.2,
    ("defiant", "conciliatory"): 0.1,
    ("conciliatory", "defiant"): 0.1,
    ("conciliatory", "threatening"): 0.0,
    ("threatening", "conciliatory"): 0.0,
}

DIMENSION_WEIGHTS = {
    "escalation_risk": 0.50,
    "action_types": 0.30,
    "tone": 0.20,
}


class ScoringService:
    def __init__(self):
        self.leader_service = LeaderService()

    async def run_ablation(self, cases: list[ScoringCase], include_generic_llm: bool = True) -> AblationResult:
        """Run all baselines + full model side-by-side to demonstrate value-add."""
        from grms.services.baselines import (
            CognitiveOnlyPredictor,
            GenericLLMPredictor,
            HeuristicPredictor,
            NullPredictor,
        )

        baselines: dict[str, object] = {
            "null": NullPredictor(),
            "heuristic": HeuristicPredictor(),
            "cognitive_only": CognitiveOnlyPredictor(),
        }
        if include_generic_llm:
            baselines["generic_llm"] = GenericLLMPredictor()

        full_scores: list[float] = []
        baseline_scores: dict[str, list[float]] = {name: [] for name in baselines}
        case_results: list[AblationCaseResult] = []

        for case in cases:
            full_result = await self._score_case(case)
            full_scores.append(full_result.overall_score)

            case_baseline_scores = {}
            for name, predictor in baselines.items():
                try:
                    response = await predictor.predict(case.leader, case.event)
                    score = self._score_response(response, case.known_outcome)
                    case_baseline_scores[name] = score
                    baseline_scores[name].append(score)
                except Exception as e:
                    logger.error(f"Baseline '{name}' failed on '{case.label}': {e}")
                    case_baseline_scores[name] = 0.0
                    baseline_scores[name].append(0.0)

            best_baseline_score = max(case_baseline_scores.values()) if case_baseline_scores else 0.0
            case_results.append(AblationCaseResult(
                case_id=case.id,
                label=case.label,
                full_model_score=full_result.overall_score,
                baseline_scores=case_baseline_scores,
                value_added=full_result.overall_score - best_baseline_score,
            ))

        full_agg = sum(full_scores) / max(len(full_scores), 1)
        baseline_aggs = {
            name: sum(scores) / max(len(scores), 1)
            for name, scores in baseline_scores.items()
        }
        best_baseline_name = max(baseline_aggs, key=baseline_aggs.get) if baseline_aggs else "none"
        best_baseline_agg = baseline_aggs.get(best_baseline_name, 0.0)

        return AblationResult(
            full_model_aggregate=full_agg,
            baseline_aggregates=baseline_aggs,
            value_added=full_agg - best_baseline_agg,
            best_baseline=best_baseline_name,
            cases=case_results,
            total_cases=len(cases),
            baselines_used=list(baselines.keys()),
        )

    def _score_response(self, response, known: KnownOutcome) -> float:
        """Score a single response against a known outcome (same logic as _score_case but takes a response directly)."""
        dimensions: list[ScoreDimension] = []

        if known.escalation_risk is not None:
            diff = abs(response.escalation_risk - known.escalation_risk)
            score = max(0.0, 1.0 - diff)
            dimensions.append(ScoreDimension(
                dimension="escalation_risk", predicted=response.escalation_risk,
                actual=known.escalation_risk, score=score,
            ))

        if known.action_types:
            predicted_types = [a.action_type for a in response.actions]
            predicted_set = set(predicted_types)
            known_set = set(known.action_types)
            true_positives = len(predicted_set & known_set)
            precision = true_positives / len(predicted_set) if predicted_set else 0.0
            recall = true_positives / len(known_set)
            score = (2 * precision * recall / (precision + recall)) if (precision + recall) > 0 else 0.0
            dimensions.append(ScoreDimension(
                dimension="action_types", predicted=predicted_types,
                actual=known.action_types, score=score,
            ))

        if known.tone:
            predicted_tone = response.verbal_response.tone
            if predicted_tone == known.tone:
                score = 1.0
            else:
                score = TONE_SIMILARITY.get((predicted_tone, known.tone), 0.0)
            dimensions.append(ScoreDimension(
                dimension="tone", predicted=predicted_tone, actual=known.tone, score=score,
            ))

        total_weight = sum(DIMENSION_WEIGHTS.get(d.dimension, 1.0) for d in dimensions)
        return sum(d.score * DIMENSION_WEIGHTS.get(d.dimension, 1.0) for d in dimensions) / max(total_weight, 0.01)

    async def run_batch(self, cases: list[ScoringCase]) -> BatchRunResult:
        results: list[CaseResult] = []
        failures: list[str] = []

        for case in cases:
            try:
                result = await self._score_case(case)
                results.append(result)
            except Exception as e:
                logger.error(f"Failed to score case '{case.label}': {e}")
                failures.append(f"{case.label}: {e}")

        aggregate_score = sum(r.overall_score for r in results) / max(len(results), 1)

        escalation_errors = []
        escalation_biases = []
        action_scores = []
        tone_scores = []

        for r in results:
            for d in r.dimensions:
                if d.dimension == "escalation_risk":
                    error = float(d.predicted) - float(d.actual)
                    escalation_errors.append(error ** 2)
                    escalation_biases.append(error)
                elif d.dimension == "action_types":
                    action_scores.append(d.score)
                elif d.dimension == "tone":
                    tone_scores.append(d.score)

        escalation_rmse = math.sqrt(sum(escalation_errors) / max(len(escalation_errors), 1))
        escalation_bias = sum(escalation_biases) / max(len(escalation_biases), 1)
        action_type_accuracy = sum(action_scores) / max(len(action_scores), 1)
        tone_accuracy = sum(tone_scores) / max(len(tone_scores), 1)

        eras = list({c.era for c in cases})
        scenario_era = eras[0] if len(eras) == 1 else "all"

        prompt_hash = compute_prompt_hash()
        metadata = RunMetadata(
            llm_source=settings.llm_source,
            llm_model=settings.llm_model,
            llm_temperature=settings.llm_temperature,
            llm_seed=settings.llm_seed,
            prompt_hash=prompt_hash,
            scenario_era=scenario_era,
        )

        batch_result = BatchRunResult(
            llm_model=settings.llm_model,
            total_cases=len(cases),
            aggregate_score=aggregate_score,
            escalation_rmse=escalation_rmse,
            escalation_bias=escalation_bias,
            action_type_accuracy=action_type_accuracy,
            tone_accuracy=tone_accuracy,
            cases=results,
            failures=failures,
            metadata=metadata,
        )

        await self._persist(batch_result, cases)

        return batch_result

    async def _persist(self, result: BatchRunResult, cases: list[ScoringCase]) -> None:
        try:
            case_map = {c.id: c for c in cases}
            run_data = {
                "id": str(result.id),
                "run_at": result.run_at.isoformat(),
                "llm_source": result.metadata.llm_source if result.metadata else settings.llm_source,
                "llm_model": result.llm_model,
                "llm_temperature": result.metadata.llm_temperature if result.metadata else settings.llm_temperature,
                "llm_seed": result.metadata.llm_seed if result.metadata else settings.llm_seed,
                "prompt_hash": result.metadata.prompt_hash if result.metadata else "",
                "scenario_era": result.metadata.scenario_era if result.metadata else "all",
                "total_cases": result.total_cases,
                "aggregate_score": result.aggregate_score,
                "escalation_rmse": result.escalation_rmse,
                "escalation_bias": result.escalation_bias,
                "action_type_accuracy": result.action_type_accuracy,
                "tone_accuracy": result.tone_accuracy,
                "failures": result.failures,
            }
            case_results = []
            for cr in result.cases:
                source_case = case_map.get(cr.case_id)
                actual_esc = None
                actual_actions = []
                actual_tone = None
                for d in cr.dimensions:
                    if d.dimension == "escalation_risk":
                        actual_esc = float(d.actual)
                    elif d.dimension == "action_types":
                        actual_actions = d.actual if isinstance(d.actual, list) else []
                    elif d.dimension == "tone":
                        actual_tone = str(d.actual) if d.actual else None
                case_results.append({
                    "case_id": cr.case_id,
                    "label": cr.label,
                    "era": source_case.era if source_case else "ww2",
                    "leader_name": source_case.leader.name if source_case else "",
                    "leader_country": source_case.leader.country if source_case else "",
                    "overall_score": cr.overall_score,
                    "predicted_escalation": cr.predicted_escalation_risk,
                    "actual_escalation": actual_esc,
                    "predicted_actions": cr.predicted_actions,
                    "actual_actions": actual_actions,
                    "predicted_tone": cr.predicted_tone,
                    "actual_tone": actual_tone,
                    "confidence": cr.confidence,
                    "dimensions": [d.model_dump() for d in cr.dimensions],
                })
            await persist_run(run_data, case_results)
        except Exception as e:
            logger.warning("Failed to persist scoring run: %s", e)

    async def _score_case(self, case: ScoringCase) -> CaseResult:
        response = await self.leader_service.predict(case.leader, case.event)

        dimensions: list[ScoreDimension] = []
        known = case.known_outcome

        if known.escalation_risk is not None:
            diff = abs(response.escalation_risk - known.escalation_risk)
            score = max(0.0, 1.0 - diff)
            dimensions.append(ScoreDimension(
                dimension="escalation_risk",
                predicted=response.escalation_risk,
                actual=known.escalation_risk,
                score=score,
                note=f"off by {diff:.2f}",
            ))

        if known.action_types:
            predicted_types = [a.action_type for a in response.actions]
            predicted_set = set(predicted_types)
            known_set = set(known.action_types)
            true_positives = len(predicted_set & known_set)
            precision = true_positives / len(predicted_set) if predicted_set else 0.0
            recall = true_positives / len(known_set)
            score = (2 * precision * recall / (precision + recall)) if (precision + recall) > 0 else 0.0
            dimensions.append(ScoreDimension(
                dimension="action_types",
                predicted=predicted_types,
                actual=known.action_types,
                score=score,
                note=f"F1={score:.2f} (p={precision:.2f}, r={recall:.2f})",
            ))

        predicted_tone = response.verbal_response.tone
        if known.tone:
            if predicted_tone == known.tone:
                score = 1.0
            else:
                score = TONE_SIMILARITY.get((predicted_tone, known.tone), 0.0)
            dimensions.append(ScoreDimension(
                dimension="tone",
                predicted=predicted_tone,
                actual=known.tone,
                score=score,
            ))

        total_weight = sum(DIMENSION_WEIGHTS.get(d.dimension, 1.0) for d in dimensions)
        overall = sum(d.score * DIMENSION_WEIGHTS.get(d.dimension, 1.0) for d in dimensions) / max(total_weight, 0.01)

        return CaseResult(
            case_id=case.id,
            label=case.label,
            overall_score=overall,
            dimensions=dimensions,
            predicted_escalation_risk=response.escalation_risk,
            predicted_actions=[a.action_type for a in response.actions],
            predicted_tone=predicted_tone,
            confidence=response.reasoning.confidence,
        )
