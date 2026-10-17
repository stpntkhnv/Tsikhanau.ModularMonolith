#!/usr/bin/env bash
set -uo pipefail
shopt -s inherit_errexit

while IFS= read -r variable; do
  unset "$variable"
done < <(compgen -e | grep '^GIT_')
export LC_ALL=C GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1
export GIT_CONFIG_COUNT=2 GIT_CONFIG_KEY_0=maintenance.auto GIT_CONFIG_VALUE_0=false GIT_CONFIG_KEY_1=gc.auto GIT_CONFIG_VALUE_1=0
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
custom_rulebook=$work/custom-rulebook
app_host=src/Aspire/ModularMonolith.AppHost/AppHost.cs
database_name_budget=30
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
printf 'markers\n' > "$rulebook/check-markers.cs"
cp -r -- "$rulebook" "$custom_rulebook"
printf '#!/usr/bin/env bash\nprintf "%%s\\n" "$*" >> "${DOTNET_CALLS:?}"\n' > "$work/bin/dotnet"
chmod +x "$work/bin/dotnet"
export PATH=$work/bin:$PATH HOME=$work/home RULEBOOK_PATH=$rulebook

fail() {
  echo "$*" >&2
  return 1
}

new_app() {
  local app=$work/cases/$1/app
  mkdir -p "$app/scripts" "$app/.githooks" "$app/src/Aspire/App.AppHost" "$app/.claude"
  cp "$template_root/gate.sh" "$app/"
  cp "$template_root"/scripts/*.sh "$app/scripts/"
  cp "$template_root"/.githooks/* "$app/.githooks/"
  chmod -x "$app/gate.sh" "$app"/scripts/*.sh "$app"/.githooks/*
  sed "s/\"modularmonolith\"/\"$2\"/" "$template_root/$app_host" > "$app/src/Aspire/App.AppHost/AppHost.cs"
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
  local pair
  for pair in "${rule_links[@]}"; do
    expect_link "$1" "${pair%%=*}" "${2:-$rulebook}/${pair#*=}"
  done
}

expect_link() {
  local target
  target=$(readlink -- "$1/$2") || fail "$2 is not a link"
  [[ "$target" == "$3" ]] || fail "$2 points to $target, not $3"
  git -C "$1" check-ignore -q -- "$2" || fail "git sees $2"
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
  local app budget=$database_name_budget
  app=$(new_app over "$(letters $((budget + 1)))")
  run_init "$app"
  expect_refused "has $((budget + 1)) characters, and init allows at most $budget "
  expect_said "the database name in src/Aspire/App.AppHost/AppHost.cs"
  [[ ! -e "$app/.git" ]] || fail "a refused name must stop before the repository is created"
  app=$(new_app at "$(letters "$budget")")
  run_init "$app" --no-rules
  expect_success
}

case_database_name_rule() {
  local app name
  for name in acme__shop acme_ _acme; do
    app=$(new_app "rule-$name" "$name")
    run_init "$app"
    expect_refused "app name, $name .*, must hold only a-z and 0-9 in groups joined by single underscores"
    [[ ! -e "$app/.git" ]] || fail "a refused name must stop before the repository is created"
  done
  app=$(new_app reserved pg_shop)
  run_init "$app"
  expect_refused "app name, pg_shop .*, is pg or starts with pg_, which PostgreSQL reserves for its own roles"
}

case_another_database() {
  local app
  app=$(new_app another acme_shop)
  printf '%s\n' 'var reports = postgres.AddDatabase("reports", databaseName: "Reports");' >> "$app/src/Aspire/App.AppHost/AppHost.cs"
  run_init "$app" --no-rules
  expect_success
  expect_said "app name, acme_shop, is a valid database name"
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

app_with_custom_rules() {
  local app
  app=$(new_app "$1" acme_shop)
  RULEBOOK_PATH=$custom_rulebook run_init "$app" > /dev/null
  expect_success
  expect_rule_links "$app" "$custom_rulebook"
  commit_all "$app"
  printf '%s\n' "$app"
}

case_custom_rulebook_kept() {
  local app
  app=$(app_with_custom_rules kept)
  run_in "$app" env -u RULEBOOK_PATH git checkout -q -b without-default
  expect_success
  ! grep -qF 'post-checkout' "$work/out" || fail "the hook needed the default rulebook"
  expect_rule_links "$app" "$custom_rulebook"
  ln -s "$rulebook" "$work/cases/kept/Tsikhanau.Rulebook"
  run_in "$app" env -u RULEBOOK_PATH git checkout -q -b with-default
  expect_success
  expect_rule_links "$app" "$custom_rulebook"
  run_in "$app" env -u RULEBOOK_PATH bash scripts/init.sh --rules-only
  expect_success
  expect_rule_links "$app" "$custom_rulebook"
}

case_dangling_link_without_rulebook_path() {
  local app
  app=$(app_with_custom_rules kept-dangling)
  ln -s "$rulebook" "$work/cases/kept-dangling/Tsikhanau.Rulebook"
  ln -sfn "$work/gone/AGENTS.md" "$app/AGENTS.md"
  run_in "$app" env -u RULEBOOK_PATH bash scripts/init.sh --rules-only
  expect_success
  expect_said "linked to the rulebook $custom_rulebook: AGENTS.md$"
  expect_rule_links "$app" "$custom_rulebook"
}

case_missing_links_follow_the_kept_rulebook() {
  local app
  app=$(app_with_custom_rules missing-kept)
  ln -s "$rulebook" "$work/cases/missing-kept/Tsikhanau.Rulebook"
  rm -- "$app/STYLE.md" "$app/docs/playbooks"
  run_in "$app" env -u RULEBOOK_PATH bash scripts/init.sh --rules-only
  expect_success
  expect_rule_links "$app" "$custom_rulebook"
}

mixed_app() {
  local app
  app=$(app_with_custom_rules "$1")
  ln -sfn "$rulebook/STYLE.md" "$app/STYLE.md"
  git -C "$app" config --local --unset core.hooksPath
  chmod -x "$app/gate.sh"
  : > "$(dotnet_calls "$app")"
  printf '%s\n' "$app"
}

case_kept_links_to_two_rulebooks() {
  local app mode
  app=$(mixed_app conflict)
  snapshot "$app" > "$work/before"
  for mode in '' --rules-only; do
    run_in "$app" env -u RULEBOOK_PATH bash scripts/init.sh $mode
    expect_refused "different rulebooks: .*RULEBOOK_PATH"
    expect_said "$custom_rulebook"
    expect_said "$rulebook"
    snapshot "$app" > "$work/after"
    diff -- "$work/before" "$work/after" || fail "a refused init changed the app"
    [[ ! -s "$(dotnet_calls "$app")" ]] || fail "a refused init must stop before the build"
  done
}

snapshot_tree() {
  (
    cd "$1"
    find . -printf '%m %y %s %p -> %l\n' | sort
    git config --local --list
    cat "$(git rev-parse --git-path info/exclude)"
  )
}

case_new_worktree_of_links_to_two_rulebooks() {
  local app mode tree=$work/cases/tree-conflict/tree
  app=$(mixed_app tree-conflict)
  ln -s "$rulebook" "$work/cases/tree-conflict/Tsikhanau.Rulebook"
  git -C "$app" worktree add -q "$tree" -b tree
  : > "$(dotnet_calls "$tree")"
  snapshot_tree "$tree" > "$work/before"
  for mode in '' --rules-only; do
    run_in "$tree" env -u RULEBOOK_PATH DOTNET_CALLS="$(dotnet_calls "$tree")" bash scripts/init.sh $mode
    expect_refused "the rules links of the main worktree $app point to different rulebooks: .*RULEBOOK_PATH"
    expect_said "$custom_rulebook"
    expect_said "$rulebook"
    snapshot_tree "$tree" > "$work/after"
    diff -- "$work/before" "$work/after" || fail "a refused init changed the worktree"
    expect_no_rule_links "$tree"
    [[ ! -s "$(dotnet_calls "$tree")" ]] || fail "a refused init must stop before the build"
  done
}

case_explicit_path_ignores_conflict() {
  local app
  app=$(mixed_app conflict-explicit)
  RULEBOOK_PATH=$rulebook run_init "$app" --rules-only
  expect_success
  expect_rule_links "$app"
}

case_relative_and_absolute_links_agree() {
  local app relative
  app=$(app_with_custom_rules relative)
  relative=$(realpath --relative-to="$app" "$custom_rulebook")
  ln -sfn "$relative/AGENTS.md" "$app/AGENTS.md"
  run_in "$app" env -u RULEBOOK_PATH bash scripts/init.sh --rules-only
  expect_success
  [[ "$(readlink -- "$app/AGENTS.md")" == "$relative/AGENTS.md" ]] || fail "init changed the relative link"
  expect_link "$app" STYLE.md "$custom_rulebook/STYLE.md"
}

case_alias_and_real_path_agree() {
  local app
  app=$(app_with_custom_rules alias)
  ln -s "$custom_rulebook" "$work/cases/alias/alias-rulebook"
  ln -sfn "$work/cases/alias/alias-rulebook/AGENTS.md" "$app/AGENTS.md"
  run_in "$app" env -u RULEBOOK_PATH bash scripts/init.sh --rules-only
  expect_success
  expect_link "$app" AGENTS.md "$work/cases/alias/alias-rulebook/AGENTS.md"
  expect_link "$app" STYLE.md "$custom_rulebook/STYLE.md"
}

case_rulebook_path_with_a_space() {
  local app spaced="$work/rule book"
  cp -r -- "$rulebook" "$spaced"
  app=$(new_app space acme_shop)
  RULEBOOK_PATH=$spaced run_init "$app"
  expect_success
  expect_rule_links "$app" "$spaced"
  commit_all "$app"
  rm -- "$app/STYLE.md" "$app/docs/playbooks"
  run_in "$app" env -u RULEBOOK_PATH bash scripts/init.sh --rules-only
  expect_success
  expect_rule_links "$app" "$spaced"
}

case_new_worktree_follows_the_main_checkout() {
  local app
  app=$(app_with_custom_rules tree-kept)
  ln -s "$rulebook" "$work/cases/tree-kept/Tsikhanau.Rulebook"
  run_in "$app" env -u RULEBOOK_PATH git worktree add -q "$work/cases/tree-kept/tree" -b tree
  expect_success
  expect_rule_links "$work/cases/tree-kept/tree" "$custom_rulebook"
}

case_new_worktree_without_main_links_gets_the_default() {
  local app
  app=$(new_app tree-plain acme_shop)
  run_init "$app" --no-rules
  expect_success
  commit_all "$app"
  ln -s "$rulebook" "$work/cases/tree-plain/Tsikhanau.Rulebook"
  run_in "$app" env -u RULEBOOK_PATH git worktree add -q "$work/cases/tree-plain/tree" -b tree
  expect_success
  expect_rule_links "$work/cases/tree-plain/tree"
  expect_no_rule_links "$app"
}

case_explicit_rulebook_path_wins() {
  local app
  app=$(app_with_custom_rules explicit)
  RULEBOOK_PATH=$rulebook run_init "$app" --rules-only
  expect_success
  expect_rule_links "$app"
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
run_case "a name longer than the database name budget is refused, one at the budget passes" case_budget
run_case "a name whose PostgreSQL form breaks the database name rule is refused before anything is made" case_database_name_rule
run_case "another database in the AppHost leaves the name of the app's database" case_another_database
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
run_case "links to a rulebook chosen with RULEBOOK_PATH stay through a checkout and --rules-only without it" case_custom_rulebook_kept
run_case "without RULEBOOK_PATH a dangling link is made again from the kept rulebook" case_dangling_link_without_rulebook_path
run_case "an explicit RULEBOOK_PATH replaces links to another rulebook" case_explicit_rulebook_path_wins
run_case "missing links go to the rulebook the kept links point to" case_missing_links_follow_the_kept_rulebook
run_case "kept links to two rulebooks stop init before any write and name RULEBOOK_PATH" case_kept_links_to_two_rulebooks
run_case "an explicit RULEBOOK_PATH is not stopped by links to two rulebooks" case_explicit_path_ignores_conflict
run_case "a new worktree of a main checkout with links to two rulebooks stops init before any write and names RULEBOOK_PATH" case_new_worktree_of_links_to_two_rulebooks
run_case "a relative and an absolute link to the same rulebook are no conflict" case_relative_and_absolute_links_agree
run_case "a rulebook reached through an alias and through its real path is no conflict" case_alias_and_real_path_agree
run_case "a rulebook path with a space works" case_rulebook_path_with_a_space
run_case "a new worktree links the rulebook of the main checkout" case_new_worktree_follows_the_main_checkout
run_case "a new worktree of a checkout without links gets the default rulebook" case_new_worktree_without_main_links_gets_the_default

echo "test-init: $passes passed, $failures failed"
[[ $failures -eq 0 ]]
