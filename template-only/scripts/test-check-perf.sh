#!/usr/bin/env bash
set -uo pipefail
shopt -s inherit_errexit

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
source "$script_dir/check-perf.sh"
set +e

passes=0
failures=0

pass() {
  passes=$((passes + 1))
  echo "PASS: $1"
}

fail() {
  failures=$((failures + 1))
  echo "FAIL: $1 ($2)"
}

expect_equal() {
  if [[ "$2" == "$3" ]]; then
    pass "$1"
  else
    fail "$1" "expected '$3', got '$2'"
  fi
}

expect_success() {
  local name=$1
  shift
  if "$@" > /dev/null 2>&1; then
    pass "$name"
  else
    fail "$name" "expected success"
  fi
}

expect_failure() {
  local name=$1
  shift
  if "$@" > /dev/null 2>&1; then
    fail "$name" "expected failure"
  else
    pass "$name"
  fi
}

command_line='create c=1 n=3000: 1389 ops/s, p50 0.719 ms, p95 0.931 ms, p99 0.987 ms, mean 0.720 ms, load 4.35'
queue_line='background x1: n=2000 outbox drained 2.52 s (795/s), all pongs 2.85 s (702 events/s end to end)'
command_output=$'# command n=3000\n'"$command_line"$'\nwall 7551 ms'
queue_output=$'# queue-background\n'"$queue_line"$'\nwall 9000 ms'

expect_equal "command line gives its ops per second" "$(parse_command_ops "$command_output")" "1389"
expect_equal "queue line gives its events per second" "$(parse_queue_events "$queue_output")" "702"
expect_equal "command parser reads only the c=1 line" \
  "$(parse_command_ops $'create c=8 n=3000: 10470 ops/s\ncreate c=1 n=3000: 1200 ops/s')" "1200"
expect_failure "command output without the number fails" parse_command_ops \
  $'create c=1 n=3000: failed\nwall 1 ms'
expect_failure "queue output without the number fails" parse_queue_events \
  $'background x1: n=2000 outbox drained 2.52 s (795/s)'
expect_failure "empty output fails the command parser" parse_command_ops ""
expect_failure "empty output fails the queue parser" parse_queue_events ""
expect_equal "median of three" "$(median 1389 1200 1500)" "1389"
expect_equal "median of three with decimals" "$(median 10.5 9.5 11.5)" "10.5"
expect_equal "ratio of 91 percent" "$(ratio_percent 1000 910)" "91.0"
expect_success "head at 91 percent of the base passes" verdict 1000 910
expect_success "head at 90 percent of the base passes" verdict 1000 900
expect_failure "head at 89 percent of the base fails" verdict 1000 890
expect_success "a faster head passes" verdict 1000 1500

expect_code() {
  local name=$1
  local expected=$2
  shift 2
  local code=0
  "$@" > /dev/null 2>&1 || code=$?
  expect_equal "$name" "$code" "$expected"
}

expect_contains() {
  if [[ "$2" == *"$3"* ]]; then
    pass "$1"
  else
    fail "$1" "missing '$3' in: $2"
  fi
}

expect_equal "min of samples" "$(min_of 1389 1200 1500)" "1200"
expect_equal "max of samples" "$(max_of 1389 1200 1500)" "1500"
expect_equal "spread of 10 percent" "$(spread_percent 1000 1100)" "10.0"
expect_failure "spread of exactly 10 percent is not noisy" noisy 1000 1100
expect_success "spread above 10 percent is noisy" noisy 1000 1101

expect_code "spread inside the limit on both sides passes" 0 judge command "1000 1050 1100" "1000 1020 1080"
expect_code "spread inside the limit and a slow head fails" 1 \
  judge command "1000 1050 1100" "700 720 760"
expect_code "spread above the limit on the base is inconclusive" 3 judge command "1000 1050 1300" "1000 1020 1080"
expect_code "spread above the limit on the head is inconclusive" 3 judge queue "1000 1050 1100" "700 1020 1080"
expect_contains "inconclusive names the scenario, the side and the spread" \
  "$(judge queue "1000 1050 1100" "700 1020 1080" 2>&1)" \
  "inconclusive: queue head spread 54.3%; rerun on a quieter machine"
expect_contains "the verdict prints min and max next to the median" \
  "$(judge command "1000 1050 1100" "1000 1020 1080" 2>&1)" "base median 1050 (min 1000, max 1100)"

clear_routing() {
  local names variables
  if ! names=$(git rev-parse --local-env-vars) || [[ -z $names ]]; then
    echo "test-check-perf: cannot list the git routing variables" >&2
    exit 1
  fi
  mapfile -t variables <<< "$names"
  unset -- "${variables[@]}"
}

repository_state() {
  git -C "$1" rev-parse HEAD
  cksum < "$1/.git/index"
  cat "$1/.git/config"
}

