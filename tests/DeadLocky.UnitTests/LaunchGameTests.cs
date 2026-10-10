using System.Diagnostics;
using DeadLocky.Features.LaunchGame.Api;
using DeadLocky.Features.LaunchGame.Model;

namespace DeadLocky.UnitTests;

public sealed class LaunchGameTests
{
    [Fact]
    public async Task RunningStateTracksGameCompletionAndPreventsDuplicateLaunch()
    {
        var launcher = new FakeLauncher();
        using var controller = new GameLaunchController(launcher);
        controller.SetInstallation("/game", true, false);
        var launch = controller.LaunchAsync();
        Assert.True(controller.State.IsRunning);
        Assert.False(controller.State.CanLaunch);
        await controller.LaunchAsync();
        Assert.Equal(1, launcher.Starts);
        launcher.Session.Exit.SetResult();
        await launch;
        Assert.False(controller.State.IsRunning);
        Assert.True(controller.State.CanLaunch);
    }

    [Fact]
    public async Task StartupFailureAllowsRetry()
    {
        var launcher = new FakeLauncher { Fail = true };
        using var controller = new GameLaunchController(launcher);
        controller.SetInstallation("/game", true, false);
        await controller.LaunchAsync();
        Assert.Contains("Failed", controller.State.Error);
        Assert.True(controller.State.CanLaunch);
        launcher.Fail = false;
        var retry = controller.LaunchAsync();
        Assert.True(controller.State.IsRunning);
        launcher.Session.Exit.SetResult();
        await retry;
    }

    [Fact]
    public async Task ExistingGameIsObservedWithoutLaunchingAndDisposalOnlyDetaches()
    {
        var launcher = new FakeLauncher { Existing = true };
        var controller = new GameLaunchController(launcher);
        controller.SetInstallation("/game", true, false);
        var observation = controller.CheckRunningAsync();
        Assert.True(controller.State.IsRunning);
        Assert.Equal(0, launcher.Starts);
        controller.Dispose();
        await observation;
        Assert.False(launcher.Session.Exit.Task.IsCompleted);
        Assert.True(launcher.Session.Disposed);
    }

    [Fact]
    public async Task DownloadOrUnverifiedInstallationBlocksLaunch()
    {
        var launcher = new FakeLauncher();
        using var controller = new GameLaunchController(launcher);
        controller.SetInstallation("/game", false, false);
        await controller.LaunchAsync();
        controller.SetInstallation("/game", true, true);
        await controller.LaunchAsync();
        Assert.Equal(0, launcher.Starts);
    }

    [Fact]
    public async Task NativeScannerMatchesGameAndPrefixWhileIgnoringOtherPrefix()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string executable = Path.Combine(root, "game", "bin", "win64", "deadlock.exe");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        var compilerInfo = new ProcessStartInfo("/usr/bin/clang")
        {
            UseShellExecute = false,
            RedirectStandardInput = true
        };
        foreach (string argument in new[] { "-x", "c", "-o", executable, "-" })
        {
            compilerInfo.ArgumentList.Add(argument);
        }

        using (var compiler = Process.Start(compilerInfo)!)
        {
            await compiler.StandardInput.WriteAsync("#include <unistd.h>\nint main(void) { sleep(15); return 0; }\n");
            compiler.StandardInput.Close();
            await compiler.WaitForExitAsync();
            Assert.Equal(0, compiler.ExitCode);
        }

