#!/bin/bash
set -euo pipefail

# Nothing to install. This feature is a HANDLE, not a payload: everything it provides comes
# from dependsOn in devcontainer-feature.json, so listing it is the only thing that pulls
# that tooling into the image and commenting it out is the only thing that keeps it out.
#
# What it carries today is PowerShell, which is maintenance-only tooling: `init.cmd`,
# `setup.ps1` and `spawn.ps1` all run on the HOST, so nothing in a built container ever
# invokes pwsh. A maintainer testing the PowerShell half of those scripts needs it; a project
# built from this repo never does, and it is not a small layer.
#
# The gate is the root `.template-container` marker, read by set_template_feature() in
# setup.sh (Set-TemplateFeature in setup.ps1) during --spawning: present (a checkout of
# agent-dev) leaves the line on, absent (any spawned or adopted project, because the clean
# slate deletes the marker before setup runs) comments it out. That is the same marker the
# CUI data question bypass keys on, for the same reason — "is this the template repo?" has
# exactly one answer, and it lives in one file.
#
# Add anything else agent-dev's own container needs here, as another dependsOn entry, rather
# than to devcontainer.json's feature list, and it becomes template-only for free.
#
# Do not "optimize" this file away: a feature without an install.sh entrypoint is not a valid
# feature, and the CLI fails the build when it cannot run one.
echo "_template_: nothing to install (dependsOn carries the maintenance tooling)."
