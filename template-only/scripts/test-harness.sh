#!/usr/bin/env bash
set -uo pipefail
shopt -s inherit_errexit

script_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
runner=$script_dir/../Harness/run-mutations.sh
readonly mutation_total=53

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

expect_contains() {
  if [[ "$2" == *"$3"* ]]; then
    pass "$1"
  else
    fail "$1" "missing '$3' in: $(tail -n 5 <<< "$2")"
  fi
}

expect_not_contains() {
  if [[ "$2" != *"$3"* ]]; then
    pass "$1"
  else
    fail "$1" "unexpected '$3'"
  fi
}

clear_routing() {
  local names variables
  if ! names=$(git rev-parse --local-env-vars) || [[ -z $names ]]; then
    echo "test-harness: cannot list the git routing variables" >&2
    exit 1
  fi
  mapfile -t variables <<< "$names"
  unset -- "${variables[@]}"
}

content_state() {
  git -C "$1" rev-parse HEAD
  sha256sum < "$1/.git/index"
  (cd "$1" && git ls-files -z --cached --others --exclude-standard | sort -z | xargs -0 sha256sum)
}

scratch=$(mktemp -d)
trap 'rm -rf -- "${scratch:?}"' EXIT
clear_routing
printf '[maintenance]\n\tauto = false\n[gc]\n\tauto = 0\n' > "$scratch/gitconfig"
export GIT_CONFIG_GLOBAL=$scratch/gitconfig GIT_CONFIG_NOSYSTEM=1
export GIT_AUTHOR_NAME=test GIT_AUTHOR_EMAIL=test@example.com
export GIT_COMMITTER_NAME=test GIT_COMMITTER_EMAIL=test@example.com
fake_source=$scratch/source
harness=$fake_source/template-only/Harness
mkdir -p "$scratch/bin" "$scratch/elsewhere" "$scratch/patches" "$harness/scratch-commits"

cat > "$scratch/bin/dotnet" <<'FAKE'
#!/usr/bin/env bash
planted=$(cat planted 2> /dev/null)
case $1 in
  build)
    if [[ $2 != *Harness* && -n $planted && $planted == "${FAKE_BUILD_CATCH-}" ]]; then
      echo "$PWD/src/Fake/Planted.cs(1,1): error TS0001: planted mistake [$PWD/src/Fake/Fake.csproj]"
      exit 1
    fi
    ;;
  msbuild)
    echo "$FAKE_BIN/Tsikhanau.Monolith.Harness.dll"
    ;;
  test)
    skipped=1
    if [[ -n $planted && $planted == "${FAKE_EXTRA_SKIP-}" ]]; then
      skipped=2
    fi
    prefix=$'\033[m'
    if [[ " $* " == *" --no-ansi "* ]]; then
      prefix=
    fi
    if [[ $3 == */ArchTests && -n $planted && $planted == "${FAKE_TEST_CATCH-}" ]]; then
      echo "${prefix}failed Fake.ArchTests.Planted_mistake_fails(name: \"Fake\") (3ms)"
      printf '%sTest run summary: Failed!\n%s  skipped: %s\n' "$prefix" "$prefix" "$skipped"
      exit 2
    fi
    printf '%sTest run summary: Passed!\n%s  skipped: %s\n' "$prefix" "$prefix" "$skipped"
    ;;
  *.dll)
    if [[ " ${FAKE_UNAPPLIED-} " == *" $2 "* ]]; then
      echo "harness: pattern found 0 times, expected once"
      exit 1
    fi
    if [[ -n ${FAKE_TOUCH_SOURCE-} ]]; then
      echo touched >> "$FAKE_TOUCH_SOURCE"
    fi
    echo "$2" > planted
    echo "$2" >> src/Fake/tracked.txt
    echo "applied $2"
    ;;
esac
FAKE
chmod +x "$scratch/bin/dotnet"

mutation_names() {
  local number
  for ((number = 1; number <= mutation_total; number++)); do
    printf 'f%02d_fake_mutation\n' "$number"
  done
}

write_expectations() {
  mutation_names |
    sed -E "s/^(f01_fake_mutation)\$/\1 error TS0001/; s/^(f02_fake_mutation)\$/\1 failed $1/; s/_mutation\$/_mutation MISSED/"
}

