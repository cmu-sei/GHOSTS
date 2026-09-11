#!/usr/bin/env bash
# Claude Code status line: model | cwd | git branch | context used | session cost
# Installed to ~/.claude/statusline.sh by postcreate.sh. Edit this copy.

input=$(cat)

model=$(echo "$input" | jq -r '.model.display_name // .model.id // "unknown"')

# Current directory, shortened with ~
cwd=$(echo "$input" | jq -r '.workspace.current_dir // .cwd // ""')
dir_str="$cwd"
case "$dir_str" in
  "$HOME"*) dir_str="~${dir_str#"$HOME"}" ;;
esac

# Git branch, falling back to a short SHA when detached.
# core.fsmonitor=false avoids taking optional locks in the repo.
branch=""
if [ -n "$cwd" ] && git -C "$cwd" rev-parse --git-dir >/dev/null 2>&1; then
  branch=$(git -C "$cwd" -c core.fsmonitor=false symbolic-ref --short HEAD 2>/dev/null \
    || git -C "$cwd" rev-parse --short HEAD 2>/dev/null)
fi

ctx_str=""
ctx_pct=$(echo "$input" | jq -r '.context_window.used_percentage // empty')
[ -n "$ctx_pct" ] && ctx_str=$(printf 'ctx:%.0f%%' "$ctx_pct")

cost_str=""
cost=$(echo "$input" | jq -r '.cost.total_cost_usd // empty')
[ -n "$cost" ] && cost_str=$(printf 'cost:$%.2f' "$cost")

parts=("$model")
[ -n "$dir_str" ] && parts+=("$dir_str")
[ -n "$branch" ] && parts+=("branch:$branch")
[ -n "$ctx_str" ] && parts+=("$ctx_str")
[ -n "$cost_str" ] && parts+=("$cost_str")

out="${parts[0]}"
for p in "${parts[@]:1}"; do
  out="$out | $p"
done
printf '%s' "$out"
