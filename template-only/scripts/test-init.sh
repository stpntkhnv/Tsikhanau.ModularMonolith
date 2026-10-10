#!/usr/bin/env bash
set -uo pipefail
shopt -s inherit_errexit

while IFS= read -r variable; do
  unset "$variable"
done < <(compgen -e | grep '^GIT_')
export LC_ALL=C GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1
export GIT_AUTHOR_NAME=test GIT_AUTHOR_EMAIL=test@example.com GIT_COMMITTER_NAME=test GIT_COMMITTER_EMAIL=test@example.com

if ! template_root=$(git -C "$(dirname -- "${BASH_SOURCE[0]}")" rev-parse --show-toplevel); then
  echo "test-init: cannot find the repository of the template" >&2
  exit 1
fi
if ! work=$(mktemp -d) || [[ ! -d "$work" ]] || ! work=$(cd -- "$work" && pwd -P); then
  echo "test-init: cannot create a temporary folder" >&2
  exit 1
fi
readonly work
rulebook=$work/rulebook
host_settings=src/Bootstrapper/ModularMonolith.Bootstrapper/appsettings.json
database_settings=src/Core/Infrastructure/Database/DatabaseSettings.cs
rule_links=(
  AGENTS.md=AGENTS.md
  CLAUDE.md=AGENTS.md
  STYLE.md=STYLE.md
  docs/engineering-principles.md=engineering-principles.md
  docs/playbooks=playbooks
)
passes=0
failures=0
status=0

trap 'rm -rf -- "$work"' EXIT
mkdir -p "$work/bin" "$work/cases" "$work/logs" "$rulebook/playbooks" "$work/home"
printf 'rules\n' > "$rulebook/AGENTS.md"
printf 'style\n' > "$rulebook/STYLE.md"
printf 'principles\n' > "$rulebook/engineering-principles.md"
printf 'module\n' > "$rulebook/playbooks/add-module.md"
printf '#!/usr/bin/env bash\nprintf "%%s\\n" "$*" >> "${DOTNET_CALLS:?}"\n' > "$work/bin/dotnet"
chmod +x "$work/bin/dotnet"
export PATH=$work/bin:$PATH HOME=$work/home RULEBOOK_PATH=$rulebook

fail() {
  echo "$*" >&2
  return 1
}

new_app() {
  local app=$work/cases/$1/app
  mkdir -p "$app/scripts" "$app/.githooks" "$app/src/Bootstrapper/App.Bootstrapper" "$app/.claude"
  cp "$template_root/gate.sh" "$app/"
  cp "$template_root"/scripts/*.sh "$app/scripts/"
  cp "$template_root"/.githooks/* "$app/.githooks/"
  chmod -x "$app/gate.sh" "$app"/scripts/*.sh "$app"/.githooks/*
  sed "s/\"modularmonolith\"/\"$2\"/" "$template_root/$host_settings" > "$app/src/Bootstrapper/App.Bootstrapper/appsettings.json"
  printf '<Solution />\n' > "$app/App.slnx"
  printf '{}\n' > "$app/.claude/settings.json"
  : > "$(dotnet_calls "$app")"
  printf '%s\n' "$app"
}

dotnet_calls() {
  printf '%s\n' "${1%/*}/dotnet-calls"
}

run_init() {
  local app=$1
  shift
  status=0
  (cd "$app" && DOTNET_CALLS=$(dotnet_calls "$app") bash scripts/init.sh "$@") > "$work/out" 2>&1 || status=$?
  cat "$work/out"
}

run_in() {
  local folder=$1
  shift
  status=0
  (cd "$folder" && "$@") > "$work/out" 2>&1 || status=$?
  cat "$work/out"
}

expect_success() {
  [[ $status -eq 0 ]] || fail "the command failed with exit code $status"
}

expect_refused() {
  [[ $status -ne 0 ]] || fail "init did not refuse"
  expect_said "$1"
}

expect_said() {
  grep -qE -- "$1" "$work/out" || fail "the output does not say: $1"
}

expect_rule_links() {
  local pair target
  for pair in "${rule_links[@]}"; do
    target=$(readlink -- "$1/${pair%%=*}") || fail "${pair%%=*} is not a link"
    [[ "$target" == "$rulebook/${pair#*=}" ]] || fail "${pair%%=*} points to $target, not $rulebook/${pair#*=}"
    git -C "$1" check-ignore -q -- "${pair%%=*}" || fail "git sees ${pair%%=*}"
  done
}

expect_no_rule_links() {
  local pair
  for pair in "${rule_links[@]}"; do
    [[ ! -e "$1/${pair%%=*}" && ! -L "$1/${pair%%=*}" ]] || fail "${pair%%=*} exists"
  done
}

expect_excluded_in() {
  local pair
  for pair in "${rule_links[@]}"; do
    grep -qxF -- "/${pair%%=*}" "$1" || fail "$1 has no line /${pair%%=*}"
  done
}

expect_untouched_claude() {
  [[ "$(cd "$1/.claude" && find . -mindepth 1 -printf '%p\n')" == ./settings.json ]] || fail "init changed .claude/"
  [[ "$(< "$1/.claude/settings.json")" == '{}' ]] || fail "init changed .claude/settings.json"
}

snapshot() {
  (
    cd "$1"
    find . -path ./.git -prune -o -printf '%m %y %s %p -> %l\n' | sort
    git config --local --list
    cat .git/info/exclude
  )
}

commit_all() {
  git -C "$1" add -A
  git -C "$1" commit -q --no-verify -m app
}

snapshot_repository() {
  find "$1" -printf '%y %s %p\n' | sort
  cat "$1/.git/config"
}

core_budget() {
  local budget
  budget=$(sed -nE 's/.*\[StringLength\(([0-9]+),.*/\1/p' "$template_root/$database_settings")
  [[ "$budget" =~ ^[0-9]+$ ]] || fail "cannot read the login prefix length from $database_settings"
  printf '%s\n' "$budget"
}

