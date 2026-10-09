#!/usr/bin/env bash
set -uo pipefail
shopt -s inherit_errexit

export LC_ALL=C GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1
export GIT_AUTHOR_NAME=test GIT_AUTHOR_EMAIL=test@example.com GIT_COMMITTER_NAME=test GIT_COMMITTER_EMAIL=test@example.com
unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE

template_root=$(git -C "$(dirname -- "${BASH_SOURCE[0]}")" rev-parse --show-toplevel)
work=$(mktemp -d)
template=$work/template
b_tree=$work/b-tree
consumer=$work/consumer
core_project='<Project[[:space:]][^>]*Path="src[/\\](Core|Packages)[/\\]'
extra_block='  <Folder Name="/src/Core/Extra/">\n    <Project Path="src/Core/Extra/Tsikhanau.Monolith.Extra.csproj" />\n  </Folder>\n'
orders_block='  <Folder Name="/src/Modules/Orders/">\n    <Project Path="src/Modules/Orders/Acme.Shop.Modules.Orders/Acme.Shop.Modules.Orders.csproj" />\n  </Folder>\n'
uncommitted_refusal='uncommitted changes or untracked files; commit or discard them first'
passes=0
failures=0
status=0

cleanup() {
  chmod -R u+w "$work" 2> /dev/null
  rm -rf -- "$work"
}

trap cleanup EXIT
mkdir -p "$work/tmp" "$work/cases" "$work/logs"
export TMPDIR=$work/tmp

fail() {
  echo "$*" >&2
  return 1
}

