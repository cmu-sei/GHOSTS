"""Async SQLite database for persisting scoring run history."""

import json
import logging
from pathlib import Path

import aiosqlite

from grms.config import settings

logger = logging.getLogger("grms.db")

_db: aiosqlite.Connection | None = None

_SCHEMA = """
CREATE TABLE IF NOT EXISTS scoring_runs (
    id TEXT PRIMARY KEY,
    run_at TEXT NOT NULL,
    llm_source TEXT NOT NULL,
    llm_model TEXT NOT NULL,
    llm_temperature REAL NOT NULL,
    llm_seed INTEGER,
    prompt_hash TEXT NOT NULL,
    scenario_era TEXT NOT NULL DEFAULT 'all',
    total_cases INTEGER NOT NULL,
    aggregate_score REAL NOT NULL,
    escalation_rmse REAL NOT NULL,
    escalation_bias REAL NOT NULL,
    action_type_accuracy REAL NOT NULL,
    tone_accuracy REAL NOT NULL,
    failures_json TEXT DEFAULT '[]',
    metadata_json TEXT DEFAULT '{}'
);

CREATE TABLE IF NOT EXISTS case_results (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    run_id TEXT NOT NULL REFERENCES scoring_runs(id),
    case_id TEXT NOT NULL,
    label TEXT NOT NULL,
    era TEXT NOT NULL DEFAULT 'ww2',
    leader_name TEXT NOT NULL,
    leader_country TEXT NOT NULL,
    overall_score REAL NOT NULL,
    predicted_escalation REAL NOT NULL,
    actual_escalation REAL,
    predicted_actions_json TEXT,
    actual_actions_json TEXT,
    predicted_tone TEXT,
    actual_tone TEXT,
    confidence REAL NOT NULL,
    dimensions_json TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS regression_checks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    run_id TEXT NOT NULL REFERENCES scoring_runs(id),
    baseline_run_id TEXT REFERENCES scoring_runs(id),
    delta_aggregate REAL,
    delta_escalation_rmse REAL,
    delta_action_accuracy REAL,
    delta_tone_accuracy REAL,
    is_regression INTEGER NOT NULL DEFAULT 0,
    significance_p REAL,
    notes TEXT DEFAULT ''
);

CREATE TABLE IF NOT EXISTS predictions (
    id TEXT PRIMARY KEY,
    created_at TEXT NOT NULL,
    historical_date TEXT,
    label TEXT NOT NULL DEFAULT '',
    prediction_type TEXT NOT NULL,
    event_json TEXT NOT NULL,
    leader_id TEXT,
    leader_name TEXT,
    country TEXT,
    leader_response_json TEXT,
    population_response_json TEXT,
    cascade_responses_json TEXT DEFAULT '[]',
    known_outcome_json TEXT,
    llm_model TEXT,
    overall_score REAL,
    escalation_score REAL,
    action_type_score REAL,
    tone_score REAL,
    dimensions_json TEXT,
    scored_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_case_results_run_id ON case_results(run_id);
CREATE INDEX IF NOT EXISTS idx_scoring_runs_run_at ON scoring_runs(run_at);
CREATE INDEX IF NOT EXISTS idx_scoring_runs_era ON scoring_runs(scenario_era);
CREATE INDEX IF NOT EXISTS idx_predictions_historical_date ON predictions(historical_date);
CREATE INDEX IF NOT EXISTS idx_predictions_leader_name ON predictions(leader_name);
CREATE INDEX IF NOT EXISTS idx_predictions_overall_score ON predictions(overall_score);
"""


async def get_db() -> aiosqlite.Connection:
    global _db
    if _db is None:
        db_path = Path(settings.db_path)
        db_path.parent.mkdir(parents=True, exist_ok=True)
        _db = await aiosqlite.connect(str(db_path))
        _db.row_factory = aiosqlite.Row
        await _db.executescript(_SCHEMA)
        await _migrate(_db)
        await _db.commit()
        logger.info("Database initialized at %s", db_path)
    return _db


