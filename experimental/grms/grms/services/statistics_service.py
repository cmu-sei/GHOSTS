"""Statistical analysis for scoring validation and stakeholder reporting."""

import json
import logging
import math
from collections import defaultdict
from datetime import datetime

import numpy as np
from scipy import stats

from grms.config import settings
from grms.db import get_case_results_for_run, get_previous_run, get_run_history, get_trends, persist_regression_check
from grms.models.scoring import (
    BrierDecomposition,
    CalibrationBin,
    ConfCalBin,
    ConfidenceCalibration,
    ConfidenceBand,
    EraBreakdown,
    LeaderBreakdown,
    StakeholderReport,
)
from grms.prompt_hash import compute_prompt_hash

logger = logging.getLogger("grms.statistics_service")


class StatisticsService:
    async def check_regression(self, run_id: str, era: str = "all") -> dict:
        previous = await get_previous_run(run_id, era)
        if previous is None:
            return {"is_regression": False, "notes": "No baseline run found for comparison"}

        current_cases = await get_case_results_for_run(run_id)
        baseline_cases = await get_case_results_for_run(previous["id"])

        baseline_map = {c["case_id"]: c["overall_score"] for c in baseline_cases}
        paired_current = []
        paired_baseline = []
        for c in current_cases:
            if c["case_id"] in baseline_map:
                paired_current.append(c["overall_score"])
                paired_baseline.append(baseline_map[c["case_id"]])

        if len(paired_current) < 3:
            return {"is_regression": False, "notes": "Too few paired cases for statistical test"}

        current_arr = np.array(paired_current)
        baseline_arr = np.array(paired_baseline)
        delta = float(current_arr.mean() - baseline_arr.mean())

        t_stat, p_value = stats.ttest_rel(current_arr, baseline_arr)
        is_regression = p_value < 0.05 and delta < -0.03

        check = {
            "run_id": run_id,
            "baseline_run_id": previous["id"],
            "delta_aggregate": delta,
            "delta_escalation_rmse": None,
            "delta_action_accuracy": None,
            "delta_tone_accuracy": None,
            "is_regression": is_regression,
            "significance_p": float(p_value),
            "notes": f"Paired t-test: t={t_stat:.3f}, p={p_value:.4f}, delta={delta:.4f}",
        }
        await persist_regression_check(check)
        return check

    async def compute_brier_score(self, run_id: str) -> BrierDecomposition:
        cases = await get_case_results_for_run(run_id)
        pairs = []
        for c in cases:
            if c["actual_escalation"] is not None and c["predicted_escalation"] is not None:
                pairs.append((c["predicted_escalation"], c["actual_escalation"]))

        if len(pairs) < 3:
            return BrierDecomposition(brier_score=1.0, reliability=1.0, resolution=0.0, uncertainty=0.25, n_cases=len(pairs))

        predicted = np.array([p[0] for p in pairs])
        actual = np.array([p[1] for p in pairs])
        n = len(pairs)

        brier = float(np.mean((predicted - actual) ** 2))
        base_rate = float(actual.mean())
        uncertainty = base_rate * (1 - base_rate)

        n_bins = min(5, n // 3)
        if n_bins < 2:
            return BrierDecomposition(brier_score=brier, reliability=brier, resolution=0.0, uncertainty=uncertainty, n_cases=n)

        sorted_idx = np.argsort(predicted)
        bin_size = n // n_bins
        reliability = 0.0
        resolution = 0.0
        for i in range(n_bins):
            start = i * bin_size
            end = start + bin_size if i < n_bins - 1 else n
            idx = sorted_idx[start:end]
            bin_pred = predicted[idx]
            bin_actual = actual[idx]
            n_k = len(idx)
            mean_pred = float(bin_pred.mean())
            mean_actual = float(bin_actual.mean())
            reliability += n_k * (mean_pred - mean_actual) ** 2
            resolution += n_k * (mean_actual - base_rate) ** 2

        reliability = reliability / n
        resolution = resolution / n

        return BrierDecomposition(
            brier_score=brier,
            reliability=reliability,
            resolution=resolution,
            uncertainty=uncertainty,
            n_cases=n,
        )

    async def compute_confidence_calibration(self, run_id: str, n_bins: int = 5) -> ConfidenceCalibration:
        cases = await get_case_results_for_run(run_id)
        if len(cases) < n_bins:
            return ConfidenceCalibration(n_cases=len(cases), note="Too few cases for confidence calibration")

        median_score = float(np.median([c["overall_score"] for c in cases]))
        pairs = [(c["confidence"], 1.0 if c["overall_score"] >= median_score else 0.0) for c in cases if c["confidence"] is not None]
        if len(pairs) < n_bins:
            return ConfidenceCalibration(n_cases=len(pairs), note="Too few cases with confidence values")

        pairs.sort(key=lambda x: x[0])
        bin_size = len(pairs) // n_bins
        bins = []
        for i in range(n_bins):
            start = i * bin_size
            end = start + bin_size if i < n_bins - 1 else len(pairs)
            bucket = pairs[start:end]
            confs = [p[0] for p in bucket]
            corrects = [p[1] for p in bucket]
            bins.append(ConfCalBin(
                confidence_lower=min(confs),
                confidence_upper=max(confs),
                mean_confidence=sum(confs) / len(confs),
                fraction_correct=sum(corrects) / len(corrects),
                count=len(bucket),
            ))

        return ConfidenceCalibration(bins=bins, n_cases=len(pairs))

    async def compute_confidence_bands(self, run_id: str) -> list[ConfidenceBand]:
        cases = await get_case_results_for_run(run_id)
        if len(cases) < 5:
            return []

        scores = np.array([c["overall_score"] for c in cases])
        bands = [self._bootstrap_ci(scores, "aggregate_score")]

        esc_scores = []
        for c in cases:
            if c["actual_escalation"] is not None:
                diff = abs(c["predicted_escalation"] - c["actual_escalation"])
                esc_scores.append(max(0.0, 1.0 - diff))
        if len(esc_scores) >= 5:
            bands.append(self._bootstrap_ci(np.array(esc_scores), "escalation_accuracy"))

        return bands

    def _bootstrap_ci(self, data: np.ndarray, metric: str, n_resamples: int = 1000) -> ConfidenceBand:
        rng = np.random.default_rng(42)
        means = []
        for _ in range(n_resamples):
            sample = rng.choice(data, size=len(data), replace=True)
            means.append(float(sample.mean()))
        means.sort()
        ci_lower = means[int(0.025 * n_resamples)]
        ci_upper = means[int(0.975 * n_resamples)]
        return ConfidenceBand(
            metric=metric,
            point_estimate=float(data.mean()),
            ci_lower=ci_lower,
            ci_upper=ci_upper,
        )

    async def compute_breakdowns(self, run_id: str) -> dict:
        cases = await get_case_results_for_run(run_id)

        by_era: dict[str, list[dict]] = defaultdict(list)
        by_leader: dict[str, list[dict]] = defaultdict(list)
        by_event_type: dict[str, list[float]] = defaultdict(list)

        for c in cases:
            by_era[c["era"]].append(c)
            key = f"{c['leader_name']}|{c['leader_country']}"
            by_leader[key].append(c)

        era_breakdowns = []
        for era, era_cases in by_era.items():
            scores = [c["overall_score"] for c in era_cases]
            esc_errors = []
            action_scores = []
            tone_scores = []
            for c in era_cases:
                dims = json.loads(c["dimensions_json"]) if isinstance(c["dimensions_json"], str) else c.get("dimensions", [])
                for d in dims:
                    if d["dimension"] == "escalation_risk":
                        err = float(d["predicted"]) - float(d["actual"])
                        esc_errors.append(err ** 2)
                    elif d["dimension"] == "action_types":
                        action_scores.append(d["score"])
                    elif d["dimension"] == "tone":
                        tone_scores.append(d["score"])
            era_breakdowns.append(EraBreakdown(
                era=era,
                case_count=len(era_cases),
                aggregate_score=sum(scores) / len(scores),
                escalation_rmse=math.sqrt(sum(esc_errors) / max(len(esc_errors), 1)),
                action_type_accuracy=sum(action_scores) / max(len(action_scores), 1),
                tone_accuracy=sum(tone_scores) / max(len(tone_scores), 1),
            ))

        leader_breakdowns = []
        for key, leader_cases in by_leader.items():
            name, country = key.split("|", 1)
            scores = [c["overall_score"] for c in leader_cases]
            leader_breakdowns.append(LeaderBreakdown(
                leader_name=name,
                country=country,
                case_count=len(leader_cases),
                aggregate_score=sum(scores) / len(scores),
            ))

        return {
            "by_era": era_breakdowns,
            "by_leader": sorted(leader_breakdowns, key=lambda x: x.aggregate_score, reverse=True),
        }

    async def compute_calibration(self, run_id: str, n_bins: int = 5) -> list[CalibrationBin]:
        cases = await get_case_results_for_run(run_id)
        pairs = []
        for c in cases:
            if c["actual_escalation"] is not None:
                pairs.append((c["predicted_escalation"], c["actual_escalation"]))
        if len(pairs) < n_bins:
            return []

        pairs.sort(key=lambda x: x[0])
        bin_size = len(pairs) // n_bins
        bins = []
        for i in range(n_bins):
            start = i * bin_size
            end = start + bin_size if i < n_bins - 1 else len(pairs)
            bucket = pairs[start:end]
            preds = [p[0] for p in bucket]
            actuals = [p[1] for p in bucket]
            bins.append(CalibrationBin(
                bin_lower=min(preds),
                bin_upper=max(preds),
                mean_predicted=sum(preds) / len(preds),
                mean_actual=sum(actuals) / len(actuals),
                count=len(bucket),
            ))
        return bins

    async def generate_report(self) -> StakeholderReport:
        history = await get_run_history(limit=100)
        if not history:
            return StakeholderReport(
                total_runs=0,
                total_scenarios_evaluated=0,
                eras_covered=[],
                latest_aggregate_score=0.0,
                latest_escalation_rmse=0.0,
                latest_action_accuracy=0.0,
                latest_tone_accuracy=0.0,
                forward_confidence=0.0,
                forward_confidence_note="No scoring runs available yet",
            )

        latest = history[0]
        trends = await get_trends(limit=30)

        confidence_bands = await self.compute_confidence_bands(latest["id"])
        breakdowns = await self.compute_breakdowns(latest["id"])
        calibration = await self.compute_calibration(latest["id"])

        eras_covered = list({r["scenario_era"] for r in history})
        total_cases = sum(r["total_cases"] for r in history)

        agg_scores = [r["aggregate_score"] for r in history]
        forward_confidence = float(np.mean(agg_scores))
        score_std = float(np.std(agg_scores)) if len(agg_scores) > 1 else 0.0
        forward_note = (
            f"Based on {len(history)} historical runs: mean accuracy {forward_confidence:.3f} "
            f"(std {score_std:.3f}). Future predictions for similar scenarios are expected "
            f"to fall within this accuracy range."
        )

        same_config_count = sum(
            1 for r in history
            if r["llm_model"] == latest["llm_model"] and r["prompt_hash"] == latest["prompt_hash"]
        )

        regression_check = await self.check_regression(latest["id"], latest["scenario_era"])

        return StakeholderReport(
            total_runs=len(history),
            total_scenarios_evaluated=total_cases,
            eras_covered=eras_covered,
            latest_aggregate_score=latest["aggregate_score"],
            latest_escalation_rmse=latest["escalation_rmse"],
            latest_action_accuracy=latest["action_type_accuracy"],
            latest_tone_accuracy=latest["tone_accuracy"],
            confidence_bands=confidence_bands,
            forward_confidence=forward_confidence,
            forward_confidence_note=forward_note,
            trend_timestamps=[t["run_at"] for t in trends],
            trend_aggregate_scores=[t["aggregate_score"] for t in trends],
            trend_escalation_rmse=[t["escalation_rmse"] for t in trends],
            by_era=breakdowns["by_era"],
            by_leader=breakdowns["by_leader"],
            calibration_bins=calibration,
            regression_detected=regression_check["is_regression"],
            regression_details=regression_check.get("notes"),
            current_model=settings.llm_model,
            current_prompt_hash=compute_prompt_hash(),
            runs_with_same_config=same_config_count,
        )
