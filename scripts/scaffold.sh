#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname -- "${BASH_SOURCE[0]}")/.."

project=src/Core/Scaffold/Tsikhanau.Monolith.Scaffold.csproj
assembly=src/Core/Scaffold/bin/Release/net10.0/Tsikhanau.Monolith.Scaffold.dll
build_log=$(mktemp)
trap 'rm -f -- "$build_log"' EXIT

if ! dotnet build "$project" -c Release -nologo -v q -clp:NoSummary > "$build_log" 2>&1; then
  cat -- "$build_log" >&2
  echo "scaffold: cannot build $project" >&2
  exit 1
fi

if [[ ! -f "$assembly" ]]; then
  echo "scaffold: the build did not produce $assembly" >&2
  exit 1
fi

rm -f -- "$build_log"
trap - EXIT
exec dotnet "$assembly" "$@"