letters() {
  printf 'a%.0s' $(seq 1 "$1")
}

case_digit_start() {
  local app
  app=$(new_app digit 1shop)
  run_init "$app"
  expect_refused 'the PostgreSQL form of the app name, 1shop .*, starts with a digit'
  expect_said "remove $app and create the app again with another name"
  [[ ! -e "$app/.git" ]] || fail "a refused name must stop before the repository is created"
  [[ ! -s "$(dotnet_calls "$app")" ]] || fail "a refused name must stop before the build"
}

case_budget() {
  local app budget
  budget=$(core_budget)
  app=$(new_app over "$(letters $((budget + 1)))")
  run_init "$app"
  expect_refused "has $((budget + 1)) characters, and the core allows at most $budget "
  [[ ! -e "$app/.git" ]] || fail "a refused name must stop before the repository is created"
  app=$(new_app at "$(letters "$budget")")
  run_init "$app" --no-rules
  expect_success
}

case_fresh_repository() {
  local app hooks file
  app=$(new_app fresh acme_shop)
  run_init "$app"
  expect_success
  [[ "$(head -n 1 "$work/out")" == *'app name, acme_shop, is a valid'* ]] || fail "the name check must come first"
  [[ "$(git -C "$app" rev-parse --show-toplevel)" == "$app" ]] || fail "no repository at the app root"
  [[ "$(git -C "$app" symbolic-ref --short HEAD)" == main ]] || fail "the branch is not main"
  hooks=$(git -C "$app" config --local --get core.hooksPath) || fail "core.hooksPath is not set"
  [[ "$hooks" == .githooks ]] || fail "core.hooksPath is $hooks"
  for file in "$app/gate.sh" "$app"/scripts/*.sh "$app"/.githooks/*; do
    [[ -x "$file" ]] || fail "$file is not executable"
  done
  [[ -x "$app/.githooks/post-checkout" ]] || fail "the post-checkout hook is missing"
  expect_rule_links "$app"
  expect_excluded_in "$app/.git/info/exclude"
  [[ "$(< "$(dotnet_calls "$app")")" == 'build App.slnx -v quiet -nologo' ]] || fail "init did not build App.slnx once"
  expect_untouched_claude "$app"
}

case_repeat_run() {
  local app
  app=$(new_app repeat acme_shop)
  run_init "$app"
  expect_success
  snapshot "$app" > "$work/first"
  run_init "$app"
  expect_success
  expect_said 'rules links to .* are in place'
  ! grep -qF 'added to' "$work/out" || fail "a second run added exclude lines"
  snapshot "$app" > "$work/second"
  diff -- "$work/first" "$work/second" || fail "a second run changed the app"
}

case_worktree() {
  local app worktree=$work/cases/worktree/app/.worktrees/tree
  app=$(new_app worktree acme_shop)
  run_init "$app" --no-rules
  expect_success
  commit_all "$app"
  git -C "$app" -c core.hooksPath=/dev/null worktree add -q "$worktree" -b other
  [[ -f "$worktree/.git" ]] || fail "the worktree has no .git file"
  ln -s "$rulebook" "$work/cases/worktree/Tsikhanau.Rulebook"
  RULEBOOK_PATH='' run_init "$worktree"
  expect_success
  expect_rule_links "$worktree"
  expect_excluded_in "$app/.git/info/exclude"
  expect_no_rule_links "$app"
}

case_dangling_link() {
  local app
  app=$(new_app dangling acme_shop)
  ln -s "$work/gone/AGENTS.md" "$app/AGENTS.md"
  run_init "$app"
  expect_success
  expect_said 'linked to the rulebook .*: AGENTS.md '
  expect_rule_links "$app"
}

case_file_in_place_of_a_link() {
  local app
  app=$(new_app regular acme_shop)
  printf 'mine\n' > "$app/STYLE.md"
  mkdir -p "$app/docs/playbooks"
  run_init "$app"
  expect_refused 'not a link to the rulebook, left as it is: STYLE.md docs/playbooks; move it away'
  [[ "$(< "$app/STYLE.md")" == mine ]] || fail "init changed STYLE.md"
  [[ ! -L "$app/AGENTS.md" ]] || fail "a refusal must come before the first link"
  [[ ! -s "$(dotnet_calls "$app")" ]] || fail "a refusal must stop before the build"
}

case_file_in_place_of_a_folder() {
  local app
  app=$(new_app docs-file acme_shop)
  printf 'mine\n' > "$app/docs"
  run_init "$app"
  expect_refused 'a file where a folder for the rules links belongs, left as it is: docs; move it away'
  [[ "$(< "$app/docs")" == mine ]] || fail "init changed docs"
  [[ ! -L "$app/AGENTS.md" ]] || fail "a refusal must come before the first link"
  [[ ! -s "$(dotnet_calls "$app")" ]] || fail "a refusal must stop before the build"
}

case_unwritable_exclude() {
  local app
  app=$(new_app read-only acme_shop)
  run_init "$app" --no-rules
  expect_success
  mkdir -p "$app/.git/info"
  touch "$app/.git/info/exclude"
  chmod a-w "$app/.git/info/exclude"
  run_init "$app" --rules-only
  expect_refused 'cannot add /AGENTS.md to .*info/exclude'
  expect_no_rule_links "$app"
}

case_inherited_git_dir() {
  local app linked other=$work/cases/routed/other
  git init -q "$other"
  snapshot_repository "$other" > "$work/other-before"
  app=$(new_app routed acme_shop)
  GIT_DIR=$other/.git GIT_INDEX_FILE=$other/.git/index run_init "$app"
  expect_success
  [[ "$(git -C "$app" rev-parse --show-toplevel)" == "$app" ]] || fail "no repository at the app root"
  [[ "$(git -C "$app" config --local --get core.hooksPath)" == .githooks ]] \
    || fail "core.hooksPath is not set in the app"
  expect_rule_links "$app"
  expect_excluded_in "$app/.git/info/exclude"
  linked=$(new_app routed-rules acme_shop)
  run_init "$linked" --no-rules
  expect_success
  GIT_DIR=$other/.git GIT_WORK_TREE=$other GIT_INDEX_FILE=$other/.git/index GIT_COMMON_DIR=$other/.git \
    run_init "$linked" --rules-only
  expect_success
  expect_rule_links "$linked"
  expect_excluded_in "$linked/.git/info/exclude"
  snapshot_repository "$other" > "$work/other-after"
  diff -- "$work/other-before" "$work/other-after" || fail "init changed the repository GIT_DIR pointed at"
}

case_rules_only_outside_own_repository() {
  local app outer=$work/cases/nested
  app=$(new_app nested acme_shop)
  run_init "$app" --rules-only
  expect_refused "$app is not a git repository; run bash scripts/init.sh to create it"
  git init -q "$outer"
  run_init "$app" --rules-only
  expect_refused "$app is inside the git repository $outer; the app must be the root of its own repository"
  expect_no_rule_links "$app"
  ! grep -qF /AGENTS.md "$outer/.git/info/exclude" || fail "init wrote the exclude file of the outer repository"
}

case_no_rules() {
  local app
  app=$(new_app no-rules acme_shop)
  RULEBOOK_PATH=$work/nowhere run_init "$app" --no-rules
  expect_success
  expect_said 'skipped the rules links \(--no-rules\)'
  expect_no_rule_links "$app"
  ! grep -qF /AGENTS.md "$app/.git/info/exclude" || fail "--no-rules wrote exclude lines"
  [[ -s "$(dotnet_calls "$app")" ]] || fail "--no-rules must still build"
}

case_missing_rulebook() {
  local app
  app=$(new_app missing acme_shop)
  RULEBOOK_PATH=$work/nowhere run_init "$app"
  expect_refused "no rulebook at $work/nowhere; clone Tsikhanau.Rulebook there or set RULEBOOK_PATH .* --no-rules"
  expect_no_rule_links "$app"
  [[ ! -s "$(dotnet_calls "$app")" ]] || fail "a missing rulebook must stop before the build"
  RULEBOOK_PATH='' run_init "$app"
  expect_refused "no rulebook at $work/cases/missing/Tsikhanau.Rulebook;"
}

case_post_checkout() {
  local app first=$work/cases/hook/first second=$work/cases/hook/second
  app=$(new_app hook acme_shop)
  run_init "$app"
  expect_success
  commit_all "$app"
  run_in "$app" git worktree add -q "$first" -b first
  expect_success
  expect_rule_links "$first"
  RULEBOOK_PATH=$work/nowhere run_in "$app" git worktree add -q "$second" -b second
  expect_success
  expect_said "no rulebook at $work/nowhere"
  expect_said 'post-checkout: the rules links were not made; the checkout itself is done'
  expect_no_rule_links "$second"
}

run_case() {
  local name=$1
  local log=$work/logs/$((passes + failures + 1)).log
  shift
  (
    set -e
    "$@"
  ) > "$log" 2>&1
  if [[ $? -eq 0 ]]; then
    passes=$((passes + 1))
    echo "PASS $name"
  else
    failures=$((failures + 1))
    echo "FAIL $name"
    tail -n 25 "$log" | sed 's/^/    | /'
  fi
}

run_case "a name whose PostgreSQL form starts with a digit is refused before anything is made" case_digit_start
run_case "a name longer than the core's login prefix budget is refused, one at the budget passes" case_budget
run_case "a fresh app gets a repository on main, the hooks path, executable bits, ignored rules links and one build; .claude is untouched" case_fresh_repository
run_case "a second run changes nothing" case_repeat_run
run_case "in a git worktree the links are made there from the default rulebook beside the app and the exclude lines go to the shared exclude file" case_worktree
run_case "a dangling link is made again" case_dangling_link
run_case "a file or folder where a link belongs is refused, named and left as it is" case_file_in_place_of_a_link
run_case "a file where a folder of the links belongs is refused before any link" case_file_in_place_of_a_folder
run_case "an exclude file init cannot write fails init before any link" case_unwritable_exclude
run_case "GIT_DIR and friends inherited from another repository do not route init there" case_inherited_git_dir
run_case "--rules-only outside the app's own repository is refused" case_rules_only_outside_own_repository
run_case "--no-rules skips the links with a message and needs no rulebook" case_no_rules
run_case "a missing rulebook at RULEBOOK_PATH or at the default path beside the app is refused with a message" case_missing_rulebook
run_case "the post-checkout hook links the rules in a new worktree and does not fail the checkout without a rulebook" case_post_checkout

echo "test-init: $passes passed, $failures failed"
[[ $failures -eq 0 ]]
