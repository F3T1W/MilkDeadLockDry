using DeadLocky.Entities.Game.Model;

namespace DeadLocky.Features.SelectGameDirectory.Model;

internal sealed record DirectorySelectionState(
    GameDirectory? Directory = null,
    bool IsChoosing = false,
    string? Error = null);
