# FSD launcher development route

FSD defines code organization and dependency direction. AppKit provides the native UI.
State uses immutable records, actions and explicit Render calls.
MVVM is not a requirement of this architecture.

## 1. Learn FSD boundaries

Read about [layers](https://feature-sliced.design/docs/reference/layers) and
[public APIs](https://feature-sliced.design/docs/reference/public-api).
Trace App → Pages → Features → Entities / Shared inside `src/DeadLocky/`.
Find the SelectGameDirectory slice and its Ui/Model segments; explain why Shared has no game knowledge.
Completion criterion: a new scenario can be placed without importing a sibling feature.

## 2. Understand the AppKit lifecycle

Read about [NSApplication](https://developer.apple.com/documentation/appkit/nsapplication),
[NSWindowController](https://developer.apple.com/documentation/appkit/nswindowcontroller) and
[Auto Layout](https://developer.apple.com/documentation/appkit/nslayoutconstraint).
Trace Program → AppDelegate → composition → page → feature.
Understand the UI thread, NSObject ownership, event subscriptions and Dispose.
Completion criterion: the window and dialog open, and ownership of every resource is clear.

## 3. Understand state and actions

Study DirectorySelectionState, ChooseAsync, Clear and Render.
Exercise selection, cancellation, failure, repeated clicks and owner disposal.
Adapt `scripts/check-selection.sh` to the current layout before running it;
it was designed to check production model code without AppKit.
Completion criterion: actions change state, Render only displays it, and I/O goes through a port.

## 4. Detect an installation

Add Features/DetectGameInstallation and the required Entities/Game models.
Check the selected directory and Steam metadata; distinguish missing, incomplete and ready installations.
First inspect real paths after installation; a directory's existence does not prove file integrity.
Completion criterion: diagnostic errors are clear, and repeating a check does not change game files.

## 5. Verify the runtime separately

Record the Mac model, macOS version, Rosetta, Wine, graphics layer and prefix.
Verify Windows Steam, sign-in, a complete match, input, audio and repeated launches.
Track the game separately from Steam. UI development can use a fake adapter,
but compatibility requires verification with the actual game.

## 6. Implement installation and launch

Features/InstallGame and Features/LaunchGame remain independent.
Pages coordinates them through public APIs; Shared provides generic process and I/O mechanisms.
Use ArgumentList, asynchronous I/O, progress reporting, timeouts, cancellation and diagnostics.
Cancelling preparation must not implicitly terminate an already running game.
Block file changes while the game or another launcher instance is running.

## 7. Implement mods and recovery

Start with a local VPK and one profile.
Calculate the plan, conflicts, ordering and checksums before writing files.
Apply changes through staging and a recovery journal; preserve unknown manual changes.
After a Steam update, read the current configuration. Test failures between replacements,
cancellation and recovery after process termination.

## 8. Release the application

Verify the Release .app on another Mac, storage in Application Support,
signing and notarization. Check redistribution terms for external runtimes.
Introduce Native AOT, an updater or a database when a concrete need arises.

## Ongoing requirements

Follow SOLID, DRY, KISS and Microsoft C# conventions; enable nullable analysis and analyzers.
Keep C# lines within 120 characters. Write documentation and code comments in English.
Introduce abstractions at I/O boundaries and where reuse has been demonstrated.
Validate the solution layout, FSD imports, builds and formatting; test behavior and failure risks.
Avoid mandatory inheritance, a global store, RabbitMQ/Kafka or Aspire.
See fsd-architecture.md and architecture.puml for details.
