"""Load a scenario into the in-memory model.

Two interchangeable sources, both producing a `ScenarioBundle`:

- `load_bundle_file(path)`   — read a fixture/export JSON off disk (offline path).
- `load_from_api(id, ...)`   — the three live GETs against a running Ghosts.Api.

Downstream code depends only on `ScenarioBundle`, so the engine never knows or
cares which source was used."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Optional

import httpx

from .config import Settings
from .models import Graph, Objective, Scenario, ScenarioBundle


def load_bundle_file(path: str | Path) -> ScenarioBundle:
    """Load a bundled export (the shape of the fixtures/ files)."""
    raw = json.loads(Path(path).read_text(encoding="utf-8"))
    return ScenarioBundle.model_validate(raw)


def load_from_api(
    scenario_id: int,
    settings: Optional[Settings] = None,
    client: Optional[httpx.Client] = None,
) -> ScenarioBundle:
    """Assemble a bundle from the three live GETs.

    GET /api/scenarios/{id}
    GET /api/scenarios/{id}/builder/graph
    GET /api/objectives?scenarioId={id}
    """
    settings = settings or Settings.from_env()
    base = settings.ghosts_api_url.rstrip("/")
    owns_client = client is None
    client = client or httpx.Client(timeout=30.0)
    try:
        scenario = Scenario.model_validate(
            _get_json(client, f"{base}/api/scenarios/{scenario_id}")
        )
        graph = Graph.model_validate(
            _get_json(client, f"{base}/api/scenarios/{scenario_id}/builder/graph")
        )
        objectives_raw = _get_json(
            client, f"{base}/api/objectives", params={"scenarioId": scenario_id}
        )
        objectives = [Objective.model_validate(o) for o in objectives_raw]
        return ScenarioBundle(scenario=scenario, graph=graph, objectives=objectives)
    finally:
        if owns_client:
            client.close()


def _get_json(client: httpx.Client, url: str, params: Optional[dict] = None):
    resp = client.get(url, params=params)
    resp.raise_for_status()
    return resp.json()
