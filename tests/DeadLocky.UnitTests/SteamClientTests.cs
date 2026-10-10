using DeadLocky.Entities.Steam.Model;
using DeadLocky.Features.LaunchGame.Api;
using DeadLocky.Features.PrepareGame.Api;
using DeadLocky.Features.PrepareGame.Model;
using DeadLocky.Shared.Api.Directories;

namespace DeadLocky.UnitTests;

public sealed class SteamClientTests
{
    [Theory]
    [InlineData("4", true)]
    [InlineData("1024", false)]
    [InlineData("0", false)]
    public void OnlyCompletedSteamManifestReportsGameInstalled(string flags, bool expected)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string game = Path.Combine(root, "steamapps", "common", "Deadlock");
        _ = Directory.CreateDirectory(Path.Combine(game, "game", "bin", "win64"));
        try
        {
            File.WriteAllText(Path.Combine(game, "game", "bin", "win64", "deadlock.exe"), "game");
            File.WriteAllText(SteamClientState.ManifestPath(root),
                $"AppState {{ appid 1422450 StateFlags {flags} installdir Deadlock }}");
            Assert.Equal(expected, SteamClientState.IsInstalled(root, game));
            Assert.False(SteamClientState.IsInstalled(root, Path.Combine(root, "other-game")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("users { 123 { AutoLogin 1 RememberPassword 1 PersonaName \"Player\" }")]
    [InlineData("users { 123 { AutoLogin 1 RememberPassword 1 PersonaName \"Player }")]
    [InlineData("users { 123 { AutoLogin 1 RememberPassword 1 PersonaName Player } } trailing")]
    public void MalformedMetadataDoesNotRestoreAnAccount(string contents)
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Path.Combine(root, "config"));
        try
        {
            File.WriteAllText(Path.Combine(root, "config", "loginusers.vdf"), contents);
            Assert.Null(SteamClientState.ReadSavedAccount(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SteamMetadataSupportsCommentsEscapesAndMostRecentTimestamp()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Path.Combine(root, "config"));
        try
        {
            File.WriteAllText(Path.Combine(root, "config", "loginusers.vdf"), """
                                                                              // Steam may persist several remembered accounts.
                                                                              "users" {
                                                                                  "123" { "AutoLogin" "1" "RememberPassword" "1" "PersonaName" "Older" "Timestamp" "2" }
                                                                                  "456" { "AutoLogin" "1" "RememberPassword" "1" "PersonaName" "Player \"Two\"" "Timestamp" "3" }
                                                                              }
                                                                              """);
            SteamAccount? account = SteamClientState.ReadSavedAccount(root);
            Assert.NotNull(account);
            Assert.Equal(456UL, account.Id);
            Assert.Equal("Player \"Two\"", account.Name);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("1", "1", true)]
    [InlineData("0", "1", false)]
    [InlineData("1", "0", false)]
    public void OnlyMostRecentRememberedAccountIsRestored(string recent, string remember, bool expected)
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Path.Combine(root, "config"));
        try
        {
            File.WriteAllText(Path.Combine(root, "config", "loginusers.vdf"),
                $"\"users\" {{ \"76561198000000001\" {{ \"PersonaName\" \"Test player\" "
                + $"\"MostRecent\" \"{recent}\" \"AllowAutoLogin\" \"{remember}\" }} }}");
            Assert.Equal(expected, SteamClientState.ReadSavedAccount(root) is not null);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RestoringClientSignInDoesNotOpenSteamOrNeedNetwork()
    {
        using var service = new SteamClientPreparationService("/unused", static () => new SteamAccount(1, "Player"),
            static _ => throw new InvalidOperationException("Unexpected Steam launch"),
            static (_, _) => throw new InvalidOperationException("Unexpected download"), static _ => false);
        SteamAccount? account = await service.RestoreSessionAsync(CancellationToken.None);
        Assert.Equal("Player", account?.Name);
    }

    [Theory]
    [InlineData("1", "1", true)]
    [InlineData("0", "1", false)]
    [InlineData("1", "0", false)]
    public void CurrentSteamRememberedAccountSchemaIsSupported(string autoLogin, string remember, bool expected)
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(Path.Combine(root, "config"));
        try
        {
            File.WriteAllText(Path.Combine(root, "config", "loginusers.vdf"),
                $"\"users\" {{ \"76561198000000001\" {{ \"PersonaName\" \"Test player\" "
                + $"\"AutoLogin\" \"{autoLogin}\" \"RememberPassword\" \"{remember}\" }} }}");
            Assert.Equal(expected, SteamClientState.ReadSavedAccount(root) is not null);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SignInWaitsForTheSameClientToPersistItsLogin()
    {
        SteamAccount? account = null;
        int opens = 0;
        using var service = new SteamClientPreparationService("/unused", () => account, _ =>
        {
            opens++;
            account = new SteamAccount(1, "Player");
            return Task.CompletedTask;
        }, static (_, _) => Task.CompletedTask, static _ => false);
        SteamAccount result = await service.SignInAsync(CancellationToken.None);
        Assert.Equal(1, opens);
        Assert.Equal(account, result);
    }

    [Fact]
    public async Task CancellingClientDownloadWaitPreservesInstalledGame()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new SteamClientPreparationService("/unused", static () => new SteamAccount(1, "Player"),
            static _ => Task.CompletedTask, async (_, token) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }, static _ => true);
        using var controller = new GamePreparationController(service, new UnusedPicker());
        await controller.InitializeAsync();
        Task download = controller.DownloadAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        controller.Cancel();
        await download.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(controller.State.IsDownloaded);
        Assert.Null(controller.State.Error);
    }

    [Fact]
    public void LinkingEmptyInstallFolderDoesNotPretendGameIsInstalled()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string game = Path.Combine(root, "game");
        _ = Directory.CreateDirectory(game);
        try
        {
            SteamInstallationLink.Register(Path.Combine(root, "Steam", "steam.exe"), game);
            Assert.False(File.Exists(SteamClientState.ManifestPath(Path.Combine(root, "Steam"))));
            Assert.False(SteamClientState.IsInstalled(Path.Combine(root, "Steam"), game));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class UnusedPicker : IDirectoryPicker
    {
        public Task<string?> PickAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
