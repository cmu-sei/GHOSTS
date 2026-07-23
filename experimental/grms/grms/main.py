"""GRMS - Geopolitical Response Modeling Service."""

import json
import logging
from datetime import datetime
from pathlib import Path
from uuid import UUID, uuid4, uuid5, NAMESPACE_URL

from fastapi import FastAPI, HTTPException, WebSocket, WebSocketDisconnect
from pydantic import BaseModel, Field

from grms.config import settings
from grms.models import (
    CombinedResponse,
    GeopoliticalEvent,
    LeaderDecisionContext,
    LeaderProfile,
    LeaderResponse,
    PopulationProfile,
    PopulationResponse,
)
from grms.models.scoring import AblationResult, BatchRunResult, BrierDecomposition, ConfidenceCalibration, KnownOutcome, PredictionScore, ScoreDimension, ScoringCase, StakeholderReport
from grms.services.leader_service import LeaderService
from grms.services.population_service import PopulationService
from grms.services.scoring_service import ScoringService
from grms.services.statistics_service import StatisticsService
from grms import db as scoring_db

logger = logging.getLogger(__name__)

app = FastAPI(
    title="Geopolitical Response Modeling Service",
    version="0.1.0",
    description="Models national leader and population responses to geopolitical events",
)

# In-memory stores (swap for persistent storage in production)
leaders: dict[UUID, LeaderProfile] = {}
populations: dict[str, PopulationProfile] = {}
scoring_cases: dict[str, ScoringCase] = {}


class SavedPrediction(BaseModel):
    id: UUID = Field(default_factory=uuid4)
    created_at: datetime = Field(default_factory=datetime.utcnow)
    historical_date: str | None = Field(None, description="ISO date of historical event for ordering")
    label: str = ""
    prediction_type: str = Field(description="leader, population, combined")
    event: GeopoliticalEvent
    leader_id: UUID | None = None
    leader_name: str | None = None
    country: str | None = None
    llm_model: str | None = Field(None, description="LLM model used to generate this prediction")
    leader_response: LeaderResponse | None = None
    population_response: PopulationResponse | None = None
    cascade_responses: list[LeaderResponse] = Field(default_factory=list)
    known_outcome: KnownOutcome | None = Field(None, description="What actually happened — if set, prediction is scored")
    score: PredictionScore | None = Field(None, description="Accuracy score computed from known_outcome")


def _seed_leaders():
    """Load leader profiles from seed data on startup."""
    seed_dir = Path(__file__).parent / "data" / "sample_leaders"
    if not seed_dir.exists():
        return
    for filepath in sorted(seed_dir.glob("*.json")):
        if filepath.name in ("g20_scenario.json", "sample_leader.json", "sample_population.json"):
            continue
        try:
            data = json.loads(filepath.read_text())
            profile = LeaderProfile(**data)
            leaders[profile.id] = profile
            logger.info("Seeded leader: %s (%s)", profile.name, profile.country)
        except Exception as e:
            logger.warning("Failed to load %s: %s", filepath.name, e)
    logger.info("Seeded %d leaders on startup", len(leaders))


def _pop_key(country: str, period: str = "current") -> str:
    return f"{country}:{period}"


def _seed_populations():
    """Load population profiles from seed data on startup."""
    seed_dir = Path(__file__).parent / "data" / "sample_populations"
    if not seed_dir.exists():
        return
    for filepath in sorted(seed_dir.glob("*.json")):
        try:
            data = json.loads(filepath.read_text())
            profile = PopulationProfile(**data)
            populations[_pop_key(profile.country, profile.period)] = profile
            logger.info("Seeded population: %s (%s)", profile.country, profile.period)
        except Exception as e:
            logger.warning("Failed to load %s: %s", filepath.name, e)
    logger.info("Seeded %d populations on startup", len(populations))


def _seed_scoring_cases():
    """Load scoring cases from seed data on startup."""
    seed_dir = Path(__file__).parent / "data" / "scoring"
    if not seed_dir.exists():
        return
    for filepath in sorted(seed_dir.glob("*.json")):
        try:
            data = json.loads(filepath.read_text())
            for item in data:
                case = ScoringCase(**item)
                scoring_cases[case.id] = case
                leaders[case.leader.id] = case.leader
            logger.info("Seeded %d scoring cases from %s", len(data), filepath.name)
        except Exception as e:
            logger.warning("Failed to load scoring file %s: %s", filepath.name, e)


