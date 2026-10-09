#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit nullglob
export LC_ALL=C

lock_file=core.lock

main() {
  read_arguments "$@"
  if [[ -n "$from" ]]; then
    from=$(absolute_source "$from")
  fi
  cd "$(git -C "$(dirname -- "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)"
  work=$(mktemp -d)

  refuse_in_template
  find_solution
  require_committed_core
  require_locked_core
  choose_source
  fetch_candidate
  list_changes
  plan_copies
  merge_solutions "$candidate_solution" "$solution" > "$work/solution" \
    || stop "cannot place the core projects of the template in $solution"

  armed=1
  swap_core
  write_if_changed "$work/solution" "$solution"
  verify_swapped_core
  write_lock
  git_core add -A -- "${old_files[@]}" "${new_files[@]}" "$lock_file" "$solution" \
    || stop "cannot stage the new core"
  armed=0

  report
}

usage() {
  echo "usage: scripts/update-core.sh <template ref> [--from <template path or url>]" >&2
  exit 2
}

stop() {
  echo "update-core: $1" >&2
  exit 1
}

git_core() {
  git -c core.attributesFile=/dev/null --literal-pathspecs "$@"
}

read_arguments() {
  ref=""
  from=""
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --from)
        [[ $# -ge 2 && -n "$2" ]] || usage
        from=$2
        shift 2
        ;;
      -*)
        usage
        ;;
      *)
        [[ -z "$ref" ]] || usage
        ref=$1
        shift
        ;;
    esac
  done
  [[ -n "$ref" ]] || usage
}

absolute_source() {
  if [[ -d "$1" ]]; then
    (CDPATH='' cd -- "$1" && pwd)
  else
    printf '%s\n' "$1"
  fi
}

lock_value() {
  awk -v key="$2" 'index($0, key "=") == 1 { value = substr($0, length(key) + 2); sub(/\r$/, "", value); print value; exit }' "$1"
}

refuse_in_template() {
  if [[ -d template-only ]]; then
    stop "this is the template: change its core here and record it with template-only/scripts/lock-core.sh"
  fi
}

