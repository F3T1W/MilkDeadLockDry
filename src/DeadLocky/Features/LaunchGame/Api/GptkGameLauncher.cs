using System.Diagnostics;
using DeadLocky.Entities.Steam.Model;
using DeadLocky.Features.LaunchGame.Model;

namespace DeadLocky.Features.LaunchGame.Api;

public sealed class GptkGameLauncher : IGameLauncher, IGameExitPreferences, IRuntimeSetupProvider
{
    private readonly GameExitPreferencesStore _preferences;
    private readonly SemaphoreSlim _start = new(1, 1);
    private readonly GptkConfigurationStore _store;
    private readonly string _storePath;
    private bool _closeSteamAfterExit;

    public GptkGameLauncher(string configurationPath)
    {
        _storePath = configurationPath;
        _store = new GptkConfigurationStore(configurationPath);
        Configuration = _store.Read() ?? _store.DiscoverOwnedInstallation();
        _preferences = new GameExitPreferencesStore(Path.ChangeExtension(configurationPath, ".preferences.json"));
        _closeSteamAfterExit = _preferences.Read().CloseSteamAfterExit;
        RuntimeSetup = new RuntimeSetupInstaller(Path.GetDirectoryName(Path.GetFullPath(configurationPath))!);
    }

    public bool CloseSteamAfterExit
    {
        get => Volatile.Read(ref _closeSteamAfterExit);
        set
        {
            _preferences.Save(new GameExitPreferences(value));
            Volatile.Write(ref _closeSteamAfterExit, value);
        }
    }

    public GameLaunchConfiguration? Configuration { get; private set; }

    public void Configure(GameLaunchConfiguration configuration)
    {
        GptkConfigurationStore.Validate(configuration);
        _store.Save(configuration);
        Configuration = configuration;
    }

