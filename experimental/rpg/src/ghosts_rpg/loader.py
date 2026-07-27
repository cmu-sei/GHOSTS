"""Load a scenario into the in-memory model.

A Kriegspiel scenario is a self-contained authored spec on disk (situation, player
mandate, OPFOR playbook, world, triggers). `load_bundle_file` reads one fixture into
a `ScenarioBundle`, which is all the engine depends on.

Live GHOSTS-API loading is deferred: the world-state spec has no direct API export
yet (the builder integration is future work). Only the offline fixture path exists.
"""

from __future__ import annotations

import json
from pathlib import Path

from .models import ScenarioBundle


def load_bundle_file(path: str | Path) -> ScenarioBundle:
    """Load a Kriegspiel scenario fixture (the shape of the fixtures/ files)."""
    raw = json.loads(Path(path).read_text(encoding="utf-8"))
    return ScenarioBundle.model_validate(raw)
