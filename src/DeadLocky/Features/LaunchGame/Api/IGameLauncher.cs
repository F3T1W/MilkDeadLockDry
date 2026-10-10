using DeadLocky.Features.LaunchGame.Model;

namespace DeadLocky.Features.LaunchGame.Api;

public interface IGameSession : IDisposable
{
    Task Completion { get; }
}

public interface IGameLauncher : IDisposable
{
    GameLaunchConfiguration? Configuration { get; }

    void Configure(GameLaunchConfiguration configuration);

    Task OpenSteamAsync(CancellationToken cancellationToken);

    Task<IGameSession?> FindRunningAsync(string directory, CancellationToken cancellationToken);

    Task<IGameSession> LaunchAsync(string directory, CancellationToken cancellationToken);
}
