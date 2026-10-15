#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit nullglob

while IFS= read -r variable; do
  unset "$variable"
done < <(compgen -e | grep '^GIT_')
export LC_ALL=C GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1
export GIT_AUTHOR_NAME=upgrade GIT_AUTHOR_EMAIL=upgrade@example.com
export GIT_COMMITTER_NAME=upgrade GIT_COMMITTER_EMAIL=upgrade@example.com
unset App__Database__LoginKey App__Database__PrepareMode ConnectionStrings__database

short_name=tsikhanau-monolith
post_action_not_run=104
name=Upgrade
old_commit=5946c8ae62a0379de50e963c986f0c4824d90979
old_version=77
generated_lines=('localhost:[0-9]' '<UserSecretsId>')
noted_files=(
  src/Bootstrapper/$name.Bootstrapper/Program.cs
  src/Bootstrapper/$name.Bootstrapper/appsettings.json
  src/Aspire/$name.AppHost/AppHost.cs
  src/Aspire/$name.AppHost/LoginKeyDefault.cs
  src/Tests/IntegrationTests/Host/HostProcessTests.cs
)
host=src/Bootstrapper/$name.Bootstrapper
closed_port_connection="Host=127.0.0.1;Port=1;Database=${name,,};Username=postgres;Password=postgres;SSL Mode=Disable"
login_key_refusal='App:Database:LoginKey is required'
notes='the upgrade notes in docs/engineering-principles.md, section 5.6'
skips=56
patience=120

main() {
  template_root=$(git -C "$(dirname -- "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)
  work=$(mktemp -d)
  mkdir "$work/logs"
  app=$work/old/$name
  echo "check-upgrade: working folder $work"

  step 1 "app of core $old_version" make_old_app
  step 2 update-core update_core
  step 3 "start without the notes" start_without_notes
  step 4 "edits of the notes" follow_notes
  step 5 gate run_gate
  echo "check-upgrade: green ($((SECONDS - started)) s)"
}

step() {
  local begun=$SECONDS
  current="step $1 ($2)"
  summary=""
  "$3"
  echo "check-upgrade: $1 $2: $summary ($((SECONDS - begun)) s)"
}

fail() {
  echo "check-upgrade: $current: $1" >&2
  exit 1
}

fail_with_log() {
  tail -n 40 "$1" | sed 's/^/  | /' >&2
  fail "$2; the whole output is in $1"
}

logged() {
  local log=$work/logs/$1.log
  shift
  "$@" > "$log" 2>&1 || fail_with_log "$log" "$* failed"
}

new_without_scripts() {
  local status=0
  dotnet new "$short_name" --allow-scripts no "$@" || status=$?
  [[ $status -eq 0 || $status -eq $post_action_not_run ]]
}

working_set() {
  comm -z -23 \
    <(git -C "$template_root" ls-files -z --cached --others --exclude-standard | sort -zu) \
    <(git -C "$template_root" ls-files -z --deleted | sort -zu)
}

lock_version() {
  sed -n 's/^version=//p' "$1/core.lock"
}

install_and_create() {
  logged "$1-install" dotnet new install "$2" --debug:custom-hive "$work/hive-$1"
  logged "$1-new" new_without_scripts -n "$name" -o "$3" --debug:custom-hive "$work/hive-$1"
}

make_old_app() {
  git -C "$template_root" cat-file -e "$old_commit^{commit}" 2> /dev/null \
    || fail "the template history lacks $old_commit, the last commit of core $old_version; fetch the whole history"
  mkdir "$work/old-template"
  git -C "$template_root" archive "$old_commit" | tar -x -C "$work/old-template"
  install_and_create old "$work/old-template" "$app"
  [[ $(lock_version "$app") == "$old_version" ]] || fail "the app made from $old_commit is not core $old_version"
  git -C "$app" init -q -b main
  git -C "$app" add -A
  git -C "$app" commit -q -m "app of core $old_version"
  summary="$name made from template commit ${old_commit:0:7}, core $old_version, committed"
}

update_core() {
  local new_version
  mkdir "$work/template"
  working_set | tar -C "$template_root" --null -T - -cf - | tar -C "$work/template" -xf -
  git -C "$work/template" init -q -b main
  git -C "$work/template" add -A
  git -C "$work/template" commit -q -m template
  new_version=$(lock_version "$work/template")
  (cd "$app" && logged 2-update bash scripts/update-core.sh main --from "$work/template")
  [[ $(lock_version "$app") == "$new_version" ]] || fail_with_log "$work/logs/2-update.log" \
    "update-core left the app at core $(lock_version "$app"), not $new_version"
  git -C "$app" commit -q -m "take core $new_version"
  summary="update-core moved $name from core $old_version to core $new_version, the working set of the template"
}

start_without_notes() {
  local log=$work/logs/3-start.log status=0
  logged 3-build dotnet build "$app/$host" -c Release
  (ulimit -c 0 && cd "$app/$host" && ConnectionStrings__database=$closed_port_connection \
    ASPNETCORE_URLS=http://127.0.0.1:0 timeout "$patience" dotnet "bin/Release/net10.0/$name.Bootstrapper.dll"
    exit $?) > "$log" 2>&1 || status=$?
  [[ $status -ne 0 && $status -ne 124 ]] || fail_with_log "$log" "the old app did not stop at its start (exit $status)"
  grep -qF "$login_key_refusal" "$log" || fail_with_log "$log" "the start did not refuse the missing login key"
  grep -qF "$notes" "$log" || fail_with_log "$log" "the login key refusal does not name $notes"
  summary="the old Program.cs builds on the new core; its start stops with exit code $status:"
  summary+=" \"$login_key_refusal\", naming $notes"
}

follow_notes() {
  local file differences
  install_and_create new "$work/template" "$work/new/$name"
  for file in "${noted_files[@]}"; do
    mkdir -p "$(dirname "$app/$file")"
    cp "$work/new/$name/$file" "$app/$file"
  done
  differences=$work/logs/4-differences.diff
  if ! diff -r -x bin -x obj -x .git -x core.lock "${generated_lines[@]/#/-I}" "$app" "$work/new/$name" \
    > "$differences"; then
    fail_with_log "$differences" "after the edits of the notes the app still differs from a new app of the template"
  fi
  git -C "$app" add -A
  git -C "$app" commit -q -m "follow the upgrade notes"
  summary="${#noted_files[@]} files edited as the notes say; the app now equals a new app of the template"
  summary+=" but for core.lock, the generated ports and UserSecretsId"
}

run_gate() {
  local log=$work/logs/5-gate.log counts
  (cd "$app" && logged 5-gate env CI=true bash gate.sh)
  grep -qxF "gate: green, skipped tests as recorded: $name.slnx $skips" "$log" \
    || fail_with_log "$log" "the gate of the upgraded app did not end green with $skips skipped tests"
  counts=$(sed -n -E 's/^  (total|failed|skipped): ([0-9]+)$/\1 \2/p' "$log" | paste -sd ',' | sed 's/,/, /g')
  summary="the real build and every test of the app: $counts; gate green"
}

finish() {
  local status=$?
  trap - EXIT
  if [[ -z "$work" ]]; then
    exit "$status"
  fi
  if [[ $status -ne 0 ]]; then
    echo "check-upgrade: ${current:-start} stopped with exit code $status; kept $work" >&2
  else
    rm -rf -- "$work"
  fi
  exit "$status"
}

started=$SECONDS
current=""
work=""
trap finish EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
main "$@"
