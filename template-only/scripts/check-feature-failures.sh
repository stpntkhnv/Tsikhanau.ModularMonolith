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
  problems=()
  check_test_apps
  if [[ ${#expected[@]} -eq 0 ]]; then
    check_green
  else
    check_failures
  fi
}

usage() {
  echo "usage: template-only/scripts/check-feature-failures.sh <app folder> [<Test.Name> ...]" >&2
  echo "runs the app's gate.sh once; passes when the build has no warnings, exactly the named tests fail," >&2
  echo "each with '<Feature> in <Module> is not implemented', the run skips as many tests as .gate/skips holds," >&2
  echo "and every test project of the app's *.slnx reports: passed, or failed with exactly its named tests;" >&2
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
  local values
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

check_test_apps() {
  local solutions=("$app"/*.slnx) projects project
  if [[ ${#solutions[@]} -ne 1 || ! -f "${solutions[0]}" ]]; then
    fail "the app root must hold one *.slnx, which names the test projects"
  fi
  solution=${solutions[0]##*/}
  mapfile -t projects < <(sed -nE 's#.*["/\\]([^/\\"]+Tests)\.csproj".*#\1#p' "${solutions[0]}" | sort -u)
  [[ ${#projects[@]} -gt 0 ]] || fail "$solution names no test project (*Tests.csproj)"
  for project in "${projects[@]}"; do
    check_test_app "$project"
  done
  mapfile -t -O "${#problems[@]}" problems < <(crashed_test_apps)
}

check_test_app() {
  local results count wanted
  mapfile -t results < <(awk -v app="/$1.dll (" '
    index($0, "  /") == 1 && index($0, app) { sub(/^.*\.dll \([^)]*\) /, ""); print }
  ' <<< "$summary")
  count=$(failed_in "$1")
  wanted="failed with $count error(s)"
  if [[ ${#results[@]} -eq 0 ]]; then
    problems+=("no test summary line for $1; every test project of $solution must report one")
  elif [[ ${#results[@]} -gt 1 ]]; then
    problems+=("the test summary has ${#results[@]} lines for $1; every test project reports one")
  elif [[ $count -eq 0 && "${results[0]}" != "passed ("* ]]; then
    problems+=("$1 has no failed test, so it must pass, but its summary says '${results[0]}'")
  elif [[ $count -gt 0 && "${results[0]}" != "$wanted ("* ]]; then
    problems+=("$1 names $count failed tests, so its summary must say '$wanted', but it says '${results[0]}'")
  fi
}

failed_in() {
  awk -v app="/$1.dll (" '
    /^failed .+ \([0-9][^()]*\)$/ { pending = 1; next }
    pending && index($0, "  from ") == 1 && index($0, app) { count++ }
    { pending = 0 }
    END { print count + 0 }
  ' "$log"
}

crashed_test_apps() {
  awk '
    /^\/.+\.dll \([^)]*\) / { app = $0; sub(/\.dll \(.*$/, "", app); sub(/^.*\//, "", app) }
    /^Exit code: [0-9]+$/ && $3 != 2 {
      print app " ended with exit code " $3 ", not 2: its test process failed (aborted or crashed), not only its tests"
    }
  ' "$log"
}

report_problems() {
  if [[ ${#problems[@]} -gt 0 ]]; then
    printf 'check-feature-failures: %s\n' "${problems[@]}" >&2
    exit 1
  fi
}

check_green() {
  report_problems
  if [[ $gate_status -ne 0 || $failed_count -ne 0 ]]; then
    fail "no feature test is named, but the gate failed (exit $gate_status); failed tests: ${failed[*]:-none}"
  fi
  echo "check-feature-failures: build without warnings; gate green, as expected with no test named"
}

check_failures() {
  local skips name
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
  report_problems
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
