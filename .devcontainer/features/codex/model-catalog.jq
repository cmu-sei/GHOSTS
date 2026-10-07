# Codex 0.156.0's Bedrock catalog predates GPT-6 Sol/Luna. Until upstream
# includes them, reuse each predecessor's complete, version-matched entry.
# This preserves the installed binary's instruction templates and tool schema;
# the request still targets the new model id. Prefer native entries when present.
def ensure_gpt6($family; $description; $priority):
    ("us.openai.gpt-6-" + $family) as $id
    | if any(.models[]; .slug == $id) then .
      else
        ([.models[] | select(.slug == ("us.openai.gpt-5.6-" + $family))][0]
            // error("No Bedrock catalog template for " + $id)) as $template
        | .models += [$template | . + {
            slug: $id,
            display_name: ("GPT-6 " + (if $family == "sol" then "Sol" else "Luna" end)
                + " (US cross-region)"),
            description: $description,
            default_reasoning_level: "medium",
            default_reasoning_summary: "none",
            priority: $priority,
            upgrade: null
        }]
      end;

if $profile == "awsgov" then
    .models |= map(select(
        .slug == "openai.gpt-5.6-terra" or .slug == "openai.gpt-5.6-luna"
    ))
else
    .models |= map(select(.slug | startswith("global.") | not))
    | if $profile == "aws" then
        ensure_gpt6("sol"; "Complex coding and agentic workflows."; 0)
        | ensure_gpt6("luna"; "Efficient model for focused, high-volume tasks."; 1)
        # Generate replacements before removing their predecessor templates.
        | .models |= map(select(
            .slug | test("(^|\\.)openai\\.gpt-5\\.6-(sol|luna)$") | not
        ))
      else . end
end