async def _migrate(db: aiosqlite.Connection) -> None:
    """Apply schema migrations for existing databases."""
    cursor = await db.execute("PRAGMA table_info(predictions)")
    columns = {row[1] for row in await cursor.fetchall()}
    if "llm_model" not in columns:
        await db.execute("ALTER TABLE predictions ADD COLUMN llm_model TEXT")
        logger.info("Migration: added llm_model column to predictions")


async def persist_run(run_data: dict, case_results: list[dict]) -> None:
    db = await get_db()
    await db.execute(
        """INSERT INTO scoring_runs
           (id, run_at, llm_source, llm_model, llm_temperature, llm_seed,
            prompt_hash, scenario_era, total_cases, aggregate_score,
            escalation_rmse, escalation_bias, action_type_accuracy,
            tone_accuracy, failures_json, metadata_json)
           VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
        (
            run_data["id"],
            run_data["run_at"],
            run_data["llm_source"],
            run_data["llm_model"],
            run_data["llm_temperature"],
            run_data.get("llm_seed"),
            run_data["prompt_hash"],
            run_data.get("scenario_era", "all"),
            run_data["total_cases"],
            run_data["aggregate_score"],
            run_data["escalation_rmse"],
            run_data["escalation_bias"],
            run_data["action_type_accuracy"],
            run_data["tone_accuracy"],
            json.dumps(run_data.get("failures", [])),
            json.dumps(run_data.get("metadata", {})),
        ),
    )
    for case in case_results:
        await db.execute(
            """INSERT INTO case_results
               (run_id, case_id, label, era, leader_name, leader_country,
                overall_score, predicted_escalation, actual_escalation,
                predicted_actions_json, actual_actions_json,
                predicted_tone, actual_tone, confidence, dimensions_json)
               VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (
                run_data["id"],
                case["case_id"],
                case["label"],
                case.get("era", "ww2"),
                case["leader_name"],
                case["leader_country"],
                case["overall_score"],
                case["predicted_escalation"],
                case.get("actual_escalation"),
                json.dumps(case.get("predicted_actions", [])),
                json.dumps(case.get("actual_actions", [])),
                case.get("predicted_tone"),
                case.get("actual_tone"),
                case["confidence"],
                json.dumps(case.get("dimensions", [])),
            ),
        )
    await db.commit()
    logger.info("Persisted scoring run %s with %d cases", run_data["id"], len(case_results))


async def get_run_history(limit: int = 50, offset: int = 0) -> list[dict]:
    db = await get_db()
    cursor = await db.execute(
        "SELECT * FROM scoring_runs ORDER BY run_at DESC LIMIT ? OFFSET ?",
        (limit, offset),
    )
    rows = await cursor.fetchall()
    return [dict(row) for row in rows]


async def get_run_detail(run_id: str) -> dict | None:
    db = await get_db()
    cursor = await db.execute("SELECT * FROM scoring_runs WHERE id = ?", (run_id,))
    row = await cursor.fetchone()
    if row is None:
        return None
    result = dict(row)
    cursor = await db.execute(
        "SELECT * FROM case_results WHERE run_id = ? ORDER BY id", (run_id,)
    )
    result["cases"] = [dict(r) for r in await cursor.fetchall()]
    return result


async def get_trends(era: str | None = None, limit: int = 30) -> list[dict]:
    db = await get_db()
    if era:
        cursor = await db.execute(
            """SELECT id, run_at, llm_model, aggregate_score, escalation_rmse,
                      escalation_bias, action_type_accuracy, tone_accuracy
               FROM scoring_runs WHERE scenario_era = ?
               ORDER BY run_at DESC LIMIT ?""",
            (era, limit),
        )
    else:
        cursor = await db.execute(
            """SELECT id, run_at, llm_model, aggregate_score, escalation_rmse,
                      escalation_bias, action_type_accuracy, tone_accuracy
               FROM scoring_runs ORDER BY run_at DESC LIMIT ?""",
            (limit,),
        )
    rows = await cursor.fetchall()
    return [dict(row) for row in reversed(rows)]


