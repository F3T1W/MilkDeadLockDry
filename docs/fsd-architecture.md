# FSD architecture: native macOS launcher

The organizing rules are FSD layers, isolated business slices and explicit public APIs.
AppKit is the platform adapter. State is represented by immutable records and controlled actions.
There is no mandatory MVVM, binding engine, RelayCommand, shared store or event bus.

## Current directories

- App/EntryPoint: Program and application lifecycle.
- App/Composition: creates the Launcher and binds IDirectoryPicker to AppKitDirectoryPicker.
- App/Ui: application menu.
- Pages/Launcher/Ui: screen layout, owns and disposes its feature views.
- Features/SelectGameDirectory/Model: immutable DirectorySelectionState and action controller.
- Features/SelectGameDirectory/Ui: native controls, event handlers and rendering.
- Entities/Game/Model: GameDirectory value, not a verified installation.
- Shared/Api/Directories: reusable directory-picker contract and native implementation.

MilkDeadLockDry/MilkDeadLockDry.csproj targets net10.0-macos.
FSD layers live directly in the project directory; the SDK discovers C# files automatically.
Docs and Scripts live at solution level and are solution items in MilkDeadLockDry.slnx.
The FSD layers are directories inside this project, not separate assemblies.
App → Pages / Shared; Pages → Features / Shared; Features → Entities / Shared.
No ProjectReference is needed. Dependencies below describe code imports, not assembly references.

App and Shared contain segments directly. Other layers contain slices, then segments.
Widgets appears only when there is a large reusable UI block. Processes is not used.
There is one executable project. Info.plist stays at the project root as required bundle metadata.

## Feature public API

SelectGameDirectoryView takes an IDirectoryPicker. Its SelectedDirectory property exposes the
current value and SelectionChanged notifies only when that value changes. Upper layers never
access DirectorySelectionState or DirectorySelectionController: both are internal.

GameDirectory is the entity public API. IDirectoryPicker is a generic Shared port:
PickAsync returns a path, null for user cancellation, or throws for owner cancellation/failure.
App supplies the concrete adapter; page and feature do not construct NSOpenPanel.

C# namespaces at the slice root are the public entry point. Public API consists only of documented
public types. All slices compile into one assembly: internal does not enforce slice isolation.
check-fsd.py guards the single-project layout and explicit namespace imports, not all semantic dependencies.
Same-layer cross-slice imports require removal or composition in a higher layer.

## State, actions and lifetime

The feature owns state: selected directory, dialog busy flag and error.
Choose sets busy, invokes the picker, then publishes a new immutable state.
A second Choose and Clear are ignored while busy. User cancellation preserves the old selection.
Failure preserves selection and becomes visible; retry clears the old error.
Clear resets state. Dispose cancels the operation and suppresses further notifications.
Controllers are owned by their view and used on the UI thread, not a global concurrent store.

View listens for StateChanged, marshals Render to the AppKit UI thread and sets native control
properties. Domain rules and external I/O never belong in Render. AppKitDirectoryPicker owns
the panel for the duration of its task, closes it when the owner's token is cancelled and releases it.
The page disposes views before releasing its window. AppDelegate owns the page controller.

## Product boundaries and next route

1. Detect an existing installation: Features/DetectGameInstallation, entity installation metadata.
   Confirm required files and metadata; a selected path alone is not an installation.
2. Verify a runtime and a complete match through a CLI before implementing real launch.
3. Install/update: Features/InstallGame. The feature API adapts Steam/DepotDownloader;
   Shared supplies domain-independent process/network mechanisms, not Steam Game rules.
4. Launch: Features/LaunchGame. Runtime/prefix/Steam ownership belongs in this scenario and its API.
5. Mods: Features/ApplyModProfile; Entities/ModProfile holds profile models.
   Plan before writes, check conflicts and serialize changes with staging and recovery journal.
6. Pages composes independent actions and coordinates results through their public APIs.
   A feature must not invoke a sibling feature. Shared must never accumulate business orchestration.

Installation data, runtime metadata, mod profiles and running sessions are separate entity candidates.
They are added when real scenarios need them; empty classes, repository interfaces and universal
generic services are not created in advance. JSON is sufficient initially; SQLite is introduced
only for a demonstrated storage/query requirement. No RabbitMQ, Kafka or Aspire orchestration.

## Code quality and validation

SOLID, DRY and KISS guide responsibilities, ports for I/O and removal of duplicated knowledge.
Abstractions need a concrete boundary or reuse, not a base class for every UI object.
Microsoft C# conventions are enforced by .editorconfig, Nullable and .NET analyzers.
File-changing scenarios need recovery tests; UI changes need real native smoke checks.

Scripts/check-selection.sh compiles the production Model, picker contract, entity and behavioral
checks in a temporary portable harness outside the repository, then removes it. No second project
is kept in the repository. This allows action behavior to be checked without AppKit or extra packages.
It checks selection, clear, user cancellation, failure/retry, busy guard and owner disposal.
It is a behavioral check executable, not a dotnet test runner and not a substitute for native UI tests.

Sources: [FSD layers](https://feature-sliced.design/docs/reference/layers),
[FSD public API](https://feature-sliced.design/docs/reference/public-api).
