"""Fog: indicator visibility per policy."""

from ghosts_rpg.fog import reveals_ground_truth, visible_indicators
from ghosts_rpg.models import FogSpec, Scenario


def _scenario(policy: str) -> Scenario:
    return Scenario(id=1, name="t", fog=FogSpec(default=policy))


def test_partial_shows_indicators_hides_prefixed():
    sc = _scenario("partial")
    seen = visible_indicators(sc, ["SMB spike observed", "!ground-truth-only"])
    assert "SMB spike observed" in seen
    assert "!ground-truth-only" not in seen
    assert not reveals_ground_truth(sc)


def test_off_reveals_everything_and_ground_truth():
    sc = _scenario("off")
    seen = visible_indicators(sc, ["a", "!b"])
    assert seen == ["a", "!b"]
    assert reveals_ground_truth(sc) is True


def test_empty_indicators():
    assert visible_indicators(_scenario("partial"), []) == []
