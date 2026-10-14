#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit nullglob

export LC_ALL=C GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1
export GIT_AUTHOR_NAME=scaffold GIT_AUTHOR_EMAIL=scaffold@example.com
export GIT_COMMITTER_NAME=scaffold GIT_COMMITTER_EMAIL=scaffold@example.com
unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE

rolled_back='; files scaffold created are deleted, files it changed are back as they were'
patience=300
modules=src/Modules
tags_tests=src/Tests/IntegrationTests/Modules/Tags

main() {
  read_arguments "$@"
  if [[ $(id -u) -eq 0 ]]; then
    echo "check-scaffold: run it as a normal user; root writes to read-only files, which the forced failures need" >&2
    exit 2
  fi
  scripts=$(cd "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
  work=$(mktemp -d)
  mkdir "$work/logs"
  echo "check-scaffold: $name, working folder $work"

  make_app 54
  scaffold 28 module Notes --settings
  scaffold 52 module Tags
  scaffold 45 feature Notes ArchiveNote --kind command
  scaffold 43 feature Notes CreateNote --kind command --endpoint post
  scaffold 43 feature Notes RenameNote --kind command --endpoint put
  scaffold 43 feature Notes DeleteNote --kind command --endpoint delete
  scaffold 43 feature Notes AddNote --kind command --endpoint post --route ""
  declare_integration_event
  scaffold 34 feature Tags NoteCreated --kind event-handler --event NoteCreatedIntegrationEvent
  scaffold 31 feature Notes CountNotes --kind query
  scaffold 31 feature Notes GetNoteSummary --kind query --endpoint get
  scaffold 31 feature Notes GetNote --kind query --endpoint get --route "/{noteId:guid}"
  scaffold 24 feature Tags GetTag --kind public-query
  declare_domain_event
  scaffold 16 feature Notes NoteCreated --kind event-handler --event NoteCreatedDomainEvent
  scaffold 14 feature Tags RecountTags --kind internal-command

  force_failures
  print_summary
}

usage() {
  echo "usage: template-only/scripts/check-scaffold.sh [<name>] [--keep]" >&2
  exit 2
}

read_arguments() {
  name=""
  keep=0
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --keep) keep=1 ;;
      -*) usage ;;
      *)
        [[ -z "$name" ]] || usage
        name=$1
        ;;
    esac
    shift
  done
  name=${name:-Acme.Shop}
  host=src/Bootstrapper/$name.Bootstrapper
  tags_facade=src/Modules/Tags/$name.Modules.Tags/Infrastructure/TagsModuleFacade.cs
  tags_features=src/Modules/Tags/$name.Modules.Tags/Features
}

fail() {
  echo "check-scaffold: $current: $1" >&2
  exit 1
}

fail_with_log() {
  tail -n 40 "$1" | sed 's/^/  | /' >&2
  fail "$2; the whole output is in $1"
}

in_app() {
  (cd "$app" && "$@")
}

seconds_since() {
  awk -v start="$1" -v now="$EPOCHREALTIME" 'BEGIN { printf "%.1f s", now - start }'
}

is_past() {
  awk -v start="$1" -v now="$EPOCHREALTIME" -v limit="$2" 'BEGIN { exit !(now - start > limit) }'
}

