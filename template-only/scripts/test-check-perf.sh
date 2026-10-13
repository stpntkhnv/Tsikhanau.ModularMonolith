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

PERF_PG=host=elsewhere expect_failure "PERF_PG refuses to run" main main
expect_failure "no base ref is a usage error" main

echo "test-check-perf: $passes passed, $failures failed"
[[ $failures -eq 0 ]]
