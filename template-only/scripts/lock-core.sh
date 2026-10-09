#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit

cd "$(git rev-parse --show-toplevel)"

version_file=src/Core/VERSION
lock_file=core.lock

if bash scripts/core-hash.sh check > /dev/null 2>&1; then
  echo "lock-core: the core matches $lock_file, version $(< "$version_file"); nothing to record"
  exit 0
fi

version=$(tr -d '[:space:]' < "$version_file")
if [[ ! "$version" =~ ^[0-9]+$ ]]; then
  echo "lock-core: $version_file must hold one non-negative integer, found '$version'" >&2
  exit 1
fi

next=$((version + 1))
echo "$next" > "$version_file"
hash=$(bash scripts/core-hash.sh print)
printf 'version=%s\nhash=%s\n' "$next" "$hash" > "$lock_file"
echo "lock-core: recorded the changed core as version $next, $hash"