async def get_previous_run(run_id: str, era: str) -> dict | None:
    db = await get_db()
    cursor = await db.execute(
        """SELECT * FROM scoring_runs
           WHERE scenario_era = ? AND run_at < (SELECT run_at FROM scoring_runs WHERE id = ?)
           ORDER BY run_at DESC LIMIT 1""",
        (era, run_id),
    )
    row = await cursor.fetchone()
    return dict(row) if row else None


async def persist_regression_check(check: dict) -> None:
    db = await get_db()
    await db.execute(
        """INSERT INTO regression_checks
           (run_id, baseline_run_id, delta_aggregate, delta_escalation_rmse,
            delta_action_accuracy, delta_tone_accuracy, is_regression,
            significance_p, notes)
           VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)""",
        (
            check["run_id"],
            check.get("baseline_run_id"),
            check.get("delta_aggregate"),
            check.get("delta_escalation_rmse"),
            check.get("delta_action_accuracy"),
            check.get("delta_tone_accuracy"),
            int(check.get("is_regression", False)),
            check.get("significance_p"),
            check.get("notes", ""),
        ),
    )
    await db.commit()


async def get_case_results_for_run(run_id: str) -> list[dict]:
    db = await get_db()
    cursor = await db.execute(
        "SELECT * FROM case_results WHERE run_id = ? ORDER BY id", (run_id,)
    )
    return [dict(r) for r in await cursor.fetchall()]


async def persist_prediction(prediction: dict) -> None:
    db = await get_db()
    await db.execute(
        """INSERT OR REPLACE INTO predictions
           (id, created_at, historical_date, label, prediction_type,
            event_json, leader_id, leader_name, country,
            leader_response_json, population_response_json,
            cascade_responses_json, known_outcome_json, llm_model,
            overall_score, escalation_score, action_type_score,
            tone_score, dimensions_json, scored_at)
           VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
        (
            prediction["id"],
            prediction["created_at"],
            prediction.get("historical_date"),
            prediction.get("label", ""),
            prediction["prediction_type"],
            prediction["event_json"],
            prediction.get("leader_id"),
            prediction.get("leader_name"),
            prediction.get("country"),
            prediction.get("leader_response_json"),
            prediction.get("population_response_json"),
            prediction.get("cascade_responses_json", "[]"),
            prediction.get("known_outcome_json"),
            prediction.get("llm_model"),
            prediction.get("overall_score"),
            prediction.get("escalation_score"),
            prediction.get("action_type_score"),
            prediction.get("tone_score"),
            prediction.get("dimensions_json"),
            prediction.get("scored_at"),
        ),
    )
    await db.commit()


async def get_all_predictions() -> list[dict]:
    db = await get_db()
    cursor = await db.execute(
        "SELECT * FROM predictions ORDER BY COALESCE(historical_date, created_at)"
    )
    return [dict(row) for row in await cursor.fetchall()]


async def get_prediction(prediction_id: str) -> dict | None:
    db = await get_db()
    cursor = await db.execute("SELECT * FROM predictions WHERE id = ?", (prediction_id,))
    row = await cursor.fetchone()
    return dict(row) if row else None


async def delete_prediction(prediction_id: str) -> bool:
    db = await get_db()
    cursor = await db.execute("DELETE FROM predictions WHERE id = ?", (prediction_id,))
    await db.commit()
    return cursor.rowcount > 0


async def get_scored_predictions(leader_name: str | None = None) -> list[dict]:
    db = await get_db()
    if leader_name:
        cursor = await db.execute(
            "SELECT * FROM predictions WHERE overall_score IS NOT NULL AND leader_name = ? ORDER BY historical_date",
            (leader_name,),
        )
    else:
        cursor = await db.execute(
            "SELECT * FROM predictions WHERE overall_score IS NOT NULL ORDER BY historical_date"
        )
    return [dict(row) for row in await cursor.fetchall()]


async def close_db() -> None:
    global _db
    if _db is not None:
        await _db.close()
        _db = None
