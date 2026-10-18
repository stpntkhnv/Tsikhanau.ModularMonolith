# The 56-mistake harness

Plants typical mistakes of an AI agent, one at a time, into a copy of the template that has two scratch modules, Notes and Tags, and checks that the build or the tests catch each one exactly where `expected.txt` says. 55 mutations make 56 items: m14 also counts item 12, internal types of another module. The harness never ships to an app (`template-only` is excluded in `.template.config/template.json`).

## Files

- `run-mutations.sh`: the runner.
- `Tsikhanau.Monolith.Harness.csproj`: the mutations in C#. `dotnet <its dll> <mutation>`, run in the root of a copy, plants one mutation and prints `applied <mutation>`; the table of names is `Mutations/MutationCatalog.cs`.
- `mutations54.txt`: the mutations in run order.
- `expected.txt`: one line per mutation: its name, then what must catch it.
  - `error <id>`: the build fails and names this diagnostic.
  - `failed <full test name>`: the first test project that fails reports this test.
  - `output <text>`: the first test project that fails prints this text (a mutation that stops the app from starting fails every integration test).
  - `MISSED`: an accepted miss (m37 and m18b); all three test projects pass with the baseline's skipped counts.
- `scratch-commits/000[12367]-*.patch`: the Notes and Tags modules, the Notes tests and their migrations (autovacuum and queue secrets and indexes).

## Run

```
bash template-only/Harness/run-mutations.sh --dry-run . <new folder outside the repository>
bash template-only/Harness/run-mutations.sh . <new folder outside the repository>
```

Both first clear the git routing variables and run git without the user's and the system's config (`GIT_CONFIG_GLOBAL=/dev/null`, `GIT_CONFIG_NOSYSTEM=1`); in the copy git also runs without hooks, a template folder and the default global ignore and attributes files, so a personal setting cannot change the copy. Then they record the source repository (HEAD, index and the content of its working set), copy the working set (tracked and not ignored files, so uncommitted changes count) into `<folder>/repo`, commit it there, apply the patches, build the harness and restore the dotnet tools. At the end they check that the source did not change.

- Dry run (about half a minute; the gate and the pre-push hook run it): every mutation applies and reverts; nothing is built but the harness and, for m25 and m25b, Notes with its migration.
- Full run (about 12 minutes, needs the gate's container settings; the `harness` job of `.github/workflows/template.yml` runs it): a baseline (both solutions built, `src/Tests/ArchTests`, `template-only/ArchRules.Tests` and `src/Tests/IntegrationTests` run, skipped tests counted per project), then per mutation the same checks, stopping at the first failing stage.

Results go to `<folder>/results`: `summary.txt` or `dry-run.txt`, and per mutation the harness output, the changed paths, the build and test logs and the skipped counts. The run fails on any line that is not `CAUGHT` or `MISSED`:

- `DEVIATION`: another stage or check caught the mutation, a mutation expected to be caught was missed, or an accepted miss is now caught (update `expected.txt` on purpose, in the commit that moves it).
- `ERROR skips moved`: a test project skipped more tests than in the baseline, or, for an accepted miss, any other number.
- `ERROR`: a mutation did not apply or a stage failed without a compiler error or a failed test.

`template-only/scripts/test-harness.sh` tests the runner on a fake repository and a fake `dotnet`.

## Re-anchor

When the template changes what a mutation edits, the dry run names the mutation and its `.mutate` log names the anchor (`pattern found 0 times, expected once, in <path>`). Change the anchor in `Mutations/`, keep the planted mistake the same, and write the mistake so that only its own check fires (the line stays within 120 characters, the endpoint holds no other branching). When the template changes what the scratch modules must look like, apply the patches to a branch with `git am -3`, fix the modules until the baseline is green, and save them again with `git format-patch` under the same five names. A full run then shows which catchers moved.
