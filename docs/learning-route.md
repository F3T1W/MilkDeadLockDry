# Launcher development route

1. Trace Program → AppDelegate → LauncherComposition → LauncherWindowController.
   Identify who owns and disposes every native window, view and controller.
2. Read GamePreparationController and GameLaunchController. Exercise cancellation and failures;
   Render should display immutable state while API adapters perform external I/O.
3. Trace the page's injected callbacks between PrepareGame and LaunchGame. Explain why neither
   feature imports the other and why Shared contains no game workflow.
4. Read SteamClientState and SteamInstallationLink. Verify remembered login and installation
   detection using portable tests before checking them against real Windows Steam metadata.
5. Run the isolated installation test from the app menu. Observe progress, cancel/retry and inspect
   its separate SetupTests directory. Confirm the working launch configuration remains unchanged.
6. Publish NativeAOT and smoke-test the actual app bundle. Debug builds and unit tests validate
   different layers; a complete game session remains necessary for runtime compatibility.

See the [architecture](fsd-architecture.md) and [setup instructions](../README.md).
