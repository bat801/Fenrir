namespace Fenrir.Core.Services;

/// <summary>
/// Общие проверки путей перед удалением. Ни один путь не должен выходить
/// за пределы заранее разрешённой папки и не должен быть опасным корнем.
/// </summary>
internal static class CleanupPathValidator
{
    public static bool TryNormalizeDirectory(string path, out string fullPath)
    {
        fullPath = string.Empty;

        if (!TryNormalize(path, out fullPath))
            return false;

        if (!Directory.Exists(fullPath))
            return false;

        return !IsReparsePoint(fullPath) && !IsDangerousRoot(fullPath);
    }

    public static bool TryNormalizeFile(string path, out string fullPath)
    {
        fullPath = string.Empty;

        if (!TryNormalize(path, out fullPath))
            return false;

        if (!File.Exists(fullPath))
            return false;

        return !IsReparsePoint(fullPath) && !IsDangerousRoot(fullPath);
    }

    public static bool HasLeafName(string path, string leafName)
    {
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return string.Equals(name, leafName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUnder(string path, string parent)
    {
        try
        {
            string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent))
                + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(path);

            return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (IsPathError(ex))
        {
            return false;
        }
    }

    public static bool IsReparsePoint(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (IsPathError(ex))
        {
            return true;
        }
    }

    public static bool IsDangerousRoot(string fullPath)
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
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86)
        };

        foreach (string protectedPath in protectedPaths)
        {
            if (string.IsNullOrWhiteSpace(protectedPath))
                continue;

            if (string.Equals(
                    normalized,
                    Path.TrimEndingDirectorySeparator(protectedPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryNormalize(string path, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            return true;
        }
        catch (Exception ex) when (IsPathError(ex))
        {
            return false;
        }
    }

    private static bool IsPathError(Exception ex)
    {
        return ex is ArgumentException
            or NotSupportedException
            or PathTooLongException
            or IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException;
    }
}
