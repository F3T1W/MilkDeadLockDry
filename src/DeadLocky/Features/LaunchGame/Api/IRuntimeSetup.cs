using DeadLocky.Features.LaunchGame.Model;

namespace DeadLocky.Features.LaunchGame.Api;

public interface IRuntimeSetupProvider
{
    IRuntimeSetup RuntimeSetup { get; }

    IRuntimeSetup CreateTestSetup();
}

public interface IRuntimeSetup
{
    GameLaunchConfiguration? FindExisting();

    string? FindAppleDownload();

    Task InstallRosettaAsync(CancellationToken token);

    Task<GameLaunchConfiguration> InstallAsync(string appleDownload,
        IProgress<RuntimeSetupProgress> progress, CancellationToken token);
}

public sealed record RuntimeSetupProgress(string Step, string Message, double? Fraction = null);
