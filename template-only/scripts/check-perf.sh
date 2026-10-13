#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit

readonly perf_project=template-only/perf/Tsikhanau.Monolith.Perf
readonly rounds=3
readonly threshold_percent=90
readonly spread_limit_percent=10

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

min_of() {
  printf '%s\n' "$@" | sort -n | head -n 1
}

max_of() {
  printf '%s\n' "$@" | sort -n | tail -n 1
}

spread_percent() {
  awk -v low="$1" -v high="$2" 'BEGIN { printf "%.1f", (high / low - 1) * 100 }'
}

noisy() {
  awk -v low="$1" -v high="$2" -v limit="$spread_limit_percent" 'BEGIN { exit !(high * 100 > low * (100 + limit)) }'
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
  local record=$3
  local output ops events load
  output=$(run_scenario "$tree" command n=3000 warmup=1000 conc=1)
  load=$(load_now)
  if ! ops=$(parse_command_ops "$output"); then
    echo "check-perf: no 'create c=1 n=3000: <N> ops/s' number in the $label run; output tail:" >&2
    tail -n 30 <<< "$output" >&2
    return 1
  fi
  if [[ $record == discard ]]; then
    echo "  $label command ops/s $ops (load $load, warm-up, discarded)" >&2
  else
    echo "  $label command ops/s $ops (load $load)" >&2
    command_samples[$label]+="$ops "
  fi

  output=$(run_scenario "$tree" queue-background n=2000 instances=1)
  load=$(load_now)
  if ! events=$(parse_queue_events "$output"); then
    echo "check-perf: no '(<N> events/s end to end)' number in the $label run; output tail:" >&2
    tail -n 30 <<< "$output" >&2
    return 1
  fi
  if [[ $record == discard ]]; then
    echo "  $label queue events/s $events (load $load, warm-up, discarded)" >&2
  else
    echo "  $label queue events/s $events (load $load)" >&2
    queue_samples[$label]+="$events "
  fi
}

judge() {
  local scenario=$1
  local -a base_values head_values
  local median_base median_head ratio side low high spread
  local -A lows highs
  read -ra base_values <<< "$2"
  read -ra head_values <<< "$3"
  median_base=$(median "${base_values[@]}")
  median_head=$(median "${head_values[@]}")
  ratio=$(ratio_percent "$median_base" "$median_head")
  lows[base]=$(min_of "${base_values[@]}")
  highs[base]=$(max_of "${base_values[@]}")
  lows[head]=$(min_of "${head_values[@]}")
  highs[head]=$(max_of "${head_values[@]}")
  echo "check-perf: $scenario: base median $median_base (min ${lows[base]}, max ${highs[base]})"
  echo "check-perf: $scenario: head median $median_head (min ${lows[head]}, max ${highs[head]})"
  echo "check-perf: $scenario: head is $ratio percent of base"
  for side in base head; do
    low=${lows[$side]}
    high=${highs[$side]}
    if noisy "$low" "$high"; then
      spread=$(spread_percent "$low" "$high")
      echo "check-perf: inconclusive: $scenario $side spread ${spread}%; rerun on a quieter machine" >&2
      return 3
    fi
  done
  if ! verdict "$median_base" "$median_head"; then
    echo "check-perf: $scenario: the head is below $threshold_percent percent of the base" >&2
    return 1
  fi
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
  local base_commit head_commit round label start load output scenario failed=0 inconclusive=0 code=0
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
    if ! output=$(cd "$work/$label" && dotnet build "$perf_project" -c Release 2>&1); then
      echo "check-perf: the $label build failed; output tail:" >&2
      tail -n 30 <<< "$output" >&2
      return 1
    fi
  done

  for label in base head; do
    echo "check-perf: warm-up, $label"
    measure "$label" "$work/$label" discard
  done

  for ((round = 1; round <= rounds; round++)); do
    for label in base head; do
      echo "check-perf: round $round, $label"
      measure "$label" "$work/$label" keep
    done
  done

  for scenario in command queue; do
    if [[ $scenario == command ]]; then
      judge "$scenario" "${command_samples[base]}" "${command_samples[head]}" || code=$?
    else
      judge "$scenario" "${queue_samples[base]}" "${queue_samples[head]}" || code=$?
    fi
    if [[ $code -eq 1 ]]; then
      failed=1
    elif [[ $code -eq 3 ]]; then
      inconclusive=1
    fi
    code=0
  done

  echo "check-perf: took $((SECONDS - start)) s, load now $(load_now)"
  if [[ $failed -ne 0 ]]; then
    return 1
  fi
  if [[ $inconclusive -ne 0 ]]; then
    return 3
  fi
  echo "check-perf: ok"
}

if [[ ${BASH_SOURCE[0]} == "$0" ]]; then
  main "$@"
fi