scratch=$(mktemp -d)
trap 'rm -rf -- "${scratch:?}"' EXIT
clear_routing
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1
export GIT_CONFIG_COUNT=2 GIT_CONFIG_KEY_0=maintenance.auto GIT_CONFIG_VALUE_0=false GIT_CONFIG_KEY_1=gc.auto GIT_CONFIG_VALUE_1=0
export GIT_AUTHOR_NAME=test GIT_AUTHOR_EMAIL=test@example.com
export GIT_COMMITTER_NAME=test GIT_COMMITTER_EMAIL=test@example.com
mkdir -p "$scratch/repo" "$scratch/other" "$scratch/bin" "$scratch/state" "$scratch/tmp"
git -C "$scratch/other" init -q
printf 'other\n' > "$scratch/other/file"
git -C "$scratch/other" add file
git -C "$scratch/other" commit -q -m other
other_before=$(repository_state "$scratch/other")
cat > "$scratch/bin/dotnet" <<'FAKE'
#!/usr/bin/env bash
if [[ $1 == build ]]; then
  if [[ -n ${FAKE_BUILD_FAIL-} ]]; then
    echo "error CS1234: fake compile failure"
    exit 1
  fi
  exit 0
fi
scenario=
for argument in "$@"; do
  if [[ $argument == command || $argument == queue-background ]]; then
    scenario=$argument
  fi
done
counter=$FAKE_STATE/$(basename "$PWD")-$scenario
calls=$(cat "$counter" 2> /dev/null || echo 0)
calls=$((calls + 1))
echo "$calls" > "$counter"
value=100
if [[ $calls -eq 1 ]]; then
  value=1
fi
if [[ -n ${FAKE_NOISY-} && $calls -eq 3 && $PWD == */head ]]; then
  value=150
fi
echo "create c=1 n=3000: $value ops/s, p50 1 ms"
echo "background x1: n=2000 outbox drained 1 s (1/s), all pongs 1 s ($value events/s end to end)"
FAKE
chmod +x "$scratch/bin/dotnet"

run_fake() {
  rm -f -- "$scratch"/state/*
  (cd "$scratch/repo" && PATH="$scratch/bin:$PATH" FAKE_STATE="$scratch/state" TMPDIR="$scratch/tmp" \
    env -u PERF_PG bash "$script_dir/check-perf.sh" HEAD 2>&1)
}

routed_to_other() {
  GIT_DIR=$scratch/other/.git GIT_WORK_TREE=$scratch/other GIT_INDEX_FILE=$scratch/other/.git/index "$@"
}

worktree_lines() {
  git -C "$scratch/repo" worktree list | wc -l
}

scratch_cases() {
  clear_routing
  git -C "$scratch/repo" init -q
  git -C "$scratch/repo" commit -q --allow-empty -m first

  output=$(run_fake)
  code=$?
  expect_equal "a clean fake run exits 0" "$code" "0"
  expect_contains "the warm-up run is discarded" "$output" "head command ops/s 1 (load"
  expect_contains "the warm-up spike does not reach the medians" "$output" "command: base median 100 (min 100, max 100)"
  expect_equal "worktrees are removed after a clean run" "$(worktree_lines)" "1"
  expect_equal "the temp folder is emptied after a clean run" "$(ls -A "$scratch/tmp" | wc -l)" "0"

  output=$(PERF_PG='' run_fake)
  code=$?
  expect_equal "a fake run ignores an exported empty PERF_PG" "$code" "0"

  output=$(FAKE_NOISY=1 run_fake)
  code=$?
  expect_equal "a noisy head exits 3" "$code" "3"
  expect_contains "a noisy head says inconclusive" "$output" "inconclusive: command head spread 50.0%"
  expect_equal "worktrees are removed after an inconclusive run" "$(worktree_lines)" "1"

  output=$(FAKE_BUILD_FAIL=1 run_fake)
  code=$?
  expect_equal "a failed build exits 1" "$code" "1"
  expect_contains "a failed build prints the build output" "$output" "error CS1234: fake compile failure"
  expect_equal "worktrees are removed after a failed build" "$(worktree_lines)" "1"
  expect_equal "the temp folder is emptied after a failed build" "$(ls -A "$scratch/tmp" | wc -l)" "0"

  output=$(routed_to_other run_fake)
  code=$?
  expect_equal "a run with another repository's GIT_DIR exits 0" "$code" "0"
  expect_contains "a run with another repository's GIT_DIR measures the repository it runs in" "$output" \
    "head $(git -C "$scratch/repo" rev-parse HEAD)"
  expect_equal "worktrees are removed after a run with another repository's GIT_DIR" "$(worktree_lines)" "1"
}

routed_to_other scratch_cases
expect_equal "GIT_DIR, GIT_WORK_TREE and GIT_INDEX_FILE from a hook leave their repository's HEAD, index and config" \
  "$(repository_state "$scratch/other")" "$other_before"

output=$(cd "$scratch/tmp" && PERF_PG=host=elsewhere main main 2>&1)
code=$?
expect_equal "PERF_PG refuses with exit 1" "$code" "1"
expect_contains "PERF_PG refusal says why" "$output" "PERF_PG is set"
output=$(cd "$scratch/tmp" && PERF_PG='' main main 2>&1)
code=$?
expect_equal "an empty PERF_PG refuses with exit 1" "$code" "1"
expect_contains "an empty PERF_PG refusal says why" "$output" "PERF_PG is set"
expect_code "no base ref is a usage error" 2 main

echo "test-check-perf: $passes passed, $failures failed"
[[ $failures -eq 0 ]]