def _score_prediction(prediction: SavedPrediction) -> PredictionScore | None:
    """Score a prediction against its known outcome. Returns None if no outcome set."""
    if prediction.known_outcome is None:
        return None
    if prediction.leader_response is None:
        return None

    known = prediction.known_outcome
    lr = prediction.leader_response
    dimensions: list[ScoreDimension] = []

    escalation_score = None
    if known.escalation_risk is not None:
        diff = abs(lr.escalation_risk - known.escalation_risk)
        escalation_score = max(0.0, 1.0 - diff)
        dimensions.append(ScoreDimension(
            dimension="escalation_risk",
            predicted=lr.escalation_risk,
            actual=known.escalation_risk,
            score=escalation_score,
            note=f"off by {diff:.2f}",
        ))

    action_type_score = None
    if known.action_types:
        predicted_types = [a.action_type for a in lr.actions]
        matches = sum(1 for t in known.action_types if t in predicted_types)
        action_type_score = matches / len(known.action_types)
        dimensions.append(ScoreDimension(
            dimension="action_types",
            predicted=predicted_types,
            actual=known.action_types,
            score=action_type_score,
            note=f"{matches}/{len(known.action_types)} matched",
        ))

    tone_score = None
    if known.tone:
        predicted_tone = lr.verbal_response.tone
        tone_score = 1.0 if predicted_tone == known.tone else 0.0
        dimensions.append(ScoreDimension(
            dimension="tone",
            predicted=predicted_tone,
            actual=known.tone,
            score=tone_score,
        ))

    overall = sum(d.score for d in dimensions) / max(len(dimensions), 1)

    return PredictionScore(
        overall_score=overall,
        escalation_score=escalation_score,
        action_type_score=action_type_score,
        tone_score=tone_score,
        dimensions=dimensions,
    )


async def _persist_prediction(prediction: SavedPrediction) -> None:
    """Persist a prediction (with optional score) to SQLite."""
    data = {
        "id": str(prediction.id),
        "created_at": prediction.created_at.isoformat(),
        "historical_date": prediction.historical_date,
        "label": prediction.label,
        "prediction_type": prediction.prediction_type,
        "event_json": prediction.event.model_dump_json(),
        "leader_id": str(prediction.leader_id) if prediction.leader_id else None,
        "leader_name": prediction.leader_name,
        "country": prediction.country,
        "llm_model": prediction.llm_model,
        "leader_response_json": prediction.leader_response.model_dump_json() if prediction.leader_response else None,
        "population_response_json": prediction.population_response.model_dump_json() if prediction.population_response else None,
        "cascade_responses_json": json.dumps([r.model_dump(mode="json") for r in prediction.cascade_responses]),
        "known_outcome_json": prediction.known_outcome.model_dump_json() if prediction.known_outcome else None,
    }
    if prediction.score:
        data["overall_score"] = prediction.score.overall_score
        data["escalation_score"] = prediction.score.escalation_score
        data["action_type_score"] = prediction.score.action_type_score
        data["tone_score"] = prediction.score.tone_score
        data["dimensions_json"] = json.dumps([d.model_dump(mode="json") for d in prediction.score.dimensions])
        data["scored_at"] = prediction.score.scored_at.isoformat()
    await scoring_db.persist_prediction(data)


