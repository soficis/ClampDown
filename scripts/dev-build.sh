#!/usr/bin/env bash
set -euo pipefail

export DOTNET_ROOT="${HOME}/.dotnet"
export PATH="${DOTNET_ROOT}:${PATH}"

dotnet build ClampDown.sln -c Debug
