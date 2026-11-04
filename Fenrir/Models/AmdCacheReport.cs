namespace Fenrir.Core.Models;

public sealed class AmdCacheReport
{
    public bool CacheFound { get; set; }
    public int DeletedFiles { get; set; }
    public int DeletedDirectories { get; set; }
    public int SkippedEntries { get; set; }
    public long FreedBytes { get; set; }
    public List<string> MissingFolders { get; } = new();
    public List<string> Warnings { get; } = new();

    public bool HasChanges => DeletedFiles > 0 || DeletedDirectories > 0;
}
