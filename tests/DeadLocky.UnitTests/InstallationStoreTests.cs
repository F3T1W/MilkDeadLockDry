using System.Text.Json;
using DeadLocky.Entities.Game.Model;
using DeadLocky.Features.LaunchGame.Api;
using DeadLocky.Features.PrepareGame.Model;

namespace DeadLocky.UnitTests;

public sealed class InstallationStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        OperatingSystem.IsMacOS() ? MacGameProcessScanner.CanonicalPath(Path.GetTempPath()) : Path.GetTempPath(),
        "deadlocky-store-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public void SavedDirectorySurvivesRestartAndDamagedPreferencesFallBack()
    {
        string settings = Path.Combine(_root, "settings.json");
        var store = new InstallationStore(settings);
        var selected = new GameDirectory(Path.Combine(_root, "game"));
        store.SaveDirectory(selected);
        Assert.Equal(selected, new InstallationStore(settings).ReadDirectory());
        File.WriteAllText(settings, "{invalid");
        Assert.NotEqual(selected, new InstallationStore(settings).ReadDirectory());
    }

    [Fact]
    public async Task LegacyReceiptRecognizesCompleteFilesAndRejectsChangedFiles()
    {
        var directory = new GameDirectory(Path.Combine(_root, "game"));
        _ = Directory.CreateDirectory(Path.Combine(directory.Path, ".deadlocky"));
        string file = Path.Combine(directory.Path, "game.bin");
        await File.WriteAllTextAsync(file, "complete");
        var info = new FileInfo(file);
        var receipt = new InstallationReceipt(InstallationStore.AppId, "legacy",
            [new InstalledFile("game.bin", info.Length, info.LastWriteTimeUtc.Ticks)]);
        await File.WriteAllTextAsync(InstallationStore.ReceiptPath(directory.Path),
            JsonSerializer.Serialize(receipt, LauncherJsonContext.Default.InstallationReceipt));
        Assert.True(await InstallationStore.IsDownloadedAsync(directory, CancellationToken.None));
        await File.WriteAllTextAsync(file, "incomplete or changed");
        Assert.False(await InstallationStore.IsDownloadedAsync(directory, CancellationToken.None));
    }
}
