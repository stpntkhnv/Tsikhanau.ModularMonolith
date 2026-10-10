#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit

export LC_ALL=C

scaffolded_test='^.+\.Modules\.([A-Za-z0-9]+)\.([A-Za-z0-9]+)Tests\.[A-Za-z0-9_]+$'

main() {
  read_arguments "$@"
  log=$(mktemp --suffix=.gate.log)
  gate_status=0
  (cd "$app" && bash gate.sh) > "$log" 2>&1 || gate_status=$?

  check_tests_ran
  check_warnings
  read_failed_tests
  if [[ ${#expected[@]} -eq 0 ]]; then
    check_green
  else
    check_failures
  fi
}

usage() {
  echo "usage: template-only/scripts/check-feature-failures.sh <app folder> [<Test.Name> ...]" >&2
  echo "runs the app's gate.sh once; passes when the build has no warnings, exactly the named tests fail," >&2
  echo "each with '<Feature> in <Module> is not implemented', and the run skips as many tests as .gate/skips holds;" >&2
  echo "a name matches a failed test by its full name or by its end after a dot (Tags.NoteCreatedTests.<Method>)" >&2
  exit 2
}

read_arguments() {
  [[ $# -ge 1 && -f "$1/gate.sh" && -f "$1/.gate/skips" ]] || usage
  app=$1
  shift
  expected=("$@")
}

fail() {
  echo "check-feature-failures: $1" >&2
  exit 1
}

check_tests_ran() {
  if ! grep -q '^Test run summary:' "$log"; then
    tail -n 30 "$log" | sed 's/^/  | /' >&2
    fail "the gate stopped before the tests ran (exit $gate_status)"
  fi
}

check_warnings() {
  local counts
  counts=$(sed -nE 's/^ +([0-9]+) Warning\(s\)$/\1/p' "$log" | sort -u | paste -sd ' ')
  [[ -n "$counts" ]] || fail "the gate printed no build summary ('N Warning(s)')"
  [[ "$counts" == 0 ]] || fail "the build reported warnings: $counts"
}

read_failed_tests() {
  local summary values
  summary=$(sed -n '/^Test run summary:/,$p' "$log")
  values=$(sed -nE 's/^ +failed: ([0-9]+)$/\1/p' <<< "$summary" | paste -sd ' ')
  [[ "$values" =~ ^[0-9]+$ ]] || fail "cannot read the failed count; the test summary must hold one 'failed: N' line"
  failed_count=$values
  values=$(sed -nE 's/^ +skipped: ([0-9]+)$/\1/p' <<< "$summary" | paste -sd ' ')
  [[ "$values" =~ ^[0-9]+$ ]] || fail "cannot read the skipped count; the test summary must hold one 'skipped: N' line"
  skipped=$values
  mapfile -t failed < <(sed -nE 's/^failed (.+) \([0-9][^()]*\)$/\1/p' "$log" | sort -u)
  if [[ ${#failed[@]} -ne $failed_count ]]; then
    fail "the summary counts $failed_count failed tests, but the output names ${#failed[@]}: ${failed[*]:-none}"
  fi
}

check_green() {
  if [[ $gate_status -ne 0 || $failed_count -ne 0 ]]; then
    fail "no feature test is named, but the gate failed (exit $gate_status); failed tests: ${failed[*]:-none}"
  fi
  echo "check-feature-failures: build without warnings; gate green, as expected with no test named"
}

check_failures() {
  local skips problems=() name
  skips=$(tr -d '[:space:]' < "$app/.gate/skips")
  [[ $gate_status -ne 0 ]] || fail "the gate is green, but these tests should fail: ${expected[*]}"
  [[ "$skipped" == "$skips" ]] || problems+=("skipped tests: .gate/skips holds $skips, the run skipped $skipped")
  for name in "${failed[@]}"; do
    if expected_by_name "$name"; then
      check_text "$name"
    else
      problems+=("$name failed, but it is not among the expected tests")
    fi
  done
  for name in "${expected[@]}"; do
    check_expected "$name"
  done
  if [[ ${#problems[@]} -gt 0 ]]; then
    printf 'check-feature-failures: %s\n' "${problems[@]}" >&2
    exit 1
  fi
  echo "check-feature-failures: build without warnings; exactly the $failed_count named tests failed," \
    "each with its feature's '<Feature> in <Module> is not implemented':"
  printf '  %s\n' "${failed[@]}"
  echo "check-feature-failures: skipped $skipped as .gate/skips records; every other test passed"
}

matches() {
  [[ "$1" == "$2" || "$1" == *".$2" ]]
}

expected_by_name() {
  local name
  for name in "${expected[@]}"; do
    if matches "$1" "$name"; then
      return 0
    fi
  done
  return 1
}

check_expected() {
  local name matched=()
  for name in "${failed[@]}"; do
    if matches "$name" "$1"; then
      matched+=("$name")
    fi
  done
  if [[ ${#matched[@]} -eq 0 ]]; then
    problems+=("$1 was expected to fail, but no failed test has that name")
  elif [[ ${#matched[@]} -gt 1 ]]; then
    problems+=("$1 names ${#matched[@]} failed tests (${matched[*]}); give more of the name")
  fi
}

check_text() {
  local message failure
  if [[ ! "$1" =~ $scaffolded_test ]]; then
    problems+=("$1 is not a scaffolded feature test (<App>.IntegrationTests.Modules.<Module>.<Feature>Tests.<Method>)")
    return
  fi
  message="${BASH_REMATCH[2]} in ${BASH_REMATCH[1]} is not implemented"
  failure=$(failure_of "$1")
  if [[ "$failure" != *"$message"* ]]; then
    problems+=("$1 failed without '$message':"$'\n'"$(head -n 12 <<< "$failure")")
  fi
}

failure_of() {
  awk -v name="$1" '
    index($0, "failed " name " (") == 1 { inside = 1; next }
    inside && ($0 == "" || /^[[:space:]]/) { print; next }
    inside { exit }
  ' "$log"
}

finish() {
  local status=$?
  trap - EXIT
  if [[ -n "${log:-}" ]]; then
    if [[ $status -eq 0 ]]; then
      rm -f -- "$log"
    else
      echo "check-feature-failures: the gate's output is kept in $log" >&2
    fi
  fi
  exit "$status"
}

trap finish EXIT
main "$@"
