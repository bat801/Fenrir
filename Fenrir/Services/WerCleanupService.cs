using Fenrir.Core.Models;

namespace Fenrir.Core.Services;

/// <summary>
/// Отчёты Windows Error Reporting: очереди, архив и временные файлы
/// как для текущего пользователя, так и системные (нужны права администратора).
/// </summary>
public sealed class WerCleanupService
{
    private readonly IReadOnlyList<string> _directories;

    public WerCleanupService()
        : this(GetDefaultDirectories())
    {
    }

    internal WerCleanupService(IEnumerable<string> directories)
    {
        _directories = directories.ToArray();
    }

    public bool HasTargets => _directories.Any(Directory.Exists);

    public CleanupReport Clean()
    {
        return PathSetCleaner.Clean(_directories, Array.Empty<string>());
    }

    private static IEnumerable<string> GetDefaultDirectories()
    {
        var roots = new List<string>();

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
            roots.Add(Path.Combine(localAppData, "Microsoft", "Windows", "WER"));

        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(programData))
            roots.Add(Path.Combine(programData, "Microsoft", "Windows", "WER"));

        foreach (string root in roots)
        {
            yield return Path.Combine(root, "ReportQueue");
            yield return Path.Combine(root, "ReportArchive");
            yield return Path.Combine(root, "ERC");
            yield return Path.Combine(root, "Temp");
        }
    }
}
