// Analyze and Report: one item per analyzed run with its row id and analyzed_at, for Update: runs.
return $('Analyze').first().json.runs.map((r) => ({ json: r }));
