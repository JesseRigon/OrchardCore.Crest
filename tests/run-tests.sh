#!/usr/bin/env bash
# Crest-owned test entrypoint. Host repos (fruitful.orchard, OrchardCore.Crest.Host) delegate
# here instead of walking OrchardCore.Crest's internal OrchardCore.Crest.*/ subproject layout
# themselves - this script is the one place that knows that layout.
#
# This script holds no credentials and makes no decisions about .env, server lifecycle, or
# whether tests should run at all - that's entirely the calling host's job. It only accepts
# BASE_URL (already resolved by the caller) and discovers/runs what's underneath it.
#
# Discovers and runs, for every OrchardCore.Crest.*/tests/ subdirectory:
#   - a C# test project (*.csproj directly under tests/<ProjectName>/) via `dotnet test`
#   - a Playwright suite (tests/playwright/) via the existing checks/ convention
# plus this directory's own shared modules/OrchardCore.Crest/tests/playwright/ suite.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CREST_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

export BASE_URL="${BASE_URL:-${FRUITFUL_SERVER_URL:-${CREST_SERVER_URL:-}}}"

overall_failed=0

echo "=== OrchardCore.Crest C# tests ==="
csharp_total=0
csharp_failures=0
while IFS= read -r -d '' csproj; do
  csharp_total=$((csharp_total + 1))
  relative="${csproj#${CREST_DIR}/}"
  echo "==> dotnet test ${relative}"
  if ! dotnet test "${csproj}"; then
    csharp_failures=$((csharp_failures + 1))
  fi
done < <(find "${CREST_DIR}" -mindepth 3 -maxdepth 4 -path "*/tests/*/*.csproj" -print0 | sort -z)

if ((csharp_total == 0)); then
  echo "(none found)"
elif ((csharp_failures > 0)); then
  overall_failed=1
fi
echo "C# test projects: $((csharp_total - csharp_failures))/${csharp_total} passed"

echo
echo "=== OrchardCore.Crest shared Playwright suite ==="
if [ -f "${SCRIPT_DIR}/playwright/run-admin-suite.js" ]; then
  if ! node "${SCRIPT_DIR}/playwright/run-admin-suite.js"; then
    overall_failed=1
  fi
else
  echo "(shared run-admin-suite.js not found, skipping)"
fi

exit "${overall_failed}"