make_patches() {
  local helper=$scratch/patch-source number
  git init -q "$helper"
  git -C "$helper" commit -q --allow-empty -m base
  mkdir -p "$helper/src/Fake"
  for number in 1 2 3 4 5; do
    printf 'scratch %s\n' "$number" > "$helper/src/Fake/scratch-$number.txt"
    git -C "$helper" add src/Fake
    git -C "$helper" commit -q -m "scratch $number"
  done
  git -C "$helper" format-patch -q -5 -o "$scratch/patches"
  cp "$scratch"/patches/0001-*.patch "$scratch"/patches/0002-*.patch "$scratch"/patches/0003-*.patch \
    "$harness/scratch-commits/"
  cp "$scratch"/patches/0004-scratch-4.patch "$harness/scratch-commits/0006-scratch-4.patch"
  cp "$scratch"/patches/0005-scratch-5.patch "$harness/scratch-commits/0007-scratch-5.patch"
}

git init -q "$fake_source"
mkdir -p "$fake_source/src/Fake"
printf 'committed\n' > "$fake_source/src/Fake/tracked.txt"
: > "$fake_source/App.slnx"
: > "$fake_source/template-only/Template.slnx"
mutation_names > "$harness/mutations54.txt"
write_expectations 'Fake.ArchTests.Planted_mistake_fails(name: "Fake")' > "$harness/expected.txt"
make_patches
git -C "$fake_source" add -A
git -C "$fake_source" commit -q -m source
printf 'changed in the working tree\n' > "$fake_source/src/Fake/tracked.txt"
printf 'not yet added\n' > "$fake_source/src/Fake/untracked.txt"
source_before=$(content_state "$fake_source")

run_routed() {
  (
    export GIT_DIR=$fake_source/.git GIT_WORK_TREE=$fake_source
    cd "$scratch/elsewhere" && PATH="$scratch/bin:$PATH" FAKE_BIN="$scratch/bin" bash "$runner" "$@" 2>&1
  )
}

expect_source_unchanged() {
  expect_equal "$1 leaves HEAD, index and working set files of the source unchanged" \
    "$(content_state "$fake_source")" "$source_before"
}

output=$(run_routed --dry-run "$fake_source" "$scratch/work-dry")
code=$?
expect_equal "a dry run of applying mutations exits 0" "$code" "0"
expect_contains "a dry run reports every mutation applied" "$output" "applied 53 of 53 mutations"
expect_source_unchanged "a successful dry run with GIT_DIR and GIT_WORK_TREE of the source"
expect_equal "the copy holds the modified tracked file, not HEAD" \
  "$(cat "$scratch/work-dry/repo/src/Fake/tracked.txt" 2> /dev/null)" "changed in the working tree"
expect_equal "the copy holds the untracked file" \
  "$(cat "$scratch/work-dry/repo/src/Fake/untracked.txt" 2> /dev/null)" "not yet added"
expect_equal "the copy has the five scratch commits on its working set commit" \
  "$(git -C "$scratch/work-dry/repo" rev-list --count HEAD 2> /dev/null)" "6"
expect_equal "the copy is clean after the last revert" \
  "$(git -C "$scratch/work-dry/repo" status --porcelain 2>&1 || echo "no repository")" ""

copy_files() {
  (cd "$1" && find . -path ./.git -prune -o -type f ! -name 'scratch-*.txt' -printf '%P\0' |
    sort -z | xargs -0 sha256sum --)
}

source_files() {
  (cd "$1" && git ls-files -z --cached --others --exclude-standard | sort -z | xargs -0 sha256sum --)
}

hostile=$scratch/hostile
mkdir -p "$hostile/hooks" "$hostile/template/hooks" "$hostile/home/.config/git"
for hook in "$hostile/hooks/post-checkout" "$hostile/template/hooks/post-checkout"; do
  printf '#!/usr/bin/env bash\necho "%s ran" >> "%s"\n' "$hook" "$hostile/hook-ran" > "$hook"
  chmod +x "$hook"
done
printf 'planted\n' > "$hostile/excludes"
printf '*.txt text eol=crlf\n' > "$hostile/home/.config/git/attributes"
cat > "$hostile/home/.gitconfig" <<CONFIG
[core]
  hooksPath = $hostile/hooks
  autocrlf = true
  excludesFile = $hostile/excludes
[init]
  templateDir = $hostile/template
CONFIG
output=$(HOME=$hostile/home XDG_CONFIG_HOME=$hostile/home/.config GIT_CONFIG_GLOBAL=$hostile/home/.gitconfig \
  GIT_TEMPLATE_DIR=$hostile/template run_routed --dry-run "$fake_source" "$scratch/work-hostile")
code=$?
expect_equal "a dry run under a hostile global git configuration exits 0" "$code" "0"
expect_equal "a dry run under a hostile global git configuration runs none of its hooks" \
  "$(cat "$hostile/hook-ran" 2> /dev/null)" ""
expect_equal "the copy under a hostile global git configuration gets no template hooks" \
  "$(ls -A "$scratch/work-hostile/repo/.git/hooks" 2> /dev/null)" ""
