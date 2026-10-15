#!/usr/bin/env bash
set -uo pipefail
shopt -s inherit_errexit

export LC_ALL=C GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1
export GIT_AUTHOR_NAME=test GIT_AUTHOR_EMAIL=test@example.com GIT_COMMITTER_NAME=test GIT_COMMITTER_EMAIL=test@example.com
unset GIT_DIR GIT_WORK_TREE GIT_INDEX_FILE

if ! template_root=$(git -C "$(dirname -- "${BASH_SOURCE[0]}")" rev-parse --show-toplevel); then
  echo "test-update-core: cannot find the repository of the template" >&2
  exit 1
fi
if ! work=$(mktemp -d) || [[ ! -d "$work" ]] || ! work=$(cd -- "$work" && pwd -P); then
  echo "test-update-core: cannot create a temporary folder" >&2
  exit 1
fi
readonly work
template=$work/template
b_tree=$work/b-tree
consumer=$work/consumer
core_project='<Project[[:space:]][^>]*Path="src[/\\](Core|Packages)[/\\]'
extra_block='  <Folder Name="/src/Core/Extra/">\n    <Project Path="src/Core/Extra/Tsikhanau.Monolith.Extra.csproj" />\n  </Folder>\n'
orders_block='  <Folder Name="/src/Modules/Orders/">\n    <Project Path="src/Modules/Orders/Acme.Shop.Modules.Orders/Acme.Shop.Modules.Orders.csproj" />\n  </Folder>\n'
uncommitted_refusal='uncommitted changes or untracked files; commit or discard them first'
missing_tools='needs bash 4\.4 or newer and GNU coreutils \(sort -z, comm -z, sha256sum\); missing:'
passes=0
failures=0
not_run=0
status=0

cleanup() {
  chmod -R u+w -- "$work" 2> /dev/null
  rm -rf -- "$work"
}

trap cleanup EXIT
if ! mkdir -p "$work/tmp" "$work/cases" "$work/logs"; then
  echo "test-update-core: cannot create folders in $work" >&2
  exit 1
fi
export TMPDIR=$work/tmp

inside_work() {
  local path
  path=$(realpath -m -- "$1")
  if [[ "$path" != "$work"/?* ]]; then
    echo "test-update-core: refusing to delete $1, which is not inside $work" >&2
    exit 1
  fi
}

remove_folder() {
  inside_work "$1"
  rm -rf -- "$1"
}

empty_folder() {
  inside_work "$1"
  find "$1" -mindepth 1 -delete
}

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

make_file_then_folder() {
  git checkout -q -b file-tool A
  mkdir -p tools
  printf '#!/usr/bin/env bash\necho file\n' > tools/check
  printf 'tools/check\n' >> src/Core/core-files.txt
  bash template-only/scripts/lock-core.sh > /dev/null
  git add -A
  git commit -q -m C1
  git tag C1
  git rm -q tools/check
  mkdir -p tools/check
  printf '#!/usr/bin/env bash\necho folder\n' > tools/check/run.sh
  sed -i 's|^tools/check$|tools/check/|' src/Core/core-files.txt
  bash template-only/scripts/lock-core.sh > /dev/null
  git add -A
  git commit -q -m C2
  git tag C2
}

make_extra() {
  git checkout -q -b extra A
  mkdir -p src/Core/Extra
  printf '<Project Sdk="Microsoft.NET.Sdk" />\n' > src/Core/Extra/Tsikhanau.Monolith.Extra.csproj
  printf 'namespace Tsikhanau.Monolith.Extra;\n\npublic static class ExtraMarker\n{\n}\n' > src/Core/Extra/ExtraMarker.cs
  insert_block "$(only_solution .)" '<Folder Name="/src/Core/Domain/">' '</Folder>' "$extra_block"
  bash template-only/scripts/lock-core.sh > /dev/null
  git add -A
  git commit -q -m E
  git tag E
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
  make_file_then_folder
  make_extra
  make_broken
  git checkout -q main
}