    public async Task OpenSteamAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GameLaunchConfiguration configuration = ValidConfiguration();
        await PrepareCrashHandlingAsync(configuration, cancellationToken);
        Task<int> command = StartSteam(configuration, false);
        _ = await Task.WhenAny(command, Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));
        cancellationToken.ThrowIfCancellationRequested();
        if (command.IsCompletedSuccessfully && command.Result != 0)
        {
            throw new IOException($"GPTK failed to start Windows Steam (exit code {command.Result}).");
        }
    }

    public async Task<IGameSession?> FindRunningAsync(string directory, CancellationToken cancellationToken)
    {
        if (Configuration is not { } configuration)
        {
            return null;
        }

        bool running = await Task.Run(() => MacGameProcessScanner.IsRunning(configuration, directory),
            cancellationToken);
        return running ? CreateSession(configuration, directory) : null;
    }

    public async Task<IGameSession> LaunchAsync(string directory, CancellationToken cancellationToken)
    {
        await _start.WaitAsync(cancellationToken);
        try
        {
            GameLaunchConfiguration configuration = ValidConfiguration();
            await PrepareCrashHandlingAsync(configuration, cancellationToken);
            if (!File.Exists(Path.Combine(directory, "game", "bin", "win64", "deadlock.exe")))
            {
                throw new IOException("Deadlock's executable is missing. Verify the download first.");
            }

            if (await FindRunningAsync(directory, cancellationToken) is { } existing)
            {
                return existing;
            }

            SteamInstallationLink.Register(GptkConfigurationStore.SteamExecutable(configuration), directory);
            Task<int> command = StartSteam(configuration, true);
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromMinutes(2))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await Task.Run(() => MacGameProcessScanner.IsRunning(configuration, directory), cancellationToken))
                {
                    return CreateSession(configuration, directory);
                }

                if (command.IsCompletedSuccessfully && command.Result != 0)
                {
                    throw new IOException($"GPTK failed to start Windows Steam (exit code {command.Result}).");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }

            throw new IOException("Windows Steam did not start Deadlock. Finish signing in there, "
                                  + "let Steam recognize the linked game files, then retry Play.");
        }
        finally
        {
            _ = _start.Release();
        }
    }

    public void Dispose()
    {
    }

    public IRuntimeSetup RuntimeSetup { get; }

    public IRuntimeSetup CreateTestSetup()
    {
        string root = Path.GetDirectoryName(Path.GetFullPath(_storePath))!;
        return new RuntimeSetupInstaller(Path.Combine(root, "SetupTests", Guid.NewGuid().ToString("N")));
    }

    public SteamAccount? ReadSavedSteamAccount()
    {
        return Configuration is null
            ? null
            : SteamClientState.ReadSavedAccount(
                Path.GetDirectoryName(GptkConfigurationStore.SteamExecutable(Configuration))!);
    }

    public bool IsSteamInstallationReady(string directory)
    {
        return Configuration is not null
               && SteamClientState.IsInstalled(
                   Path.GetDirectoryName(GptkConfigurationStore.SteamExecutable(Configuration))!,
                   directory);
    }

    public async Task DownloadWithSteamAsync(string directory, CancellationToken token)
    {
        GameLaunchConfiguration configuration = ValidConfiguration();
        await PrepareCrashHandlingAsync(configuration, token);
        string executable = GptkConfigurationStore.SteamExecutable(configuration);
        string steamDirectory = Path.GetDirectoryName(executable)!;
        _ = Directory.CreateDirectory(directory);
        SteamInstallationLink.Register(executable, directory);
        string manifest = SteamClientState.ManifestPath(steamDirectory);
        DateTime before = File.GetLastWriteTimeUtc(manifest);
        ProcessStartInfo start = CreateStartInfo(configuration, false);
        start.ArgumentList.Add("steam://install/1422450");
        Process process = Process.Start(start) ?? throw new IOException("Unable to open Steam installation.");
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        Task<int> command = WaitForCommandAsync(process);
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), token);
            if (command.IsCompletedSuccessfully && command.Result != 0)
            {
                throw new IOException("Steam installation failed to open.");
            }

            if (File.GetLastWriteTimeUtc(manifest) != before && SteamClientState.IsInstalled(steamDirectory, directory))
            {
                return;
            }
        }
    }

    private GameLaunchConfiguration ValidConfiguration()
    {
        GameLaunchConfiguration configuration = Configuration
                                                ?? throw new IOException(
                                                    "Set up GPTK and a Windows Steam prefix before playing.");
        GptkConfigurationStore.Validate(configuration);
        return configuration;
    }

    private GameProcessSession CreateSession(GameLaunchConfiguration configuration, string directory)
    {
        return new GameProcessSession(() => MacGameProcessScanner.IsRunning(configuration, directory),
            token => CloseSteamAfterExit
                ? CloseSteamAfterGameAsync(configuration, directory, token)
                : Task.CompletedTask);
    }

    private static async Task PrepareCrashHandlingAsync(GameLaunchConfiguration configuration, CancellationToken token)
    {
        ProcessStartInfo start = CreateStartInfo(configuration, false);
        start.ArgumentList.Clear();
        foreach (string argument in new[]
                 {
                     "reg", "add", @"HKCU\Software\Wine\WineDbg", "/v", "ShowCrashDialog", "/t", "REG_DWORD", "/d", "0",
                     "/f"
                 })
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)
                                ?? throw new IOException("Unable to configure Wine crash handling.");
        Task output = process.StandardOutput.ReadToEndAsync(token);
        Task error = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        await Task.WhenAll(output, error);
        if (process.ExitCode != 0)
        {
            throw new IOException("Unable to configure automatic crash-dialog dismissal.");
        }
    }

    private static async Task CloseSteamAfterGameAsync(GameLaunchConfiguration configuration, string directory,
        CancellationToken token)
    {
        GptkConfigurationStore.Validate(configuration);
        if (MacGameProcessScanner.IsRunning(configuration, directory))
        {
            return;
        }

        ProcessStartInfo shutdown = CreateStartInfo(configuration, false);
        shutdown.ArgumentList.Add("-shutdown");
        using (Process process = Process.Start(shutdown) ?? throw new IOException("Unable to close Windows Steam."))
        {
            Task output = process.StandardOutput.ReadToEndAsync(token);
            Task error = process.StandardError.ReadToEndAsync(token);
            _ = await Task.WhenAny(process.WaitForExitAsync(token), Task.Delay(TimeSpan.FromSeconds(8), token));
            token.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(3), token);
            _ = Task.WhenAll(output, error);
        }

        if (MacGameProcessScanner.IsRunning(configuration, directory))
        {
            return;
        }

        string server = Path.Combine(Path.GetDirectoryName(configuration.WineExecutable)!, "wineserver");
        if (!File.Exists(server))
        {
            throw new IOException("The GPTK wineserver executable is missing.");
        }

        var stop = new ProcessStartInfo(server)
        {
            UseShellExecute = false,
            Environment = { ["WINEPREFIX"] = configuration.PrefixDirectory }
        };
        stop.ArgumentList.Add("-k");
        token.ThrowIfCancellationRequested();
        using Process cleanup = Process.Start(stop) ?? throw new IOException("Unable to stop the GPTK prefix.");
        await cleanup.WaitForExitAsync(token);
        if (cleanup.ExitCode != 0)
        {
            throw new IOException("GPTK could not stop leftover Steam processes.");
        }

        if (!MacGameProcessScanner.IsRunning(configuration, directory))
        {
            await MacGameProcessScanner.StopPrefixProcessesAsync(configuration, token);
        }
    }

    private static Task<int> StartSteam(GameLaunchConfiguration configuration, bool launchGame)
    {
        ProcessStartInfo start = CreateStartInfo(configuration, launchGame);
        Process process = Process.Start(start) ?? throw new IOException("Unable to start GPTK.");
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return WaitForCommandAsync(process);
    }

    private static async Task<int> WaitForCommandAsync(Process process)
    {
        using (process)
        {
            await process.WaitForExitAsync();
            return process.ExitCode;
        }
    }

    internal static ProcessStartInfo CreateStartInfo(GameLaunchConfiguration configuration, bool launchGame)
    {
        var start = new ProcessStartInfo(configuration.WineExecutable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = configuration.PrefixDirectory
        };
        start.ArgumentList.Add(GptkConfigurationStore.SteamExecutable(configuration));
        if (launchGame)
        {
            start.ArgumentList.Add("-applaunch");
            start.ArgumentList.Add("1422450");
            start.ArgumentList.Add("-dx11");
        }

        start.Environment["WINEPREFIX"] = configuration.PrefixDirectory;
        start.Environment["WINEESYNC"] = "1";
        start.Environment["WINEDEBUG"] = "-all";
        start.Environment["MTL_HUD_ENABLED"] = "0";
        start.Environment["WINEDLLOVERRIDES"] = "d3d11,d3d12,dxgi=n,b";
        string runtime = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(configuration.WineExecutable)!, ".."));
        start.Environment["DYLD_FALLBACK_LIBRARY_PATH"] = Path.Combine(runtime, "lib") + ":/usr/local/lib:/usr/lib";
        return start;
    }
}
