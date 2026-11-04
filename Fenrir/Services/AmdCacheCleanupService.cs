using Fenrir.Core.Models;

namespace Fenrir.Core.Services;

/// <summary>
/// Очистка кэша шейдеров AMD. Удаляется только содержимое известных
/// кэш-папок; сам каталог AMD и посторонние подпапки не трогаются.
/// </summary>
public sealed class AmdCacheCleanupService
{
    private static readonly string[] CacheFolderNames =
    {
        "DxCache",
        "DxcCache",
        "OglCache",
        "VkCache"
    };

    private readonly string _amdRoot;

    public AmdCacheCleanupService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AMD"))
    {
    }

    internal AmdCacheCleanupService(string amdRoot)
    {
        _amdRoot = amdRoot;
    }

    public AmdCacheReport Clean()
    {
        var report = new AmdCacheReport();

        if (!CleanupPathValidator.TryNormalizeDirectory(_amdRoot, out string root) ||
            !CleanupPathValidator.HasLeafName(root, "AMD"))
        {
            report.CacheFound = false;
            return report;
        }

        report.CacheFound = true;

        foreach (string folderName in CacheFolderNames)
        {
            string folder = Path.Combine(root, folderName);

            if (!Directory.Exists(folder))
            {
                report.MissingFolders.Add(folderName);
                continue;
            }

            if (!CleanupPathValidator.TryNormalizeDirectory(folder, out string fullFolder) ||
                !CleanupPathValidator.IsUnder(fullFolder, root))
            {
                report.Warnings.Add($"Пропущена небезопасная папка кэша: {folder}");
                continue;
            }

            var stats = FileSystemCleaner.CleanDirectoryContents(fullFolder);
            report.DeletedFiles += stats.DeletedFiles;
            report.DeletedDirectories += stats.DeletedDirectories;
            report.SkippedEntries += stats.SkippedEntries;
            report.FreedBytes += stats.FreedBytes;
        }

        return report;
    }
}
