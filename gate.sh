#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

solution=ModularMonolith.slnx
expected_skips=$(< .gate/skips)
test_output=$(mktemp)
trap 'rm -f "$test_output"' EXIT

dotnet restore "$solution"
dotnet build "$solution" -c Release --no-restore
dotnet test --solution "$solution" -c Release --no-build --no-ansi | tee "$test_output"

summary=$(sed -n '/^Test run summary:/,$p' "$test_output")
test_projects=$(grep -cE 'Tests\.csproj"' "$solution")
reported_apps=$(grep -cE '\.dll \([^)]*\) (passed|failed)' <<< "$summary" || true)
skipped=$(grep -oP '^\s*skipped: \K[0-9]+' <<< "$summary" || true)

if [[ "$reported_apps" -ne "$test_projects" || -z "$skipped" ]]; then
  echo "gate: expected a summary for $test_projects test apps, found $reported_apps; cannot count skipped tests" >&2
  exit 1
fi

if [[ "$skipped" -ne "$expected_skips" ]]; then
  echo "gate: skipped tests: was $expected_skips, now $skipped; if intended, write $skipped to .gate/skips" >&2
  exit 1
fi

echo "gate: green, $skipped skipped tests as recorded"
