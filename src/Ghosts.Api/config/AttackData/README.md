# MITRE ATT&CK data

Put `enterprise-attack.json` in this directory. It is MITRE's Enterprise ATT&CK STIX 2.1 bundle, about 45 MB,
which the repository does not ship (it is in `.gitignore`):

https://github.com/mitre-attack/attack-stix-data/raw/master/enterprise-attack/enterprise-attack.json

On startup the API loads it into the `attack_techniques` and `attack_groups` tables once, when they are empty
(`DbInitializer`). `POST api/attack/import` loads it again. Without the file, startup logs a warning and the
Scenario Builder's ATT&CK enrichment has nothing to search; scenario authoring is unaffected, since its lookup
tools read the index files embedded in the API.
