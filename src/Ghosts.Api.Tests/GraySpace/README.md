# GraySpace contract snapshot

Copied unchanged from GraySpace `main` at commit `6ad3fcd` (2026-10-29), Terrain API 1.3.0:

| File | GraySpace path |
|---|---|
| `grayspace-v1.json` | `openapi/grayspace-v1.json`, the Terrain API (OpenAPI 3.1) |
| `grayspace-slice-v1.json` | `openapi/grayspace-slice-v1.json`, the slice spec (JSON Schema 2020-12) |
| `grayspace-events-v1.json` | `openapi/grayspace-events-v1.json`, `/api/v1/events` messages |

`GraySpaceContract` validates what GHOSTS sends and what its stub answers against these. To move to a newer
GraySpace, copy the three files from its `openapi/` folder and run the tests.