make_consumer() {
  local module=src/Modules/Orders/Acme.Shop.Modules.Orders
  mkdir -p "$consumer"
  git -C "$template" archive A | tar -C "$consumer" -xf -
  cd "$consumer"
  remove_folder "$consumer/template-only"
  mv "$(only_solution .)" Acme.Shop.slnx
  mkdir -p "$module"
  printf '<Project Sdk="Microsoft.NET.Sdk" />\n' > "$module/Acme.Shop.Modules.Orders.csproj"
  printf 'namespace Acme.Shop.Modules.Orders;\n' > "$module/Order.cs"
  insert_block Acme.Shop.slnx '<Folder Name="/src/Modules/" />' '<Folder Name="/src/Modules/" />' "$orders_block"
  printf '#!/usr/bin/env bash\necho app\n' > scripts/app-tool.sh
  printf '0\n' > .gate/skips
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
  empty_folder "$TMPDIR"
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

expect_no_credentials() {
  grep -qxF "update-core: cloning $1" "$work/out" || fail "update-core did not name $1 before the clone"
  ! grep -qF s3cr3t-token core.lock "$work/out" || fail "the credentials reached core.lock or the output"
}

case_url_with_credentials() {
  local app url=file://ci-bot:s3cr3t-token@$template
  app=$(new_consumer credentials-from)
  cd "$app"
  sed -i '/^source=/d' core.lock
  git commit -q -am "no source"
  run_update scripts/update-core.sh B --from "$url"
  expect_update_to_b_from "file://$template"
  expect_no_credentials "file://$template"
  app=$(new_consumer credentials-in-lock)
  cd "$app"
  sed -i "s|^source=.*|source=$url|" core.lock
  git commit -q -am "source with credentials"
  run_update scripts/update-core.sh B
  expect_update_to_b_from "file://$template"
  expect_no_credentials "file://$template"
}

case_ignored_core_file() {
  local app
  app=$(new_consumer ignored)
  cd "$app"
  ignored_core_file
  run_update scripts/update-core.sh B
  expect_success
  expect_core_of "$b_tree"
  git ls-files --error-unmatch -- src/Core/Extra/ExtraMarker.cs > /dev/null || fail "the ignored core file is not staged"
  bash scripts/core-hash.sh check
}

expect_check() {
  local expected=$1 actual=0
  bash scripts/core-hash.sh check > "$work/check" 2>&1 || actual=$?
  if [[ $actual -ne $expected ]]; then
    cat "$work/check"
    fail "core-hash.sh check exited $actual, not $expected, with $2"
  fi
}

case_core_hash_counts_ignored_files() {
  local app exclude
  app=$(new_consumer counted)
  cd "$app"
  mkdir -p "src/Core/Domain/Generated Files"
  printf 'namespace Injected;\n' > "src/Core/Domain/Generated Files/Injected.cs"
  git check-ignore -q "src/Core/Domain/Generated Files/Injected.cs" || fail "Generated Files/ is not ignored here"
  expect_check 1 "a .cs file that .gitignore ignores"
  grep -qx '  src/Core/Domain/Generated Files/Injected.cs' "$work/check" || fail "the ignored core file is not named"
  remove_folder "src/Core/Domain/Generated Files"
  exclude=$(git rev-parse --git-path info/exclude)
  mkdir -p "$(dirname -- "$exclude")"
  printf 'Excluded.cs\n' >> "$exclude"
  printf 'namespace Excluded;\n' > src/Core/Domain/Excluded.cs
  git check-ignore -q src/Core/Domain/Excluded.cs || fail "info/exclude does not ignore Excluded.cs"
  expect_check 1 "a file that .git/info/exclude ignores"
  rm src/Core/Domain/Excluded.cs
  printf '<Project />\n' > src/Core/Domain/Tsikhanau.Monolith.Domain.csproj.user
  expect_check 1 "a .csproj.user file, which MSBuild imports"
  rm src/Core/Domain/Tsikhanau.Monolith.Domain.csproj.user
  mkdir -p src/Core/Domain/Nested/bin
  printf 'namespace Nested;\n' > src/Core/Domain/Nested/bin/Compiled.cs
  expect_check 1 "a bin folder that is not the output folder of a project"
  remove_folder src/Core/Domain/Nested
  mkdir -p src/Core/Domain/bin/Release src/Core/Domain/obj src/Packages/Tsikhanau.Railway/obj
  printf 'build\n' > src/Core/Domain/bin/Release/Out.dll
  printf 'restore\n' > src/Core/Domain/obj/project.assets.json
  printf 'restore\n' > src/Packages/Tsikhanau.Railway/obj/project.assets.json
  mkdir -p src/Core/Old/bin/Debug src/Core/Old/obj src/Packages/Old/obj
  printf 'build\n' > src/Core/Old/bin/Debug/Out.dll
  printf 'restore\n' > src/Core/Old/obj/project.assets.json
  printf 'restore\n' > src/Packages/Old/obj/project.assets.json
  printf 'swap\n' > src/Core/Domain/.Notes.cs.swp
  printf 'backup\n' > src/Core/Domain/Notes.cs~
  printf 'finder\n' > src/Core/.DS_Store
  expect_check 0 "build output of the core projects, build output left by a removed core project and editor files"
}

case_removed_core_project_leaves_build_output() {
  local app
  app=$(new_consumer removed-project)
  cd "$app"
  core_fingerprint . > "$work/core-a"
  run_update scripts/update-core.sh B
  expect_success
  git commit -q -m "core B"
  mkdir -p src/Core/Extra/bin/Debug src/Core/Extra/obj
  printf 'build\n' > src/Core/Extra/bin/Debug/Tsikhanau.Monolith.Extra.dll
  printf 'restore\n' > src/Core/Extra/obj/project.assets.json
  run_update scripts/update-core.sh A
  expect_success
  core_fingerprint . > "$work/core-back"
  expect_same "$work/core-a" "$work/core-back" "the core is not A's after the update that removed the Extra project"
  bash scripts/core-hash.sh check
  [[ -f src/Core/Extra/bin/Debug/Tsikhanau.Monolith.Extra.dll && -f src/Core/Extra/obj/project.assets.json ]] \
    || fail "build output removed"
}

stub_sha256sum_failing_on_data() {
  mkdir -p "$work/failing-sha256sum"
  cat > "$work/failing-sha256sum/sha256sum" << 'EOF'
#!/usr/bin/env bash
if [[ -n "$(cat)" ]]; then
  echo "sha256sum: read error" >&2
  exit 1
fi
echo "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  -"
EOF
  chmod +x "$work/failing-sha256sum/sha256sum"
}

expect_failed_checksum() {
  local hash_status=0
  PATH="$work/failing-sha256sum:$PATH" bash scripts/core-hash.sh "$1" > "$work/out" 2>&1 || hash_status=$?
  if [[ $hash_status -eq 0 ]]; then
    cat "$work/out"
    fail "core-hash.sh $1 exited 0 although sha256sum failed, $2"
  fi
}

case_failed_checksum() {
  local app
  app=$(new_consumer failed-checksum)
  cd "$app"
  stub_sha256sum_failing_on_data
  expect_failed_checksum print "with the lock as committed"
  sed -i 's/^hash=.*/hash=sha256:/' core.lock
  expect_failed_checksum check "with hash=sha256: in core.lock"
}

case_mangled_lock() {
  local app hash mangled
  app=$(new_consumer mangled-lock)
  cd "$app"
  hash=$(sed -n 's/^hash=sha256://p' core.lock)
  for mangled in sha256: "sha256:${hash^^}" "sha256:${hash:1}"; do
    sed -i "s/^hash=.*/hash=$mangled/" core.lock
    expect_check 1 "hash=$mangled in core.lock"
    grep -qF "core-hash: core.lock holds the hash '$mangled', which is not sha256: followed by 64 lowercase hex digits" \
      "$work/check" || fail "the mangled hash $mangled is not named"
  done
}

stub_dotnet() {
  mkdir -p "$1"
  cat > "$1/dotnet" << 'EOF'
#!/usr/bin/env bash
if [[ "$1" == test ]]; then
  echo "Test run summary: Passed!"
  grep -oE '[^/"]+Tests\.csproj"' "$3" | sed -E 's/\.csproj"$/.dll (net10.0|x64) passed/; s/^/  /'
  echo "  skipped: 0"
fi
if [[ "$1" == run ]]; then
  printf '%s\n' "$*" >> "$(dirname "$0")/runs"
  echo "check-markers: stub"
  exit "${STUB_RUN_EXIT:-0}"
fi
EOF
  chmod +x "$1/dotnet"
}

run_gate() {
  local gate_status=0
  stub_dotnet "$work/stub-dotnet"
  rm -f -- "$work/stub-dotnet/runs"
  env "$@" PATH="$work/stub-dotnet:$PATH" bash gate.sh > "$work/gate" 2>&1 || gate_status=$?
  return "$gate_status"
}

expect_green_gate() {
  local gate_status=0
  run_gate "$@" || gate_status=$?
  if [[ $gate_status -ne 0 ]]; then
    tail -n 5 "$work/gate"
    fail "the gate failed with exit code $gate_status"
  fi
  grep -qx 'gate: green, skipped tests as recorded: Acme.Shop.slnx 0' "$work/gate" || fail "the gate did not end green for Acme.Shop.slnx alone"
}

expect_red_gate() {
  local gate_status=0 message=$1
  shift
  run_gate "$@" || gate_status=$?
  [[ $gate_status -ne 0 ]] || fail "the gate passed"
  grep -qF -- "$message" "$work/gate" || fail "the gate did not say: $message"
  ! grep -qF 'gate: green' "$work/gate" || fail "the gate said green"
}

fake_rulebook() {
  local rulebook=$work/rulebooks/$1
  mkdir -p "$rulebook"
  printf '# AGENTS.md\n' > "$rulebook/AGENTS.md"
  if [[ $2 == with-check ]]; then
    printf 'return 0;\n' > "$rulebook/check-markers.cs"
  fi
  printf '%s\n' "$rulebook"
}

case_gate_needs_linked_rules() {
  local app rulebook not_linked='gate: the rules are not linked: AGENTS.md is not a link into a rulebook that holds check-markers.cs; run bash scripts/init.sh'
  app=$(new_consumer no-rules)
  cd "$app"
  expect_red_gate "$not_linked" CI=
  grep -qF 'Test run summary' "$work/gate" || fail "the gate checked the rules before the tests"
  printf '# AGENTS.md\n' > AGENTS.md
  expect_red_gate "$not_linked" CI=
  rulebook=$(fake_rulebook no-check without-check)
  ln -sfn -- "$rulebook/AGENTS.md" AGENTS.md
  expect_red_gate "$not_linked" CI=
}

case_gate_checks_rule_markers() {
  local app rulebook
  app=$(new_consumer markers)
  cd "$app"
  rulebook=$(fake_rulebook markers with-check)
  ln -s -- "$rulebook/AGENTS.md" AGENTS.md
  expect_green_gate CI=
  grep -qxF "gate: rule markers: checking with $rulebook/check-markers.cs" "$work/gate" || fail "the gate did not name the check"
  [[ "$(cat "$work/stub-dotnet/runs")" == "run --file $rulebook/check-markers.cs -- ." ]] || fail "the gate did not run the check once on the app"
  expect_red_gate 'check-markers: stub' CI= STUB_RUN_EXIT=1
}

case_gate_skips_rule_markers_in_ci() {
  local app
  app=$(new_consumer markers-in-ci)
  cd "$app"
  expect_green_gate CI=true
  grep -qxF 'gate: rule markers: skipped, CI=true; the check needs the rulebook that a developer copy links' "$work/gate" || fail "the gate did not say it skipped the check"
  [[ ! -e "$work/stub-dotnet/runs" ]] || fail "the gate ran the check in CI"
}

case_leftover_template_folder() {
  local app
  app=$(new_consumer leftover)
  cd "$app"
  mkdir -p template-only/bin
  dirty_core_file
  expect_check 1 "a hand edit"
  grep -qF 'the core was changed in this app' "$work/check" || fail "core-hash gives the template's advice"
  git checkout -q -- src/Core/Shared/Collections/EnumerableExtensions.cs
  expect_green_gate CI=true
  ! grep -qF 'test-update-core' "$work/gate" || fail "the gate of an app ran the template's test"
  run_update scripts/update-core.sh B
  expect_success
  expect_core_of "$b_tree"
}

case_gate_reads_a_crlf_skip_file() {
  local app
  app=$(new_consumer crlf-skips)
  cd "$app"
  printf '0\r\n' > .gate/skips
  expect_green_gate CI=true
}

case_gate_removes_a_failed_harness_dry_run_folder() {
  local app script
  app=$(new_consumer harness-dry)
  cd "$app"
  mkdir -p template-only/.gate template-only/scripts template-only/Harness
  cp Acme.Shop.slnx template-only/Template.slnx
  printf '0\n' > template-only/.gate/skips
  for script in test-update-core test-init test-check-perf test-distinct-ports test-harness; do
    printf '#!/usr/bin/env bash\n' > "template-only/scripts/$script.sh"
  done
  printf '#!/usr/bin/env bash\nmkdir -p "$3/repo"\necho "run-mutations: stub failed" >&2\nexit 1\n' \
    > template-only/Harness/run-mutations.sh
  empty_folder "$TMPDIR"
  expect_red_gate 'run-mutations: stub failed' CI=true
  [[ -z "$(ls -A "$TMPDIR")" ]] || fail "the gate left in its temporary folder: $(ls -A "$TMPDIR")"
}

make_legacy_app() {
  local inputs=src/Tests/ArchTests/Inputs/AppInputs.cs
  local host_line='    public static Assembly HostAssembly => typeof(Program).Assembly;'
  local legacy_line='    public static EmptySubject EmptySubject => EmptySubject.Pass;'
  mkdir -p "$1"
  git -C "$template" archive A | tar -C "$1" -xf -
  cd "$1"
  remove_folder "$1/template-only"
  grep -qxF "$host_line" "$inputs" || fail "$inputs has no HostAssembly line to put the legacy declaration after"
  awk -v host="$host_line" -v legacy="$legacy_line" '{ print } $0 == host { print ""; print legacy }' "$inputs" \
    > "$work/inputs.tmp"
  cat "$work/inputs.tmp" > "$inputs"
  grep -qxF "$legacy_line" "$inputs" || fail "the legacy EmptySubject declaration is not in $inputs"
  cat > src/Tests/ArchTests/Modules/LegacyPlaceholderTests.cs << 'CSHARP'
using Tsikhanau.Monolith.Testing.Modules;

namespace ModularMonolith.ArchTests.Modules;

public sealed class LegacyPlaceholderTests
{
    [Theory]
    [EveryProductionModule]
    public void Module_name_is_not_empty(String name)
    {
        if (name == ProductionModules.Placeholder)
        {
            return;
        }

        name.ShouldNotBeEmpty();
    }

    [Theory]
    [EveryProductionModulePair]
    public void Module_names_differ(String name, String otherName)
    {
        if (name == ProductionModules.Placeholder)
        {
            return;
        }

        name.ShouldNotBe(otherName);
    }
}
CSHARP
  printf 'source=%s\ncommit=%s\n' "$template" "$(commit_of A)" >> core.lock
  git init -q -b main
  git add -A
  git commit -q -m legacy
}

in_dotnet_tmp() {
  mkdir -p "$work/dotnet-tmp"
  TMPDIR=$work/dotnet-tmp "$@"
}

case_legacy_app_skips_an_empty_subject() {
  local app=$work/cases/legacy expected_skips=47 skipped_test
  make_legacy_app "$app"
  run_update scripts/update-core.sh E
  expect_success
  grep -qx "commit=$(commit_of E)" core.lock || fail "core.lock does not hold E"
  if ! in_dotnet_tmp dotnet build src/Tests/ArchTests --disable-build-servers > "$work/legacy-build" 2>&1; then
    tail -n 20 "$work/legacy-build"
    fail "the app with the legacy AppInputs and placeholder returns does not build after update-core"
  fi
  if ! in_dotnet_tmp dotnet test --project src/Tests/ArchTests --no-build --no-ansi > "$work/legacy-test" 2>&1; then
    tail -n 20 "$work/legacy-test"
    fail "the arch tests of the legacy app failed"
  fi
  grep -qx "  skipped: $expected_skips" "$work/legacy-test" \
    || fail "the legacy app skipped $(sed -n 's/^  skipped: //p' "$work/legacy-test") arch tests, not $expected_skips"
  for skipped_test in \
    'IntegrationEventNameTests.Every_integration_event_has_a_unique_stable_name' \
    'NullabilityTests.Domain_and_feature_classes_return_optional_instead_of_null(name: "(none)")' \
    'LegacyPlaceholderTests.Module_name_is_not_empty(name: "(none)")' \
    'LegacyPlaceholderTests.Module_names_differ(name: "(none)", otherName: "(none)")'; do
    grep -qF "skipped ModularMonolith.ArchTests.Modules.$skipped_test (" "$work/legacy-test" \
      || fail "$skipped_test was not skipped"
  done
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

staged_deletion_where_the_template_adds_a_file() {
  app_file_where_the_template_adds_one
  git rm -q tools/check/run.sh
}

index_only_file_where_the_template_adds_one() {
  mkdir -p tools/check
  printf 'own\n' > tools/check/run.sh
  git add tools/check/run.sh
  rm tools/check/run.sh
}

app_file_where_the_template_adds_a_folder() {
  mkdir -p tools
  printf 'own\n' > tools/check
  git add tools/check
}

staged_file_gone_from_disk_where_the_template_adds_a_folder() {
  app_file_where_the_template_adds_a_folder
  rm tools/check
}

committed_file_where_the_template_adds_a_folder() {
  app_file_where_the_template_adds_a_folder
  git commit -q -m own
}

deleted_file_where_the_template_adds_a_folder() {
  committed_file_where_the_template_adds_a_folder
  git rm -q tools/check
}

untracked_file_where_the_template_adds_a_folder() {
  mkdir -p tools
  printf 'own\n' > tools/check
}

file_link_where_the_template_adds_a_folder() {
  mkdir -p tools
  ln -s ../core.lock tools/check
}

locked_index() {
  : > "$(git rev-parse --git-path index.lock)"
}

linked_solution() {
  mv Acme.Shop.slnx "$outside/Acme.Shop.slnx"
  ln -s "$outside/Acme.Shop.slnx" Acme.Shop.slnx
  git add -A Acme.Shop.slnx
  git commit -q -m "linked solution"
}

linked_lock() {
  mv core.lock "$outside/core.lock"
  ln -s "$outside/core.lock" core.lock
  git add -A core.lock
  git commit -q -m "linked lock"
}

linked_tools_folder() {
  ln -s "$outside" tools
}

committed_link_to_a_folder() {
  linked_tools_folder
  git add tools
  git commit -q -m "linked tools"
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

link_refusal() {
  local name=$1 prepare=$2 ref=$3 says=${4:-symbolic link} app
  outside=$work/outside/$name
  mkdir -p "$outside"
  app=$(new_consumer "$name")
  cd "$app"
  "$prepare"
  snapshot . > "$work/before"
  files_snapshot "$outside" > "$work/outside-before"
  run_update scripts/update-core.sh "$ref"
  expect_refused "$says"
  snapshot . > "$work/after"
  expect_same "$work/before" "$work/after" "a refusal changed the app"
  files_snapshot "$outside" > "$work/outside-after"
  expect_same "$work/outside-before" "$work/outside-after" "the update wrote through the link"
}

case_refuse_linked_files() {
  link_refusal linked-solution linked_solution B
  grep -qx '  Acme.Shop.slnx' "$work/out" || fail "the linked solution is not named"
  link_refusal linked-lock linked_lock B
  grep -qx '  core.lock' "$work/out" || fail "the linked core.lock is not named"
}

case_refuse_linked_folder() {
  link_refusal linked-folder linked_tools_folder B2
  grep -qx '  tools/check/run.sh (tools is a symbolic link)' "$work/out" || fail "the path under the link is not named"
}

case_refuse_added_paths_in_head_or_index() {
  refusal staged-deletion staged_deletion_where_the_template_adds_a_file 'already has' B2
  grep -qx '  tools/check/run.sh' "$work/out" || fail "the deleted app file is not named"
  refusal index-only index_only_file_where_the_template_adds_one 'already has' B2
  grep -qx '  tools/check/run.sh' "$work/out" || fail "the app file in the index is not named"
}

case_core_file_becomes_a_folder() {
  local app c2_tree=$work/c2-tree
  git clone -q "$template" "$c2_tree"
  git -C "$c2_tree" checkout -q C2
  app=$(new_consumer file-to-folder)
  cd "$app"
  run_update scripts/update-core.sh C1
  expect_success
  [[ -f tools/check ]] || fail "C1 did not place the core file tools/check"
  git commit -q -m "core C1"
  run_update scripts/update-core.sh C2
  expect_success
  expect_core_of "$c2_tree"
  [[ "$(git ls-files -- tools)" == tools/check/run.sh ]] || fail "the index does not hold tools/check/run.sh alone"
  bash scripts/core-hash.sh check
}

case_refuse_staged_file_where_the_template_adds_a_folder() {
  refusal staged-file-gone staged_file_gone_from_disk_where_the_template_adds_a_folder 'already has' B2
  grep -qx '  tools/check' "$work/out" || fail "the app file in the index is not named"
}

case_refuse_app_files_where_the_template_adds_a_folder() {
  refusal committed-file committed_file_where_the_template_adds_a_folder 'already has' B2
  grep -qx '  tools/check' "$work/out" || fail "the committed app file is not named"
  refusal deleted-file deleted_file_where_the_template_adds_a_folder 'already has' B2
  grep -qx '  tools/check' "$work/out" || fail "the app file deleted in the index is not named"
  refusal untracked-file untracked_file_where_the_template_adds_a_folder 'already has' B2
  grep -qx '  tools/check' "$work/out" || fail "the untracked app file is not named"
  refusal file-link file_link_where_the_template_adds_a_folder 'already has' B2
  grep -qx '  tools/check' "$work/out" || fail "the link to a file is not named"
  link_refusal committed-link committed_link_to_a_folder B2 'already has'
  grep -qx '  tools' "$work/out" || fail "the committed link to a folder is not named"
}

stub_failing_tools() {
  local real_comm
  real_comm=$(command -v comm)
  mkdir -p "$work/no-sha256sum" "$work/no-comm-z"
  printf '#!/usr/bin/env bash\necho "sha256sum: command not found" >&2\nexit 127\n' > "$work/no-sha256sum/sha256sum"
  printf '#!/usr/bin/env bash\nif [[ "$1" == -z ]]; then\n  echo "comm: illegal option -- z" >&2\n  exit 1\nfi\nexec %q "$@"\n' "$real_comm" > "$work/no-comm-z/comm"
  chmod +x "$work/no-sha256sum/sha256sum" "$work/no-comm-z/comm"
}

expect_missing_tool() {
  local stub=$1 missing=$2 tool_status=0
  shift 2
  PATH="$work/$stub:$PATH" bash "$@" > "$work/out" 2>&1 || tool_status=$?
  if [[ $tool_status -ne 2 || $(wc -l < "$work/out") -ne 1 ]]; then
    cat "$work/out"
    fail "$1 exited $tool_status without $missing; expected exit 2 and one line"
  fi
  grep -qE -- "$missing_tools $missing\$" "$work/out" || fail "$1 does not name $missing"
}

case_missing_tools() {
  local app
  app=$(new_consumer missing-tools)
  cd "$app"
  stub_failing_tools
  snapshot . > "$work/before"
  expect_missing_tool no-sha256sum sha256sum scripts/core-hash.sh check
  expect_missing_tool no-sha256sum sha256sum scripts/update-core.sh B
  expect_missing_tool no-comm-z 'comm -z' scripts/core-hash.sh check
  expect_missing_tool no-comm-z 'comm -z' scripts/update-core.sh B
  snapshot . > "$work/after"
  expect_same "$work/before" "$work/after" "a missing tool changed the app"
}

read_only_vanishing_folder() {
  chmod 555 src/Core/Shared/Time
  [[ ! -w src/Core/Shared/Time ]] || fail "cannot make a folder read-only (running as root?)"
}

read_only_folder_where_the_template_adds_one() {
  mkdir -p tools
  printf 'own\n' > tools/notes.txt
  git add tools/notes.txt
  git commit -q -m own
  chmod 555 tools
  [[ ! -w tools ]] || fail "cannot make a folder read-only (running as root?)"
}

read_only_solution() {
  chmod 444 Acme.Shop.slnx
  [[ ! -w Acme.Shop.slnx ]] || fail "cannot make a file read-only (running as root?)"
}

ignored_core_file() {
  printf 'ExtraMarker.cs\n' >> .gitignore
  git commit -q -am "ignore a name"
}

app_file_under_a_new_core_folder() {
  mkdir -p tools/check
  printf 'own\n' > tools/check/notes.txt
  git add tools/check/notes.txt
  git commit -q -m own
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

make_locking_git() {
  local real_git
  real_git=$(command -v git)
  mkdir -p "$1"
  cat > "$1/git" << EOF
#!/usr/bin/env bash
if [[ " \$* " == *" add "* ]]; then
  : > "\$($(printf '%q' "$real_git") rev-parse --git-path index.lock)"
fi
exec $(printf '%q' "$real_git") "\$@"
EOF
  chmod +x "$1/git"
}

case_index_locked_while_staging() {
  local app
  app=$(new_consumer locked-while-staging)
  cd "$app"
  make_locking_git "$work/locking-git"
  snapshot . > "$work/before"
  PATH="$work/locking-git:$PATH" run_update scripts/update-core.sh B
  expect_restored 'cannot stage'
  [[ -e "$(git rev-parse --git-path index.lock)" ]] || fail "update-core removed an index lock it did not take"
  snapshot . > "$work/after"
  expect_same "$work/before" "$work/after" "the app is not as it was"
  rm -- "$(git rev-parse --git-path index.lock)"
  bash scripts/core-hash.sh check
}

make_interrupting_tools() {
  local real_git real_cp
  real_git=$(command -v git)
  real_cp=$(command -v cp)
  mkdir -p "$1"
  cat > "$1/git" << EOF
#!/usr/bin/env bash
if [[ " \$* " == *" add "* ]]; then
  kill -INT "\$PPID"
fi
exec $(printf '%q' "$real_git") "\$@"
EOF
  cat > "$1/cp" << EOF
#!/usr/bin/env bash
if [[ "\$1" == -f ]]; then
  kill -INT "\$PPID"
  kill -TERM "\$PPID"
fi
exec $(printf '%q' "$real_cp") "\$@"
EOF
  chmod +x "$1/git" "$1/cp"
}

case_second_signal_during_the_rollback() {
  local app
  app=$(new_consumer second-signal)
  cd "$app"
  make_interrupting_tools "$work/interrupting-tools"
  snapshot . > "$work/before"
  PATH="$work/interrupting-tools:$PATH" run_update scripts/update-core.sh B
  expect_restored 'back as they were'
  snapshot . > "$work/after"
  expect_same "$work/before" "$work/after" "the app is not as it was"
  bash scripts/core-hash.sh check
}

run_read_only_case() {
  if [[ "$(id -u)" == 0 ]]; then
    not_run=$((not_run + 1))
  else
    run_case "$@"
  fi
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
run_case "a URL with credentials is cloned as given, but named and recorded without them" case_url_with_credentials
run_case "a core file that the app ignores is placed, staged and counted" case_ignored_core_file
run_case "core-hash counts core files that git ignores, but not the build output of core projects or editor files" case_core_hash_counts_ignored_files
run_case "an update that removes a core project the app has built succeeds and the lock check passes" case_removed_core_project_leaves_build_output
run_case "core-hash print and check fail when sha256sum fails on the data" case_failed_checksum
run_case "core-hash check refuses a core.lock whose hash is not sha256: and 64 lowercase hex digits" case_mangled_lock
run_case "an app with a leftover template-only folder but no Template.slnx is an app for the gate, core-hash and update-core" case_leftover_template_folder
run_case "the gate reads a skip file with a CR line ending" case_gate_reads_a_crlf_skip_file
run_case "the gate removes the work folder of a harness dry run that fails" case_gate_removes_a_failed_harness_dry_run_folder
run_case "app code that still declares EmptySubject.Pass and returns on the placeholder builds on the current core (an update-core from A to E, not from an old core), and its arch tests on an empty subject skip" case_legacy_app_skips_an_empty_subject
run_case "the gate stops after the tests and names init when AGENTS.md is not a link into a rulebook with check-markers.cs" case_gate_needs_linked_rules
run_case "the gate runs the linked rulebook's check-markers.cs on the app and fails when it fails" case_gate_checks_rule_markers
run_case "with CI=true the gate skips the marker check with a message" case_gate_skips_rule_markers_in_ci
run_case "core-hash and update-core name a missing tool, exit 2 and change nothing" case_missing_tools
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
run_case "refuses when the template adds a path the app deleted in its index or has only in its index" case_refuse_added_paths_in_head_or_index
run_case "refuses a symlinked solution or core.lock and leaves the files they point to alone" case_refuse_linked_files
run_case "refuses a core path under a symlinked folder and leaves the folder it points to alone" case_refuse_linked_folder
run_case "a core file that the template turns into a folder is replaced by the folder" case_core_file_becomes_a_folder
run_case "refuses when the template adds a folder where the app has a file staged and gone from disk, and leaves the index alone" case_refuse_staged_file_where_the_template_adds_a_folder
run_case "refuses when the template adds a folder where the app has a file in HEAD, in the index, on disk or as a link" case_refuse_app_files_where_the_template_adds_a_folder
run_case "refuses while the git index is locked" refusal locked-index locked_index 'index\.lock' B
run_case "refuses to run in the template" case_refuse_in_template
run_read_only_case "failure while removing a vanished core file restores the app" injection read-only-folder read_only_vanishing_folder 'cannot remove src/Core/Shared/Time/TimeSpanExtensions\.cs' B
run_read_only_case "failure creating a new core folder restores the app" injection read-only-parent read_only_folder_where_the_template_adds_one 'cannot create the folder tools/check' B2
run_read_only_case "failure writing the solution restores the app" injection read-only-solution read_only_solution 'cannot write Acme\.Shop\.slnx' B
run_case "an app file under a new core folder restores the app" injection app-file-in-core-folder app_file_under_a_new_core_folder 'extra.*tools/check/notes\.txt' B2
run_read_only_case "failure writing core.lock after the swap restores the app" injection read-only-lock read_only_lock 'cannot write core\.lock' B
run_case "an index lock taken while staging leaves the app as it was" case_index_locked_while_staging
run_case "a second signal during the rollback does not stop it" case_second_signal_during_the_rollback

if [[ $not_run -gt 0 ]]; then
  echo "test-update-core: running as root, which can write to read-only files and folders, so the $not_run cases that need them were not run"
fi
echo "test-update-core: $passes passed, $failures failed"
[[ $failures -eq 0 ]]
