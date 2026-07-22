"""HTTP game endpoints via TestClient (offline, fixture-backed)."""

from fastapi.testclient import TestClient

from ghosts_rpg.app import app

client = TestClient(app)


def test_health_reports_llm_configuration(monkeypatch):
    monkeypatch.setenv("OLLAMA_HOST", "http://host.docker.internal:11434")
    monkeypatch.setenv("OLLAMA_MODEL", "mistral:7b")

    r = client.get("/health")

    assert r.status_code == 200
    assert r.json()["llm"] == {
        "enabled": True,
        "host": "http://host.docker.internal:11434",
        "model": "mistral:7b",
    }


def test_fixture_catalog_lists_two_player_scenarios():
    r = client.get("/api/fixtures")
    assert r.status_code == 200
    fixtures = r.json()["fixtures"]
    assert [f["fixture"] for f in fixtures] == ["soc-morning", "operation-overlord"]
    assert fixtures[1]["name"] == "OPERATION OVERLORD: The Normandy Decision"
    assert all(f["estimatedMinutes"] > 0 for f in fixtures)
    assert "phishing-drill" not in {f["fixture"] for f in fixtures}


def test_new_game_returns_first_frame():
    r = client.post("/api/games", json={"fixture": "phishing-drill"})
    assert r.status_code == 200
    body = r.json()
    assert body["gameId"]
    frame = body["frame"]
    assert frame["awaiting_player"]
    assert frame["tasks"]
    assert frame["tasks"][0]["actions"]
    assert frame["beats"][-1]["cell"] == "Blue Team"


def test_new_overlord_game_uses_planning_labels():
    r = client.post("/api/games", json={"fixture": "operation-overlord"})
    assert r.status_code == 200
    frame = r.json()["frame"]
    assert frame["awaiting_player"]
    assert frame["hud"]["role"] == "Blue Team (SHAEF Joint Planner)"
    assert frame["hud"]["windowLabel"] == "in conference"
    assert frame["hud"]["deadlineLabel"] == "COMMAND DECISION"
    assert frame["tasks"][0]["actions"][1]["label"] == (
        "approve and sign the Normandy joint concept"
    )


def test_full_win_path_over_http():
    gid = client.post("/api/games", json={"fixture": "phishing-drill"}).json()["gameId"]
    client.post(f"/api/games/{gid}/act", json={"input": "investigate"})
    client.post(f"/api/games/{gid}/act", json={"input": "quarantine the email and block the domain"})
    final = client.post(f"/api/games/{gid}/act", json={"input": "stand down"}).json()["frame"]
    assert final["is_complete"]
    assert final["aar"]["outcome"] == "WIN"


def test_act_on_missing_game_404():
    assert client.post("/api/games/nope/act", json={"input": "x"}).status_code == 404


def test_new_game_requires_source():
    assert client.post("/api/games", json={}).status_code == 400
