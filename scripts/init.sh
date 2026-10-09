#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit nullglob

hooks_path=.githooks

main() {
  root=$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)
  cd "$root"
  create_repository
  set_hooks_path
  make_executable
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

main "$@"
