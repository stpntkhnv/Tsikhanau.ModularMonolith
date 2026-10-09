#!/usr/bin/env bash
set -euo pipefail

require_tools() {
  local missing=() names
  if ((BASH_VERSINFO[0] < 4 || (BASH_VERSINFO[0] == 4 && BASH_VERSINFO[1] < 4))); then
    missing+=("bash 4.4")
  fi
  printf 'b\0a\0' | sort -z > /dev/null 2>&1 || missing+=("sort -z")
  comm -z /dev/null /dev/null > /dev/null 2>&1 || missing+=("comm -z")
  sha256sum < /dev/null > /dev/null 2>&1 || missing+=("sha256sum")
  if [[ ${#missing[@]} -gt 0 ]]; then
    printf -v names '%s, ' "${missing[@]}"
    echo "$1: needs bash 4.4 or newer and GNU coreutils (sort -z, comm -z, sha256sum); missing: ${names%, }" >&2
    exit 2
  fi
}

require_tools core-hash
shopt -s inherit_errexit
export LC_ALL=C

cd "$(git rev-parse --show-toplevel)"

list_file=src/Core/core-files.txt
version_file=src/Core/VERSION
lock_file=core.lock
template_solution=template-only/Template.slnx
digest_pattern='^sha256:[0-9a-f]{64}$'
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

git_core() {
  git -c core.attributesFile=/dev/null --literal-pathspecs "$@"
}

advice() {
  if [[ -f "$template_solution" ]]; then
    echo "core-hash: the core files differ from $lock_file; record them with template-only/scripts/lock-core.sh"
  else
    echo "core-hash: the core was changed in this app; undo the edit (git checkout HEAD -- <files>, or git revert <commit> if committed), make the fix in the template, then run scripts/update-core.sh"
  fi
}

refuse() {
  advice >&2
  echo "core-hash: $1" >&2
  exit 1
}

load_entries() {
  if [[ ! -f "$1" ]]; then
    refuse "$2 is missing"
  fi

  local line
  entries=()
  while IFS= read -r line || [[ -n "$line" ]]; do
    line=${line%$'\r'}
    if [[ -n "$line" ]]; then
      entries+=("$line")
    fi
  done < "$1"

  if [[ ${#entries[@]} -eq 0 ]]; then
    refuse "$2 names no core files"
  fi
}

copy_staged() {
  if git cat-file -e ":$1" 2> /dev/null; then
    git cat-file blob ":$1" > "$2"
  fi
}

drop_build_output_and_editor_files() {
  local entry folders=()
  for entry in "${entries[@]}"; do
    if [[ "$entry" == */ ]]; then
      folders+=("${entry%/}")
    fi
  done
  core_folders=$(printf '%s\n' "${folders[@]}") awk -F '\t' '
    BEGIN {
      count = split(ENVIRON["core_folders"], listed, "\n")
      for (k = 1; k <= count; k++) core_folder[listed[k]] = 1
    }
    function folder_of(file) { return sub(/\/[^\/]*$/, "", file) ? file : "" }
    function editor_file(file) { sub(/.*\//, "", file); return file ~ /(\.swp|~)$/ || file == ".DS_Store" }
    function build_output(file,    part, count, k, folder) {
      count = split(file, part, "/")
      folder = ""
      for (k = 1; k < count; k++) {
        if ((part[k] == "bin" || part[k] == "obj") && ((folder in project) || (folder_of(folder) in core_folder))) return 1
        folder = folder == "" ? part[k] : folder "/" part[k]
      }
      return 0
    }
    { row[NR] = $0; name[NR] = $1 }
    $1 ~ /\.csproj$/ { project[folder_of($1)] = 1 }
    END { for (i = 1; i <= NR; i++) if (!editor_file(name[i]) && !build_output(name[i])) print row[i] }
  '
}

present_entries() {
  local entry
  present=()
  for entry in "${entries[@]}"; do
    entry=${entry%/}
    if [[ -e "$entry" || -L "$entry" ]]; then
      present+=("$entry")
    fi
  done
}

core_paths() {
  present_entries
  if [[ ${#present[@]} -gt 0 ]]; then
    find "${present[@]}" \( -type f -o -type l \) -print0 | sort -zu | tr '\0' '\n' | drop_build_output_and_editor_files
  fi
}

manifest() {
  core_paths > "$work/paths"
  git_core hash-object --stdin-paths < "$work/paths" > "$work/ids"
  paste -d '\t' "$work/paths" "$work/ids" | sort -t $'\t' -k1,1
}

staged_manifest() {
  git_core ls-files -s -z -- "${entries[@]}" | tr '\0' '\n' > "$work/stage"
  awk -F '\t' '{ split($1, entry, " "); if (entry[3] == 0) print $2 "\t" entry[2] }' "$work/stage" \
    | drop_build_output_and_editor_files \
    | sort -t $'\t' -k1,1
}

digest() {
  local sum
  sum=$(sha256sum < "$1") || return
  printf 'sha256:%s\n' "${sum%% *}"
}

compute() {
  manifest > "$work/manifest"
  digest "$work/manifest"
}

compute_staged() {
  staged_manifest > "$work/manifest"
  digest "$work/manifest"
}

lock_value() {
  if [[ -f "$1" ]]; then
    awk -v key="$2" 'index($0, key "=") == 1 { value = substr($0, length(key) + 2); sub(/\r$/, "", value); print value; exit }' "$1"
  fi
}

read_version() {
  if [[ -f "$1" ]]; then
    tr -d '[:space:]' < "$1"
  fi
}

describe_changes() {
  local changes
  changes=$(git_core status --porcelain --untracked-files=all -- "${entries[@]}")
  if [[ -n "$changes" ]]; then
    echo "core-hash: uncommitted changes in core files:"
    printf '%s\n' "$changes" | sed 's/^/  /'
  fi
}

describe_ignored() {
  git_core ls-files -z --others --ignored --exclude-standard -- "${entries[@]}" | tr '\0' '\n' | sort > "$work/ignored"
  comm -12 "$work/paths" "$work/ignored" > "$work/counted"
  if [[ -s "$work/counted" ]]; then
    echo "core-hash: core files that git ignores, which count all the same:"
    sed 's/^/  /' "$work/counted"
  fi
}

describe_working_tree() {
  describe_changes
  describe_ignored
}

verify() {
  local hash=$1 lock=$2 version_path=$3 core_name=$4 lock_name=$5 describe=$6 locked_hash locked_version version
  locked_hash=$(lock_value "$lock" hash)
  locked_version=$(lock_value "$lock" version)
  version=$(read_version "$version_path")

  if [[ ! "$hash" =~ $digest_pattern ]]; then
    refuse "the hash of $core_name came out as '$hash', which is not sha256: followed by 64 lowercase hex digits"
  fi
  if [[ -n "$locked_hash" && ! "$locked_hash" =~ $digest_pattern ]]; then
    refuse "$lock_name holds the hash '$locked_hash', which is not sha256: followed by 64 lowercase hex digits"
  fi
  if [[ -z "$locked_hash" || "$locked_hash" != "$hash" || "$locked_version" != "$version" ]]; then
    advice >&2
    echo "core-hash: $lock_name holds version ${locked_version:-none} and ${locked_hash:-no hash};" \
      "$core_name is version ${version:-none} with $hash" >&2
    "$describe" >&2
    exit 1
  fi

  echo "core-hash: $core_name matches $lock_name, version $version"
}

check() {
  local hash
  hash=$(compute)
  verify "$hash" "$lock_file" "$version_file" "the core" "$lock_file" describe_working_tree
}

check_staged() {
  local hash
  hash=$(compute_staged)
  copy_staged "$lock_file" "$work/lock"
  copy_staged "$version_file" "$work/version"
  verify "$hash" "$work/lock" "$work/version" "the staged core" "the staged $lock_file" describe_changes
}

case "${1:-}" in
  print)
    load_entries "$list_file" "$list_file"
    compute
    ;;
  check)
    load_entries "$list_file" "$list_file"
    check
    ;;
  check-staged)
    copy_staged "$list_file" "$work/list"
    load_entries "$work/list" "the staged $list_file"
    check_staged
    ;;
  list)
    load_entries "$list_file" "$list_file"
    core_paths
    ;;
  entries)
    load_entries "$list_file" "$list_file"
    printf '%s\n' "${entries[@]}"
    ;;
  *)
    echo "usage: scripts/core-hash.sh print|check|check-staged|list|entries" >&2
    exit 2
    ;;
esac
