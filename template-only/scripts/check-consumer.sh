#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit nullglob

export LC_ALL=C GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1
export GIT_AUTHOR_NAME=consumer GIT_AUTHOR_EMAIL=consumer@example.com
export GIT_COMMITTER_NAME=consumer GIT_COMMITTER_EMAIL=consumer@example.com
unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE

template_name=ModularMonolith
template_database=modularmonolith
bumped_package=OpenTelemetry.Instrumentation.Runtime
bumped_version=1.17.0
extra_folder=src/Core/Extra
extra_project=$extra_folder/Tsikhanau.Monolith.Extra.csproj
extra_type=$extra_folder/ExtraMarker.cs
extra_assembly=Tsikhanau.Monolith.Extra.dll
hook_mark='core-hash: the staged core matches the staged core.lock'

main() {
  read_arguments "$@"
  template_root=$(git -C "$(dirname -- "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)
  work=$(mktemp -d)
  consumer=$work/$name
  mkdir "$work/logs"
  echo "check-consumer: $name, working folder $work"

  step 1 copy copy_template
  step 2 rename rename_app
  step 3 "core and name" check_core_and_name
  step 4 "init, hook, gate" init_commit_gate
  if [[ $update -eq 1 ]]; then
    step 5 update-core update_across_template_commits
  fi
  echo "check-consumer: $name is green ($((SECONDS - started)) s)"
}

usage() {
  echo "usage: template-only/scripts/check-consumer.sh [<name>] [--keep] [--no-update]" >&2
  exit 2
}

read_arguments() {
  name=""
  keep=0
  update=1
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --keep) keep=1 ;;
      --no-update) update=0 ;;
      -*) usage ;;
      *)
        [[ -z "$name" ]] || usage
        name=$1
        ;;
    esac
    shift
  done
  name=${name:-Acme.Shop}
  if [[ ! "$name" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*$ ]]; then
    echo "check-consumer: the name may hold only letters, digits, dots, hyphens and underscores: $name" >&2
    exit 2
  fi
  database=${name,,}
  database=${database//[.-]/_}
}

step() {
  local begun=$SECONDS
  current="step $1 ($2)"
  summary=""
  "$3"
  echo "check-consumer: $1 $2: $summary ($((SECONDS - begun)) s)"
}

fail() {
  echo "check-consumer: $current: $1" >&2
  exit 1
}

logged() {
  local log=$work/logs/$1.log
  shift
  if ! "$@" > "$log" 2>&1; then
    tail -n 40 "$log" | sed 's/^/  | /' >&2
    fail "$* failed; the whole output is in $log"
  fi
}

in_folder() {
  (cd "$1" && "${@:2}")
}

search() {
  local status=0
  git -C "$consumer" grep --no-index "$@" || status=$?
  if [[ $status -gt 1 ]]; then
    fail "git grep $* failed with exit code $status"
  fi
}

working_set() {
  comm -z -23 \
    <(git -C "$1" ls-files -z --cached --others --exclude-standard | sort -zu) \
    <(git -C "$1" ls-files -z --deleted | sort -zu)
}

copy_files() {
  tar -C "$template_root" --null -T "$1" -cf - | tar -C "$2" -xf -
}

copy_template() {
  local report count executable
  if git -C "$work" rev-parse --is-inside-work-tree > /dev/null 2>&1; then
    fail "$work is inside a git work tree; point TMPDIR to a folder outside every repository"
  fi
  if ! report=$(cd "$template_root" && bash scripts/core-hash.sh check 2>&1); then
    printf '%s\n' "$report" >&2
    fail "the core of the template does not match its core.lock; run template-only/scripts/lock-core.sh first"
  fi

  working_set "$template_root" | grep -z -v '^template-only/' > "$work/consumer-files" \
    || fail "the template has no files outside template-only/"
  mkdir "$consumer"
  copy_files "$work/consumer-files" "$consumer"
  count=$(tr -cd '\0' < "$work/consumer-files" | wc -c)
  executable=$(find "$consumer" -type f -perm /111 | wc -l)
  find "$consumer" -type f -perm /111 -exec chmod a-x -- {} +
  summary="$count files of the template's working set without template-only/ into $consumer;"
  summary+=" $executable executable bits dropped, as a template engine may drop them"
}

other_spellings() {
  search -I -n -o -i -E 'modular.?monolith' \
    | awk -F ':' -v a="$template_name" -v b="$template_database" '$NF != a && $NF != b'
  find "$consumer" -mindepth 1 -printf '%P\n' \
    | awk -v a="$template_name" '{ rest = $0; gsub(a, "", rest); if (tolower(rest) ~ /modular.?monolith/) print "path " $0 }'
}

