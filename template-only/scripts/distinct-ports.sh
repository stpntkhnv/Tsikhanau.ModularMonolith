#!/usr/bin/env bash
set -euo pipefail
shopt -s inherit_errexit nullglob
export LC_ALL=C

ranges=("5000 5300" "7000 7300" "15000 15300" "17000 17300" "19000 19300" "20000 20300" "21000 21300" "22000 22300")

main() {
  if [[ $# -ne 2 ]]; then
    echo "usage: distinct-ports.sh <first app dir> <second app dir>" >&2
    exit 2
  fi
  command -v ss > /dev/null || fail "ss is needed to see which ports are listening"
  local first=$1 second=$2 port replacement
  declare -gA taken=()
  declare -gA listening=()
  read_listening
  for port in $(ports_of "$first") $(ports_of "$second"); do
    taken[$port]=1
  done
  for port in $(shared_ports "$first" "$second"); do
    replacement=$(next_free "$port")
    taken[$replacement]=1
    rewrite "$second" "$port" "$replacement"
  done
}

fail() {
  echo "distinct-ports: $1" >&2
  exit 1
}

launch_files() {
  find "$1/src" -path '*/Properties/launchSettings.json' -type f | sort
}

ports_of() {
  local file
  while IFS= read -r file; do
    grep -o -E 'localhost:[0-9]+' "$file" || true
  done < <(launch_files "$1") | sed 's/^localhost://' | sort -u
}

shared_ports() {
  comm -12 <(ports_of "$1") <(ports_of "$2")
}

read_listening() {
  local port
  while IFS= read -r port; do
    listening[$port]=1
  done < <(ss -ltn | awk 'NR > 1 { n = split($4, parts, ":"); print parts[n] }')
}

next_free() {
  local old=$1 low high range candidate offset size
  for range in "${ranges[@]}"; do
    read -r low high <<< "$range"
    if [[ $old -ge $low && $old -le $high ]]; then
      size=$((high - low + 1))
      for ((offset = 1; offset < size; offset++)); do
        candidate=$((low + (old - low + offset) % size))
        if [[ -z ${taken[$candidate]:-} && -z ${listening[$candidate]:-} ]]; then
          echo "$candidate"
          return
        fi
      done
      fail "no free port left in $low-$high for $old"
    fi
  done
  fail "port $old is in none of the generator ranges"
}

rewrite() {
  local app=$1 old=$2 new=$3 file
  while IFS= read -r file; do
    if grep -q -E "localhost:$old\b" "$file"; then
      sed -i -E "s/localhost:$old\b/localhost:$new/g" "$file"
      echo "distinct-ports: ${app##*/}: $old -> $new in $file"
    fi
  done < <(launch_files "$app")
}

main "$@"
