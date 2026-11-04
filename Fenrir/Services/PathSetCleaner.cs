using Fenrir.Core.Models;

namespace Fenrir.Core.Services;

/// <summary>
/// Удаляет содержимое набора заранее известных папок и отдельные файлы.
/// Используется для дампов падений и отчётов WER.
/// </summary>
internal static class PathSetCleaner
{
    public static CleanupReport Clean(
        IEnumerable<string> directories,
        IEnumerable<string> files)
    {
        var report = new CleanupReport();

        foreach (string directory in directories
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!CleanupPathValidator.TryNormalizeDirectory(directory, out string fullDirectory))
                continue;

            var stats = FileSystemCleaner.CleanDirectoryContents(fullDirectory);
            report.DeletedFiles += stats.DeletedFiles;
            report.DeletedDirectories += stats.DeletedDirectories;
            report.SkippedEntries += stats.SkippedEntries;
            report.FreedBytes += stats.FreedBytes;
        }

        foreach (string file in files
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!CleanupPathValidator.TryNormalizeFile(file, out string fullFile))
                continue;

            if (FileSystemCleaner.TryDeleteFile(fullFile, out long freedBytes))
            {
                report.DeletedFiles++;
                report.FreedBytes += freedBytes;
            }
            else if (File.Exists(fullFile))
            {
                report.SkippedEntries++;
            }
        }

        return report;
    }
}
