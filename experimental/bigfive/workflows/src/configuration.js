// Run Agents settings. lockMinutes: a run lock older than this is treated as abandoned.
// promptVersion must match the header of build_prompt.js; a run row with another prompt_version is refused.
return [{ json: { lockMinutes: 30, promptVersion: 'mm-v1' } }];
