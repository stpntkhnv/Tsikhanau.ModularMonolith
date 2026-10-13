#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit

readonly perf_project=template-only/perf/Tsikhanau.Monolith.Perf
readonly rounds=3
readonly threshold_percent=90

repo_root=
work=
worktrees=()

parse_number() {
  local pattern=$1
  local text=$2
  local number
  number=$(sed -nE "$pattern" <<< "$text" | head -n 1)
  if [[ ! "$number" =~ ^[0-9]+(\.[0-9]+)?$ ]]; then
    return 1
  fi
  echo "$number"
}

parse_command_ops() {
  parse_number 's/^create c=1 n=3000: ([0-9]+(\.[0-9]+)?) ops\/s.*/\1/p' "$1"
}

parse_queue_events() {
  parse_number 's/.*\(([0-9]+(\.[0-9]+)?) events\/s end to end\).*/\1/p' "$1"
}

median() {
  printf '%s\n' "$@" |
    sort -n |
    awk '{ v[NR] = $1 } END { if (NR % 2) print v[(NR + 1) / 2]; else print (v[NR / 2] + v[NR / 2 + 1]) / 2 }'
}

ratio_percent() {
  awk -v base="$1" -v head="$2" 'BEGIN { printf "%.1f", head * 100 / base }'
}

verdict() {
  awk -v base="$1" -v head="$2" -v limit="$threshold_percent" 'BEGIN { exit !(head * 100 >= base * limit) }'
}

load_now() {
  cut -d ' ' -f 1 /proc/loadavg
}

cleanup() {
  local path
  trap - EXIT
  for path in "${worktrees[@]}"; do
    git -C "$repo_root" worktree remove --force "$path" > /dev/null 2>&1 || true
  done
  git -C "$repo_root" worktree prune > /dev/null 2>&1 || true
  if [[ -n "$work" ]]; then
    rm -rf -- "${work:?}"
  fi
}

run_scenario() {
  local tree=$1
  shift
  local output
  if ! output=$(cd "$tree" && dotnet run --no-build --project "$perf_project" -c Release -- "$@" 2>&1); then
    echo "check-perf: the harness failed in $tree; output tail:" >&2
    tail -n 30 <<< "$output" >&2
    return 1
  fi
  echo "$output"
}

measure() {
  local label=$1
  local tree=$2
  local output ops events load
  output=$(run_scenario "$tree" command n=3000 warmup=1000 conc=1)
  load=$(load_now)
  if ! ops=$(parse_command_ops "$output"); then
    echo "check-perf: no 'create c=1 n=3000: <N> ops/s' number in the $label run; output tail:" >&2
    tail -n 30 <<< "$output" >&2
    return 1
  fi
  echo "  $label command ops/s $ops (load $load)" >&2
  command_samples[$label]+="$ops "

  output=$(run_scenario "$tree" queue-background n=2000 instances=1)
  load=$(load_now)
  if ! events=$(parse_queue_events "$output"); then
    echo "check-perf: no '(<N> events/s end to end)' number in the $label run; output tail:" >&2
    tail -n 30 <<< "$output" >&2
    return 1
  fi
  echo "  $label queue events/s $events (load $load)" >&2
  queue_samples[$label]+="$events "
}

main() {
  if [[ $# -ne 1 ]]; then
    echo "usage: check-perf.sh <base-ref>" >&2
    return 2
  fi
  if [[ -n ${PERF_PG-} ]]; then
    echo "check-perf: PERF_PG is set; the comparison would run on an external database, unset it" >&2
    return 1
  fi

  local base_ref=$1
  local base_commit head_commit round label start load median_base median_head ratio failed=0
  local scenario base_values head_values
  declare -gA command_samples=() queue_samples=()

  repo_root=$(git rev-parse --show-toplevel)
  base_commit=$(git -C "$repo_root" rev-parse --verify "$base_ref^{commit}")
  head_commit=$(git -C "$repo_root" rev-parse --verify "HEAD^{commit}")

  load=$(load_now)
  echo "check-perf: base $base_ref ($base_commit), head $head_commit; uncommitted changes are not measured"
  if awk -v current="$load" 'BEGIN { exit !(current >= 2) }'; then
    echo "check-perf: warning: the one-minute load is $load, the machine is not quiet; the numbers are noisy"
  fi

  work=$(mktemp -d)
  trap cleanup EXIT
  trap 'exit 130' INT
  trap 'exit 143' TERM

  worktrees=("$work/base" "$work/head")
  git -C "$repo_root" worktree add --detach "$work/base" "$base_commit" > /dev/null
  git -C "$repo_root" worktree add --detach "$work/head" "$head_commit" > /dev/null

  start=$SECONDS
  for label in base head; do
    echo "check-perf: building $label"
    (cd "$work/$label" && dotnet build "$perf_project" -c Release > /dev/null)
  done

  for ((round = 1; round <= rounds; round++)); do
    for label in base head; do
      echo "check-perf: round $round, $label"
      measure "$label" "$work/$label"
    done
  done

  for scenario in command queue; do
    if [[ $scenario == command ]]; then
      read -ra base_values <<< "${command_samples[base]}"
      read -ra head_values <<< "${command_samples[head]}"
    else
      read -ra base_values <<< "${queue_samples[base]}"
      read -ra head_values <<< "${queue_samples[head]}"
    fi
    median_base=$(median "${base_values[@]}")
    median_head=$(median "${head_values[@]}")
    ratio=$(ratio_percent "$median_base" "$median_head")
    echo "check-perf: $scenario: base median $median_base, head median $median_head, head is $ratio percent of base"
    if ! verdict "$median_base" "$median_head"; then
      echo "check-perf: $scenario: the head is below $threshold_percent percent of the base" >&2
      failed=1
    fi
  done

  echo "check-perf: took $((SECONDS - start)) s, load now $(load_now)"
  if [[ $failed -ne 0 ]]; then
    return 1
  fi
  echo "check-perf: ok"
}

if [[ ${BASH_SOURCE[0]} == "$0" ]]; then
  main "$@"
fi
