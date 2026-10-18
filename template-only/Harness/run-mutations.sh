#!/usr/bin/env bash
set -euo pipefail
shopt -s nullglob inherit_errexit

readonly mutation_count=55
readonly item_count=56
readonly item_twelve_mutation=m14_integration_event_carries_domain_type
readonly harness_folder=template-only/Harness
readonly harness_project=$harness_folder/Tsikhanau.Monolith.Harness.csproj
readonly template_solution=template-only/Template.slnx
readonly test_projects=(src/Tests/ArchTests template-only/ArchRules.Tests src/Tests/IntegrationTests)

source_repository=
source_before=
work=
repository=
results=
harness_dll=
solutions=()
declare -A expectations=()

usage() {
  echo "usage: run-mutations.sh [--dry-run] <source repository> <new or empty work directory outside it>" >&2
}

abort() {
  echo "run-mutations: $*" >&2
  exit 1
}

clear_git_routing() {
  local names variables
  names=$(git rev-parse --local-env-vars)
  mapfile -t variables <<< "$names"
  unset -- "${variables[@]}" GIT_CEILING_DIRECTORIES
  mapfile -t variables < <(compgen -v GIT_CONFIG || true)
  if [[ ${#variables[@]} -gt 0 ]]; then
    unset -- "${variables[@]}"
  fi
  export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1 GIT_ATTR_NOSYSTEM=1
  export GIT_CONFIG_COUNT=2 GIT_CONFIG_KEY_0=maintenance.auto GIT_CONFIG_VALUE_0=false
  export GIT_CONFIG_KEY_1=gc.auto GIT_CONFIG_VALUE_1=0
}

working_set() {
  comm -z -23 \
    <(git -C "$1" ls-files -z --cached --others --exclude-standard | sort -zu) \
    <(git -C "$1" ls-files -z --deleted | sort -zu)
}

source_state() {
  local index files
  echo "HEAD $(git -C "$source_repository" rev-parse --verify -q HEAD || echo none)"
  index=$(git -C "$source_repository" rev-parse --path-format=absolute --git-path index)
  if [[ -f $index ]]; then
    echo "index $(sha256sum < "$index")"
  else
    echo "index none"
  fi
  files=$(cd "$source_repository" && working_set . | xargs -0 -r sha256sum --)
  echo "files $(sha256sum <<< "$files")"
}

check_source_unchanged() {
  local status=$?
  trap - EXIT
  if [[ "$(source_state)" != "$source_before" ]]; then
    echo "run-mutations: the source repository changed during the run (HEAD, index or working set files):" \
      "$source_repository" >&2
    exit 1
  fi
  echo "run-mutations: the source repository is unchanged (HEAD, index and working set files)"
  exit "$status"
}

in_copy() {
  git -C "$repository" \
    -c user.name=harness \
    -c user.email=harness@localhost \
    -c commit.gpgsign=false \
    -c core.hooksPath=/dev/null \
    -c core.excludesFile=/dev/null \
    -c core.attributesFile=/dev/null \
    "$@"
}

copy_working_set() {
  local list=$work/working-set
  working_set "$source_repository" > "$list"
  mkdir -p "$repository"
  tar -C "$source_repository" --null -T "$list" -cf - | tar -C "$repository" -xf -
  in_copy init -q --template=
  in_copy add -A
  in_copy commit -q -m "working set of $source_repository"
  echo "run-mutations: copied the working set of $source_repository ($(tr -cd '\0' < "$list" | wc -c) files)"
}

check_inventory() {
  local listed expected duplicates only_listed only_expected
  listed=$(sort "$repository/$harness_folder/mutations54.txt")
  expected=$(cut -d ' ' -f 1 "$repository/$harness_folder/expected.txt" | sort)
  duplicates=$({ uniq -d <<< "$listed"; uniq -d <<< "$expected"; } | sort -u | tr '\n' ' ')
  only_listed=$(comm -23 <(sort -u <<< "$listed") <(sort -u <<< "$expected") | tr '\n' ' ')
  only_expected=$(comm -13 <(sort -u <<< "$listed") <(sort -u <<< "$expected") | tr '\n' ' ')
  if [[ -n $duplicates || -n $only_listed || -n $only_expected ]]; then
    abort "mutations54.txt and expected.txt must name the same mutations once each;" \
      "only in mutations54.txt: ${only_listed:-none}; only in expected.txt: ${only_expected:-none};" \
      "named twice: ${duplicates:-none}"
  fi
  if [[ $(wc -l <<< "$listed") -ne $mutation_count ]]; then
    abort "mutations54.txt names $(wc -l <<< "$listed") mutations, not $mutation_count"
  fi
}

read_expectations() {
  local name expectation
  while read -r name expectation; do
    expectations[$name]=$expectation
  done < "$repository/$harness_folder/expected.txt"
}

apply_scratch_commits() {
  local patches=("$repository/$harness_folder"/scratch-commits/000[12367]-*.patch)
  if [[ ${#patches[@]} -ne 5 ]]; then
    abort "expected the 5 scratch commits $harness_folder/scratch-commits/000[12367]-*.patch, found ${#patches[@]}"
  fi
  in_copy am -q "${patches[@]}"
}

logged() {
  local log=$results/$1.log
  shift
  if ! (cd "$repository" && "$@") > "$log" 2>&1 < /dev/null; then
    tail -n 30 "$log" >&2
    abort "$* failed; the whole output is in $log"
  fi
}

build_harness() {
  logged harness-build dotnet build "$harness_project" -nologo -v q
  harness_dll=$(cd "$repository" && dotnet msbuild "$harness_project" -getProperty:TargetPath -nologo < /dev/null)
  logged tool-restore dotnet tool restore
}

find_solutions() {
  local root_solutions=("$repository"/*.slnx)
  if [[ ${#root_solutions[@]} -ne 1 ]]; then
    abort "the root must hold exactly one *.slnx, found ${#root_solutions[@]}"
  fi
  solutions=("$(basename "${root_solutions[0]}")" "$template_solution")
}

apply_mutation() {
  local mutation=$1
  (cd "$repository" && dotnet "$harness_dll" "$mutation") > "$results/$mutation.mutate" 2>&1 < /dev/null &&
    grep -qx "applied $mutation" "$results/$mutation.mutate" &&
    in_copy status --porcelain > "$results/$mutation.status" &&
    [[ -s "$results/$mutation.status" ]]
}

revert_mutation() {
  in_copy checkout -q -- .
  in_copy clean -fdq
  if [[ -n "$(in_copy status --porcelain)" ]]; then
    in_copy status --porcelain >&2
    abort "reverting $1 left changes behind"
  fi
}

run_checks() {
  local out=$results/$1
  local solution project stage log skipped status
  : > "$out.build"
  : > "$out.skips"
  for solution in "${solutions[@]}"; do
    if ! (cd "$repository" && dotnet build "$solution" -nologo -v q) >> "$out.build" 2>&1 < /dev/null; then
      grep -E ': error ' "$out.build" | sed -E "s# \[/.*##; s#${repository}/##" | sort -u > "$out" || true
      if [[ -s $out ]]; then
        echo "CAUGHT build" >> "$out"
      else
        echo "ERROR build of $solution failed without a compiler error" > "$out"
      fi
      return
    fi
  done
  for project in "${test_projects[@]}"; do
    stage=$(basename "$project")
    log=$out.$stage
    status=0
    (cd "$repository" && dotnet test --project "$project" --no-build --no-ansi) > "$log" 2>&1 < /dev/null || status=$?
    skipped=$(sed -nE 's/^[[:space:]]*skipped:[[:space:]]*([0-9]+)[[:space:]]*$/\1/p' "$log")
    if [[ ! $skipped =~ ^[0-9]+$ ]]; then
      echo "ERROR $stage printed no single skipped count" > "$out"
      return
    fi
    echo "$stage $skipped" >> "$out.skips"
    if [[ $status -ne 0 ]]; then
      grep -E '^failed ' "$log" | sed -E 's/ \([0-9][^()]*\)$//' > "$out" || true
      if [[ -s $out ]]; then
        echo "CAUGHT $stage" >> "$out"
      else
        echo "ERROR $stage exited with $status without a failed test" > "$out"
      fi
      return
    fi
  done
  echo MISSED > "$out"
}

moved_skips() {
  local mutation=$1
  local rule=$2
  local stage skipped baseline
  while read -r stage skipped; do
    baseline=$(awk -v stage="$stage" '$1 == stage { print $2 }' "$results/baseline.skips")
    if [[ $skipped != "$baseline" ]] && [[ $rule == equal || $skipped -gt ${baseline:-0} ]]; then
      echo "$stage skipped $skipped, baseline ${baseline:-none}"
      return
    fi
  done < "$results/$mutation.skips"
}

evidence() {
  local out=$results/$1
  if [[ $2 == build ]]; then
    grep -oE ': error [A-Za-z]+[0-9]+' "$out" | sed 's/: error //' | sort -u | tr '\n' ' ' | sed 's/ $//'
  else
    grep -E '^failed ' "$out" | head -n 1 | sed 's/^failed //'
  fi
}

found_expected() {
  local out=$results/$1
  local stage=$2
  local kind=$3
  local token=$4
  case $kind in
    error) [[ $stage == build ]] && grep -qF ": error $token:" "$out" ;;
    failed) [[ $stage != build ]] && grep -qxF "failed $token" "$out" ;;
    output) [[ $stage != build ]] && grep -qF -- "$token" "$out.$stage" ;;
    *) return 1 ;;
  esac
}

judge() {
  local mutation=$1
  local kind token outcome stage moved
  read -r kind token <<< "$2"
  outcome=$(tail -n 1 "$results/$mutation")
  if [[ $outcome == ERROR* ]]; then
    echo "$outcome"
    return
  fi
  if [[ $kind == MISSED ]]; then
    if [[ $outcome != MISSED ]]; then
      stage=${outcome#CAUGHT }
      echo "DEVIATION caught by $stage, expected MISSED: $(evidence "$mutation" "$stage")"
      return
    fi
    moved=$(moved_skips "$mutation" equal)
  elif [[ $outcome == MISSED ]]; then
    echo "DEVIATION missed, expected $kind $token"
    return
  else
    stage=${outcome#CAUGHT }
    if ! found_expected "$mutation" "$stage" "$kind" "$token"; then
      echo "DEVIATION expected $kind $token, caught by $stage: $(evidence "$mutation" "$stage")"
      return
    fi
    moved=$(moved_skips "$mutation" no-extra)
  fi
  if [[ -n $moved ]]; then
    echo "ERROR skips moved: $moved"
  elif [[ $kind == MISSED ]]; then
    echo MISSED
  else
    echo "CAUGHT $stage $token"
  fi
}

run_dry() {
  local mutation outcome applied=0
  while read -r mutation <&3; do
    if apply_mutation "$mutation"; then
      outcome="APPLIED $(wc -l < "$results/$mutation.status") paths changed"
      applied=$((applied + 1))
    else
      outcome="ERROR mutation did not apply"
    fi
    revert_mutation "$mutation"
    echo "$mutation $outcome" | tee -a "$results/dry-run.txt"
  done 3< "$repository/$harness_folder/mutations54.txt"
  echo "applied $applied of $mutation_count mutations; built only the harness and, for m25 and m25b, Notes" |
    tee -a "$results/dry-run.txt"
  if [[ $applied -ne $mutation_count ]]; then
    abort "some mutations did not apply; their output is in $results/<mutation>.mutate"
  fi
}

run_full() {
  local mutation verdict caught matched
  find_solutions
  run_checks baseline
  if [[ "$(tail -n 1 "$results/baseline")" != MISSED ]]; then
    cat "$results/baseline" >&2
    abort "the baseline is not green; fix the scratch commits first"
  fi
  echo "run-mutations: baseline green, skipped per test project: $(tr '\n' ' ' < "$results/baseline.skips")"
  while read -r mutation <&3; do
    if apply_mutation "$mutation"; then
      run_checks "$mutation"
      verdict=$(judge "$mutation" "${expectations[$mutation]}")
    else
      verdict="ERROR mutation did not apply"
    fi
    revert_mutation "$mutation"
    echo "$mutation $verdict" | tee -a "$results/summary.txt"
  done 3< "$repository/$harness_folder/mutations54.txt"
  caught=$(grep -c '^[^ ]* CAUGHT ' "$results/summary.txt" || true)
  if grep -q "^$item_twelve_mutation CAUGHT " "$results/summary.txt"; then
    caught=$((caught + 1))
  fi
  matched=$(grep -cE '^[^ ]* (CAUGHT|MISSED)' "$results/summary.txt" || true)
  echo "caught $caught of $item_count (m14's catch also counts item 12, internal types of another module);" \
    "$matched of $mutation_count mutations as expected" | tee -a "$results/summary.txt"
  if [[ $matched -ne $mutation_count ]]; then
    abort "some mutations deviate from expected.txt or hit an error; see the DEVIATION and ERROR lines above"
  fi
}

main() {
  local dry_run=false start=$SECONDS target
  clear_git_routing
  if [[ ${1-} == --dry-run ]]; then
    dry_run=true
    shift
  fi
  if [[ $# -ne 2 ]]; then
    usage
    return 2
  fi
  source_repository=$(git -C "$1" rev-parse --show-toplevel)
  source_repository=$(cd "$source_repository" && pwd -P)
  target=$(realpath -m "$2")
  if [[ $target == "$source_repository" || $target == "$source_repository"/* ]]; then
    echo "run-mutations: the work directory $target is inside the source repository; give one outside it" >&2
    return 2
  fi
  if [[ -e $target ]] && [[ -n $(ls -A "$target") ]]; then
    echo "run-mutations: the work directory $target exists and is not empty; give a new or empty one" >&2
    return 2
  fi
  source_before=$(source_state)
  trap check_source_unchanged EXIT
  work=$target
  repository=$work/repo
  results=$work/results
  mkdir -p "$results"
  copy_working_set
  check_inventory
  read_expectations
  apply_scratch_commits
  build_harness
  if $dry_run; then
    run_dry
  else
    run_full
  fi
  echo "run-mutations: took $((SECONDS - start)) s; results in $results"
}

if [[ ${BASH_SOURCE[0]} == "$0" ]]; then
  main "$@"
fi