only_solution() {
  local solutions=("$1"/*.slnx)
  [[ ${#solutions[@]} -eq 1 && -f "${solutions[0]}" ]] || fail "expected one *.slnx in $1"
  printf '%s\n' "${solutions[0]}"
}

insert_block() {
  awk -v start="$2" -v end="$3" -v block="$4" '
    { print }
    !done && index($0, start) { open = 1 }
    open && index($0, end) { printf "%s", block; open = 0; done = 1 }
    END { exit done ? 0 : 1 }
  ' "$1" > "$work/insert.tmp" || fail "cannot find $2 in $1"
  cat "$work/insert.tmp" > "$1"
}

copy_working_set() {
  (
    cd "$template_root"
    comm -z -23 <(git ls-files -z --cached --others --exclude-standard | sort -zu) <(git ls-files -z --deleted | sort -zu)
  ) | tar -C "$template_root" --null -T - -cf - | tar -C "$1" -xf -
}

commit_of() {
  git -C "$template" rev-parse "$1^{commit}"
}

make_b() {
  local packages=src/Core/Core.Packages.props before
  git checkout -q -b next
  before=$(sha256sum < "$packages")
  sed -i -E 's/(Include="Dapper" Version=")[^"]*"/\199.0.0"/' "$packages"
  [[ "$(sha256sum < "$packages")" != "$before" ]] || fail "no Dapper version in $packages"
  mkdir -p src/Core/Extra
  printf '<Project Sdk="Microsoft.NET.Sdk" />\n' > src/Core/Extra/Tsikhanau.Monolith.Extra.csproj
  printf 'namespace Tsikhanau.Monolith.Extra;\n\npublic static class ExtraMarker\n{\n}\n' > src/Core/Extra/ExtraMarker.cs
  insert_block "$(only_solution .)" '<Folder Name="/src/Core/Domain/">' '</Folder>' "$extra_block"
  git rm -q src/Core/Shared/Time/TimeSpanExtensions.cs
  printf 'P:System.Environment.TickCount;Use the injected TimeProvider\n' >> BannedSymbols.txt
  if [[ -f scripts/update-core.sh ]]; then
    sed -i "1a candidate_marker=$(printf 'b%.0s' {1..200})" scripts/update-core.sh
  fi
  bash template-only/scripts/lock-core.sh > /dev/null
  git add -A
  git commit -q -m B
  git tag B
}

make_b2() {
  git checkout -q -b crowded B
  mkdir -p tools/check
  printf '#!/usr/bin/env bash\necho check\n' > tools/check/run.sh
  chmod +x tools/check/run.sh
  printf 'tools/check/\n' >> src/Core/core-files.txt
  bash template-only/scripts/lock-core.sh > /dev/null
  git add -A
  git commit -q -m B2
  git tag B2
}

make_broken() {
  git checkout -q -b broken A
  sed -i -E 's/^hash=sha256:[0-9a-f]/hash=sha256:x/' core.lock
  git commit -q -am bad-lock
  git tag bad-lock
  git checkout -q -b plain A
  git rm -q src/Core/core-files.txt
  git commit -q -m plain
  git tag not-template
}

make_template() {
  mkdir -p "$template"
  copy_working_set "$template"
  cd "$template"
  git init -q -b main
  bash scripts/core-hash.sh check > /dev/null \
    || fail "the core of $template_root does not match its core.lock; run template-only/scripts/lock-core.sh first"
  git add -A
  git commit -q -m A
  git tag A
  make_b
  make_b2
  make_broken
  git checkout -q main
}

make_consumer() {
  local module=src/Modules/Orders/Acme.Shop.Modules.Orders
  mkdir -p "$consumer"
  git -C "$template" archive A | tar -C "$consumer" -xf -
  cd "$consumer"
  rm -rf template-only
  mv "$(only_solution .)" Acme.Shop.slnx
  mkdir -p "$module"
  printf '<Project Sdk="Microsoft.NET.Sdk" />\n' > "$module/Acme.Shop.Modules.Orders.csproj"
  printf 'namespace Acme.Shop.Modules.Orders;\n' > "$module/Order.cs"
  insert_block Acme.Shop.slnx '<Folder Name="/src/Modules/" />' '<Folder Name="/src/Modules/" />' "$orders_block"
  printf '#!/usr/bin/env bash\necho app\n' > scripts/app-tool.sh
  printf 'source=%s\ncommit=%s\n' "$template" "$(commit_of A)" >> core.lock
  git init -q -b main
  git add -A
  git commit -q -m app
  bash scripts/core-hash.sh check > /dev/null
}

setup() {
  make_template
  git clone -q "$template" "$b_tree"
  git -C "$b_tree" checkout -q B
  make_consumer
}

new_consumer() {
  local app=$work/cases/$1
  git clone -q "$consumer" "$app"
  printf '%s\n' "$app"
}

run_update() {
  local leftovers
  status=0
  bash "$@" > "$work/out" 2>&1 || status=$?
  cat "$work/out"
  leftovers=$(ls -A "$TMPDIR")
  find "$TMPDIR" -mindepth 1 -delete
  [[ -z "$leftovers" ]] || fail "update-core left files in its temporary folder: $leftovers"
}

expect_success() {
  [[ $status -eq 0 ]] || fail "update-core failed with exit code $status"
}

expect_refused() {
  [[ $status -ne 0 ]] || fail "update-core did not refuse"
  grep -qE -- "$1" "$work/out" || fail "the output does not say: $1"
  ! grep -q 'back as they were' "$work/out" || fail "a refusal must stop before anything is changed"
}

expect_restored() {
  [[ $status -ne 0 ]] || fail "update-core did not fail"
  grep -qE -- "$1" "$work/out" || fail "the output does not say: $1"
  grep -q 'back as they were' "$work/out" || fail "the output does not say that the app was restored"
}

expect_same() {
  diff -- "$1" "$2" > "$work/diff" || {
    head -n 20 "$work/diff"
    fail "$3"
  }
}

files_snapshot() {
  (
    cd "$1"
    find . -path ./.git -prune -o -type d -print | sort
    find . -path ./.git -prune -o ! -type d -printf '%m %y %p\n' | sort
    find . -path ./.git -prune -o -type f -print0 | sort -z | xargs -0 -r sha256sum
  )
}

snapshot() {
  (
    cd "$1"
    git rev-parse HEAD
    git status --porcelain --untracked-files=all
    git ls-files --stage
  )
  files_snapshot "$1"
}

core_fingerprint() {
  (
    cd "$1"
    bash scripts/core-hash.sh list > "$work/list"
    xargs -d '\n' -a "$work/list" stat -c '%a %n'
    xargs -d '\n' -a "$work/list" sha256sum
  )
}

expect_core_of() {
  core_fingerprint "$1" > "$work/core-expected"
  core_fingerprint . > "$work/core-actual"
  expect_same "$work/core-expected" "$work/core-actual" "the core differs from the core of $1"
}

expected_lock() {
  local version hash
  version=$(sed -n 's/^version=//p' "$1/core.lock")
  hash=$(cd "$1" && bash scripts/core-hash.sh print)
  printf 'version=%s\nhash=%s\nsource=%s\ncommit=%s\n' "$version" "$hash" "$2" "$3"
}

exclusions() {
  {
    bash scripts/core-hash.sh list
    (cd "$b_tree" && bash scripts/core-hash.sh list)
    echo core.lock
    echo Acme.Shop.slnx
  } | sort -u
}

app_fingerprint() {
  find . -path ./.git -prune -o ! -type d -print | sed 's|^\./||' | sort | comm -23 - "$1" > "$work/app-list"
  xargs -d '\n' -a "$work/app-list" -r stat -c '%a %n'
  xargs -d '\n' -a "$work/app-list" -r sha256sum
}

case_update_to_b() {
  local app
  app=$(new_consumer update)
  cd "$app"
  mkdir -p src/Core/Domain/bin/Debug src/Core/Domain/obj
  printf 'build\n' > src/Core/Domain/bin/Debug/Out.dll
  printf 'restore\n' > src/Core/Domain/obj/project.assets.json
  exclusions > "$work/excluded"
  app_fingerprint "$work/excluded" > "$work/app-before"
  cp Acme.Shop.slnx "$work/expected-solution"
  insert_block "$work/expected-solution" '<Folder Name="/src/Core/Domain/">' '</Folder>' "$extra_block"
  run_update scripts/update-core.sh next
  expect_success
  expect_core_of "$b_tree"
  app_fingerprint "$work/excluded" > "$work/app-after"
  expect_same "$work/app-before" "$work/app-after" "files outside the core changed"
  expect_same "$work/expected-solution" Acme.Shop.slnx "the solution is not the app's solution plus the new core project"
  grep -E "$core_project" "$(only_solution "$b_tree")" > "$work/core-lines-expected"
  grep -E "$core_project" Acme.Shop.slnx > "$work/core-lines-actual"
  expect_same "$work/core-lines-expected" "$work/core-lines-actual" "the core projects differ from the template's"
  expected_lock "$b_tree" "$template" "$(commit_of B)" > "$work/lock-expected"
  expect_same "$work/lock-expected" core.lock "core.lock is not version, hash, source and commit of B"
  bash scripts/core-hash.sh check
  git diff --quiet || fail "changes left unstaged"
  [[ -z "$(git ls-files --others --exclude-standard)" ]] || fail "files left untracked"
  ! git diff --cached --quiet || fail "nothing staged"
  git -C "$template" show A:scripts/update-core.sh > "$work/update-core-a"
  ! cmp -s "$work/update-core-a" scripts/update-core.sh || fail "update-core.sh did not replace itself"
  grep -qE '[0-9]+ files? changed' "$work/out" || fail "no diff stat in the output"
  grep -qF './gate.sh' "$work/out" || fail "no reminder to run the gate"
  grep -qF 'scripts/add-migration.sh' "$work/out" || fail "no reminder to add a migration"
  [[ -f src/Core/Domain/bin/Debug/Out.dll && -f src/Core/Domain/obj/project.assets.json ]] || fail "build output removed"
}

case_noop() {
  local app
  app=$(new_consumer noop)
  cd "$app"
  snapshot . > "$work/before"
  run_update scripts/update-core.sh "$(commit_of A)"
  expect_success
  grep -q 'nothing to change' "$work/out" || fail "no message that nothing changed"
  snapshot . > "$work/after"
  expect_same "$work/before" "$work/after" "updating to the same commit changed the app"
}

case_round_trip() {
  local app
  app=$(new_consumer round-trip)
  cd "$app"
  files_snapshot . > "$work/before"
  run_update scripts/update-core.sh B
  expect_success
  git commit -q -m "core B"
  run_update scripts/update-core.sh A
  expect_success
  files_snapshot . > "$work/after"
  expect_same "$work/before" "$work/after" "the way back to A did not give the files of A"
  bash scripts/core-hash.sh check
}

case_subfolder() {
  local app
  app=$(new_consumer subfolder)
  cd "$app/src/Modules"
  run_update ../../scripts/update-core.sh B
  expect_success
  cd "$app"
  expect_core_of "$b_tree"
  grep -qx "commit=$(commit_of B)" core.lock || fail "core.lock does not hold B"
}

case_worktree() {
  local app side=$work/cases/side
  app=$(new_consumer worktree)
  cd "$app"
  snapshot . > "$work/before"
  git worktree add -q "$side" -b side
  cd "$side"
  run_update scripts/update-core.sh B
  expect_success
  expect_core_of "$b_tree"
  expected_lock "$b_tree" "$template" "$(commit_of B)" > "$work/lock-expected"
  expect_same "$work/lock-expected" core.lock "core.lock is not version, hash, source and commit of B"
  bash scripts/core-hash.sh check
  snapshot "$app" > "$work/after"
  expect_same "$work/before" "$work/after" "the main worktree changed"
}

case_crlf_solution() {
  local app
  app=$(new_consumer crlf)
  cd "$app"
  sed -i 's/$/\r/' Acme.Shop.slnx
  git commit -q -am crlf
  tr -d '\r' < Acme.Shop.slnx > "$work/expected-solution"
  insert_block "$work/expected-solution" '<Folder Name="/src/Core/Domain/">' '</Folder>' "$extra_block"
  sed -i 's/$/\r/' "$work/expected-solution"
  run_update scripts/update-core.sh B
  expect_success
  expect_same "$work/expected-solution" Acme.Shop.slnx "the CRLF solution did not get CRLF core lines"
}

case_no_source() {
  local app
  app=$(new_consumer no-source)
  cd "$app"
  sed -i '/^source=/d' core.lock
  git commit -q -am "no source"
  snapshot . > "$work/before"
  run_update scripts/update-core.sh B
  expect_refused 'no source='
  snapshot . > "$work/after"
  expect_same "$work/before" "$work/after" "a refusal changed the app"
  cd src/Modules
  run_update ../../scripts/update-core.sh B --from ../../../../template
  expect_success
  cd "$app"
  grep -qx "source=$template" core.lock || fail "core.lock does not hold the absolute --from path"
  bash scripts/core-hash.sh check
}

expect_update_to_b_from() {
  expect_success
  expect_core_of "$b_tree"
  expected_lock "$b_tree" "$1" "$(commit_of B)" > "$work/lock-expected"
  expect_same "$work/lock-expected" core.lock "core.lock is not version, hash, source $1 and commit of B"
  bash scripts/core-hash.sh check
  git diff --quiet || fail "changes left unstaged"
  [[ -z "$(git ls-files --others --exclude-standard)" ]] || fail "files left untracked"
  ! git diff --cached --quiet || fail "nothing staged"
}

case_url_source() {
  local app url=file://$template
  app=$(new_consumer url-in-lock)
  cd "$app"
  sed -i "s|^source=.*|source=$url|" core.lock
  git commit -q -am "url source"
  run_update scripts/update-core.sh B
  expect_update_to_b_from "$url"
  app=$(new_consumer url-from)
  cd "$app"
  sed -i '/^source=/d' core.lock
  git commit -q -am "no source"
  run_update scripts/update-core.sh B --from "$url"
  expect_update_to_b_from "$url"
}

dirty_core_file() {
  printf '\n' >> src/Core/Shared/Collections/EnumerableExtensions.cs
}

staged_core_file() {
  dirty_core_file
  git add src/Core/Shared/Collections/EnumerableExtensions.cs
}

untracked_core_file() {
  printf 'namespace Stray;\n' > src/Core/Domain/Stray.cs
}

dirty_lock() {
  printf 'note=1\n' >> core.lock
}

dirty_solution() {
  printf '<!-- note -->\n' >> Acme.Shop.slnx
}

committed_hand_edit() {
  dirty_core_file
  git commit -q -am "hand edit"
}

two_solutions() {
  cp Acme.Shop.slnx Second.slnx
  git add Second.slnx
  git commit -q -m second
}

app_file_where_the_template_adds_one() {
  mkdir -p tools/check
  printf 'own\n' > tools/check/run.sh
  git add tools/check/run.sh
  git commit -q -m own
}

nothing() {
  :
}

refusal() {
  local name=$1 prepare=$2 says=$3 app
  shift 3
  app=$(new_consumer "$name")
  cd "$app"
  "$prepare"
  snapshot . > "$work/before"
  run_update scripts/update-core.sh "$@"
  expect_refused "$says"
  snapshot . > "$work/after"
  expect_same "$work/before" "$work/after" "a refusal changed the app"
}

case_refuse_hand_edit() {
  refusal hand-edit committed_hand_edit 'does not match core\.lock' B
  grep -qF 'src/Core/Shared/Collections/EnumerableExtensions.cs' "$work/out" || fail "the edited core file is not named"
  grep -qF 'the core was changed in this app; undo the edit' "$work/out" || fail "the advice does not say to undo the edit first"
}

case_refuse_in_template() {
  local copy=$work/cases/template-copy
  git clone -q "$template" "$copy"
  cd "$copy"
  snapshot . > "$work/before"
  run_update scripts/update-core.sh B --from "$template"
  expect_refused 'template-only/scripts/lock-core\.sh'
  snapshot . > "$work/after"
  expect_same "$work/before" "$work/after" "a refusal changed the template"
}

read_only_vanishing_folder() {
  chmod 555 src/Core/Shared/Time
  [[ ! -w src/Core/Shared/Time ]] || fail "cannot make a folder read-only (running as root?)"
}

app_file_in_the_way() {
  mkdir -p tools
  printf 'own\n' > tools/check
  git add tools/check
  git commit -q -m own
}

read_only_solution() {
  chmod 444 Acme.Shop.slnx
  [[ ! -w Acme.Shop.slnx ]] || fail "cannot make a file read-only (running as root?)"
}

ignored_core_file() {
  printf 'ExtraMarker.cs\n' >> .gitignore
  git commit -q -am "ignore a name"
}

read_only_lock() {
  chmod 444 core.lock
  [[ ! -w core.lock ]] || fail "cannot make a file read-only (running as root?)"
}

injection() {
  local name=$1 prepare=$2 says=$3 ref=$4 app
  app=$(new_consumer "$name")
  cd "$app"
  "$prepare"
  snapshot . > "$work/before"
  run_update scripts/update-core.sh "$ref"
  expect_restored "$says"
  snapshot . > "$work/after"
  expect_same "$work/before" "$work/after" "the app is not as it was"
  bash scripts/core-hash.sh check
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

(
  set -e
  setup
) > "$work/logs/setup.log" 2>&1
if [[ $? -ne 0 ]]; then
  echo "FAIL setup"
  tail -n 25 "$work/logs/setup.log" | sed 's/^/    | /'
  exit 1
fi

run_case "update from A to B: the core is B's byte for byte, app files and app solution lines unchanged, lock is B's, check passes, changes staged" case_update_to_b
run_case "update from A to A changes nothing" case_noop
run_case "update from A to B and back to A gives the files of A" case_round_trip
run_case "update works from a subfolder" case_subfolder
run_case "update works in a git worktree and leaves the main worktree alone" case_worktree
run_case "a CRLF solution keeps CRLF" case_crlf_solution
run_case "no source refuses; --from with a relative path works and is recorded absolute" case_no_source
run_case "a file:// URL works as source= in core.lock and as --from, and is recorded as given" case_url_source
run_case "refuses an uncommitted core edit" refusal dirty-core dirty_core_file "$uncommitted_refusal" B
run_case "refuses a staged core edit" refusal staged-core staged_core_file "$uncommitted_refusal" B
run_case "refuses an untracked core file" refusal untracked-core untracked_core_file "$uncommitted_refusal" B
run_case "refuses an uncommitted core.lock edit" refusal dirty-lock dirty_lock "$uncommitted_refusal" B
run_case "refuses an uncommitted solution edit" refusal dirty-solution dirty_solution "$uncommitted_refusal" B
run_case "refuses a committed hand edit of the core and names the file" case_refuse_hand_edit
run_case "refuses two root solutions" refusal two-solutions two_solutions 'exactly one \*\.slnx' B
run_case "refuses a ref that does not resolve" refusal bogus-ref nothing 'cannot resolve no-such-ref' no-such-ref
run_case "refuses a --from that cannot be cloned" refusal bogus-from nothing 'cannot clone' B --from "$work/no-such-template"
run_case "refuses a template commit whose lock does not match its core" refusal corrupt-lock nothing 'does not match its own core\.lock' bad-lock
run_case "refuses a commit that is not a template" refusal not-template nothing 'is not a template' not-template
run_case "refuses when the template adds a core file the app already has" refusal collision app_file_where_the_template_adds_one 'already has' B2
run_case "refuses to run in the template" case_refuse_in_template
run_case "failure while removing a vanished core file restores the app" injection read-only-folder read_only_vanishing_folder 'cannot remove src/Core/Shared/Time/TimeSpanExtensions\.cs' B
run_case "path collision during the swap restores the app" injection in-the-way app_file_in_the_way 'cannot create the folder tools/check' B2
run_case "failure writing the solution restores the app" injection read-only-solution read_only_solution 'cannot write Acme\.Shop\.slnx' B
run_case "a template core file that the app ignores restores the app" injection ignored ignored_core_file 'missing.*src/Core/Extra/ExtraMarker\.cs' B
run_case "failure writing core.lock after the swap restores the app" injection read-only-lock read_only_lock 'cannot write core\.lock' B

echo "test-update-core: $passes passed, $failures failed"
[[ $failures -eq 0 ]]
