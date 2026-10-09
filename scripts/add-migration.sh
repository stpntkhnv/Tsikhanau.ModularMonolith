#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "usage: scripts/add-migration.sh <Module> <MigrationName>" >&2
  exit 2
fi

module=$1
migration=$2
cd "$(dirname "$0")/.."

if [[ -d "src/Modules/$module" ]]; then
  project="src/Modules/$module/ModularMonolith.Modules.$module"
  startup_project=src/Bootstrapper/ModularMonolith.Bootstrapper
  output_dir=Infrastructure/Migrations
elif [[ -d "template-only/TestModules/$module" ]]; then
  project=template-only/TestModules
  startup_project=template-only/IntegrationTests
  output_dir="$module/Infrastructure/Migrations"
else
  echo "add-migration: no module $module in src/Modules or template-only/TestModules" >&2
  exit 2
fi

dotnet tool restore
dotnet build "$startup_project"
dotnet tool run dotnet-ef migrations add "$migration" \
  --no-build \
  --project "$project" \
  --startup-project "$startup_project" \
  --context "${module}DbContext" \
  --output-dir "$output_dir"