expect_equal "the copy under a hostile global git configuration holds the working set byte for byte" \
  "$(copy_files "$scratch/work-hostile/repo")" "$(source_files "$fake_source")"
expect_source_unchanged "a dry run under a hostile global git configuration"

output=$(FAKE_UNAPPLIED=f07_fake_mutation run_routed --dry-run "$fake_source" "$scratch/work-unapplied")
code=$?
expect_equal "a dry run with a mutation that does not apply exits 1" "$code" "1"
expect_contains "a dry run names the mutation that did not apply" "$output" \
  "f07_fake_mutation ERROR mutation did not apply"
expect_source_unchanged "a failing dry run"

cp "$harness/expected.txt" "$scratch/expected.txt"
sed -i -e '/^f05_fake_mutation /d' "$harness/expected.txt"
echo "f99_fake_mutation MISSED" >> "$harness/expected.txt"
source_before=$(content_state "$fake_source")
output=$(run_routed --dry-run "$fake_source" "$scratch/work-inventory")
code=$?
expect_equal "an inventory mismatch exits 1" "$code" "1"
expect_contains "an inventory mismatch names the mutation without an expectation" "$output" \
  "only in mutations54.txt: f05_fake_mutation"
expect_contains "an inventory mismatch names the expectation without a mutation" "$output" \
  "only in expected.txt: f99_fake_mutation"
expect_source_unchanged "an inventory mismatch"
cp "$scratch/expected.txt" "$harness/expected.txt"
source_before=$(content_state "$fake_source")

mkdir -p "$scratch/work-used"
: > "$scratch/work-used/leftover"
output=$(run_routed --dry-run "$fake_source" "$scratch/work-used")
code=$?
expect_equal "a work directory that is not empty is a usage error" "$code" "2"

output=$(run_routed --dry-run "$fake_source" "$fake_source/harness-work")
code=$?
expect_equal "a work directory inside the source is a usage error" "$code" "2"
expect_equal "a refused work directory inside the source is not created" \
  "$(test -e "$fake_source/harness-work" && echo created || echo absent)" "absent"
expect_source_unchanged "a refused work directory inside the source"

output=$(FAKE_TOUCH_SOURCE=$fake_source/src/Fake/untracked.txt \
  run_routed --dry-run "$fake_source" "$scratch/work-touched")
code=$?
expect_equal "a run during which the source changes exits 1" "$code" "1"
expect_contains "a run during which the source changes says so" "$output" "the source repository changed"
printf 'not yet added\n' > "$fake_source/src/Fake/untracked.txt"

output=$(FAKE_BUILD_CATCH=f01_fake_mutation FAKE_TEST_CATCH=f02_fake_mutation \
  run_routed "$fake_source" "$scratch/work-full")
code=$?
expect_equal "a full run that matches every expectation exits 0" "$code" "0"
expect_contains "a full run counts the build catch" "$output" "f01_fake_mutation CAUGHT build TS0001"
expect_contains "a full run counts the test catch" "$output" \
  'f02_fake_mutation CAUGHT ArchTests Fake.ArchTests.Planted_mistake_fails(name: "Fake")'
expect_contains "a full run keeps an expected miss" "$output" "f03_fake_mutation MISSED"
expect_contains "a full run sums up the run" "$output" "53 of 53 mutations as expected"
expect_source_unchanged "a successful full run"

output=$(FAKE_BUILD_CATCH=f01_fake_mutation FAKE_TEST_CATCH=f02_fake_mutation FAKE_EXTRA_SKIP=f03_fake_mutation \
  run_routed "$fake_source" "$scratch/work-full-skip")
code=$?
expect_equal "a full run with an extra skip on an expected miss exits 1" "$code" "1"
expect_contains "a full run names the moved skips" "$output" \
  "f03_fake_mutation ERROR skips moved: ArchTests skipped 2, baseline 1"
expect_source_unchanged "a failing full run"

output=$(FAKE_BUILD_CATCH=f04_fake_mutation FAKE_TEST_CATCH=f02_fake_mutation \
  run_routed "$fake_source" "$scratch/work-full-deviation")
code=$?
expect_equal "a full run with a deviation exits 1" "$code" "1"
expect_contains "a full run names a lost catch" "$output" \
  "f01_fake_mutation DEVIATION missed, expected error TS0001"
expect_contains "a full run names a new catch of an accepted miss" "$output" \
  "f04_fake_mutation DEVIATION caught by build, expected MISSED"

source "$runner"
set +e
results=$scratch/canned
mkdir -p "$results"
printf 'ArchTests 20\nArchRules.Tests 9\nIntegrationTests 2\n' > "$results/baseline.skips"

