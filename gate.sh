#!/usr/bin/env bash
set -euo pipefail
shopt -s nullglob

cd "$(dirname "$0")"

bash scripts/core-hash.sh check

root_solutions=(*.slnx)
template_solution=template-only/Template.slnx
test_output=$(mktemp)
trap 'rm -f "$test_output"' EXIT
results=()

run_solution() {
  local solution=$1
  local skip_file=$2
  local expected_skips summary test_projects reported_apps skipped
  expected_skips=$(< "$skip_file")

  if [[ ! "$expected_skips" =~ ^[0-9]+$ ]]; then
    echo "gate: $solution: cannot read the skip budget; $skip_file must hold one non-negative integer and nothing else" >&2
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
    echo "gate: $solution: expected a summary for $test_projects test apps, found $reported_apps; cannot count skipped tests" >&2
    exit 1
  fi

  if [[ ! "$skipped" =~ ^[0-9]+$ ]]; then
    echo "gate: $solution: cannot read the skipped count; the test summary must hold exactly one 'skipped: N' line" >&2
    exit 1
  fi

  if [[ "$skipped" != "$expected_skips" ]]; then
    echo "gate: $solution: skipped tests: was $expected_skips, now $skipped; if intended, write $skipped to $skip_file" >&2
    exit 1
  fi

  results+=("$solution $skipped")
}

if [[ ${#root_solutions[@]} -ne 1 ]]; then
  echo "gate: the root must hold exactly one *.slnx, found ${#root_solutions[@]}: ${root_solutions[*]:-none}" >&2
  exit 1
fi

run_solution "${root_solutions[0]}" .gate/skips

if [[ -f "$template_solution" ]]; then
  run_solution "$template_solution" template-only/.gate/skips
fi

if [[ -d template-only ]]; then
  bash template-only/scripts/test-update-core.sh
fi

printf -v recorded '%s, ' "${results[@]}"
echo "gate: green, skipped tests as recorded: ${recorded%, }"
