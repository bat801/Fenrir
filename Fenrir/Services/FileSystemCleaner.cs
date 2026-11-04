using System.Security;

namespace Fenrir.Core.Services;

/// <summary>
/// Безопасное удаление содержимого уже проверенной папки.
/// Не переходит по junction/symlink, пропускает занятые файлы,
/// снимает атрибут ReadOnly и считает статистику.
/// </summary>
internal static class FileSystemCleaner
{
    public static CleanupStats CleanDirectoryContents(string validatedRoot)
    {
        var stats = new CleanupStats();
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = 0
        };

        try
        {
            foreach (string entry in Directory.EnumerateFileSystemEntries(validatedRoot, "*", options))
            {
                try
                {
                    DeleteEntry(validatedRoot, entry, stats);
                }
                catch (Exception ex) when (IsExpectedCleanupError(ex))
                {
                    stats.SkippedEntries++;
                }
            }
        }
        catch (Exception ex) when (IsExpectedCleanupError(ex))
        {
            stats.SkippedEntries++;
        }

        return stats;
    }

    public static bool TryDeleteFile(string path, out long freedBytes)
    {
        freedBytes = 0;

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return false;

            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                return false;

            long size = info.Length;

            if ((info.Attributes & FileAttributes.ReadOnly) != 0)
                info.Attributes &= ~FileAttributes.ReadOnly;

            info.Delete();
            freedBytes = size;
            return true;
        }
        catch (Exception ex) when (IsExpectedCleanupError(ex))
        {
            return false;
        }
    }

    public static bool IsInsideRoot(string root, string entry)
    {
        string prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        string fullEntry = Path.GetFullPath(entry);

        return fullEntry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(fullEntry, root, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExpectedCleanupError(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException;
    }

    private static void DeleteEntry(string root, string path, CleanupStats stats)
    {
        if (!IsInsideRoot(root, path))
        {
            stats.SkippedEntries++;
            return;
        }

        var attributes = File.GetAttributes(path);

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            stats.SkippedEntries++;
            return;
        }

        if ((attributes & FileAttributes.Directory) != 0)
            DeleteDirectory(root, path, stats);
        else
            DeleteFile(path, attributes, stats);
    }

    private static void DeleteDirectory(string root, string path, CleanupStats stats)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = 0
        };

        try
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                stats.SkippedEntries++;
                return;
            }

            foreach (string entry in Directory.EnumerateFileSystemEntries(path, "*", options))
            {
                try
                {
                    DeleteEntry(root, entry, stats);
                }
                catch (Exception ex) when (IsExpectedCleanupError(ex))
                {
                    stats.SkippedEntries++;
                }
            }
        }
        catch (Exception ex) when (IsExpectedCleanupError(ex))
        {
            stats.SkippedEntries++;
            return;
        }

        try
        {
            Directory.Delete(path, recursive: false);
            stats.DeletedDirectories++;
        }
        catch (Exception ex) when (IsExpectedCleanupError(ex))
        {
            stats.SkippedEntries++;
        }
    }

    private static void DeleteFile(string path, FileAttributes attributes, CleanupStats stats)
    {
        long size = 0;
        try
        {
            size = new FileInfo(path).Length;
        }
        catch (Exception ex) when (IsExpectedCleanupError(ex))
        {
        }

        try
        {
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);

            File.Delete(path);
            stats.DeletedFiles++;
            stats.FreedBytes += size;
        }
        catch (Exception ex) when (IsExpectedCleanupError(ex))
        {
            stats.SkippedEntries++;
        }
    }
}

internal sealed class CleanupStats
{
    public int DeletedFiles { get; set; }
    public int DeletedDirectories { get; set; }
    public int SkippedEntries { get; set; }
    public long FreedBytes { get; set; }
}
