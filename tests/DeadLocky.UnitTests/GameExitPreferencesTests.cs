using DeadLocky.Features.LaunchGame.Api;

namespace DeadLocky.UnitTests;

public sealed class GameExitPreferencesTests
{
    [Fact]
    public void OwnedRuntimeIsDetectedWhenSavedConfigurationIsMissing()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string engine = Path.Combine(root, "Runtime", "HighballWine11", "engine");
        string prefix = Path.Combine(root, "Prefixes", "Steam-Wine11");
        _ = Directory.CreateDirectory(Path.Combine(engine, "bin"));
        _ = Directory.CreateDirectory(Path.Combine(engine, "lib", "external", "D3DMetal.framework"));
        _ = Directory.CreateDirectory(Path.Combine(prefix, "drive_c", "Program Files (x86)", "Steam"));
        File.WriteAllText(Path.Combine(engine, "bin", "wine64"), "");
        File.WriteAllText(Path.Combine(prefix, "drive_c", "Program Files (x86)", "Steam", "steam.exe"), "");
        try
        {
            using var launcher = new GptkGameLauncher(Path.Combine(root, "launch.json"));
            Assert.NotNull(launcher.Configuration);
            Assert.Equal(prefix, launcher.Configuration.PrefixDirectory);
            File.Delete(Path.Combine(engine, "bin", "wine64"));
            using var incomplete = new GptkGameLauncher(Path.Combine(root, "launch.json"));
            Assert.Null(incomplete.Configuration);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CloseSteamChoiceSurvivesLauncherRestartWithoutChangingRuntimeConfiguration()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(root);
        string path = Path.Combine(root, "launch.json");
        File.WriteAllText(path, "{}");
        try
        {
            string original = File.ReadAllText(path);
            using (var launcher = new GptkGameLauncher(path))
            {
                Assert.True(launcher.CloseSteamAfterExit);
                launcher.CloseSteamAfterExit = false;
            }

            using (var launcher = new GptkGameLauncher(path))
            {
                Assert.False(launcher.CloseSteamAfterExit);
                launcher.CloseSteamAfterExit = true;
            }

            using var restored = new GptkGameLauncher(path);
            Assert.True(restored.CloseSteamAfterExit);
            Assert.Equal(original, File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
