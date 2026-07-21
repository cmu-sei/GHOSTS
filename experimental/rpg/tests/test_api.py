"""HTTP game endpoints via TestClient (offline, fixture-backed)."""

from fastapi.testclient import TestClient

from ghosts_rpg.app import app

client = TestClient(app)


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
