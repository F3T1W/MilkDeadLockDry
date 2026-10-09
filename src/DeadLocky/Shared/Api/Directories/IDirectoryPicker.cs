namespace DeadLocky.Shared.Api.Directories;

public interface IDirectoryPicker
{
    // Null means that the user cancelled; cancellation of the owner throws OperationCanceledException.
    Task<string?> PickAsync(CancellationToken cancellationToken);
}