canned() {
  local mutation=$1
  local outcome=$2
  local skips=$3
  printf '%b\n' "$outcome" > "$results/$mutation"
  printf '%b\n' "$skips" > "$results/$mutation.skips"
}

baseline_skips='ArchTests 20\nArchRules.Tests 9\nIntegrationTests 2'
build_error='src/Note.cs(15,9): error RS0030: The symbol is banned\nCAUGHT build'
arch_failure='failed ModularMonolith.ArchTests.Rules.A(name: "Notes")\nCAUGHT ArchTests'
start_failure='failed ModularMonolith.IntegrationTests.T(name: "Notes")\nCAUGHT IntegrationTests'

canned present "$build_error" ""
expect_equal "an expected diagnostic id that appears is a catch" \
  "$(judge present 'error RS0030')" "CAUGHT build RS0030"

canned absent "$build_error" ""
expect_contains "an expected diagnostic id that does not appear is a deviation" \
  "$(judge absent 'error TS0005')" "DEVIATION expected error TS0005, caught by build: RS0030"

canned other_stage "$arch_failure" 'ArchTests 20'
expect_contains "an expected diagnostic id when a test caught it is a deviation" \
  "$(judge other_stage 'error RS0030')" "DEVIATION expected error RS0030, caught by ArchTests"

canned wrong_test "$arch_failure" 'ArchTests 20'
expect_contains "another failed test than the expected one is a deviation" \
  "$(judge wrong_test 'failed ModularMonolith.ArchTests.Rules.B(name: "Notes")')" "DEVIATION expected failed"

canned kept MISSED "$baseline_skips"
expect_equal "an accepted miss that stays missed with the baseline skips is kept" "$(judge kept MISSED)" "MISSED"

canned turned "$arch_failure" 'ArchTests 20'
expect_contains "an accepted miss that a test now catches is a deviation" \
  "$(judge turned MISSED)" "DEVIATION caught by ArchTests, expected MISSED"

canned extra_skip MISSED 'ArchTests 21\nArchRules.Tests 9\nIntegrationTests 2'
expect_equal "an accepted miss with an extra skip is an error" \
  "$(judge extra_skip MISSED)" "ERROR skips moved: ArchTests skipped 21, baseline 20"

canned fewer_skips MISSED 'ArchTests 20\nArchRules.Tests 8\nIntegrationTests 2'
expect_equal "an accepted miss with fewer skips is an error" \
  "$(judge fewer_skips MISSED)" "ERROR skips moved: ArchRules.Tests skipped 8, baseline 9"

canned caught_extra_skip "$arch_failure" 'ArchTests 21'
expect_equal "an expected failure plus an extra skip is an error" \
  "$(judge caught_extra_skip 'failed ModularMonolith.ArchTests.Rules.A(name: "Notes")')" \
  "ERROR skips moved: ArchTests skipped 21, baseline 20"

canned caught_after_stage "$start_failure" "$baseline_skips"
canned caught_after_stage "$start_failure" 'ArchTests 20\nArchRules.Tests 10\nIntegrationTests 0'
expect_equal "an extra skip in a stage that passed before the failing one is an error" \
  "$(judge caught_after_stage 'failed ModularMonolith.IntegrationTests.T(name: "Notes")')" \
  "ERROR skips moved: ArchRules.Tests skipped 10, baseline 9"

canned caught_fewer_skips "$arch_failure" 'ArchTests 18'
expect_equal "an expected failure with fewer skips is a catch" \
  "$(judge caught_fewer_skips 'failed ModularMonolith.ArchTests.Rules.A(name: "Notes")')" \
  'CAUGHT ArchTests ModularMonolith.ArchTests.Rules.A(name: "Notes")'

canned start "$start_failure" 'ArchTests 20\nArchRules.Tests 9\nIntegrationTests 0'
printf 'Assembly fixture threw\n  42501: permission denied for schema tags\n' > "$results/start.IntegrationTests"
expect_equal "an expected start failure message in the failing stage output is a catch" \
  "$(judge start 'output 42501: permission denied for schema tags')" \
  "CAUGHT IntegrationTests 42501: permission denied for schema tags"
expect_contains "another start failure message is a deviation" \
  "$(judge start 'output 42501: permission denied for schema public')" "DEVIATION expected output"

canned infrastructure 'ERROR build of App.slnx failed without a compiler error' ""
expect_equal "an infrastructure error stays an error" \
  "$(judge infrastructure 'error RS0030')" "ERROR build of App.slnx failed without a compiler error"

echo "test-harness: $passes passed, $failures failed"
[[ $failures -eq 0 ]]
