#!/usr/bin/env bash
set -euo pipefail

export DOTNET_ROOT="${HOME}/.dotnet"
export PATH="${DOTNET_ROOT}:${PATH}"

dotnet format ClampDown.sln --verify-no-changes
