"""HTTP endpoints via TestClient (offline, fixture-backed)."""

from fastapi.testclient import TestClient

from ghosts_rpg.app import app

client = TestClient(app)


def test_health_reports_llm_configuration(monkeypatch):
    monkeypatch.delenv("RPG_LLM_PROVIDER", raising=False)
    monkeypatch.setenv("OLLAMA_HOST", "http://host.docker.internal:11434")
    monkeypatch.setenv("OLLAMA_MODEL", "mistral:7b")

    r = client.get("/health")

    assert r.status_code == 200
    assert r.json()["llm"] == {
        "provider": "ollama",
        "enabled": True,
        "host": "http://host.docker.internal:11434",
        "model": "mistral:7b",
    }


def test_health_reports_bedrock_provider(monkeypatch):
    monkeypatch.setenv("RPG_LLM_PROVIDER", "bedrock")
    monkeypatch.setenv("BEDROCK_MODEL", "us.anthropic.claude-opus-4-8")
    monkeypatch.setenv("AWS_REGION", "us-east-1")

    r = client.get("/health")

    assert r.status_code == 200
    llm = r.json()["llm"]
    assert llm["provider"] == "bedrock"
    assert llm["enabled"] is True
    assert llm["model"] == "us.anthropic.claude-opus-4-8"
    assert llm["host"] == "bedrock:us-east-1"


def test_fixture_catalog_lists_meridian_hybrid():
    r = client.get("/api/fixtures")
    assert r.status_code == 200
    fixtures = r.json()["fixtures"]
    assert any(f["fixture"] == "meridian-hybrid" for f in fixtures)


def test_new_game_and_act_flow():
    start = client.post("/api/games", json={"fixture": "meridian-hybrid"})
    assert start.status_code == 200
    body = start.json()
    gid = body["gameId"]
    assert body["frame"]["awaiting_player"] is True

    act = client.post(
        f"/api/games/{gid}/act",
        json={"action": "investigate the anomaly", "rationale": "because I must confirm the attack"},
    )
    assert act.status_code == 200
    frame = act.json()["frame"]
    assert frame["band"] in {"Likely", "Even", "Unlikely", "Longshot"}


def test_new_game_requires_fixture():
    r = client.post("/api/games", json={})
    assert r.status_code == 400


def test_act_on_missing_game_is_404():
    r = client.post("/api/games/deadbeef/act", json={"action": "x", "rationale": "y"})
    assert r.status_code == 404
