using System.Diagnostics;
using System.Runtime.Versioning;
using Fenrir.Core.Services;

namespace Fenrir.Tests;

[SupportedOSPlatform("windows")]
public sealed class AmdCacheCleanupServiceTests : IDisposable
{
    private static readonly string[] CacheFolders = { "DxCache", "DxcCache", "OglCache", "VkCache" };

    private readonly string _baseDirectory;
    private readonly string _amdRoot;

    public AmdCacheCleanupServiceTests()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), "FenrirTests", Guid.NewGuid().ToString("N"));
        _amdRoot = Path.Combine(_baseDirectory, "AMD");
        Directory.CreateDirectory(_amdRoot);

        foreach (string folder in CacheFolders)
            Directory.CreateDirectory(Path.Combine(_amdRoot, folder));
    }

    [Fact]
    public void Clean_DeletesOnlyKnownCacheFolders()
    {
        foreach (string folder in CacheFolders)
            File.WriteAllText(Path.Combine(_amdRoot, folder, $"{folder}.bin"), new string('x', 100));

        string unknownFolder = Path.Combine(_amdRoot, "CN");
        Directory.CreateDirectory(unknownFolder);
        string keepFile = Path.Combine(unknownFolder, "keep.txt");
        File.WriteAllText(keepFile, "keep");

        var report = new AmdCacheCleanupService(_amdRoot).Clean();

        Assert.True(report.CacheFound);
        Assert.Equal(4, report.DeletedFiles);
        Assert.True(report.FreedBytes > 0);
        Assert.True(Directory.Exists(_amdRoot));
        Assert.True(File.Exists(keepFile));

        foreach (string folder in CacheFolders)
        {
            string path = Path.Combine(_amdRoot, folder);
            Assert.True(Directory.Exists(path));
            Assert.Empty(Directory.EnumerateFileSystemEntries(path));
        }
    }

    [Fact]
    public void Clean_ReturnsNotFound_WhenAmdRootMissing()
    {
        var service = new AmdCacheCleanupService(Path.Combine(_baseDirectory, "missing"));

        var report = service.Clean();

        Assert.False(report.CacheFound);
        Assert.False(report.HasChanges);
    }

    [Fact]
    public void Clean_HandlesMissingCacheFolders()
    {
        Directory.Delete(Path.Combine(_amdRoot, "OglCache"));

        var report = new AmdCacheCleanupService(_amdRoot).Clean();

        Assert.True(report.CacheFound);
        Assert.Contains("OglCache", report.MissingFolders);
        Assert.False(report.HasChanges);
    }

    [Fact]
    public void Clean_SkipsLockedFile()
    {
        string lockedFile = Path.Combine(_amdRoot, "DxCache", "locked.bin");
        File.WriteAllText(lockedFile, "locked");

        using (var stream = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var report = new AmdCacheCleanupService(_amdRoot).Clean();

            Assert.True(File.Exists(lockedFile));
            Assert.Equal(0, report.DeletedFiles);
            Assert.True(report.SkippedEntries >= 1);
        }
    }

    [Fact]
    public void Clean_DoesNotFollowJunctions()
    {
        string target = Path.Combine(_baseDirectory, "target");
        Directory.CreateDirectory(target);
        string targetFile = Path.Combine(target, "keep.bin");
        File.WriteAllText(targetFile, "keep");

        string link = Path.Combine(_amdRoot, "DxCache", "link");
        if (!TryCreateJunction(link, target))
            return;

        var report = new AmdCacheCleanupService(_amdRoot).Clean();

        Assert.True(File.Exists(targetFile));
        Assert.True(Directory.Exists(link));
        Assert.True(report.SkippedEntries >= 1);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDirectory))
                Directory.Delete(_baseDirectory, recursive: true);
        }
        catch
        {
        }
    }

    private static bool TryCreateJunction(string link, string target)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{link}\" \"{target}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 && Directory.Exists(link);
        }
        catch
        {
            return false;
        }
    }
}