def _db_row_to_prediction(row: dict) -> SavedPrediction:
    """Reconstruct a SavedPrediction from a database row."""
    score = None
    if row.get("overall_score") is not None:
        dims = json.loads(row["dimensions_json"]) if row.get("dimensions_json") else []
        score = PredictionScore(
            overall_score=row["overall_score"],
            escalation_score=row.get("escalation_score"),
            action_type_score=row.get("action_type_score"),
            tone_score=row.get("tone_score"),
            dimensions=[ScoreDimension(**d) for d in dims],
            scored_at=datetime.fromisoformat(row["scored_at"]) if row.get("scored_at") else datetime.utcnow(),
        )

    known_outcome = None
    if row.get("known_outcome_json"):
        known_outcome = KnownOutcome.model_validate_json(row["known_outcome_json"])

    cascade = []
    if row.get("cascade_responses_json"):
        cascade = [LeaderResponse(**r) for r in json.loads(row["cascade_responses_json"])]

    return SavedPrediction(
        id=UUID(row["id"]),
        created_at=datetime.fromisoformat(row["created_at"]),
        historical_date=row.get("historical_date"),
        label=row.get("label", ""),
        prediction_type=row["prediction_type"],
        event=GeopoliticalEvent.model_validate_json(row["event_json"]),
        leader_id=UUID(row["leader_id"]) if row.get("leader_id") else None,
        leader_name=row.get("leader_name"),
        country=row.get("country"),
        llm_model=row.get("llm_model"),
        leader_response=LeaderResponse.model_validate_json(row["leader_response_json"]) if row.get("leader_response_json") else None,
        population_response=PopulationResponse.model_validate_json(row["population_response_json"]) if row.get("population_response_json") else None,
        cascade_responses=cascade,
        known_outcome=known_outcome,
        score=score,
    )


async def _run_seeded_predictions():
    """Run predictions for all seeded scoring cases, score them, and persist."""
    for case in scoring_cases.values():
        stable_id = uuid5(NAMESPACE_URL, f"grms:prediction:{case.id}")
        existing = await scoring_db.get_prediction(str(stable_id))
        if existing:
            continue
        try:
            response = await leader_service.predict(case.leader, case.event)
            prediction = SavedPrediction(
                id=stable_id,
                label=case.label,
                historical_date=case.historical_date,
                prediction_type="leader",
                event=case.event,
                leader_id=case.leader.id,
                leader_name=case.leader.name,
                country=case.leader.country,
                llm_model=settings.llm_model,
                leader_response=response,
                known_outcome=case.known_outcome,
            )
            prediction.score = _score_prediction(prediction)
            await _persist_prediction(prediction)
        except Exception as e:
            logger.warning("Failed to run seeded prediction for '%s': %s", case.label, e)
    logger.info("Seeded predictions up to date")


_seed_leaders()
_seed_populations()
_seed_scoring_cases()

leader_service = LeaderService()
population_service = PopulationService()
scoring_service = ScoringService()
statistics_service = StatisticsService()

# Scoring run history (in-memory cache; also persisted to SQLite)
scoring_runs: dict[UUID, BatchRunResult] = {}


@app.on_event("startup")
async def startup_run_predictions():
    await _run_seeded_predictions()


@app.get("/health")
async def health():
    return {
        "status": "healthy",
        "llm_backend": settings.llm_source,
        "llm_model": settings.llm_model,
        "leaders_loaded": len(leaders),
        "populations_loaded": len(populations),
    }


# --- Leader CRUD ---


@app.post("/api/v1/leaders", response_model=LeaderProfile)
async def create_leader(profile: LeaderProfile):
    leaders[profile.id] = profile
    return profile


@app.get("/api/v1/leaders/{leader_id}", response_model=LeaderProfile)
async def get_leader(leader_id: UUID):
    if leader_id not in leaders:
        raise HTTPException(404, "Leader not found")
    return leaders[leader_id]


@app.put("/api/v1/leaders/{leader_id}/context", response_model=LeaderProfile)
async def update_leader_context(leader_id: UUID, context: LeaderDecisionContext):
    if leader_id not in leaders:
        raise HTTPException(404, "Leader not found")
    leaders[leader_id].decision_context = context
    return leaders[leader_id]


@app.get("/api/v1/leaders", response_model=list[LeaderProfile])
async def list_leaders(period: str | None = None):
    if period:
        return [l for l in leaders.values() if l.period == period]
    return list(leaders.values())


# --- Population CRUD ---


@app.post("/api/v1/populations", response_model=PopulationProfile)
async def create_population(profile: PopulationProfile):
    if not profile.validate_segments():
        raise HTTPException(400, "Population segments must sum to approximately 1.0")
    populations[_pop_key(profile.country, profile.period)] = profile
    return profile


@app.get("/api/v1/populations", response_model=list[PopulationProfile])
async def list_populations():
    return list(populations.values())


@app.get("/api/v1/populations/{country}", response_model=PopulationProfile)
async def get_population(country: str, period: str = "current"):
    key = _pop_key(country, period)
    if key not in populations:
        if period != "current":
            raise HTTPException(404, "Population profile not found")
        for k, v in populations.items():
            if k.startswith(f"{country}:"):
                return v
        raise HTTPException(404, "Population profile not found")
    return populations[key]


