using System.Net;
using System.Security.Cryptography;
using DeadLocky.Features.LaunchGame.Api;
using DeadLocky.Features.LaunchGame.Model;

namespace DeadLocky.UnitTests;

public sealed class RuntimeSetupTests : IDisposable
{
    private readonly IProgress<RuntimeSetupProgress> _progress = new Progress<RuntimeSetupProgress>();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "deadlocky-setup-test-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public async Task TestSetupDoesNotDiscoverOrOverwriteProductionEnvironment()
    {
        string apple = MakeAppleDownload();
        var platform = new SetupCommands();
        GameLaunchConfiguration production = await new RuntimeSetupInstaller(_root, platform)
            .InstallAsync(apple, _progress, CancellationToken.None);
        string configurationPath = Path.Combine(_root, "launch.json");
        new GptkConfigurationStore(configurationPath).Save(production);
        byte[] original = await File.ReadAllBytesAsync(configurationPath);
        var launcher = new GptkGameLauncher(configurationPath);
        IRuntimeSetupProvider provider = Assert.IsType<IRuntimeSetupProvider>(launcher, exactMatch: false);
        IRuntimeSetup test = provider.CreateTestSetup();
        Assert.Null(test.FindExisting());
        Assert.Null(provider.CreateTestSetup().FindExisting());
        Assert.Equal(production, launcher.Configuration);
        Assert.Equal(original, await File.ReadAllBytesAsync(configurationPath));
        Assert.True(File.Exists(production.WineExecutable));
    }

    [Fact]
    public async Task SetupCommandDoesNotWaitForPipesInheritedByBackgroundServices()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _ = await new RuntimeSetupPlatform().RunAsync("/bin/sh", ["-c", "sleep 5 & exit 0"], _root, null,
                CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task CorruptDownloadCannotReplaceCacheAndCanBeRetried()
    {
        _ = Directory.CreateDirectory(_root);
        string target = Path.Combine(_root, "download");
        await File.WriteAllTextAsync(target, "invalid cached file");
        byte[] expected = [.. "verified archive"u8];
        var download = new RuntimeDownload("test", "https://example.invalid/archive",
            Convert.ToHexString(SHA256.HashData(expected)));
        using var client = new HttpClient(new Responses([.. "corrupted archive"u8], expected));
        var platform = new RuntimeSetupPlatform(client);
        _ = await Assert.ThrowsAsync<IOException>(() =>
            platform.DownloadAsync(download, target, _progress, CancellationToken.None));
        Assert.Equal("invalid cached file", await File.ReadAllTextAsync(target));
        Assert.False(File.Exists(target + ".partial"));
        await platform.DownloadAsync(download, target, _progress, CancellationToken.None);
        Assert.Equal(expected, await File.ReadAllBytesAsync(target));
        await platform.DownloadAsync(download, target, _progress, CancellationToken.None);
    }

    [Fact]
    public async Task CancellationRemovesOnlyNewInstallationAndRetryProducesDiscoverableEnvironment()
    {
        string apple = MakeAppleDownload();
        string existing = Path.Combine(_root, "Runtime", "OtherInstallation", "keep.txt");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        await File.WriteAllTextAsync(existing, "preserve");
        var platform = new SetupCommands { CancelSteamInstall = true };
        var installer = new RuntimeSetupInstaller(_root, platform);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            installer.InstallAsync(apple, _progress, CancellationToken.None));
        Assert.False(Directory.Exists(Path.Combine(_root, "Prefixes", "Steam-Managed")));
        Assert.False(Directory.Exists(Path.Combine(_root, "Runtime", "ManagedWine11-r21")));
        Assert.Empty(Directory.EnumerateDirectories(_root, ".setup-*"));
        Assert.Equal("preserve", await File.ReadAllTextAsync(existing));
        Assert.False(File.Exists(Path.Combine(_root, "launch.json")));
        platform.CancelSteamInstall = false;
        GameLaunchConfiguration configuration = await installer.InstallAsync(apple, _progress, CancellationToken.None);
        Assert.Equal(configuration, installer.FindExisting());
        int calls = platform.DownloadCalls;
        Assert.Equal(configuration,
            await installer.InstallAsync("no source needed", _progress, CancellationToken.None));
        Assert.Equal(calls, platform.DownloadCalls);
    }

