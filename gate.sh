#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

solution=ModularMonolith.slnx
expected_skips=$(< .gate/skips)
test_output=$(mktemp)
trap 'rm -f "$test_output"' EXIT

if [[ ! "$expected_skips" =~ ^[0-9]+$ ]]; then
  echo "gate: cannot read the skip budget; .gate/skips must hold one non-negative integer and nothing else" >&2
  exit 1
fi

dotnet restore "$solution"
dotnet build "$solution" -c Release --no-restore
dotnet test --solution "$solution" -c Release --no-build --no-ansi | tee "$test_output"

summary=$(sed -n '/^Test run summary:/,$p' "$test_output")
test_projects=$(grep -cE 'Tests\.csproj"' "$solution")
reported_apps=$(grep -cE '\.dll \([^)]*\) (passed|failed)' <<< "$summary" || true)
skipped=$(sed -nE 's/^[[:space:]]*skipped:[[:space:]]*//p' <<< "$summary")

if [[ "$reported_apps" -ne "$test_projects" ]]; then
  echo "gate: expected a summary for $test_projects test apps, found $reported_apps; cannot count skipped tests" >&2
  exit 1
fi

if [[ ! "$skipped" =~ ^[0-9]+$ ]]; then
  echo "gate: cannot read the skipped count; the test summary must hold exactly one 'skipped: N' line" >&2
  exit 1
fi

if [[ "$skipped" != "$expected_skips" ]]; then
  echo "gate: skipped tests: was $expected_skips, now $skipped; if intended, write $skipped to .gate/skips" >&2
  exit 1
fi

echo "gate: green, $skipped skipped tests as recorded"
