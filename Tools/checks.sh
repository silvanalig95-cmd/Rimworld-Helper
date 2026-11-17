#!/bin/bash
# Every CLAUDE.md check, compact: run from the repo root, "bash Tools/checks.sh dv" also rebuilds the DefValidator.
# Takes about ten minutes; run it in the background writing to a log with `; echo "exit $?"` appended, and read the log.
R=$(pwd)
B=$(cd Source/ColonyCouncil && dotnet build -c Release 2>&1); echo "$B" | grep -E " error |warning CS" | grep -v CS0108 | sort -u | head -5; if echo "$B" | grep -q " error "; then echo "BUILD FAILED"; else echo "build: ok"; fi
if [ "$1" = "dv" ]; then (cd Tools/DefValidator && dotnet build -c Release 2>&1 | tail -1); fi
dotnet Tools/DefValidator/bin/Release/net8.0/DefValidator.dll . ~/.nuget/packages/krafs.rimworld.ref/1.6.4871/ref/net472 1.6/Assemblies/ColonyCouncil.dll 2>&1 | grep -iE "^[0-9]+ error|errors?:|ERROR" | head -5
(cd Tools/Tests && timeout 900 dotnet run -c Release 2>&1 | grep -E "All tests passed|FAIL|fail|error|build failed" | head -8)
echo "label: $(python3 Tools/labelcheck.py 2>&1 | tail -1)"
echo "almanac: $(python3 Tools/almanaccheck.py --base HEAD 2>&1 | tail -1)"
echo "save: $(python3 Tools/save_estimate.py --strict 2>&1 | tail -1)"
echo "interact: $(python3 Tools/interaction_audit.py --open 2>&1 | tail -1)"
echo "playout: $(python3 Tools/playout_audit.py 2>&1 | grep -E "^Candidates|^Stale" | tr "\n" " ")"
echo "pool: $(python3 Tools/poolcheck.py 2>&1 | tail -1)"
echo "apidiff: $(timeout 600 dotnet run -c Release --project Tools/ApiDiff -- 1.6/Assemblies/ColonyCouncil.dll 1.6.4871 1.7.4977-alpha.2 2>&1 | grep -E "Nothing the DLL uses|changed|Removed|Missing" | head -3)"