    [Fact]
    public async Task UnmarkedDestinationIsNeverDeletedOrReplaced()
    {
        string apple = MakeAppleDownload();
        string other = Path.Combine(_root, "Prefixes", "Steam-Managed", "keep.txt");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(other)!);
        await File.WriteAllTextAsync(other, "preserve");
        var platform = new SetupCommands();
        _ = await Assert.ThrowsAsync<IOException>(() => new RuntimeSetupInstaller(_root, platform)
            .InstallAsync(apple, _progress, CancellationToken.None));
        Assert.Equal("preserve", await File.ReadAllTextAsync(other));
        Assert.Equal(0, platform.DownloadCalls);
    }

    [Fact]
    public async Task InterruptedOwnedSetupIsRecoveredButIncompleteSteamIsNotReportedReady()
    {
        string apple = MakeAppleDownload();
        string prefix = Path.Combine(_root, "Prefixes", "Steam-Managed");
        _ = Directory.CreateDirectory(prefix);
        await File.WriteAllTextAsync(Path.Combine(prefix, ".deadlocky-owned"), "Steam-Managed");
        string incomplete = Path.Combine(prefix, "half-installed.txt");
        await File.WriteAllTextAsync(incomplete, "incomplete");
        var installer = new RuntimeSetupInstaller(_root, new SetupCommands());
        Assert.Null(installer.FindExisting());
        GameLaunchConfiguration configuration = await installer.InstallAsync(apple, _progress, CancellationToken.None);
        Assert.False(File.Exists(incomplete));
        Assert.Equal(configuration, installer.FindExisting());
    }

    [Fact]
    public async Task InvalidAppleDownloadFailsBeforeAnyEnvironmentDownload()
    {
        _ = Directory.CreateDirectory(_root);
        var platform = new SetupCommands();
        _ = await Assert.ThrowsAsync<IOException>(() => new RuntimeSetupInstaller(_root, platform)
            .InstallAsync(_root, _progress, CancellationToken.None));
        Assert.Equal(0, platform.DownloadCalls);
    }

    private string MakeAppleDownload()
    {
        string redist = Path.Combine(_root, "Apple", "redist");
        foreach (string path in new[]
                 {
                     "lib/external/D3DMetal.framework/D3DMetal", "lib/external/libd3dshared.dylib",
                     "lib/wine/x86_64-windows/d3d11.dll"
                 })
        {
            string file = Path.Combine(redist, path);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "graphics");
        }

        return Path.GetDirectoryName(redist)!;
    }

    private sealed class Responses(params byte[][] responses) : HttpMessageHandler
    {
        private int _next;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            return _next >= responses.Length
                ? throw new IOException("Network unavailable")
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(responses[_next++])
                });
        }
    }

    private sealed class SetupCommands : RuntimeSetupPlatform
    {
        public bool CancelSteamInstall { get; set; }
        public int DownloadCalls { get; private set; }

        public override Task CheckHostAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public override Task StopPrefixProcessesAsync(GameLaunchConfiguration configuration, CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public override Task DownloadAsync(RuntimeDownload download, string target,
            IProgress<RuntimeSetupProgress> progress, CancellationToken token)
        {
            DownloadCalls++;
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, "archive");
            return Task.CompletedTask;
        }

        public override Task<string> RunAsync(string executable, IEnumerable<string> arguments,
            string? prefix, string? libraryPath, CancellationToken token)
        {
            string[] args = [.. arguments];
            switch (executable)
            {
                case "/usr/bin/tar":
                {
                    string destination = args[^1];
                    if (args[1].EndsWith("wine11.tar.gz", StringComparison.Ordinal))
                    {
                        _ = Directory.CreateDirectory(Path.Combine(destination, "engine", "bin"));
                        _ = Directory.CreateDirectory(Path.Combine(destination, "engine", "lib"));
                        File.WriteAllText(Path.Combine(destination, "engine", "bin", "wine"), "wine");
                    }
                    else
                    {
                        _ = Directory.CreateDirectory(Path.Combine(destination, "Game Porting Toolkit.app", "Contents",
                            "Resources", "wine", "lib"));
                    }

                    break;
                }
                case "/usr/bin/ditto":
                {
                    CopyFolder(args[0], args[1]);
                    break;
                }
                default:
                {
                    if (args.Contains("/S"))
                    {
                        if (CancelSteamInstall)
                        {
                            return Task.FromCanceled<string>(new CancellationToken(true));
                        }

                        string steam = Path.Combine(prefix!, "drive_c", "Program Files (x86)", "Steam", "steam.exe");
                        _ = Directory.CreateDirectory(Path.GetDirectoryName(steam)!);
                        File.WriteAllText(steam, "Steam");
                    }

                    break;
                }
            }

            return Task.FromResult("");
        }

        private static void CopyFolder(string source, string destination)
        {
            _ = Directory.CreateDirectory(destination);
            foreach (string folder in Directory.EnumerateDirectories(source))
            {
                CopyFolder(folder, Path.Combine(destination, Path.GetFileName(folder)));
            }

            foreach (string file in Directory.EnumerateFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            }
        }
    }
}
