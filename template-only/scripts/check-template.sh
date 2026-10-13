#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit nullglob

while IFS= read -r variable; do
  unset "$variable"
done < <(compgen -e | grep '^GIT_')
export LC_ALL=C GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1

short_name=tsikhanau-monolith
post_action_not_run=104
refused_name=1Shop
generated_lines=('localhost:[0-9]' '<UserSecretsId>')
postgres_image=postgres:17-alpine
postgres_password=check-template
patience=300
settle=3

main() {
  read_arguments "$@"
  scripts=$(cd "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
  template_root=$(git -C "$scripts" rev-parse --show-toplevel)
  work=$(mktemp -d)
  mkdir "$work/logs"
  echo "check-template: working folder $work"

  step 1 "two installs" compare_installs
  step 2 "refused name" refuse_name
  step 3 Tripown consumer Tripown --module Notes --keep
  step 4 trip-own consumer trip-own --module Notes --keep --no-update
  step 5 "Acme.Shop, the part 3 path" scaffold_path
  current="distinct ports before step 6"
  bash "$scripts/distinct-ports.sh" "${apps[@]}"
  step 6 "two apps at once" run_apps project
  if [[ $aspire -eq 1 ]]; then
    step 7 "two apps through Aspire" run_apps aspire
  else
    echo "check-template: 7 Aspire skipped: it needs a container runtime it can drive (CI runs it with --aspire)"
  fi
  echo "check-template: green ($((SECONDS - started)) s)"
}

read_arguments() {
  aspire=0
  case "$*" in
    '') ;;
    --aspire) aspire=1 ;;
    *)
      echo "usage: template-only/scripts/check-template.sh [--aspire]" >&2
      exit 2
      ;;
  esac
}

step() {
  local begun=$SECONDS
  current="step $1 ($2)"
  number=$1
  summary=""
  "${@:3}"
  echo "check-template: $1 $2: $summary ($((SECONDS - begun)) s)"
}

