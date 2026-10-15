#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit nullglob

hooks_path=.githooks
app_host_pattern=src/Aspire/*.AppHost/AppHost.cs
database_name_budget=30
rule_links=(
  AGENTS.md=AGENTS.md
  CLAUDE.md=AGENTS.md
  STYLE.md=STYLE.md
  docs/engineering-principles.md=engineering-principles.md
  docs/playbooks=playbooks
)
usage="usage: bash scripts/init.sh [--no-rules | --rules-only]"

main() {
  read_options "$@"
  clear_git_routing
  root=$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)
  cd "$root"
  if [[ $mode != no-rules ]]; then
    check_rulebook_conflict
  fi
  if [[ $mode == rules-only ]]; then
    require_repository
    link_rules
    return
  fi
  check_app_name
  create_repository
  set_hooks_path
  make_executable
  if [[ $mode == no-rules ]]; then
    echo "init: skipped the rules links (--no-rules)"
  else
    link_rules
  fi
  first_build
}

read_options() {
  mode=all
  if [[ $# -gt 1 ]]; then
    echo "init: too many arguments: $*; $usage" >&2
    exit 2
  fi
  case "${1-}" in
    '') ;;
    --no-rules) mode=no-rules ;;
    --rules-only) mode=rules-only ;;
    *)
      echo "init: unknown argument $1; $usage" >&2
      exit 2
      ;;
  esac
}

clear_git_routing() {
  local names variables
  names=$(git rev-parse --local-env-vars)
  mapfile -t variables <<< "$names"
  unset -- "${variables[@]}"
}

check_app_name() {
  local app_host name problem
  app_host=$(app_host_file)
  name=$(database_name "$app_host")
  problem=$(name_problem "$name")
  if [[ -n "$problem" ]]; then
    echo "init: the PostgreSQL form of the app name, $name (the database name in $app_host), $problem; remove $root and create the app again with another name" >&2
    exit 1
  fi
  echo "init: the PostgreSQL form of the app name, $name, is a valid database name"
}

app_host_file() {
  local files=($app_host_pattern)
  if [[ ${#files[@]} -ne 1 ]]; then
    echo "init: expected one $app_host_pattern, found ${#files[@]}: ${files[*]:-none}" >&2
    exit 1
  fi
  printf '%s\n' "${files[0]}"
}

database_name() {
  local names
  mapfile -t names < <(sed -nE 's/.*databaseName:[[:space:]]*"([^"]*)".*/\1/p' "$1")
  if [[ ${#names[@]} -ne 1 ]]; then
    echo "init: cannot read the database name from $1: expected one databaseName: \"<name>\", found ${#names[@]}" >&2
    exit 1
  fi
  printf '%s\n' "${names[0]}"
}

name_problem() {
  local LC_ALL=C name=$1
  if [[ $name =~ ^[0-9] ]]; then
    echo "starts with a digit, and SQL can name such a database only in double quotes"
  elif [[ ! $name =~ ^[a-z0-9]+(_[a-z0-9]+)*$ ]]; then
    echo "must hold only a-z and 0-9 in groups joined by single underscores, so that the module roles <database>__<schema> split one way"
  elif [[ $name == pg || $name == pg_* ]]; then
    echo "is pg or starts with pg_, which PostgreSQL reserves for its own roles"
  elif [[ ${#name} -gt $database_name_budget ]]; then
    echo "has ${#name} characters, and init allows at most $database_name_budget so that every module login <database>__<schema> keeps room for the schema within the 63 characters of a PostgreSQL role name"
  fi
}

create_repository() {
  local top
  if ! top=$(git rev-parse --show-toplevel 2> /dev/null); then
    git init -q -b main
    echo "init: created a git repository on branch main"
    return
  fi
  check_repository_root "$top"
  echo "init: the git repository exists"
}

require_repository() {
  local top
  if ! top=$(git rev-parse --show-toplevel 2> /dev/null); then
    echo "init: $root is not a git repository; run bash scripts/init.sh to create it" >&2
    exit 1
  fi
  check_repository_root "$top"
}

check_repository_root() {
  if [[ "$(CDPATH='' cd -- "$1" && pwd -P)" != "$root" ]]; then
    echo "init: $root is inside the git repository $1; the app must be the root of its own repository" >&2
    exit 1
  fi
}

set_hooks_path() {
  if [[ "$(git config --local --get core.hooksPath || true)" == "$hooks_path" ]]; then
    echo "init: core.hooksPath is already $hooks_path"
    return
  fi
  git config --local core.hooksPath "$hooks_path"
  echo "init: set core.hooksPath to $hooks_path"
}

make_executable() {
  local file pending=()
  for file in gate.sh scripts/*.sh "$hooks_path"/*; do
    if [[ -f "$file" && ! -x "$file" ]]; then
      pending+=("$file")
    fi
  done
  if [[ ${#pending[@]} -eq 0 ]]; then
    echo "init: gate.sh, scripts/*.sh and $hooks_path/* are already executable"
    return
  fi
  chmod +x -- "${pending[@]}"
  echo "init: made executable: ${pending[*]}"
}

link_rules() {
  local rulebook exclude pair linked=() excluded=() pending=()
  for pair in "${rule_links[@]}"; do
    if [[ -n ${RULEBOOK_PATH-} ]] || ! kept_rulebook "${pair%%=*}" "${pair#*=}" > /dev/null; then
      pending+=("$pair")
    fi
  done
  rulebook=$(select_rulebook)
  if [[ ${#pending[@]} -gt 0 ]]; then
    check_rule_sources "$rulebook"
  fi
  check_link_places
  exclude=$(git rev-parse --git-path info/exclude)
  mkdir -p -- "$(dirname -- "$exclude")" || cannot "make the folder of $exclude"
  for pair in "${rule_links[@]}"; do
    exclude_rule "/${pair%%=*}" "$exclude"
  done
  for pair in "${pending[@]}"; do
    link_rule "${pair%%=*}" "$rulebook/${pair#*=}"
  done
  report_rules "$rulebook" "$exclude"
}

kept_rulebook() {
  local path=$1 source=$2 target book
  [[ -L "$path" ]] || return 1
  target=$(readlink -- "$path")
  if [[ $target != /* ]]; then
    target=$(dirname -- "$path")/$target
  fi
  [[ $target == */"$source" ]] || return 1
  book=${target%/"$source"}
  [[ -f "$book/check-markers.cs" && -z "$(missing_rule_sources "$book")" ]] || return 1
  (CDPATH='' cd -- "$book" && pwd -P)
}

tree_rulebooks() {
  local pair
  for pair in "${rule_links[@]}"; do
    kept_rulebook "$1${pair%%=*}" "${pair#*=}" || true
  done | sort -u
}

check_rulebook_conflict() {
  local books main_folder
  if [[ -n ${RULEBOOK_PATH-} ]]; then
    return
  fi
  mapfile -t books < <(tree_rulebooks '')
  if [[ ${#books[@]} -gt 1 ]]; then
    echo "init: the rules links point to different rulebooks: ${books[*]}; set RULEBOOK_PATH to the one to keep and run init again" >&2
    exit 1
  fi
  if [[ ${#books[@]} -eq 1 ]] || ! main_folder=$(own_main_tree); then
    return
  fi
  mapfile -t books < <(tree_rulebooks "$main_folder/")
  if [[ ${#books[@]} -gt 1 ]]; then
    echo "init: the rules links of the main worktree $main_folder point to different rulebooks: ${books[*]}; set RULEBOOK_PATH to the one to keep and run init again" >&2
    exit 1
  fi
}

own_main_tree() {
  local top
  top=$(git rev-parse --show-toplevel 2> /dev/null) || return 1
  [[ "$(CDPATH='' cd -- "$top" && pwd -P)" == "$root" ]] || return 1
  main_tree
}

select_rulebook() {
  local books
  if [[ -z ${RULEBOOK_PATH-} ]]; then
    mapfile -t books < <(tree_rulebooks '')
    if [[ ${#books[@]} -eq 0 ]]; then
      mapfile -t books < <(tree_rulebooks "$(main_tree)/")
    fi
    if [[ ${#books[@]} -eq 1 ]]; then
      printf '%s\n' "${books[0]}"
      return
    fi
  fi
  find_rulebook
}

main_tree() {
  local common
  common=$(git rev-parse --path-format=absolute --git-common-dir)
  dirname -- "$common"
}

find_rulebook() {
  local path
  path=${RULEBOOK_PATH:-$(default_rulebook)}
  if [[ ! -d "$path" ]]; then
    echo "init: no rulebook at $path; clone Tsikhanau.Rulebook there or set RULEBOOK_PATH to its folder, or run init with --no-rules to skip the rules links" >&2
    exit 1
  fi
  CDPATH='' cd -- "$path" && pwd -P
}

default_rulebook() {
  printf '%s/Tsikhanau.Rulebook\n' "$(dirname -- "$(main_tree)")"
}

check_rule_sources() {
  local missing
  missing=$(missing_rule_sources "$1")
  if [[ -n "$missing" ]]; then
    echo "init: the rulebook $1 has no ${missing//$'\n'/ }" >&2
    exit 1
  fi
}

missing_rule_sources() {
  local pair
  for pair in "${rule_links[@]}"; do
    if [[ ! -e "$1/${pair#*=}" ]]; then
      printf '%s\n' "${pair#*=}"
    fi
  done
}

check_link_places() {
  local pair path folder blocked=() folders=()
  for pair in "${rule_links[@]}"; do
    path=${pair%%=*}
    folder=$(file_in_folder_place "$path")
    if [[ -n "$folder" && " ${folders[*]} " != *" $folder "* ]]; then
      folders+=("$folder")
    elif [[ -z "$folder" && -e "$path" && ! -L "$path" ]]; then
      blocked+=("$path")
    fi
  done
  report_blocked "a file where a folder for the rules links belongs" "${folders[@]}"
  report_blocked "not a link to the rulebook" "${blocked[@]}"
  if [[ ${#folders[@]} -gt 0 || ${#blocked[@]} -gt 0 ]]; then
    exit 1
  fi
}

file_in_folder_place() {
  local folder
  folder=$(dirname -- "$1")
  while [[ $folder != . ]]; do
    if [[ (-e "$folder" || -L "$folder") && ! -d "$folder" ]]; then
      printf '%s\n' "$folder"
      return
    fi
    folder=$(dirname -- "$folder")
  done
}

report_blocked() {
  if [[ $# -gt 1 ]]; then
    echo "init: $1, left as it is: ${*:2}; move it away and run init again" >&2
  fi
}

link_rule() {
  local path=$1 target=$2
  if [[ -L "$path" && "$(readlink -- "$path")" == "$target" ]]; then
    return
  fi
  mkdir -p -- "$(dirname -- "$path")" || cannot "make the folder of $path"
  ln -sfn -- "$target" "$path" || cannot "link $path to $target"
  linked+=("$path")
}

exclude_rule() {
  local line=$1 file=$2 separator=""
  if [[ -f "$file" ]] && grep -qxF -- "$line" "$file"; then
    return
  fi
  if [[ -s "$file" && -n "$(tail -c 1 -- "$file")" ]]; then
    separator=$'\n'
  fi
  printf '%s%s\n' "$separator" "$line" >> "$file" || cannot "add $line to $file"
  excluded+=("$line")
}

cannot() {
  echo "init: cannot $1" >&2
  exit 1
}

report_rules() {
  if [[ ${#linked[@]} -gt 0 ]]; then
    echo "init: linked to the rulebook $1: ${linked[*]}"
  elif [[ $mode != rules-only ]]; then
    echo "init: the rules links to $1 are in place"
  fi
  if [[ ${#excluded[@]} -gt 0 ]]; then
    echo "init: added to $2: ${excluded[*]}"
  fi
}

first_build() {
  local solutions=(*.slnx)
  if [[ ${#solutions[@]} -ne 1 ]]; then
    echo "init: the root must hold exactly one *.slnx, found ${#solutions[@]}: ${solutions[*]:-none}" >&2
    exit 1
  fi
  echo "init: building ${solutions[0]}"
  dotnet build "${solutions[0]}" -v quiet -nologo
  echo "init: built ${solutions[0]}"
}

main "$@"
