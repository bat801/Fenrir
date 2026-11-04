using System.Runtime.InteropServices;
using Fenrir.Core.Models;

namespace Fenrir.Core.Services;

public sealed class CleanupService
{
    private const int S_OK = 0;
    private const int S_FALSE = 1;
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
            catch (Exception ex) when (FileSystemCleaner.IsExpectedCleanupError(ex))
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

        return FileSystemCleaner.CleanDirectoryContents(fullRoot);
    }

    internal static bool TryValidateTempRoot(string path, out string fullPath)
    {
        if (!CleanupPathValidator.TryNormalizeDirectory(path, out fullPath))
            return false;

        return CleanupPathValidator.HasLeafName(fullPath, "Temp");
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
            bool statsAvailable = queryHr == S_OK;

            if (statsAvailable && info.i64NumItems == 0)
            {
                report.RecycleBinCleared = true;
                return;
            }

            int hr = SHEmptyRecycleBin(
                IntPtr.Zero,
                null,
                SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);

            if (hr == S_OK || hr == S_FALSE)
            {
                report.RecycleBinCleared = true;

                if (hr == S_OK && statsAvailable)
                {
                    report.RecycleBinItems = info.i64NumItems;
                    report.RecycleBinBytes = info.i64Size;
                    report.DeletedFiles += (int)Math.Min(info.i64NumItems, int.MaxValue);
                    report.FreedBytes += info.i64Size;
                }
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
}