# --- Prediction ---


class PredictLeaderRequest(BaseModel):
    leader_id: UUID
    event: GeopoliticalEvent


class PredictPopulationRequest(BaseModel):
    country: str
    event: GeopoliticalEvent
    leader_action_summary: str = ""
    period: str = "current"


class PredictCombinedRequest(BaseModel):
    leader_id: UUID
    country: str
    event: GeopoliticalEvent
    period: str = "current"


@app.post("/api/v1/predict/leader", response_model=LeaderResponse)
async def predict_leader(request: PredictLeaderRequest):
    if request.leader_id not in leaders:
        raise HTTPException(404, "Leader not found")
    leader = leaders[request.leader_id]
    response = await leader_service.predict(leader, request.event)
    return response


@app.post("/api/v1/predict/population", response_model=PopulationResponse)
async def predict_population(request: PredictPopulationRequest):
    key = _pop_key(request.country, request.period)
    if key not in populations:
        for k, v in populations.items():
            if k.startswith(f"{request.country}:"):
                pop = v
                break
        else:
            raise HTTPException(404, "Population profile not found")
    else:
        pop = populations[key]
    response = population_service.predict(pop, request.event, request.leader_action_summary)
    return response


@app.post("/api/v1/predict/combined", response_model=CombinedResponse)
async def predict_combined(request: PredictCombinedRequest):
    if request.leader_id not in leaders:
        raise HTTPException(404, "Leader not found")

    key = _pop_key(request.country, request.period)
    if key not in populations:
        for k, v in populations.items():
            if k.startswith(f"{request.country}:"):
                pop = v
                break
        else:
            raise HTTPException(404, "Population profile not found")
    else:
        pop = populations[key]

    leader = leaders[request.leader_id]

    leader_response = await leader_service.predict(leader, request.event)

    action_summary = "; ".join(a.description for a in leader_response.actions[:3])
    pop_response = population_service.predict(pop, request.event, action_summary)

    return CombinedResponse(leader=leader_response, population=pop_response)


# --- Prediction History ---


@app.get("/api/v1/predictions", response_model=list[SavedPrediction])
async def list_predictions():
    rows = await scoring_db.get_all_predictions()
    return [_db_row_to_prediction(row) for row in rows]


@app.get("/api/v1/predictions/scored", response_model=list[SavedPrediction])
async def list_scored_predictions(leader_name: str | None = None):
    """List only predictions that have been scored against known outcomes."""
    rows = await scoring_db.get_scored_predictions(leader_name=leader_name)
    return [_db_row_to_prediction(row) for row in rows]


@app.get("/api/v1/predictions/{prediction_id}", response_model=SavedPrediction)
async def get_prediction(prediction_id: UUID):
    row = await scoring_db.get_prediction(str(prediction_id))
    if row is None:
        raise HTTPException(404, "Prediction not found")
    return _db_row_to_prediction(row)


@app.post("/api/v1/predictions", response_model=SavedPrediction)
async def save_prediction(prediction: SavedPrediction):
    if not prediction.llm_model:
        prediction.llm_model = settings.llm_model
    if prediction.known_outcome and prediction.leader_response:
        prediction.score = _score_prediction(prediction)
    await _persist_prediction(prediction)
    return prediction


class ScorePredictionRequest(BaseModel):
    known_outcome: KnownOutcome


@app.post("/api/v1/predictions/{prediction_id}/score", response_model=SavedPrediction)
async def score_prediction(prediction_id: UUID, request: ScorePredictionRequest):
    """Score an existing prediction by providing its known outcome."""
    row = await scoring_db.get_prediction(str(prediction_id))
    if row is None:
        raise HTTPException(404, "Prediction not found")
    prediction = _db_row_to_prediction(row)
    prediction.known_outcome = request.known_outcome
    prediction.score = _score_prediction(prediction)
    await _persist_prediction(prediction)
    return prediction


@app.delete("/api/v1/predictions/{prediction_id}")
async def delete_prediction(prediction_id: UUID):
    deleted = await scoring_db.delete_prediction(str(prediction_id))
    if not deleted:
        raise HTTPException(404, "Prediction not found")
    return {"deleted": True}


# --- Scoring Cases (seeded on startup) ---