next_step() {
  step=$((step + 1))
  current="step $step ($1)"
  step_log=$work/logs/$(printf '%02d' "$step")-${1//[^A-Za-z0-9]/-}
}

make_app() {
  local begun=$EPOCHREALTIME log
  next_step consumer
  log=$step_log.log
  if ! bash "$scripts/check-consumer.sh" "$name" --keep --no-update > "$log" 2>&1; then
    fail_with_log "$log" "check-consumer.sh failed"
  fi
  consumer=$(sed -n 's/^check-consumer: kept //p' "$log")
  app=$consumer/$name
  [[ -n "$consumer" && -d "$app/.git" ]] || fail_with_log "$log" "check-consumer.sh named no kept app"
  expect_committed_skips "$1"
  if [[ ! -L "$app/AGENTS.md" ]]; then
    export CI=true
    echo "check-scaffold: $name links no rules, so its gate runs with CI=true and skips the rule marker check"
  fi
  record "app $name from dotnet new (check-consumer.sh steps 1-3)" "" "" \
    "gate green in $(seconds_since "$begun"), skipped $1"
}

describe() {
  local argument words=()
  for argument in "$@"; do
    words+=("${argument:-\"\"}")
  done
  echo "${words[*]}"
}

scaffold() {
  local skips=$1 begun scaffold_time gate_time new_test label
  shift
  label=$(describe "$@")
  next_step "$label"
  begun=$EPOCHREALTIME
  if ! in_app bash scripts/scaffold.sh "$@" > "$step_log-scaffold.log" 2>&1; then
    fail_with_log "$step_log-scaffold.log" "bash scripts/scaffold.sh $label failed"
  fi
  scaffold_time=$(seconds_since "$begun")
  if [[ $1 == feature ]]; then
    new_test=$(sed -nE 's/^scaffold: done; the new test ([^ ]+) fails with .*$/\1/p' "$step_log-scaffold.log")
    [[ -n "$new_test" ]] || fail_with_log "$step_log-scaffold.log" "scaffold named no new test"
    expected+=("$2.$new_test")
  fi
  commit_step "$skips" "$label"
  begun=$EPOCHREALTIME
  if ! bash "$scripts/check-feature-failures.sh" "$app" "$skips" "${expected[@]}" > "$step_log-check.log" 2>&1; then
    sed 's/^/  | /' "$step_log-check.log" >&2
    fail "check-feature-failures.sh failed"
  fi
  gate_time=$(seconds_since "$begun")
  expect_committed_skips "$skips"
  record "$label" "$scaffold_time" "$gate_time" "$(gate_result), skipped $skips"
}

gate_result() {
  if [[ ${#expected[@]} -eq 0 ]]; then
    echo "green"
  else
    echo "exactly ${#expected[@]} failed, each not implemented"
  fi
}

commit_step() {
  printf '%s\n' "$1" > "$app/.gate/skips"
  git -C "$app" add -A
  if ! git -C "$app" commit -q --no-verify -m "$2" > "$step_log-commit.log" 2>&1; then
    fail_with_log "$step_log-commit.log" "cannot commit the step with $1 in .gate/skips"
  fi
}

expect_committed_skips() {
  local recorded
  recorded=$(tr -d '[:space:]' < "$app/.gate/skips")
  [[ "$recorded" == "$1" ]] || fail ".gate/skips holds $recorded, the step expects $1"
  [[ -z "$(git -C "$app" status --porcelain -- .gate/skips)" ]] || fail ".gate/skips differs from its committed text"
}

declare_integration_event() {
  local contracts=$app/src/Modules/Notes/$name.Modules.Notes.Contracts
  next_step "by hand: NoteCreatedIntegrationEvent in the Contracts of Notes"
  mkdir -p "$contracts/IntegrationEvents"
  cat > "$contracts/IntegrationEvents/NoteCreatedIntegrationEvent.cs" << EOF
using Tsikhanau.Monolith.Contracts.IntegrationEvents;
using Tsikhanau.Railway;

namespace $name.Modules.Notes.Contracts.IntegrationEvents;

[IntegrationEventName("notes.note-created")]
public sealed record NoteCreatedIntegrationEvent(
    DateTimeOffset OccurredOn,
    Guid NoteId,
    String Title,
    Optional<String> CreatorId)
    : IntegrationEvent(OccurredOn);
EOF
  record "by hand: NoteCreatedIntegrationEvent (Notes Contracts)" "" "" "written"
}

declare_domain_event() {
  local domain=$app/src/Modules/Notes/$name.Modules.Notes/Domain/Notes
  next_step "by hand: NoteCreatedDomainEvent in the domain of Notes"
  mkdir -p "$domain/Events"
  cat > "$domain/NoteId.cs" << EOF
using Tsikhanau.Monolith.Domain.Ids;

namespace $name.Modules.Notes.Domain.Notes;

internal readonly record struct NoteId(Guid Value) : ITypedId<NoteId>
{
    public static NoteId From(Guid value) => new(value);
}
EOF
  cat > "$domain/NoteTitle.cs" << EOF
using Tsikhanau.Monolith.Domain.ValueObjects;

namespace $name.Modules.Notes.Domain.Notes;

internal readonly record struct NoteTitle : IValueObject<NoteTitle, String>
{
    private NoteTitle(String value) => Value = value;

    public String Value { get; }

    static NoteTitle IValueObject<NoteTitle, String>.From(String value) => new(value);
}
EOF
  cat > "$domain/NoteKind.cs" << EOF
namespace $name.Modules.Notes.Domain.Notes;

internal enum NoteKind
{
    Plain,
    Checklist,
}
EOF
  cat > "$domain/Events/NoteCreatedDomainEvent.cs" << EOF
using Tsikhanau.Monolith.Domain.Events;
using Tsikhanau.Railway;

namespace $name.Modules.Notes.Domain.Notes.Events;

internal sealed record NoteCreatedDomainEvent(
    DateTimeOffset OccurredOn,
    NoteId NoteId,
    NoteTitle Title,
    NoteKind Kind,
    Optional<NoteTitle> Subtitle,
    IReadOnlyList<String> Labels,
    IReadOnlyCollection<NoteId> LinkedNoteIds,
    Int32 Revision,
    Int64 Size,
    Boolean IsPinned,
    Guid AuthorId,
    DateOnly DueOn,
    String DisplayName)
    : DomainEvent(OccurredOn);
EOF
  record "by hand: NoteId, NoteTitle, NoteKind, NoteCreatedDomainEvent (Notes domain)" "" "" "written"
}

working_set() {
  comm -z -23 \
    <(git ls-files -z --cached --others --exclude-standard | sort -zu) \
    <(git ls-files -z --deleted | sort -zu)
}

snapshot() {
  (
    cd "$app"
    git status --porcelain=v1 --untracked-files=all
    working_set | xargs -0 -r sha256sum --
    find . \( -name .git -o -name bin -o -name obj \) -prune -o -printf '%p %y %m\n' | sort
  ) > "$1"
}

expect_tree_unchanged() {
  snapshot "$step_log-after.txt"
  if ! diff "$step_log-before.txt" "$step_log-after.txt" > "$step_log-tree.diff"; then
    sed 's/^/  /' "$step_log-tree.diff" | head -n 40 >&2
    fail "the tree is not as it was (< before, > after)"
  fi
}

expect_refusal() {
  local message=$1 ending=$2 log=$step_log-scaffold.log status=0 last
  shift 2
  in_app bash scripts/scaffold.sh "$@" > "$log" 2>&1 || status=$?
  last=$(tail -n 1 "$log")
  [[ $status -eq 1 ]] || fail_with_log "$log" "scaffold ended with exit code $status, not 1"
  [[ "$last" == "scaffold: $message"* && "$last" == *"$ending" ]] \
    || fail_with_log "$log" "scaffold did not fail where it was forced to: expected 'scaffold: $message...$ending'"
}

with_read_only() {
  local path=$app/$1 mode
  shift
  mode=$(stat -c %a -- "$path")
  chmod a-w -- "$path"
  "$@"
  chmod "$mode" -- "$path"
}

force() {
  local begun=$EPOCHREALTIME
  next_step "forced failure: $1"
  snapshot "$step_log-before.txt"
  "$2"
  expect_tree_unchanged
  record "forced failure: $1" "$(seconds_since "$begun")" "" "tree unchanged"
}

force_failures() {
  force "module, the first folder cannot be created" fail_module_folder
  force "module, the last edit cannot be written" fail_module_edit
  force "module, the add-migration step fails" fail_module_migration
  force "module, Ctrl+C during the add-migration step" stop_module_migration
  force "feature, a new file cannot be written" fail_feature_file
  force "feature, an edit cannot be written" fail_feature_edit
  force "feature, a public query fails after its Contracts files are written" fail_public_query
}

fail_module_folder() {
  with_read_only "$modules" expect_refusal "cannot create the folder $modules/Zones: " "" module Zones --settings
}

fail_module_edit() {
  with_read_only "$host/appsettings.json" \
    expect_refusal "cannot write $host/appsettings.json: " "$rolled_back" module Zones --settings
}

fail_module_migration() {
  mv -- "$app/scripts/add-migration.sh" "$app/scripts/add-migration-real.sh"
  printf '%s\n' '#!/usr/bin/env bash' 'bash "$(dirname -- "$0")/add-migration-real.sh" "$@"' 'exit 1' \
    > "$app/scripts/add-migration.sh"
  expect_refusal "bash scripts/add-migration.sh Zones Initial failed with exit code 1" "$rolled_back" \
    module Zones --settings
  grep -q "^Done. To undo this action, use 'ef migrations remove'" "$step_log-scaffold.log" \
    || fail_with_log "$step_log-scaffold.log" "the real migration did not run before the forced exit code"
  mv -- "$app/scripts/add-migration-real.sh" "$app/scripts/add-migration.sh"
}

stop_module_migration() {
  local log=$step_log-scaffold.log obj=$app/src/Modules/Zones/$name.Modules.Zones/obj status=0 begun=$EPOCHREALTIME
  (cd "$app" && exec setsid env --default-signal=INT,TERM bash scripts/scaffold.sh module Zones --settings) \
    > "$log" 2>&1 &
  group=$!
  until [[ -d "$obj" ]] || ! kill -0 "$group" 2> /dev/null; do
    ! is_past "$begun" "$patience" || fail_with_log "$log" "the migration step did not start within $patience s"
    sleep 0.2
  done
  sleep 1
  kill -s INT -- "-$group" 2> /dev/null || fail_with_log "$log" "scaffold ended before the signal"
  wait "$group" || status=$?
  [[ $status -eq 1 ]] || fail_with_log "$log" "scaffold ended with exit code $status after Ctrl+C, not 1"
  [[ "$(tail -n 1 "$log")" == "scaffold: bash scripts/add-migration.sh Zones Initial was stopped$rolled_back" ]] \
    || fail_with_log "$log" "scaffold did not report the stopped migration step"
  expect_no_process_left
}

expect_no_process_left() {
  local tries=0
  while pgrep -g "$group" > /dev/null; do
    tries=$((tries + 1))
    if [[ $tries -gt 50 ]]; then
      pgrep -a -g "$group" | sed 's/^/  /' >&2
      fail "processes of the stopped run are still running"
    fi
    sleep 0.2
  done
  group=""
}

fail_feature_file() {
  with_read_only "$tags_tests" \
    expect_refusal "cannot write $tags_tests/PinTagTests.cs: " "$rolled_back" feature Tags PinTag --kind command
}

fail_feature_edit() {
  with_read_only "$tags_facade" \
    expect_refusal "cannot write $tags_facade: " "$rolled_back" feature Tags PinTag --kind command
}

fail_public_query() {
  with_read_only "$tags_features" \
    expect_refusal "cannot create the folder $tags_features/CountTags: " "$rolled_back" \
    feature Tags CountTags --kind public-query
}

record() {
  local line=""
  rows+=("$(printf '%-84s %9s %9s  %s' "$1" "$2" "$3" "$4")")
  [[ -z "$2" ]] || line+=", scaffold $2"
  [[ -z "$3" ]] || line+=", gate $3"
  [[ -z "$4" ]] || line+=", $4"
  echo "check-scaffold: $current:${line#,}"
}

print_summary() {
  echo "check-scaffold: summary"
  printf '  %-84s %9s %9s  %s\n' step scaffold gate result
  printf '  %s\n' "${rows[@]}"
  echo "check-scaffold: $name is green ($((SECONDS - started)) s)"
}

finish() {
  local status=$?
  trap - EXIT
  if [[ -n "$group" ]]; then
    kill -s KILL -- "-$group" 2> /dev/null || true
  fi
  if [[ -n "$consumer" && -d "$consumer" ]]; then
    chmod -R u+w -- "$consumer"
  fi
  if [[ -z "$work" ]]; then
    exit "$status"
  fi
  if [[ $status -ne 0 ]]; then
    echo "check-scaffold: ${current:-start} stopped with exit code $status; kept $work and ${consumer:-no app}" >&2
  elif [[ $keep -eq 1 ]]; then
    echo "check-scaffold: kept $work and $consumer"
  else
    rm -rf -- "$work" "$consumer"
  fi
  exit "$status"
}

started=$SECONDS
current=""
keep=0
work=""
consumer=""
app=""
group=""
step=0
step_log=""
expected=()
rows=()
trap finish EXIT
trap 'exit 130' INT
trap 'exit 143' TERM
main "$@"
