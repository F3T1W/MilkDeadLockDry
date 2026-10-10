using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeadLocky.Features.LaunchGame.Api;

internal sealed record GameExitPreferences(bool CloseSteamAfterExit = true);

[JsonSerializable(typeof(GameExitPreferences))]
internal partial class ExitPreferencesJsonContext : JsonSerializerContext;

internal sealed class GameExitPreferencesStore(string path)
{
    public GameExitPreferences Read()
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path),
                ExitPreferencesJsonContext.Default.GameExitPreferences) ?? new GameExitPreferences();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new GameExitPreferences();
        }
    }

    public void Save(GameExitPreferences preferences)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences,
                ExitPreferencesJsonContext.Default.GameExitPreferences));
            File.Move(temporary, path, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
