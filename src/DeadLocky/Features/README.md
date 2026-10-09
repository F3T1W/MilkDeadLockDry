# Features

Each slice represents an independent user action, with Model/Ui/Api segments as needed.
SelectGameDirectory uses immutable state and a controller, not MVVM.
Public API: SelectGameDirectoryView(IDirectoryPicker), SelectedDirectory, SelectionChanged.
State/controller details remain internal. The generic picker port is injected from App.
Business adapters belong to feature/entity APIs; generic I/O lives in Shared.
Features never import peer features. Coordinate separate features in Pages or App.