@app.get("/api/v1/scoring/cases", response_model=list[ScoringCase])
async def list_scoring_cases():
    """List all loaded scoring cases."""
    return list(scoring_cases.values())


@app.get("/api/v1/scoring/cases/{case_id}", response_model=ScoringCase)
async def get_scoring_case(case_id: str):
    if case_id not in scoring_cases:
        raise HTTPException(404, "Scoring case not found")
    return scoring_cases[case_id]


# --- Batch Scoring ---


class BatchScoreRequest(BaseModel):
    cases: list[ScoringCase]


@app.post("/api/v1/scoring/run", response_model=BatchRunResult)
async def run_scoring_batch(request: BatchScoreRequest):
    """Run a batch of historical scenarios and score predictions against known outcomes."""
    result = await scoring_service.run_batch(request.cases)
    scoring_runs[result.id] = result
    return result


@app.post("/api/v1/scoring/run-all", response_model=BatchRunResult)
async def run_all_scoring_cases():
    """Run all loaded scoring cases through the prediction engine."""
    if not scoring_cases:
        raise HTTPException(400, "No scoring cases loaded")
    result = await scoring_service.run_batch(list(scoring_cases.values()))
    scoring_runs[result.id] = result
    return result


@app.post("/api/v1/scoring/run-file", response_model=BatchRunResult)
async def run_scoring_from_file(file_path: str):
    """Run scoring from a JSON file containing an array of ScoringCase objects."""
    path = Path(file_path)
    if not path.exists():
        data_dir = Path(__file__).parent / "data" / "scoring"
        path = data_dir / file_path
    if not path.exists():
        raise HTTPException(404, f"Scoring file not found: {file_path}")
    cases = [ScoringCase(**c) for c in json.loads(path.read_text())]
    result = await scoring_service.run_batch(cases)
    scoring_runs[result.id] = result
    return result


@app.get("/api/v1/scoring/runs", response_model=list[BatchRunResult])
async def list_scoring_runs():
    """List all scoring run results."""
    return sorted(scoring_runs.values(), key=lambda r: r.run_at, reverse=True)


@app.get("/api/v1/scoring/runs/{run_id}", response_model=BatchRunResult)
async def get_scoring_run(run_id: UUID):
    if run_id not in scoring_runs:
        raise HTTPException(404, "Scoring run not found")
    return scoring_runs[run_id]


# --- Scoring History & Validation ---


@app.get("/api/v1/scoring/history")
async def scoring_history(limit: int = 50, offset: int = 0):
    """Paginated list of all persisted scoring runs."""
    return await scoring_db.get_run_history(limit=limit, offset=offset)


@app.get("/api/v1/scoring/history/{run_id}")
async def scoring_history_detail(run_id: str):
    """Full detail for a persisted scoring run including per-case results."""
    detail = await scoring_db.get_run_detail(run_id)
    if detail is None:
        raise HTTPException(404, "Run not found in history")
    return detail


@app.get("/api/v1/scoring/trends")
async def scoring_trends(era: str | None = None, limit: int = 30):
    """Time-series of aggregate metrics for charting accuracy over time."""
    return await scoring_db.get_trends(era=era, limit=limit)


@app.get("/api/v1/scoring/breakdowns")
async def scoring_breakdowns(run_id: str | None = None):
    """Per-era and per-leader accuracy breakdowns for the latest (or specified) run."""
    if run_id is None:
        history = await scoring_db.get_run_history(limit=1)
        if not history:
            raise HTTPException(404, "No scoring runs available")
        run_id = history[0]["id"]
    return await statistics_service.compute_breakdowns(run_id)


@app.get("/api/v1/scoring/regression/{run_id}")
async def scoring_regression(run_id: str, era: str = "all"):
    """Regression analysis comparing this run to the previous run."""
    return await statistics_service.check_regression(run_id, era)


class CompareRequest(BaseModel):
    run_id_a: str
    run_id_b: str


