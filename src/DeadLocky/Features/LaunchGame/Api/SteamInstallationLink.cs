using System.Text.RegularExpressions;

namespace DeadLocky.Features.LaunchGame.Api;

internal static partial class SteamInstallationLink
{
    public static void Register(string steamExecutable, string gameDirectory)
    {
        string steamApps = Path.Combine(Path.GetDirectoryName(steamExecutable)!, "steamapps");
        string common = Path.Combine(steamApps, "common");
        string destination = Path.Combine(common, "Deadlock");
        string manifest = Path.Combine(steamApps, "appmanifest_1422450.acf");
        if (File.Exists(manifest))
        {
            Match match = InstallDirectory().Match(File.ReadAllText(manifest));
            if (!match.Success || match.Groups[1].Value != "Deadlock")
            {
                throw new IOException("The existing Steam manifest points to a different folder. "
                                      + "Configure a separate Windows Steam prefix.");
            }
        }

        _ = Directory.CreateDirectory(common);
        var existing = new DirectoryInfo(destination);
        if (existing.Exists || existing.LinkTarget is not null)
        {
            if (MacGameProcessScanner.CanonicalPath(destination) != MacGameProcessScanner.CanonicalPath(gameDirectory))
            {
                throw new IOException("This Windows Steam prefix already has another Deadlock installation. "
                                      + "Choose its game folder in DeadLocky or use a separate prefix.");
            }
        }
        else
        {
            _ = Directory.CreateSymbolicLink(destination, Path.GetFullPath(gameDirectory));
        }

        if (File.Exists(manifest)
            || !File.Exists(Path.Combine(gameDirectory, "game", "bin", "win64", "deadlock.exe")))
        {
            return;
        }

        const string contents = "\"AppState\"\n{\n\t\"appid\"\t\"1422450\"\n\t\"Universe\"\t\"1\"\n"
                                + "\t\"name\"\t\"Deadlock\"\n\t\"StateFlags\"\t\"4\"\n\t\"installdir\"\t\"Deadlock\"\n}\n";
        string temporary = manifest + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, contents);
            File.Move(temporary, manifest, false);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [GeneratedRegex("\"installdir\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirectory();
}
