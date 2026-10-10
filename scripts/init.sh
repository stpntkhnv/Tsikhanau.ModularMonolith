#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit nullglob

hooks_path=.githooks
host_settings_pattern=src/Bootstrapper/*.Bootstrapper/appsettings.json
login_prefix_budget=30
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
  root=$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)
  cd "$root"
  if [[ $mode == rules-only ]]; then
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

check_app_name() {
  local settings prefix problem
  settings=$(host_settings)
  prefix=$(login_prefix "$settings")
  problem=$(name_problem "$prefix")
  if [[ -n "$problem" ]]; then
    echo "init: the PostgreSQL form of the app name, $prefix (the database name and App:Database:LoginPrefix in $settings), $problem; remove $root and create the app again with another name" >&2
    exit 1
  fi
  echo "init: the PostgreSQL form of the app name, $prefix, is a valid database name and login prefix"
}

host_settings() {
  local files=($host_settings_pattern)
  if [[ ${#files[@]} -ne 1 ]]; then
    echo "init: expected one $host_settings_pattern, found ${#files[@]}: ${files[*]:-none}" >&2
    exit 1
  fi
  printf '%s\n' "${files[0]}"
}

login_prefix() {
  local prefixes
  mapfile -t prefixes < <(sed -nE 's/^[[:space:]]*"LoginPrefix"[[:space:]]*:[[:space:]]*"([^"]*)".*$/\1/p' "$1")
  if [[ ${#prefixes[@]} -ne 1 ]]; then
    echo "init: cannot read App:Database:LoginPrefix from $1: expected one \"LoginPrefix\": \"<name>\" line, found ${#prefixes[@]}" >&2
    exit 1
  fi
  printf '%s\n' "${prefixes[0]}"
}

name_problem() {
  local LC_ALL=C name=$1
  if [[ $name =~ ^[0-9] ]]; then
    echo "starts with a digit, and these PostgreSQL names must start with a letter"
  elif [[ ! $name =~ ^[a-z][a-z0-9_]*$ ]]; then
    echo "must start with a letter and hold only a-z, 0-9 and _"
  elif [[ $name == pg || $name == pg_* ]]; then
    echo "is pg or starts with pg_, which PostgreSQL reserves"
  elif [[ ${#name} -gt $login_prefix_budget ]]; then
    echo "has ${#name} characters, and the core allows at most $login_prefix_budget so that every module login <prefix>_<schema> fits the 63 characters of a PostgreSQL role name"
  fi
}

create_repository() {
  local top
  if ! top=$(git rev-parse --show-toplevel 2> /dev/null); then
    git init -q -b main
    echo "init: created a git repository on branch main"
    return
  fi
  if [[ "$(CDPATH='' cd -- "$top" && pwd -P)" != "$root" ]]; then
    echo "init: $root is inside the git repository $top; the app must be the root of its own repository" >&2
    exit 1
  fi
  echo "init: the git repository exists"
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
  local rulebook exclude pair linked=() excluded=()
  rulebook=$(find_rulebook)
  check_rule_sources "$rulebook"
  check_link_places
  exclude=$(git rev-parse --git-path info/exclude)
  mkdir -p -- "$(dirname -- "$exclude")"
  for pair in "${rule_links[@]}"; do
    if link_rule "${pair%%=*}" "$rulebook/${pair#*=}"; then
      linked+=("${pair%%=*}")
    fi
    if exclude_rule "/${pair%%=*}" "$exclude"; then
      excluded+=("/${pair%%=*}")
    fi
  done
  report_rules "$rulebook" "$exclude"
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
  local common
  common=$(git rev-parse --path-format=absolute --git-common-dir)
  printf '%s/Tsikhanau.Rulebook\n' "$(dirname -- "$(dirname -- "$common")")"
}

check_rule_sources() {
  local pair missing=()
  for pair in "${rule_links[@]}"; do
    if [[ ! -e "$1/${pair#*=}" ]]; then
      missing+=("${pair#*=}")
    fi
  done
  if [[ ${#missing[@]} -gt 0 ]]; then
    echo "init: the rulebook $1 has no ${missing[*]}" >&2
    exit 1
  fi
}

check_link_places() {
  local pair path blocked=()
  for pair in "${rule_links[@]}"; do
    path=${pair%%=*}
    if [[ -e "$path" && ! -L "$path" ]]; then
      blocked+=("$path")
    fi
  done
  if [[ ${#blocked[@]} -gt 0 ]]; then
    echo "init: not a link to the rulebook, left as it is: ${blocked[*]}; move it away and run init again" >&2
    exit 1
  fi
}

link_rule() {
  local path=$1 target=$2
  if [[ -L "$path" && "$(readlink -- "$path")" == "$target" ]]; then
    return 1
  fi
  mkdir -p -- "$(dirname -- "$path")"
  ln -sfn -- "$target" "$path"
}

exclude_rule() {
  local line=$1 file=$2
  if [[ -f "$file" ]] && grep -qxF -- "$line" "$file"; then
    return 1
  fi
  if [[ -s "$file" && -n "$(tail -c 1 -- "$file")" ]]; then
    echo >> "$file"
  fi
  echo "$line" >> "$file"
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