        try
        {
            var info = new ProcessStartInfo(executable) { UseShellExecute = false };
            info.ArgumentList.Add("15");
            info.Environment["WINEPREFIX"] = root;
            using var process = Process.Start(info)!;
            try
            {
                var configuration = new GameLaunchConfiguration("/unused", root);
                Assert.True(MacGameProcessScanner.IsRunning(configuration, root));
                Assert.False(MacGameProcessScanner.IsRunning(configuration with { PrefixDirectory = "/other" }, root));
                int[] running = [1];
                using var session = new GameProcessSession(() => Volatile.Read(ref running[0]) == 1);
                Assert.False(session.Completion.IsCompleted);
                Volatile.Write(ref running[0], 0);
                await session.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    await process.WaitForExitAsync();
                }
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SteamRegistrationLinksFilesAndPreservesExistingManifest()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string game = Path.Combine(root, "download");
        string steam = Path.Combine(root, "Steam", "steam.exe");
        _ = Directory.CreateDirectory(game);
        _ = Directory.CreateDirectory(Path.Combine(game, "game", "bin", "win64"));
        File.WriteAllText(Path.Combine(game, "game", "bin", "win64", "deadlock.exe"), "");
        try
        {
            SteamInstallationLink.Register(steam, game);
            string manifest = Path.Combine(root, "Steam", "steamapps", "appmanifest_1422450.acf");
            File.AppendAllText(manifest, "\n// preserved Steam state");
            string original = File.ReadAllText(manifest);
            SteamInstallationLink.Register(steam, game);
            Assert.Equal(original, File.ReadAllText(manifest));
            Assert.Equal(game,
                new DirectoryInfo(Path.Combine(root, "Steam", "steamapps", "common", "Deadlock")).LinkTarget);
            File.WriteAllText(manifest, "\"installdir\" \"Other\"");
            _ = Assert.Throws<IOException>(() => SteamInstallationLink.Register(steam, game));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SteamCommandPreservesPathsAsSingleArguments()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "prefix with spaces");
        string steam = Path.Combine(root, "drive_c", "Program Files (x86)", "Steam", "steam.exe");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(steam)!);
        File.WriteAllText(steam, "");
        try
        {
            var configuration = new GameLaunchConfiguration("/runtime with spaces/bin/wine64", root);
            var info = GptkGameLauncher.CreateStartInfo(configuration, true);
            Assert.False(info.UseShellExecute);
            Assert.Equal(configuration.WineExecutable, info.FileName);
            Assert.Equal(new[] { steam, "-applaunch", "1422450", "-dx11" },
                info.ArgumentList);
            Assert.Equal(root, info.Environment["WINEPREFIX"]);
            Assert.Equal("/runtime with spaces/lib:/usr/local/lib:/usr/lib",
                info.Environment["DYLD_FALLBACK_LIBRARY_PATH"]);
            Assert.DoesNotContain("STEAM_REFRESH_TOKEN", info.Environment.Keys);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, true);
        }
    }

    [Fact]
    public void ConflictingManifestIsRejectedBeforeCreatingLink()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string steamApps = Path.Combine(root, "steamapps");
        _ = Directory.CreateDirectory(steamApps);
        File.WriteAllText(Path.Combine(steamApps, "appmanifest_1422450.acf"), "\"installdir\" \"Other\"");
        try
        {
            _ = Assert.Throws<IOException>(() => SteamInstallationLink.Register(Path.Combine(root, "steam.exe"), root));
            Assert.False(Directory.Exists(Path.Combine(steamApps, "common")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class FakeLauncher : IGameLauncher
    {
        public FakeSession Session { get; } = new();
        public int Starts { get; private set; }
        public bool Fail { get; set; }
        public bool Existing { get; init; }
        public GameLaunchConfiguration? Configuration { get; private set; }

        public void Configure(GameLaunchConfiguration configuration)
        {
            Configuration = configuration;
        }

        public Task OpenSteamAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IGameSession?> FindRunningAsync(string directory, CancellationToken cancellationToken)
        {
            return Task.FromResult<IGameSession?>(Existing ? Session : null);
        }

        public Task<IGameSession> LaunchAsync(string directory, CancellationToken cancellationToken)
        {
            Starts++;
            return Fail
                ? Task.FromException<IGameSession>(new IOException("Failed"))
                : Task.FromResult<IGameSession>(Session);
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeSession : IGameSession
    {
        public TaskCompletionSource Exit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }
        public Task Completion => Exit.Task;

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
