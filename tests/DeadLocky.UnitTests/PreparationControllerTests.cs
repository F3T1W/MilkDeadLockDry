using DeadLocky.Entities.Game.Model;
using DeadLocky.Entities.Steam.Model;
using DeadLocky.Features.PrepareGame.Api;
using DeadLocky.Features.PrepareGame.Model;
using DeadLocky.Shared.Api.Directories;

namespace DeadLocky.UnitTests;

public sealed class PreparationControllerTests
{
    [Fact]
    public async Task CancelDoesNotWaitForTransportCallbacksOnCallerThread()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeService
        {
            SignIn = async token =>
            {
                var pending = new TaskCompletionSource<SteamAccount>().Task.WaitAsync(token);
                await using var registration = token.Register(() =>
                {
                    _ = started.TrySetResult();
                    _ = release.Task.Wait(TimeSpan.FromSeconds(5));
                });
                return await pending;
            }
        };
        using var controller = new GamePreparationController(service, new FakePicker());
        var signingIn = controller.SignInAsync();
        var cancel = Task.Run(controller.Cancel);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await cancel.WaitAsync(TimeSpan.FromSeconds(1));
        }
        finally
        {
            _ = release.TrySetResult();
        }

        await signingIn;
        Assert.False(controller.State.IsBusy);
    }

    [Fact]
    public async Task SignInAfterDisconnectReusesSavedClientSession()
    {
        var service = new FakeService { Account = new SteamAccount(1, "player") };
        using var controller = new GamePreparationController(service, new FakePicker());
        await controller.InitializeAsync();
        service.Disconnect();
        Assert.Null(controller.State.Account);
        await controller.SignInAsync();
        Assert.Equal(service.Account, controller.State.Account);
        Assert.Equal(0, service.SignInCount);
        Assert.Null(controller.State.Error);
    }

    [Fact]
    public async Task StartupChecksLocalInstallationAndRestoresRealSession()
    {
        var service = new FakeService { Downloaded = true, Account = new SteamAccount(1, "player") };
        using var controller = new GamePreparationController(service, new FakePicker());
        await controller.InitializeAsync();
        Assert.True(controller.State.IsDownloaded);
        Assert.Equal(service.Account, controller.State.Account);
        Assert.False(controller.State.IsBusy);
    }

    [Fact]
    public async Task SignInFailureDoesNotSetAccountCheckmark()
    {
        var service = new FakeService { SignIn = static _ => throw new IOException("offline") };
        using var controller = new GamePreparationController(service, new FakePicker());
        await controller.SignInAsync();
        Assert.Null(controller.State.Account);
        Assert.Equal("offline", controller.State.Error);
        Assert.False(controller.State.IsBusy);
    }

    [Fact]
    public async Task CancellationRejectsLateAuthenticationResultAndBlocksRepeatedActions()
    {
        var completion = new TaskCompletionSource<SteamAccount>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeService { SignIn = _ => completion.Task };
        using var controller = new GamePreparationController(service, new FakePicker());
        var signingIn = controller.SignInAsync();
        await controller.SignInAsync();
        await controller.ChooseDirectoryAsync();
        Assert.Equal(1, service.SignInCount);
        Assert.Equal(PreparationAction.SigningIn, controller.State.Action);
        controller.Cancel();
        completion.SetResult(new SteamAccount(1, "late"));
        await signingIn;
        Assert.Null(controller.State.Account);
        Assert.Null(controller.State.Error);
        Assert.False(controller.State.IsBusy);
    }

    [Fact]
    public async Task CancelledFolderPickerPreservesDirectoryAndDownloadState()
    {
        var service = new FakeService { Downloaded = true };
        using var controller = new GamePreparationController(service, new FakePicker());
        await controller.InitializeAsync();
        await controller.ChooseDirectoryAsync();
        Assert.Equal(service.Directory, controller.State.Directory);
        Assert.True(controller.State.IsDownloaded);
        Assert.Equal(0, service.SaveCount);
    }

    [Fact]
    public async Task SelectingAnotherDirectoryRechecksInstallation()
    {
        var service = new FakeService { Downloaded = true };
        using var controller = new GamePreparationController(service, new FakePicker { Path = "/different" });
        await controller.InitializeAsync();
        service.Downloaded = false;
        await controller.ChooseDirectoryAsync();
        Assert.Equal("/different", controller.State.Directory.Path);
        Assert.False(controller.State.IsDownloaded);
        Assert.Equal(1, service.SaveCount);
    }

    [Fact]
    public async Task DownloadRequiresAccountAndConfirmedFiles()
    {
        var service = new FakeService();
        using var controller = new GamePreparationController(service, new FakePicker());
        await controller.DownloadAsync();
        Assert.Equal(0, service.DownloadCount);
        Assert.False(controller.State.IsDownloaded);
        await controller.SignInAsync();
        await controller.DownloadAsync();
        Assert.Equal(1, service.DownloadCount);
        Assert.False(controller.State.IsDownloaded);
        Assert.Contains("verified", controller.State.Error);
        service.Downloaded = true;
        await controller.DownloadAsync();
        Assert.True(controller.State.IsDownloaded);
        Assert.Null(controller.State.Error);
    }

    [Fact]
    public async Task LostSessionClearsAccountAndCancelsDownload()
    {
        var service = new FakeService
        {
            Account = new SteamAccount(1, "player"),
            Download = static async token => await Task.Delay(Timeout.InfiniteTimeSpan, token)
        };
        using var controller = new GamePreparationController(service, new FakePicker());
        await controller.InitializeAsync();
        var download = controller.DownloadAsync();
        service.Disconnect();
        await download;
        Assert.Null(controller.State.Account);
        Assert.False(controller.State.IsDownloaded);
        Assert.Contains("disconnected", controller.State.Error);
        Assert.False(controller.State.IsBusy);
    }

    [Fact]
    public async Task OwnerDisposalCancelsOperationWithoutFurtherUiNotifications()
    {
        var service = new FakeService
        {
            SignIn = static async token =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new SteamAccount(1, "unused");
            }
        };
        var controller = new GamePreparationController(service, new FakePicker());
        int events = 0;
        controller.StateChanged += (_, _) => events++;
        var signingIn = controller.SignInAsync();
        controller.Dispose();
        int before = events;
        await signingIn;
        Assert.Equal(before, events);
        Assert.True(service.Disposed);
    }

    private sealed class FakePicker : IDirectoryPicker
    {
        public string? Path { get; init; }

        public Task<string?> PickAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Path);
        }
    }

    private sealed class FakeService : IGamePreparationService
    {
        public GameDirectory Directory { get; private set; } = new("/original");

        public SteamAccount? Account { get; init; }

        public bool Downloaded { get; set; }

        public int SignInCount { get; private set; }

        public int SaveCount { get; private set; }

        public int DownloadCount { get; private set; }

        public bool Disposed { get; private set; }

        public Func<CancellationToken, Task<SteamAccount>> SignIn { get; init; }
            = static _ => Task.FromResult(new SteamAccount(1, "player"));

        public Func<CancellationToken, Task>? Download { get; init; }
        public event EventHandler? SessionLost;

        public GameDirectory ReadDirectory()
        {
            return Directory;
        }

        public void SaveDirectory(GameDirectory directory)
        {
            SaveCount++;
            Directory = directory;
        }

        public Task<bool> IsDownloadedAsync(GameDirectory directory, CancellationToken cancellationToken)
        {
            return Task.FromResult(Downloaded);
        }

        public Task<SteamAccount?> RestoreSessionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Account);
        }

        public Task<SteamAccount> SignInAsync(CancellationToken cancellationToken)
        {
            SignInCount++;
            return SignIn(cancellationToken);
        }

        public Task DownloadAsync(GameDirectory directory, IProgress<GameDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            DownloadCount++;
            return Download?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }

        public void Dispose()
        {
            Disposed = true;
        }


        public void Disconnect()
        {
            SessionLost?.Invoke(this, EventArgs.Empty);
        }
    }
}