rename_paths() {
  local path base
  renamed_paths=0
  find "$consumer" -depth -mindepth 1 -name "*$template_name*" -print0 > "$work/renamed-paths"
  while IFS= read -r -d '' path; do
    base=${path##*/}
    mv -- "$path" "${path%/*}/${base//"$template_name"/"$name"}"
    renamed_paths=$((renamed_paths + 1))
  done < "$work/renamed-paths"
}

rename_contents() {
  search -I -z -l -E "$template_name|$template_database" > "$work/renamed-files"
  (
    cd "$consumer"
    xargs -0 -r -a "$work/renamed-files" sed -i -e "s/$template_name/$name/g" -e "s/$template_database/$database/g"
  )
  renamed_files=$(tr -cd '\0' < "$work/renamed-files" | wc -c)
}

rename_app() {
  local others names databases
  others=$(other_spellings)
  if [[ -n "$others" ]]; then
    printf '%s\n' "$others" | sed 's/^/  /' >&2
    fail "the template spells the app name in a form other than $template_name and $template_database"
  fi
  names=$(search -I -o -F "$template_name" | wc -l)
  databases=$(search -I -o -F "$template_database" | wc -l)
  rename_paths
  rename_contents
  summary="no other spelling of the app name; $template_name -> $name in $renamed_paths paths and $names places,"
  summary+=" $template_database -> $database in $databases places, $renamed_files files changed"
}

probe_core() {
  if [[ ! -d "$work/probe" ]]; then
    git init -q "$work/probe"
  fi
  (cd "$consumer" && GIT_DIR=$work/probe/.git GIT_WORK_TREE=$consumer bash scripts/core-hash.sh "$1")
}

core_sums() {
  (cd "$1" && xargs -d '\n' -a "$work/template-core" -r sha256sum)
}

check_core_and_name() {
  local template_hash consumer_hash report leftovers solutions
  (cd "$template_root" && bash scripts/core-hash.sh list) > "$work/template-core"
  probe_core list > "$work/consumer-core"
  if ! diff "$work/template-core" "$work/consumer-core" > "$work/core-list.diff"; then
    sed 's/^/  /' "$work/core-list.diff" >&2
    fail "the core files of the consumer are not the template's (< template, > consumer)"
  fi
  core_sums "$template_root" > "$work/template-sums"
  core_sums "$consumer" > "$work/consumer-sums"
  if ! diff "$work/template-sums" "$work/consumer-sums" > "$work/core-sums.diff"; then
    sed 's/^/  /' "$work/core-sums.diff" >&2
    fail "core files of the consumer differ from the template's (< template, > consumer)"
  fi
  template_hash=$(cd "$template_root" && bash scripts/core-hash.sh print)
  consumer_hash=$(probe_core print)
  if [[ "$template_hash" != "$consumer_hash" ]]; then
    fail "core-hash.sh print gives $template_hash in the template and $consumer_hash in the consumer"
  fi
  if ! report=$(probe_core check 2>&1); then
    printf '%s\n' "$report" | sed 's/^/  /' >&2
    fail "core-hash.sh check fails in the consumer"
  fi

  leftovers=$(search -n -i "$template_database"; find "$consumer" -mindepth 1 -iname "*$template_database*" -printf 'path %P\n')
  if [[ -n "$leftovers" ]]; then
    printf '%s\n' "$leftovers" | sed 's/^/  /' >&2
    fail "the consumer still names the template app"
  fi
  solutions=("$consumer"/*.slnx)
  if [[ ${#solutions[@]} -ne 1 || "${solutions[0]##*/}" != "$name.slnx" ]]; then
    fail "the root must hold exactly $name.slnx, found: ${solutions[*]##*/}"
  fi
  solution=$name.slnx
  summary="$(wc -l < "$work/template-core") core files byte-identical to the template's, both $consumer_hash;"
  summary+=" core-hash.sh check passes; no $template_database in any letter case; root solution $solution"
}

init_problems() {
  local top branch hooks file
  top=$(git rev-parse --show-toplevel 2> /dev/null || true)
  [[ "$top" == "$(pwd -P)" ]] || echo "no git repository at the app root (top level: ${top:-none})"
  branch=$(git symbolic-ref --short HEAD 2> /dev/null || true)
  [[ "$branch" == main ]] || echo "the branch is ${branch:-none}, not main"
  hooks=$(git config --local --get core.hooksPath 2> /dev/null || true)
  [[ "$hooks" == .githooks ]] || echo "core.hooksPath is ${hooks:-not set}, not .githooks"
  for file in gate.sh scripts/*.sh .githooks/*; do
    [[ -x "$file" ]] || echo "$file is not executable"
  done
}

init_state() {
  find . -printf '%m %y %s %p\n' | sort
  if [[ -f .git/config ]]; then
    cat .git/config
  fi
}

commit_through_hook() {
  logged "$1" git commit -m "$2"
  if ! grep -qF "$hook_mark" "$work/logs/$1.log"; then
    fail "the commit did not go through the pre-commit hook; see $work/logs/$1.log"
  fi
  committed=$(git rev-parse --short HEAD)
}

run_gate() {
  local log=$work/logs/$1.log warnings counts
  logged "$1" ./gate.sh
  grep -q '^gate: green' "$log" || fail "the gate did not end green; see $log"
  warnings=$(sed -n -E 's/^ +([0-9]+) Warning\(s\)$/\1/p' "$log" | paste -sd '+')
  counts=$(sed -n -E 's/^  (total|failed|succeeded|skipped): ([0-9]+)$/\1 \2/p' "$log" | paste -sd ',' | sed 's/,/, /g')
  gate_totals="$counts, warnings ${warnings:-none}"
}

init_commit_gate() {
  local problems
  cd "$consumer"
  logged 4-init bash scripts/init.sh
  problems=$(init_problems)
  if [[ -n "$problems" ]]; then
    printf '%s\n' "$problems" | sed 's/^/  /' >&2
    fail "init did not set up the app"
  fi
  init_state > "$work/init-first"
  logged 4-init-again bash scripts/init.sh
  init_state > "$work/init-second"
  if ! diff "$work/init-first" "$work/init-second" > "$work/init.diff"; then
    sed 's/^/  /' "$work/init.diff" >&2
    fail "a second run of init changed the app"
  fi

  git add -A
  commit_through_hook 4-commit initial
  run_gate 4-gate
  restored_before=$(restored_versions)
  summary="init: $(paste -sd ';' "$work/logs/4-init.log" | sed 's/;init: /; /g; s/^init: //');"
  summary+=" second run changed nothing; commit $committed through the pre-commit hook; gate green: $gate_totals"
}

restored_versions() {
  find src -path '*/obj/project.assets.json' -print0 \
    | xargs -0 -r grep -h -o "\"$bumped_package/[^\"]*\"" \
    | sed -E 's|.*/([^"]*)"$|\1|' \
    | sort -u \
    | paste -sd ' '
}

make_template_a() {
  local hash
  template=$work/template
  mkdir "$template"
  working_set "$template_root" > "$work/template-files"
  copy_files "$work/template-files" "$template"
  git -C "$template" init -q -b main
  git -C "$template" add -A
  git -C "$template" commit -q -m A
  a_commit=$(git -C "$template" rev-parse HEAD)
  hash=$(cd "$template" && bash scripts/core-hash.sh print)
  if [[ "$hash" != "$(bash scripts/core-hash.sh print)" ]]; then
    fail "the core of the template changed during the run: $hash in the new template repository"
  fi
}

point_lock_at_template() {
  printf 'source=%s\ncommit=%s\n' "$template" "$a_commit" >> core.lock
  git add core.lock
  commit_through_hook 5-lock "take the core from the template copy"
}

insert_extra_folder() {
  awk -v name="/$extra_folder/" -v project="$extra_project" '
    !done && match($0, /<Folder Name="[^"]*"/) && substr($0, RSTART + 14, RLENGTH - 15) > name {
      cr = /\r$/ ? "\r" : ""
      printf "  <Folder Name=\"%s\">%s\n    <Project Path=\"%s\" />%s\n  </Folder>%s\n", name, cr, project, cr, cr
      done = 1
    }
    { print }
    END { exit done ? 0 : 1 }
  ' "$1" > "$work/solution.tmp" || fail "no folder after /$extra_folder/ in $1"
  cat "$work/solution.tmp" > "$1"
}

make_template_b() {
  local packages=src/Core/Core.Packages.props pattern solutions
  pattern="<PackageVersion Include=\"${bumped_package//./\\.}\" Version=\""
  old_version=$(sed -n -E "s/.*$pattern([^\"]*)\".*/\1/p" "$template/$packages")
  if [[ -z "$old_version" || "$old_version" == "$bumped_version" ]]; then
    fail "$packages must hold $bumped_package in a version other than $bumped_version; choose another package"
  fi
  sed -i -E "s/($pattern)[^\"]*\"/\1$bumped_version\"/" "$template/$packages"
  mkdir "$template/$extra_folder"
  printf '<Project Sdk="Microsoft.NET.Sdk">\n</Project>\n' > "$template/$extra_project"
  printf 'namespace Tsikhanau.Monolith.Extra;\n\npublic static class ExtraMarker\n{\n}\n' > "$template/$extra_type"
  solutions=("$template"/*.slnx)
  [[ ${#solutions[@]} -eq 1 ]] || fail "the template repository must hold one root *.slnx"
  insert_extra_folder "${solutions[0]}"
  logged 5-lock-core in_folder "$template" bash template-only/scripts/lock-core.sh
  git -C "$template" add -A
  git -C "$template" commit -q -m B
  b_commit=$(git -C "$template" rev-parse HEAD)
  b_version=$(< "$template/src/Core/VERSION")
}

printed_paths() {
  sed -n -E 's/^ (.*[^ ]) +\| +[0-9]+ .*$/\1/p' "$work/logs/5-update.log" | sort
}

changed_lines() {
  git diff --cached -U0 -- "$1" | grep -E '^[-+]' | grep -v -E '^(---|\+\+\+) ' || true
}

expect_lines() {
  local actual
  actual=$(changed_lines "$1")
  if [[ "$actual" != "$2" ]]; then
    printf 'expected:\n%s\nactual:\n%s\n' "$2" "$actual" | sed 's/^/  /' >&2
    fail "update-core changed $1 in another way than the template commit"
  fi
}

check_update() {
  local expected old_line new_line
  expected=$(printf '%s\n' "$solution" core.lock src/Core/Core.Packages.props "$extra_type" "$extra_project" src/Core/VERSION | sort)
  if [[ "$(printed_paths)" != "$expected" ]]; then
    printf 'expected:\n%s\nprinted:\n%s\n' "$expected" "$(printed_paths)" | sed 's/^/  /' >&2
    fail "the diff that update-core printed does not name exactly the changes of the template commit"
  fi
  if [[ "$(git diff --cached --name-only | sort)" != "$expected" ]]; then
    fail "update-core staged other files than the template commit changed: $(git diff --cached --name-only | paste -sd ' ')"
  fi
  old_line=$(git -C "$template" show "$a_commit:src/Core/Core.Packages.props" | grep -F "\"$bumped_package\"")
  new_line=$(git -C "$template" show "$b_commit:src/Core/Core.Packages.props" | grep -F "\"$bumped_package\"")
  expect_lines src/Core/Core.Packages.props "-$old_line"$'\n'"+$new_line"
  expect_lines "$solution" "+  <Folder Name=\"/$extra_folder/\">"$'\n'"+    <Project Path=\"$extra_project\" />"$'\n'"+  </Folder>"
  expect_lines src/Core/VERSION "-$(git show HEAD:src/Core/VERSION)"$'\n'"+$b_version"
  cmp -s "$template/$extra_project" "$extra_project" || fail "$extra_project is not the template's"
  cmp -s "$template/$extra_type" "$extra_type" || fail "$extra_type is not the template's"
  printf 'version=%s\nhash=%s\nsource=%s\ncommit=%s\n' "$b_version" "$(cd "$template" && bash scripts/core-hash.sh print)" \
    "$template" "$b_commit" > "$work/lock-expected"
  cmp -s "$work/lock-expected" core.lock || fail "core.lock is not version, hash, source and commit of the template commit"
}

update_across_template_commits() {
  local restored_after built
  make_template_a
  point_lock_at_template
  make_template_b
  logged 5-update bash scripts/update-core.sh "$b_commit"
  check_update
  commit_through_hook 5-commit "update the core"
  run_gate 5-gate
  built=$(find "$extra_folder/bin/Release" -name "$extra_assembly" -printf '%p\n' 2> /dev/null || true)
  [[ -n "$built" ]] || fail "the gate did not build $extra_assembly in $extra_folder/bin/Release"
  restored_after=$(restored_versions)
  if [[ "$restored_before" != "$old_version" || "$restored_after" != "$bumped_version" ]]; then
    fail "the restore did not move $bumped_package from $old_version to $bumped_version: $restored_before, then $restored_after"
  fi
  summary="template commit ${a_commit:0:7} -> ${b_commit:0:7} ($bumped_package $old_version -> $bumped_version, new $extra_project,"
  summary+=" core version $(git show HEAD~1:src/Core/VERSION) -> $b_version); update-core printed and staged exactly"
  summary+=" $(printed_paths | paste -sd ' '); commit $committed through the pre-commit hook; gate green: $gate_totals;"
  summary+=" built $built; obj/project.assets.json: $bumped_package $restored_before -> $restored_after"
}

finish() {
  local status=$?
  trap - EXIT
  if [[ -z "$work" ]]; then
    exit "$status"
  fi
  cd /
  if [[ $status -ne 0 ]]; then
    echo "check-consumer: ${current:-start} stopped with exit code $status; kept $work" >&2
  elif [[ $keep -eq 1 ]]; then
    echo "check-consumer: kept $work"
  else
    rm -rf -- "$work"
  fi
  exit "$status"
}

started=$SECONDS
current=""
keep=0
work=""
trap finish EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
main "$@"
