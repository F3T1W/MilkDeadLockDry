namespace DeadLocky.Features.PrepareGame.Model;

public sealed record GameDownloadProgress(ulong CompletedBytes, ulong TotalBytes, string Message)
{
    public double? Fraction => TotalBytes == 0 ? null : Math.Clamp((double)CompletedBytes / TotalBytes, 0, 1);
}
