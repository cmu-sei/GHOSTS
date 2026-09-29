# Tier 2 fixtures

One document per referential check, each carrying **exactly one** planted defect. Everything else in
the document is valid, so a fixture that reports a second error at error severity is a bug in the
validator or in the fixture, not a pass.

File name: `CODE__pointer.json`, where `pointer` is the JSON pointer the finding must be reported at
with `/` written as `-` (so `/timeline/events/0/at` is `timeline-events-0-at`). Two fixtures may share
a code when a check has more than one way to fire; the pointer keeps the names distinct.

`expected.json` is the sidecar the test actually reads: file name to `{ code, path, severity }`. The
name repeats the code and the pointer on purpose, so a mis-filed fixture is visible in a directory
listing.

The test is `ScenarioDocumentValidatorTests.Tier_2_finds_the_planted_defect` in
`src/Ghosts.Api.Tests/`. It asserts the expected code appears at the expected path with the expected
severity, and that nothing else in the document is an error.

Adding a check means adding a fixture: a new tier-2 code with no fixture here is a check nobody has
seen fail.
