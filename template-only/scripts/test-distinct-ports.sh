#!/usr/bin/env bash
set -uo pipefail
shopt -s inherit_errexit

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
work=$(mktemp -d)
trap 'rm -rf "${work:?}"' EXIT

passes=0
failures=0

pass() {
  passes=$((passes + 1))
  echo "PASS: $1"
}

fail() {
  failures=$((failures + 1))
  echo "FAIL: $1 ($2)"
}

expect_equal() {
  if [[ "$2" == "$3" ]]; then
    pass "$1"
  else
    fail "$1" "expected '$3', got '$2'"
  fi
}

expect_in_range() {
  if [[ $2 =~ ^[0-9]+$ && $2 -ge $3 && $2 -le $4 && $2 -ne $5 ]]; then
    pass "$1"
  else
    fail "$1" "'$2' is not a new port in $3-$4"
  fi
}

bootstrapper_file=src/Bootstrapper/X.Bootstrapper/Properties/launchSettings.json
apphost_file=src/Aspire/X.AppHost/Properties/launchSettings.json

make_app() {
  local app=$1 bootstrapper=$2 apphost=$3 resource=$4
  mkdir -p "$app/src/Bootstrapper/X.Bootstrapper/Properties" "$app/src/Aspire/X.AppHost/Properties"
  cat > "$app/src/Bootstrapper/X.Bootstrapper/Properties/launchSettings.json" <<JSON
{
  "profiles": {
    "http": { "applicationUrl": "http://localhost:$bootstrapper" },
    "https": { "applicationUrl": "https://localhost:$((bootstrapper + 2000));http://localhost:$bootstrapper" }
  }
}
JSON
  cat > "$app/src/Aspire/X.AppHost/Properties/launchSettings.json" <<JSON
{
  "profiles": {
    "https": {
      "applicationUrl": "https://localhost:$((apphost + 2000));http://localhost:$apphost",
      "environmentVariables": {
        "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL": "https://localhost:$((apphost + 6000))",
        "ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL": "https://localhost:$((resource + 2000))"
      }
    },
    "http": {
      "applicationUrl": "http://localhost:$apphost",
      "environmentVariables": {
        "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL": "http://localhost:$((apphost + 4000))",
        "ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL": "http://localhost:$resource"
      }
    }
  }
}
JSON
}

checksum() {
  find "$1" -type f -print0 | sort -z | xargs -0 cat | cksum
}

apphost_resource_port() {
  sed -n -E 's/.*"ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL": "http:\/\/localhost:([0-9]+)".*/\1/p' \
    "$1/src/Aspire/X.AppHost/Properties/launchSettings.json"
}

bootstrapper_port() {
  sed -n -E 's/.*"http": \{ "applicationUrl": "http:\/\/localhost:([0-9]+)".*/\1/p' \
    "$1/src/Bootstrapper/X.Bootstrapper/Properties/launchSettings.json"
}

make_app "$work/a" 5180 15221 20105
make_app "$work/b" 5181 15222 20106
before_a=$(checksum "$work/a")
before_b=$(checksum "$work/b")
output=$(bash "$script_dir/distinct-ports.sh" "$work/a" "$work/b" 2>&1)
expect_equal "no collision prints nothing" "$output" ""
expect_equal "no collision leaves the first app byte-identical" "$(checksum "$work/a")" "$before_a"
expect_equal "no collision leaves the second app byte-identical" "$(checksum "$work/b")" "$before_b"

make_app "$work/c" 5180 15223 20105
before_a=$(checksum "$work/a")
output=$(bash "$script_dir/distinct-ports.sh" "$work/a" "$work/c" 2>&1)
status=$?
expect_equal "collisions are resolved with exit 0" "$status" "0"
expect_equal "the first app is untouched after collisions" "$(checksum "$work/a")" "$before_a"
new_resource=$(apphost_resource_port "$work/c")
new_bootstrapper=$(bootstrapper_port "$work/c")
expect_in_range "the AppHost resource service port moved inside its range" "$new_resource" 20000 20300 20105
expect_in_range "the Bootstrapper port moved inside its range" "$new_bootstrapper" 5000 5300 5180
expect_equal "the derived https resource service port moved too" \
  "$(grep -c "localhost:22105" "$work/c/$apphost_file")" "0"
expect_equal "the derived Bootstrapper https port moved too" \
  "$(grep -c "localhost:7180" "$work/c/$bootstrapper_file")" "0"
expect_equal "each moved port is reported once per file" "$(grep -c '^distinct-ports: c: ' <<< "$output")" "4"
expect_equal "the resource service report names old and new" \
  "$(grep -c "^distinct-ports: c: 20105 -> $new_resource in " <<< "$output")" "1"
expect_equal "the Bootstrapper port is rewritten in both profiles" \
  "$(grep -c "localhost:$new_bootstrapper" "$work/c/$bootstrapper_file")" "2"
expect_equal "the ports the second app kept stay" \
  "$(grep -c "localhost:15223" "$work/c/$apphost_file")" "2"

expect_equal "a second run on the resolved apps prints nothing" \
  "$(bash "$script_dir/distinct-ports.sh" "$work/a" "$work/c" 2>&1)" ""

echo "test-distinct-ports: $passes passed, $failures failed"
[[ $failures -eq 0 ]]
