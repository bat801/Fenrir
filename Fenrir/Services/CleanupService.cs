using System.Runtime.InteropServices;
using System.Security;
using Fenrir.Core.Models;

namespace Fenrir.Core.Services;

public sealed class CleanupService
{
    private const int S_OK = 0;
    private const int S_FALSE = 1;
    private const int E_UNEXPECTED = unchecked((int)0x8000FFFF);
    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    [StructLayout(LayoutKind.Sequential)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    public CleanupReport Clean()
    {
        var report = new CleanupReport();

        CleanRecycleBin(report);

        foreach (var root in GetTempRoots())
        {
            if (!TryValidateTempRoot(root, out var fullRoot))
            {
                report.Warnings.Add($"Пропущена небезопасная папка: {root}");
                continue;
            }

            try
            {
                var stats = CleanDirectoryContents(fullRoot);
                report.DeletedFiles += stats.DeletedFiles;
                report.DeletedDirectories += stats.DeletedDirectories;
                report.SkippedEntries += stats.SkippedEntries;
                report.FreedBytes += stats.FreedBytes;
            }
            catch (Exception ex) when (IsExpectedCleanupError(ex))
            {
                report.SkippedEntries++;
                report.Warnings.Add($"Не удалось очистить {fullRoot}: {ex.Message}");
            }
        }

        return report;
    }

    internal CleanupStats CleanDirectoryContents(string root)
    {
        if (!TryValidateTempRoot(root, out var fullRoot))
            throw new ArgumentException("Небезопасный путь для очистки.", nameof(root));

        var stats = new CleanupStats();
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = 0
        };

        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(fullRoot, "*", options))
            {
                try
                {
                    DeleteEntry(fullRoot, entry, stats);
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

    internal static bool TryValidateTempRoot(string path, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (!Directory.Exists(fullPath))
            return false;

        if (!string.Equals(Path.GetFileName(fullPath), "Temp", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return false;
        }
        catch (Exception ex) when (IsExpectedCleanupError(ex))
        {
            return false;
        }

        return !IsDangerousRoot(fullPath);
    }

    private static IEnumerable<string> GetTempRoots()
    {
        var roots = new List<string>();

        var windowsPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windowsPath))
            roots.Add(Path.Combine(windowsPath, "Temp"));

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
            roots.Add(Path.Combine(localAppData, "Temp"));

        return roots.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static void CleanRecycleBin(CleanupReport report)
    {
        try
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            int queryHr = SHQueryRecycleBin(null, ref info);

            if (queryHr == S_OK && info.i64NumItems == 0)
            {
                report.RecycleBinCleared = true;
                return;
            }

            int hr = SHEmptyRecycleBin(
                IntPtr.Zero,
                null,
                SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);

            if (hr == S_OK || hr == S_FALSE || hr == E_UNEXPECTED)
            {
                report.RecycleBinCleared = true;
            }
            else
            {
                report.RecycleBinError = $"HRESULT 0x{hr:X8}";
            }
        }
        catch (Exception ex)
        {
            report.RecycleBinError = ex.Message;
        }
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

            foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", options))
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

    private static bool IsInsideRoot(string root, string entry)
    {
        string prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        string fullEntry = Path.GetFullPath(entry);

        return fullEntry.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(fullEntry, root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDangerousRoot(string fullPath)
    {
        string normalized = Path.TrimEndingDirectorySeparator(fullPath);

        var driveRoot = Path.GetPathRoot(normalized);
        if (!string.IsNullOrWhiteSpace(driveRoot) &&
            string.Equals(normalized, Path.TrimEndingDirectorySeparator(driveRoot), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var protectedPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        };

        foreach (var protectedPath in protectedPaths)
        {
            if (string.IsNullOrWhiteSpace(protectedPath))
                continue;

            if (string.Equals(normalized, Path.TrimEndingDirectorySeparator(protectedPath), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsExpectedCleanupError(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException;
    }
}

internal sealed class CleanupStats
{
    public int DeletedFiles { get; set; }
    public int DeletedDirectories { get; set; }
    public int SkippedEntries { get; set; }
    public long FreedBytes { get; set; }
}