find_solution() {
  local solutions=(*.slnx)
  if [[ ${#solutions[@]} -ne 1 ]]; then
    stop "the root must hold exactly one *.slnx, found ${#solutions[@]}: ${solutions[*]:-none}"
  fi
  solution=${solutions[0]}
}

require_committed_core() {
  local changes
  git rev-parse --verify -q HEAD > /dev/null || stop "this repository has no commit yet; commit the app first"
  bash scripts/core-hash.sh entries > "$work/entries"
  mapfile -t entries < "$work/entries"
  changes=$(git_core status --porcelain --untracked-files=all -- "${entries[@]}" "$lock_file" "$solution")
  if [[ -n "$changes" ]]; then
    echo "update-core: core files, $lock_file or $solution have uncommitted changes or untracked files; commit or discard them first:" >&2
    printf '%s\n' "$changes" | sed 's/^/  /' >&2
    exit 1
  fi
}

require_locked_core() {
  local report
  if ! report=$(bash scripts/core-hash.sh check 2>&1); then
    printf '%s\n' "$report" >&2
    name_hand_edits >&2
    stop "the core does not match $lock_file, so it was edited in this app; a core fix is made in the template: undo the edit here (git revert or git checkout), then update"
  fi
  old_version=$(lock_value "$lock_file" version)
}

name_hand_edits() {
  local base changes
  base=$(git log -1 --format=%H -- "$lock_file")
  if [[ -n "$base" ]]; then
    changes=$(git_core diff --stat "$base" HEAD -- "${entries[@]}")
    if [[ -n "$changes" ]]; then
      echo "update-core: core files changed since $lock_file was last committed (${base:0:12}):"
      printf '%s\n' "$changes" | sed 's/^/  /'
    fi
  fi
}

choose_source() {
  source=$from
  if [[ -z "$source" ]]; then
    source=$(lock_value "$lock_file" source)
  fi
  if [[ -z "$source" ]]; then
    stop "$lock_file has no source= and no --from was given; run scripts/update-core.sh $ref --from <template path or url>"
  fi
}

resolve_commit() {
  git -C "$candidate" rev-parse --verify -q "$ref^{commit}" \
    || git -C "$candidate" rev-parse --verify -q "refs/remotes/origin/$ref^{commit}"
}

fetch_candidate() {
  local report solutions
  candidate=$work/template
  git -c core.hooksPath=/dev/null clone -q --no-checkout -- "$source" "$candidate" \
    || stop "cannot clone the template from $source"
  commit=$(resolve_commit) || stop "cannot resolve $ref to a commit of $source"
  git -C "$candidate" -c core.hooksPath=/dev/null checkout -q --detach "$commit" || stop "cannot check out $commit"

  if [[ ! -f "$candidate/src/Core/core-files.txt" || ! -f "$candidate/scripts/core-hash.sh" || ! -f "$candidate/$lock_file" ]]; then
    stop "$source at $commit is not a template: it lacks src/Core/core-files.txt, scripts/core-hash.sh or $lock_file"
  fi
  if ! report=$(cd "$candidate" && bash scripts/core-hash.sh check 2>&1); then
    printf '%s\n' "$report" >&2
    stop "the template at $commit does not match its own $lock_file; take another commit"
  fi

  solutions=("$candidate"/*.slnx)
  if [[ ${#solutions[@]} -ne 1 ]]; then
    stop "the template at $commit must hold exactly one *.slnx in its root, found ${#solutions[@]}"
  fi
  candidate_solution=${solutions[0]}
  new_version=$(lock_value "$candidate/$lock_file" version)
  new_hash=$(lock_value "$candidate/$lock_file" hash)
}

list_changes() {
  local path collisions=()
  bash scripts/core-hash.sh list > "$work/old-files"
  (cd "$candidate" && bash scripts/core-hash.sh list) > "$work/new-files"
  comm -23 "$work/old-files" "$work/new-files" > "$work/vanished"
  comm -13 "$work/old-files" "$work/new-files" > "$work/added"
  comm -12 "$work/old-files" "$work/new-files" > "$work/common"
  mapfile -t old_files < "$work/old-files"
  mapfile -t new_files < "$work/new-files"

  while IFS= read -r path; do
    if [[ -e "$path" || -L "$path" ]]; then
      collisions+=("$path")
    fi
  done < "$work/added"
  if [[ ${#collisions[@]} -gt 0 ]]; then
    echo "update-core: the template adds core files that this app already has as its own; move them away first:" >&2
    printf '  %s\n' "${collisions[@]}" >&2
    exit 1
  fi
}

file_state() {
  (
    cd "$1"
    xargs -d '\n' -a "$2" -r sha256sum > "$3.sums"
    xargs -d '\n' -a "$2" -r stat -c '%a' > "$3.modes"
  )
  cut -c 1-64 "$3.sums" > "$3.ids"
  paste -d ' ' "$3.ids" "$3.modes" > "$3"
}

plan_copies() {
  file_state "$candidate" "$work/common" "$work/candidate-state"
  file_state . "$work/common" "$work/app-state"
  paste -d '\t' "$work/common" "$work/candidate-state" "$work/app-state" \
    | awk -F '\t' -v changed="$work/changed" -v modes="$work/modes" '
        { split($2, wanted, " "); split($3, present, " ") }
        wanted[1] != present[1] { print $1 > changed; next }
        wanted[2] != present[2] { print $1 > modes }
      '
  touch "$work/changed" "$work/modes"
  sort -- "$work/changed" "$work/added" > "$work/copies"
}

merge_solutions() {
  awk '
    function strip(line) { sub(/\r$/, "", line); return line }
    function name_of(line) { return match(line, /Name="[^"]*"/) ? substr(line, RSTART + 6, RLENGTH - 7) : "" }
    function folder(line) { return line ~ /^[ \t]*<Folder[ \t]/ }
    function closed(line) { return line ~ /\/>[ \t]*$/ }
    function folder_end(line) { return line ~ /^[ \t]*<\/Folder>/ }
    function solution_end(line) { return line ~ /^[ \t]*<\/Solution>/ }
    function core(line) { return line ~ /^[ \t]*<Project[ \t][^>]*Path="src[\/\\](Core|Packages)[\/\\]/ }
    function emit(line) { printf "%s%s\n", line, cr }
    function emit_projects(name,    k) {
      for (k = 1; k <= wanted[name]; k++) emit(project[name, k])
      used[name] = 1
    }
    function emit_block(name) { emit(opening[name]); emit_projects(name); emit(closing[name]) }
    function emit_new_folders(limit,    k, name) {
      for (k = 1; k <= count; k++) {
        name = order[k]
        if (name != "" && !(name in used) && !(name in present) && (limit == "" || name < limit)) emit_block(name)
      }
    }
    function refuse(message) { print "update-core: " message > "/dev/stderr"; failed = 1; exit 1 }
    FNR == 1 { file++ }
    file == 1 {
      entry = strip($0)
      if (folder(entry)) {
        current = closed(entry) ? "" : name_of(entry)
        if (current != "") opening[current] = entry
      } else if (folder_end(entry)) {
        if (current != "") closing[current] = entry
        current = ""
      } else if (core(entry)) {
        if (!closed(entry)) refuse("the solution of the template lists a core project over several lines: " entry)
        if (!(current in wanted)) order[++count] = current
        project[current, ++wanted[current]] = entry
      }
      next
    }
    {
      text[++lines] = $0
      plain[lines] = strip($0)
      if ($0 ~ /\r$/) cr = "\r"
    }
    END {
      if (failed) exit 1
      for (i = 1; i <= lines; i++) {
        if (!core(plain[i])) continue
        if (!closed(plain[i])) refuse("the solution lists a core project over several lines: " plain[i])
        drop[i] = 1
      }
      for (i = 1; i <= lines; i++) {
        if (!folder(plain[i]) || closed(plain[i])) continue
        for (j = i + 1; j <= lines && !folder_end(plain[j]); j++) ;
        dropped = 0
        kept = 0
        for (m = i + 1; m < j; m++) {
          if (m in drop) dropped++
          else if (plain[m] !~ /^[ \t]*$/) kept++
        }
        if (dropped && !kept && !(name_of(plain[i]) in wanted)) for (m = i; m <= j && m <= lines; m++) drop[m] = 1
      }
      for (i = 1; i <= lines; i++) if (!(i in drop) && folder(plain[i])) present[name_of(plain[i])] = 1
      inside = 0
      for (i = 1; i <= lines; i++) {
        row = plain[i]
        title = name_of(row)
        if (!inside && !(i in drop) && folder(row)) emit_new_folders(title)
        if (!inside && solution_end(row)) {
          emit_new_folders("")
          if ("" in wanted) emit_projects("")
          ended = 1
        }
        if (!(i in drop)) {
          if (folder(row) && closed(row) && (title in wanted) && !(title in used)) emit_block(title)
          else {
            print text[i]
            if (folder(row) && !closed(row) && (title in wanted) && !(title in used)) emit_projects(title)
          }
        }
        if (folder(row) && !closed(row)) inside = 1
        if (folder_end(row)) inside = 0
      }
      if (!ended) refuse("the solution has no </Solution> line")
      for (m = 1; m <= count; m++) if (!(order[m] in used)) refuse("no place in the solution for the core folder " order[m])
    }
  ' "$1" "$2"
}

swap_core() {
  local path
  while IFS= read -r path; do
    rm -f -- "$path" || stop "cannot remove $path"
    parent_folder "$path"
    rmdir -p --ignore-fail-on-non-empty -- "$parent" 2> /dev/null || true
  done < "$work/vanished"
  while IFS= read -r path; do
    place "$path"
  done < "$work/copies"
  while IFS= read -r path; do
    chmod --reference="$candidate/$path" -- "$path" || stop "cannot set the mode of $path"
  done < "$work/modes"
}

parent_folder() {
  if [[ "$1" == */* ]]; then
    parent=${1%/*}
  else
    parent=.
  fi
}

place() {
  local path=$1
  parent_folder "$path"
  make_parents "$parent"
  if [[ ! -e "$path" && ! -L "$path" ]]; then
    placed+=("$path")
  fi
  rm -f -- "$path" || stop "cannot remove $path"
  cp --preserve=mode -- "$candidate/$path" "$path" || stop "cannot write $path"
}

make_parents() {
  local folder=$1 missing=() index
  while [[ ! -d "$folder" ]]; do
    missing+=("$folder")
    parent_folder "$folder"
    folder=$parent
  done
  for ((index = ${#missing[@]} - 1; index >= 0; index--)); do
    mkdir -- "${missing[index]}" || stop "cannot create the folder ${missing[index]}"
    created+=("${missing[index]}")
  done
}

write_if_changed() {
  if ! cmp -s -- "$1" "$2"; then
    cat -- "$1" > "$2" || stop "cannot write $2"
  fi
}

verify_swapped_core() {
  local hash
  bash scripts/core-hash.sh list > "$work/swapped-files"
  if ! cmp -s "$work/new-files" "$work/swapped-files"; then
    {
      echo "update-core: after the swap the core files of this app differ from the template's:"
      comm -23 "$work/new-files" "$work/swapped-files" | sed 's/^/  missing (ignored by this app?): /'
      comm -13 "$work/new-files" "$work/swapped-files" | sed 's/^/  extra (an app file under a core path?): /'
    } >&2
    stop "the core of the template cannot be placed in this app as it is"
  fi
  hash=$(bash scripts/core-hash.sh print)
  if [[ "$hash" != "$new_hash" ]]; then
    stop "after the swap the core hash is $hash, but the template's $lock_file says $new_hash"
  fi
}

write_lock() {
  printf 'version=%s\nhash=%s\nsource=%s\ncommit=%s\n' "$new_version" "$new_hash" "$source" "$commit" > "$work/lock"
  write_if_changed "$work/lock" "$lock_file"
  bash scripts/core-hash.sh check > /dev/null || stop "the new $lock_file does not match the core"
}

report() {
  local paths=("${old_files[@]}" "${new_files[@]}" "$lock_file" "$solution")
  if git_core diff --cached --quiet -- "${paths[@]}"; then
    echo "update-core: the core is already version $new_version from $commit; nothing to change"
    return
  fi
  git_core diff --cached --stat -- "${paths[@]}"
  echo "update-core: the core moved from version $old_version to version $new_version, template commit $commit from $source; the changes are staged"
  echo "update-core: next, run ./gate.sh; if it stops on pending model changes of a module (PendingModelChangesWarning), add a migration with scripts/add-migration.sh <Module> <Name> and run the gate again; then commit"
}

rollback() {
  local status=0 index
  if [[ ${#placed[@]} -gt 0 ]]; then
    git_core reset -q -- "${placed[@]}" > /dev/null 2>&1 || status=1
    rm -f -- "${placed[@]}" || status=1
  fi
  for ((index = ${#created[@]} - 1; index >= 0; index--)); do
    rmdir -- "${created[index]}" || status=1
  done
  git_core checkout -q HEAD -- "${old_files[@]}" "$lock_file" "$solution" || status=1
  return "$status"
}

finish() {
  local status=$?
  trap - EXIT
  if [[ "$armed" == 1 ]]; then
    armed=0
    if rollback; then
      echo "update-core: stopped; the core files, $lock_file and $solution are back as they were" >&2
    else
      echo "update-core: stopped, and the restore failed; run git checkout HEAD -- on the core files, $lock_file and $solution, and delete these new files:" >&2
      printf '  %s\n' "${placed[@]}" >&2
    fi
    if [[ "$status" -eq 0 ]]; then
      status=1
    fi
  fi
  if [[ -n "$work" ]]; then
    rm -rf -- "$work"
  fi
  exit "$status"
}

armed=0
work=""
placed=()
created=()
trap finish EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM
main "$@"
exit $?