@app.post("/api/v1/scoring/compare")
async def scoring_compare(request: CompareRequest):
    """Side-by-side comparison of two scoring runs."""
    a = await scoring_db.get_run_detail(request.run_id_a)
    b = await scoring_db.get_run_detail(request.run_id_b)
    if a is None or b is None:
        raise HTTPException(404, "One or both runs not found")
    return {
        "run_a": a,
        "run_b": b,
        "delta_aggregate": a["aggregate_score"] - b["aggregate_score"],
        "delta_escalation_rmse": a["escalation_rmse"] - b["escalation_rmse"],
        "delta_action_accuracy": a["action_type_accuracy"] - b["action_type_accuracy"],
        "delta_tone_accuracy": a["tone_accuracy"] - b["tone_accuracy"],
    }


@app.get("/api/v1/scoring/report", response_model=StakeholderReport)
async def scoring_report():
    """Stakeholder validation report with accuracy, confidence, and regression status."""
    return await statistics_service.generate_report()


# --- Ablation & Advanced Scoring ---


class AblationRequest(BaseModel):
    case_ids: list[str] | None = Field(None, description="Specific case IDs to run. If None, runs all loaded cases.")
    include_generic_llm: bool = Field(True, description="Include the GenericLLM baseline (requires LLM call per case)")


@app.post("/api/v1/scoring/ablation", response_model=AblationResult)
async def run_ablation(request: AblationRequest):
    """Run ablation study comparing full model against baselines to prove value-add."""
    if request.case_ids:
        cases = [scoring_cases[cid] for cid in request.case_ids if cid in scoring_cases]
        if not cases:
            raise HTTPException(400, "No valid case IDs provided")
    else:
        cases = list(scoring_cases.values())
        if not cases:
            raise HTTPException(400, "No scoring cases loaded")
    return await scoring_service.run_ablation(cases, include_generic_llm=request.include_generic_llm)


@app.get("/api/v1/scoring/brier/{run_id}", response_model=BrierDecomposition)
async def scoring_brier(run_id: str):
    """Brier score with reliability/resolution/uncertainty decomposition."""
    return await statistics_service.compute_brier_score(run_id)


@app.get("/api/v1/scoring/confidence-calibration/{run_id}", response_model=ConfidenceCalibration)
async def scoring_confidence_calibration(run_id: str):
    """LLM-stated confidence vs actual prediction accuracy (calibration analysis)."""
    return await statistics_service.compute_confidence_calibration(run_id)


# --- Hybrid & Cognitive-Only Prediction ---


@app.post("/api/v1/predict/leader/hybrid", response_model=LeaderResponse)
async def predict_leader_hybrid(request: PredictLeaderRequest):
    """Hybrid prediction: cognitive engine as structured prior + LLM reasoning."""
    if request.leader_id not in leaders:
        raise HTTPException(404, "Leader not found")
    leader = leaders[request.leader_id]
    return await leader_service.predict_hybrid(leader, request.event)


@app.post("/api/v1/predict/leader/cognitive-only", response_model=LeaderResponse)
async def predict_leader_cognitive_only(request: PredictLeaderRequest):
    """Parametric cognitive engine prediction only (no LLM call)."""
    from grms.services.baselines import CognitiveOnlyPredictor

    if request.leader_id not in leaders:
        raise HTTPException(404, "Leader not found")
    leader = leaders[request.leader_id]
    predictor = CognitiveOnlyPredictor()
    return await predictor.predict(leader, request.event)


@app.on_event("shutdown")
async def shutdown_db():
    await scoring_db.close_db()


# --- WebSocket ---


@app.websocket("/ws/scenario/{scenario_id}")
async def websocket_scenario(websocket: WebSocket, scenario_id: str):
    await websocket.accept()
    try:
        while True:
            data = await websocket.receive_json()
            if data.get("type") == "event":
                event = GeopoliticalEvent(**data["data"])
                leader_id = data.get("leader_id")
                country = data.get("country")

                if leader_id and UUID(leader_id) in leaders:
                    leader = leaders[UUID(leader_id)]
                    lr = await leader_service.predict(leader, event)
                    await websocket.send_json({"type": "leader_response", "data": lr.model_dump(mode="json")})

                if country and country in populations:
                    action_summary = ""
                    if leader_id and UUID(leader_id) in leaders:
                        action_summary = "; ".join(a.description for a in lr.actions[:3])
                    pr = population_service.predict(populations[country], event, action_summary)
                    await websocket.send_json({"type": "population_response", "data": pr.model_dump(mode="json")})

                await websocket.send_json({"type": "complete", "event_id": str(event.id)})
    except WebSocketDisconnect:
        pass
