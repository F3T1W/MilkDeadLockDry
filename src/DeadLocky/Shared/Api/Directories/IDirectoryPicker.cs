namespace DeadLocky.Shared.Api.Directories;

public interface IDirectoryPicker
{
    Task<string?> PickAsync(CancellationToken cancellationToken);
}
