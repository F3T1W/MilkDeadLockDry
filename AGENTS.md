# Repository instructions

## Architecture

- Keep one application project, `src/DeadLocky/DeadLocky.csproj`, with FSD layers as directories.
- Keep tests in their separate project. Do not introduce a Core/domain library to resolve inspection findings.

## Required inspection after changes

- After making changes in this repository, run Rider's **Code → Inspect Code… → Whole solution**.
- Wait for both Rider/ReSharper and Roslyn analysis to finish. An initial `No issues found` while analysis is running is not a final result.
- Fix every reported issue, preserving application behavior, and rerun the whole-solution inspection after the fixes.
- Repeat until the completed inspection reports **0 issues / No issues found**, including warnings and style suggestions in both application and test projects.
- File-level MCP analysis, a successful build, passing tests, and `dotnet format` do not replace this inspection.
- Do not remove working code or disable inspections merely to reach zero. Resolve conflicting formatting or style settings consistently.
- If Rider or the full inspection is unavailable, report the blocker explicitly; do not claim that the inspection passed.
- In the final response, report the verified whole-solution inspection result and any other checks performed.
