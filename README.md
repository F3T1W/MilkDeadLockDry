# DeadLocky

A local Deadlock launcher for Apple Silicon Macs, built with .NET 10 and native AppKit.
The application uses FSD, with explicit state, actions and UI rendering.

## Solution structure

- `DeadLocky.slnx` — the solution to open in Rider.
- `src/DeadLocky/DeadLocky.csproj` — the application, targeting `net10.0-macos`, `osx-arm64`, macOS 14+.
- `tests/DeadLocky.UnitTests/DeadLocky.UnitTests.csproj` — the xUnit project targeting `net10.0`.
- `docs/` and `scripts/` — documentation and tools listed under Solution Items.
- `Directory.Build.props` and `.editorconfig` — shared build and code style settings.

Inside the application project:

- `App` — entry point, lifecycle, menu and dependency composition.
- `Pages/Launcher` — the launcher screen and feature coordination.
- `Features/SelectGameDirectory` — directory selection, state and UI.
- `Entities/Game` — the selected game directory model.
- `Shared/Api/Directories` — the directory-picker contract and its AppKit implementation.

Dependencies point downwards: App → Pages / Shared; Pages → Features / Shared;
Features → Entities / Shared. FSD layers are directories within one application.
The test project is a separate assembly. It currently contains only xUnit and test SDK packages,
with no test classes or application reference. A `net10.0` project cannot directly reference
an application targeting `net10.0-macos`; the approach to sharing testable code remains to be chosen.

[UML diagram](docs/architecture.puml), [architecture plan](docs/fsd-architecture.md),
[learning route](docs/learning-route.md). The plan and learning route may contain previous names.

## Shared project settings

`Directory.Build.props` applies automatically to projects under `src/` and `tests/`:
C# 14, nullable reference types, implicit usings, .NET 10 analyzers,
code style checks during builds and warnings treated as errors. NuGet packaging is disabled.
The default TargetFramework is `net10.0`; the application overrides it with `net10.0-macos`.
Platform settings, bundle identifier and runtime belong to the application project;
test packages belong to the test project. Code formatting is configured in `.editorconfig`.
Documentation and code comments are written in English.

## Build and run

`global.json` pins SDK 10.0.401 and workload set 10.0.401.1.
The current macOS workload uses Xcode 27.0. Run these commands from the solution root:

```sh
./.dotnet/dotnet build DeadLocky.slnx
./.dotnet/dotnet run --project src/DeadLocky/DeadLocky.csproj
./.dotnet/dotnet test tests/DeadLocky.UnitTests/DeadLocky.UnitTests.csproj
./.dotnet/dotnet format DeadLocky.slnx --verify-no-changes --no-restore
```

In Rider, open `DeadLocky.slnx` and select the `DeadLocky` project in the run configuration.
The local SDK path is `<solution>/.dotnet/dotnet`.
Build output: `src/DeadLocky/bin/Debug/net10.0-macos/osx-arm64/DeadLocky.app`.
The bundle name in `Info.plist` and ApplicationId still retain the old MilkDeadLockDry identity.

The `check-fsd.py` and `check-selection.sh` scripts still use the old MilkDeadLockDry layout
and need updating. Running `dotnet test` does not validate application behavior
until tests have been added to the test project.

## Implemented behavior

Choose a directory, cancel without losing the previous selection, and clear the selection.
Repeated actions are blocked while choosing a directory; errors are displayed to the user.
Disposing the owner cancels the current operation.
The selected path is held in memory and does not yet confirm that the game is installed.
Downloading, file verification, launching through Wine and mod management remain to be implemented.
