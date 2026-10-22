namespace Fenrir.Core.Models;

public sealed class CleanupReport
{
    public int DeletedFiles { get; set; }
    public int DeletedDirectories { get; set; }
    public int SkippedEntries { get; set; }
    public long FreedBytes { get; set; }
    public bool RecycleBinCleared { get; set; }
    public string? RecycleBinError { get; set; }
    public List<string> Warnings { get; } = new();

    public bool HasChanges => DeletedFiles > 0 || DeletedDirectories > 0 || RecycleBinCleared;
}
