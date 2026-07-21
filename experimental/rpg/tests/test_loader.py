"""Loader + model tests against the phishing-drill fixture (offline, no API)."""

from pathlib import Path

import pytest

from ghosts_rpg.loader import load_bundle_file
from ghosts_rpg.models import Cell

FIXTURE = (
    Path(__file__).resolve().parents[1]
    / "fixtures"
    / "scenarios"
    / "phishing-drill.json"
)


@pytest.fixture
def bundle():
    return load_bundle_file(FIXTURE)


def test_scenario_metadata_parses(bundle):
    assert bundle.scenario.id == 1
    assert bundle.scenario.name == "Phishing Drill: First Contact"
    assert bundle.scenario.builder_status == "Compiled"


def test_timeline_loads_in_number_order(bundle):
    events = bundle.scenario.timeline.events
    assert len(events) == 8
    assert [e.number for e in events] == sorted(e.number for e in events)


def test_assigned_maps_to_cells(bundle):
    events = bundle.scenario.timeline.events
    by_num = {e.number: e for e in events}
    assert by_num[2].cell is Cell.RED
    assert by_num[4].cell is Cell.BLUE
    assert by_num[4].is_player_turn
    assert by_num[1].cell is Cell.WHITE
    assert not by_num[2].is_player_turn


def test_player_turns_are_blue_team(bundle):
    events = bundle.scenario.timeline.events
    player_turns = [e.number for e in events if e.is_player_turn]
    assert player_turns == [4, 7]


def test_branch_conditions_present(bundle):
    events = bundle.scenario.timeline.events
    by_num = {e.number: e for e in events}
    assert by_num[5].trigger_condition == "flag:contained"
    assert by_num[6].trigger_condition == "!contained"
    assert by_num[5].trigger_kind == "Triggered"


def test_objectives_and_camelcase_aliases(bundle):
    assert len(bundle.objectives) == 2
    obj2 = next(o for o in bundle.objectives if o.id == 2)
    assert obj2.name == "Contain before lateral movement"
    # success_criteria comes from camelCase "successCriteria"
    assert "contained" in obj2.success_criteria


def test_objective_ids_alias_on_events(bundle):
    events = bundle.scenario.timeline.events
    by_num = {e.number: e for e in events}
    assert by_num[4].objective_ids == [1, 2]


def test_graph_cast_and_edges(bundle):
    assert len(bundle.graph.nodes) == 4
    assert len(bundle.graph.edges) == 3
    actor = next(n for n in bundle.graph.nodes if n.entity_type == "ThreatActor")
    assert actor.name == "Crimson Tide"
    # every edge references real nodes
    ids = {n.id for n in bundle.graph.nodes}
    for e in bundle.graph.edges:
        assert e.source_entity_id in ids and e.target_entity_id in ids


def test_scenario_parameters_threat_actor_ttps(bundle):
    params = bundle.scenario.scenario_parameters
    assert params is not None
    actor = params.threat_actors[0]
    assert actor.name == "Crimson Tide"
    assert "T1566.001" in actor.ttps
