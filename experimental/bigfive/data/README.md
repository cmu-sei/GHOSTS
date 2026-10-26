# Source data (not in git)

The study inputs are human-subject data and are **never committed**. Keep local copies here (they are ignored by
`.gitignore`) and upload them through the n8n form of "Big Five 2 - Import Human Data":

- `Personality Data.xlsx` — sheet `Human Responses` (Participant ID, age, Gender, ethnicity, bfi1..bfi60) and sheet `bfi lookup` (bfiN, item text)
- `Human Mini-Marker Results.xlsx` — sheet `Sheet1` (Participant ID, age, Gender, ethnicity, tda1..tda40)

Agent answers produced outside n8n go in through "Big Five 3 - Add Run" as a sheet with `Participant ID` and
`tda1..tda40`.

Ask the study owner for the files. Do not add them to the repository, a fork, or a PR.
