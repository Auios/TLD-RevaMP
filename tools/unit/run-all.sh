#!/usr/bin/env bash
# Runs every unit project the way CI does (.github/workflows/checks.yml, job "unit"): each with its own build folders,
# PASS/FAIL per project, exit 1 if any failed. Run from anywhere; the projects run from the checkout root.
#   tools/unit/run-all.sh                 every project
#   tools/unit/run-all.sh Name1 Name2     only these (tools/unit/<Name>.csproj)
# Before 2026-11-13 a local run was never green (the exec-bit test failed on Windows, the doc tests on CRLF checkouts),
# so a red result meant nothing; it should be all green on any checkout now, as in CI.
set -u
cd "$(dirname "$0")/../.." || exit 2
# the projects target net10.0: a dotnet whose SDKs include 10 (the system one can be older, ~/.dotnet newer)
if [ -z "${DOTNET:-}" ]; then
  for d in dotnet "$HOME/.dotnet/dotnet"; do
    if "$d" --list-sdks 2>/dev/null | grep -q '^1[0-9]\.'; then DOTNET="$d"; break; fi
  done
  [ -n "${DOTNET:-}" ] || { echo "no .NET SDK 10 found (set DOTNET=/path/to/dotnet)"; exit 2; }
fi
if [ $# -gt 0 ]; then projects=(); for n in "$@"; do projects+=("tools/unit/$n.csproj"); done
else projects=(tools/unit/*.csproj); fi
failed=(); passed=0
for project in "${projects[@]}"; do
  name=$(basename "$project" .csproj)
  if out=$("$DOTNET" run --project "$project" --configuration Release --disable-build-servers \
        --property:BaseIntermediateOutputPath="obj/$name/" --property:OutputPath="bin/$name/" 2>&1); then
    echo "PASS $name"; passed=$((passed + 1))
  else
    echo "FAIL $name"; printf '%s\n' "$out" | grep -E '^FAIL|error' | head -5 | sed 's/^/    /'
    failed+=("$name")
  fi
done
echo "$passed passed, ${#failed[@]} failed${failed[*]:+: ${failed[*]}}"
[ ${#failed[@]} -eq 0 ]
