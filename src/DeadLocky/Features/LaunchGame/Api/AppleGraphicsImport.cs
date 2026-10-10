using System.Xml.Linq;

namespace DeadLocky.Features.LaunchGame.Api;

internal sealed class AppleGraphicsImport(RuntimeSetupPlatform platform) : IAsyncDisposable
{
    private readonly List<string> _mounts = [];

    public async ValueTask DisposeAsync()
    {
        foreach (string mount in Enumerable.Reverse(_mounts))
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                _ = await platform.RunAsync("/usr/bin/hdiutil", ["detach", mount], null, null, timeout.Token);
                Directory.Delete(mount);
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException)
            {
            }

            if (Directory.Exists(mount) && !Directory.EnumerateFileSystemEntries(mount).Any())
            {
                Directory.Delete(mount);
            }
        }
    }

    private static string? FindInFolder(string folder)
    {
        string[] candidates =
            [folder, Path.Combine(folder, "redist"), Path.Combine(folder, "PackageContent", "redist")];
        return candidates.FirstOrDefault(static candidate =>
            File.Exists(Path.Combine(candidate, "lib", "external", "D3DMetal.framework", "D3DMetal"))
            && File.Exists(Path.Combine(candidate, "lib", "wine", "x86_64-windows", "d3d11.dll"))
            && File.Exists(Path.Combine(candidate, "lib", "external", "libd3dshared.dylib")));
    }

    public static string? FindMounted()
    {
        return Directory.Exists("/Volumes")
            ? Directory.EnumerateDirectories("/Volumes").Select(FindInFolder)
                .FirstOrDefault(static path => path is not null)
            : null;
    }

    public async Task<string> ResolveAsync(string selection, CancellationToken token)
    {
        if (Directory.Exists(selection))
        {
            return FindInFolder(selection) ?? await FindNestedAsync(selection, token);
        }

        if (!File.Exists(selection) || !Path.GetExtension(selection).Equals(".dmg", StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Choose your Apple Game Porting Toolkit DMG or its mounted evaluation environment.");
        }

        string mount = await MountAsync(selection, token);
        return FindInFolder(mount) ?? await FindNestedAsync(mount, token);
    }

    private async Task<string> FindNestedAsync(string folder, CancellationToken token)
    {
        string image = Directory.EnumerateFiles(folder, "*.dmg", SearchOption.TopDirectoryOnly)
                           .FirstOrDefault(static path =>
                               Path.GetFileName(path).Contains("Evaluation", StringComparison.OrdinalIgnoreCase))
                       ?? throw MissingGraphics();

        string mounted = await MountAsync(image, token);
        return FindInFolder(mounted) ?? throw MissingGraphics();
    }

    private static IOException MissingGraphics()
    {
        return new IOException(
            "Apple's evaluation graphics were not found. Select the GPTK download, "
            + "not the shader converter or remote tools package.");
    }

    private async Task<string> MountAsync(string image, CancellationToken token)
    {
        string mount = Path.Combine(Path.GetTempPath(), "deadlocky-gptk-" + Guid.NewGuid().ToString("N"));
        _ = Directory.CreateDirectory(mount);
        _mounts.Add(mount);
        try
        {
            string plist = await platform.RunAsync("/usr/bin/hdiutil",
                ["attach", image, "-readonly", "-nobrowse", "-noautoopen", "-plist", "-mountpoint", mount],
                null, null, token);
            var document = XDocument.Parse(plist);
            string attached = document.Descendants("key")
                                  .FirstOrDefault(static element => element.Value == "mount-point")
                                  ?.ElementsAfterSelf("string").FirstOrDefault()?.Value
                              ?? throw new IOException("The selected Apple image could not be mounted.");

            if (MacGameProcessScanner.CanonicalPath(attached) == MacGameProcessScanner.CanonicalPath(mount))
            {
                return attached;
            }

            _ = _mounts.Remove(mount);
            Directory.Delete(mount);
            return attached;
        }
        catch (IOException exception)
        {
            throw new IOException("Open this DMG in Finder and review any Apple license prompt, "
                                  + "then choose the mounted evaluation environment and retry.", exception);
        }
    }
}
