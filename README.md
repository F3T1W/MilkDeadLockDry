# MilkDeadLockDry

Local luancher starting solution: .NET 10, Avalonia/XAML,
CommunityToolkit.Mvvm

## Projects and dependencies

- **MilkDeadLockDry.Core**: Domain - models and rules; Application - scenarios and contracts
- **MilkDeadLockDry.Infrastructure**: processes, Wine, file и storage, depends on Core
- **MilkDeadLockDry.Desktop**: Avalonia Views и ViewModels, depends on Core and Infrastructure
  Infrastructure link used by build dependencies in composition root

Core doesn't depends on another projects

All three projects are inclided in desktop-app
Folders Domain/Application includes only responsibility descriptions for now

## First Step

- In Core LaunchRequest, GameSession, IGameRunner и LaunchGameUseCase need to be implemented
- Then connect FakeGameRunner and button with progress/cancel in Desktop
- Real WineGameRunner needs to be added into Infrastructure after runtime check through CLI
- Deadlock launch and mod managment isn't implemented yet
