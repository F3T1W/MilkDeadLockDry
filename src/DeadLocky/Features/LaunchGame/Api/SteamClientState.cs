using DeadLocky.Entities.Steam.Model;

namespace DeadLocky.Features.LaunchGame.Api;

internal static class SteamClientState
{
    public static SteamAccount? ReadSavedAccount(string steamDirectory)
    {
        var users = SteamVdf.LoadAsText(Path.Combine(steamDirectory, "config", "loginusers.vdf"));
        SteamVdf? recent = users?.Children
            .Where(static user => (user["MostRecent"].Value == "1" && user["AllowAutoLogin"].Value == "1")
                                  || (user["AutoLogin"].Value == "1" && user["RememberPassword"].Value == "1"))
            .OrderByDescending(static user => long.TryParse(user["Timestamp"].Value, out long time) ? time : 0)
            .FirstOrDefault();
        if (recent is null || !ulong.TryParse(recent.Name, out ulong id) || id == 0)
        {
            return null;
        }

        string? name = recent["PersonaName"].Value ?? recent["AccountName"].Value;
        return string.IsNullOrWhiteSpace(name) ? null : new SteamAccount(id, name);
    }

    public static bool IsInstalled(string steamDirectory, string directory)
    {
        var state = SteamVdf.LoadAsText(ManifestPath(steamDirectory));
        return state?["appid"].Value == "1422450" && state["StateFlags"].Value == "4"
                                                  && state["installdir"].Value == "Deadlock"
                                                  && MacGameProcessScanner.CanonicalPath(Path.Combine(steamDirectory,
                                                      "steamapps", "common", "Deadlock"))
                                                  == MacGameProcessScanner.CanonicalPath(directory)
                                                  && File.Exists(Path.Combine(directory, "game", "bin", "win64",
                                                      "deadlock.exe"));
    }

    public static string ManifestPath(string steamDirectory)
    {
        return Path.Combine(steamDirectory, "steamapps", "appmanifest_1422450.acf");
    }
}