fail() {
  echo "check-template: $current: $1" >&2
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

litter() {
  local host=$1/src/Bootstrapper/ModularMonolith.Bootstrapper rulebook=$work/rulebook
  mkdir -p "$rulebook/playbooks" "$host/bin/Release" "$host/obj" "$1/.nuget/packages/stray" "$1/.claude" \
    "$1/.worktrees/other" "$1/.superpowers" "$1/src/.vs/ModularMonolith" "$host/.idea"
  printf 'rules\n' | tee "$rulebook/AGENTS.md" "$rulebook/STYLE.md" "$rulebook/engineering-principles.md" \
    "$rulebook/playbooks/stray.md" > /dev/null
  git init -q "$1"
  logged 1-rules env RULEBOOK_PATH="$rulebook" bash "$1/scripts/init.sh" --rules-only
  touch "$host/bin/Release/stray.dll" "$host/obj/project.assets.json" "$1/.nuget/packages/stray/stray.nupkg" \
    "$1/.claude/settings.local.json" "$1/.worktrees/other/stray.cs" "$1/.superpowers/stray.md" "$1/stray.txt" \
    "$1/template-only/stray.txt" "$1/docs/stray.md" "$host/ModularMonolith.Bootstrapper.csproj.user" \
    "$1/src/.vs/ModularMonolith/stray.suo" "$host/.idea/workspace.xml" "$1/scripts/.init.sh.swp" \
    "$host/packages.lock.json" "$host/stray.filelist"
}

install_and_create() {
  logged "1-install-$1" dotnet new install "$work/$1" --debug:custom-hive "$work/hive-$1"
  logged "1-new-$1" new_without_scripts -n Tripown -o "$work/$1-app" --debug:custom-hive "$work/hive-$1"
}

compare_installs() {
  local ignored=() line
  mkdir "$work/clean"
  working_set | tar -C "$template_root" --null -T - -cf - | tar -C "$work/clean" -xf -
  cp -a "$work/clean" "$work/dirty"
  litter "$work/dirty"
  install_and_create clean
  install_and_create dirty
  for line in "${generated_lines[@]}"; do
    ignored+=(-I "$line")
  done
  if ! diff -r "${ignored[@]}" "$work/clean-app" "$work/dirty-app" > "$work/installs.diff"; then
    sed 's/^/  /' "$work/installs.diff" >&2
    fail "Tripown from the littered working copy differs from Tripown from the clean export"
  fi
  check_secrets_ids "$work/clean-app" "$work/dirty-app"
  summary="Tripown from the clean export and from a copy with rules links, .git, bin/obj, caches, editor and user"
  summary+=" files and stray files: the same $(find "$work/clean-app" -type f | wc -l) files, equal but for the"
  summary+=" generated ports and UserSecretsId; each UserSecretsId is a new GUID"
}

check_secrets_ids() {
  local original app id ids=()
  original=$(secrets_id "$template_root")
  for app in "$@"; do
    id=$(secrets_id "$app")
    [[ $id =~ ^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$ ]] || fail "the UserSecretsId of $app, $id, is not a GUID"
    [[ $id != "$original" ]] || fail "$app kept the UserSecretsId of the template, $original"
    ids+=("$id")
  done
  [[ $(printf '%s\n' "${ids[@]}" | sort -u | wc -l) -eq $# ]] || fail "the apps share a UserSecretsId: ${ids[*]}"
}

secrets_id() {
  local projects=("$1"/src/Aspire/*.AppHost/*.AppHost.csproj) ids
  [[ ${#projects[@]} -eq 1 ]] || fail "expected one AppHost project in $1, found ${#projects[@]}"
  mapfile -t ids < <(sed -n -E 's|^[[:space:]]*<UserSecretsId>([^<]*)</UserSecretsId>[[:space:]]*$|\1|p' \
    "${projects[0]}")
  [[ ${#ids[@]} -eq 1 ]] || fail "expected one UserSecretsId in ${projects[0]}, found ${#ids[@]}"
  printf '%s\n' "${ids[0],,}"
}

refuse_name() {
  local log=$work/logs/2-refused.log
  if dotnet new "$short_name" -n "$refused_name" -o "$work/$refused_name" --allow-scripts yes \
    --debug:custom-hive "$work/hive-clean" > "$log" 2>&1; then
    fail_with_log "$log" "dotnet new -n $refused_name ran init without a refusal"
  fi
  grep -q "^init: the PostgreSQL form of the app name, ${refused_name,,} .* starts with a digit" "$log" \
    || fail_with_log "$log" "init did not refuse $refused_name for its leading digit"
  summary="dotnet new -n $refused_name --allow-scripts yes: init refused ${refused_name,,} (starts with a digit)"
}

consumer() {
  local log=$work/logs/$number-$1.log kept
  bash "$scripts/check-consumer.sh" "$@" > "$log" 2>&1 || fail_with_log "$log" "check-consumer.sh $* failed"
  kept=$(sed -n 's/^check-consumer: kept //p' "$log")
  [[ -d "$kept/$1" ]] || fail_with_log "$log" "check-consumer.sh named no kept app"
  apps+=("$kept/$1")
  summary="check-consumer.sh $*: $(sed -n -E 's/^check-consumer: ([0-9]) ([^:]*):.*\(([0-9]+ s)\)$/\2 \3/p' "$log" \
    | paste -sd ';' | sed 's/;/; /g')"
}

scaffold_path() {
  local log=$work/logs/5-scaffold.log
  bash "$scripts/check-scaffold.sh" Acme.Shop > "$log" 2>&1 || fail_with_log "$log" "check-scaffold.sh Acme.Shop failed"
  summary=$(tail -n 1 "$log" | sed -E 's/^check-scaffold: //; s/ \([0-9]+ s\)$//')
}

start_postgres() {
  local begun=$SECONDS
  postgres=$(docker run -d --rm -e POSTGRES_PASSWORD="$postgres_password" -p 5432 "$postgres_image")
  postgres_port=$(docker port "$postgres" 5432/tcp | sed -n '1s/.*://p')
  until docker exec "$postgres" pg_isready -q -h 127.0.0.1 -U postgres; do
    ((SECONDS - begun < patience)) || fail "PostgreSQL in $postgres is not ready after $patience s"
    sleep 1
  done
}

app_file_name() {
  local name=${1##*/}
  printf '%s\n' "${name//-/_}"
}

http_port() {
  sed -n -E 's|^ *"applicationUrl": "http://localhost:([0-9]+)".*$|\1|p' "$1" | head -n 1
}

read_ports() {
  local app file_name port
  ports=()
  for app in "${apps[@]}"; do
    file_name=$(app_file_name "$app")
    port=$(http_port "$app/src/Bootstrapper/$file_name.Bootstrapper/Properties/launchSettings.json" || true)
    [[ $port =~ ^[0-9]+$ ]] || fail "$app has no http applicationUrl in its Bootstrapper launchSettings.json"
    ports+=("$port")
  done
  [[ $(printf '%s\n' "${ports[@]}" | sort -u | wc -l) -eq ${#ports[@]} ]] \
    || fail "the apps got the same http port: ${ports[*]}"
}

start_app() {
  local mode=$1 app=$2 file_name database log connection
  file_name=$(app_file_name "$app")
  database=${file_name,,}
  database=${database//./_}
  log=$work/logs/$number-$file_name.log
  if [[ $mode == aspire ]]; then
    ASPIRE_ALLOW_UNSECURED_TRANSPORT=true setsid dotnet run --project "$app/src/Aspire/$file_name.AppHost" \
      -c Release --no-build --launch-profile http > "$log" 2>&1 &
  else
    docker exec "$postgres" createdb -h 127.0.0.1 -U postgres "$database"
    connection="Host=$postgres_host;Port=$postgres_port;Database=$database;Username=postgres;Password=$postgres_password"
    ConnectionStrings__database=$connection setsid dotnet run \
      --project "$app/src/Bootstrapper/$file_name.Bootstrapper" \
      -c Release --no-build --launch-profile http > "$log" 2>&1 &
  fi
  groups+=("$!")
  logs+=("$log")
}

healthy() {
  [[ "$(curl -s -m 2 "http://localhost:${ports[$1]}/health" || true)" == Healthy ]]
}

expect_running() {
  kill -0 "${groups[$1]}" 2> /dev/null || fail_with_log "${logs[$1]}" "$2"
}

await_health() {
  local begun=$SECONDS
  until healthy "$1"; do
    expect_running "$1" "the app stopped before /health said Healthy"
    ((SECONDS - begun < patience)) || fail_with_log "${logs[$1]}" "/health did not say Healthy in $patience s"
    sleep 1
  done
  expect_running "$1" "the app is not running although /health on its port said Healthy"
}

expect_still_healthy() {
  expect_running "$1" "the app stopped before the shutdown"
  healthy "$1" || fail_with_log "${logs[$1]}" "/health stopped saying Healthy before the shutdown"
}

stop_apps() {
  local group
  for group in "${groups[@]}"; do
    kill -s TERM -- "-$group" 2> /dev/null || true
  done
  for group in "${groups[@]}"; do
    wait "$group" 2> /dev/null || true
  done
  groups=()
}

run_apps() {
  local index
  logs=()
  read_ports
  if [[ $1 == project ]]; then
    start_postgres
  fi
  for index in "${!apps[@]}"; do
    start_app "$1" "${apps[$index]}"
  done
  for index in "${!apps[@]}"; do
    await_health "$index"
  done
  sleep "$settle"
  for index in "${!apps[@]}"; do
    expect_still_healthy "$index"
  done
  stop_apps
  summary="${apps[*]##*/} answered Healthy on /health at the same time and kept running"
  summary+=" on distinct ports ${ports[*]} from their launchSettings.json"
}

finish() {
  local status=$?
  trap - EXIT
  if [[ ${#groups[@]} -gt 0 ]]; then
    stop_apps
  fi
  if [[ -n "$postgres" ]]; then
    docker rm -f "$postgres" > /dev/null 2>&1 || true
  fi
  if [[ -z "$work" ]]; then
    exit "$status"
  fi
  if [[ $status -ne 0 ]]; then
    echo "check-template: ${current:-start} stopped with exit code $status; kept $work ${apps[*]%/*}" >&2
  else
    rm -rf -- "$work" "${apps[@]%/*}"
  fi
  exit "$status"
}

started=$SECONDS
current=""
work=""
postgres=""
postgres_host=${TESTCONTAINERS_HOST_OVERRIDE:-localhost}
apps=()
groups=()
trap finish EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
main "$@"
