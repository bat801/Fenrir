using System.Diagnostics;
using System.Runtime.Versioning;
using Fenrir.Core.Services;
using Fenrir.Core.Skills;

namespace Fenrir.Tests;

[SupportedOSPlatform("windows")]
public sealed class CleanupServiceTests : IDisposable
{
    private readonly string _baseDirectory;
    private readonly string _root;
    private readonly CleanupService _service = new();

    public CleanupServiceTests()
    {
        _baseDirectory = Path.Combine(Path.GetTempPath(), "FenrirTests", Guid.NewGuid().ToString("N"));
        _root = Path.Combine(_baseDirectory, "Temp");
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void CleanDirectoryContents_DeletesContentsAndKeepsRoot()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        var nested = Path.Combine(_root, "sub");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "b.txt"), "bb");

        var stats = _service.CleanDirectoryContents(_root);

        Assert.Equal(2, stats.DeletedFiles);
        Assert.Equal(1, stats.DeletedDirectories);
        Assert.True(Directory.Exists(_root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
    }

    [Fact]
    public void CleanDirectoryContents_SkipsLockedFile()
    {
        var lockedFile = Path.Combine(_root, "locked.txt");
        File.WriteAllText(lockedFile, "locked");

        using (var stream = new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var stats = _service.CleanDirectoryContents(_root);

            Assert.True(File.Exists(lockedFile));
            Assert.Equal(0, stats.DeletedFiles);
            Assert.True(stats.SkippedEntries >= 1);
        }
    }

    [Fact]
    public void CleanDirectoryContents_DeletesReadOnlyFile()
    {
        var readOnlyFile = Path.Combine(_root, "readonly.txt");
        File.WriteAllText(readOnlyFile, "readonly");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        var stats = _service.CleanDirectoryContents(_root);

        Assert.False(File.Exists(readOnlyFile));
        Assert.Equal(1, stats.DeletedFiles);
    }

    [Fact]
    public void CleanDirectoryContents_DoesNotFollowJunctions()
    {
        var target = Path.Combine(_baseDirectory, "target");
        Directory.CreateDirectory(target);
        var targetFile = Path.Combine(target, "keep.txt");
        File.WriteAllText(targetFile, "keep");

        var link = Path.Combine(_root, "link");
        if (!TryCreateJunction(link, target))
            return;

        var stats = _service.CleanDirectoryContents(_root);

        Assert.True(File.Exists(targetFile));
        Assert.True(Directory.Exists(link));
        Assert.True(stats.SkippedEntries >= 1);
    }

    [Fact]
    public void CleanDirectoryContents_RejectsUnsafeRoot()
    {
        var unsafeRoot = Path.Combine(_baseDirectory, "NotTemp");
        Directory.CreateDirectory(unsafeRoot);
        var file = Path.Combine(unsafeRoot, "keep.txt");
        File.WriteAllText(file, "keep");

        Assert.Throws<ArgumentException>(() => _service.CleanDirectoryContents(unsafeRoot));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void CleanDirectoryContents_DoesNotTouchSiblingFiles()
    {
        var sibling = Path.Combine(_baseDirectory, "keep.txt");
        File.WriteAllText(sibling, "keep");
        File.WriteAllText(Path.Combine(_root, "delete.txt"), "delete");

        _service.CleanDirectoryContents(_root);

        Assert.True(File.Exists(sibling));
        Assert.False(File.Exists(Path.Combine(_root, "delete.txt")));
    }

    [Fact]
    public void TryValidateTempRoot_AcceptsDefaultTempRoots()
    {
        var windowsTemp = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
        var userTemp = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");

        if (Directory.Exists(windowsTemp))
            Assert.True(CleanupService.TryValidateTempRoot(windowsTemp, out _));

        if (Directory.Exists(userTemp))
            Assert.True(CleanupService.TryValidateTempRoot(userTemp, out _));
    }

    [Fact]
    public void CleanupSkill_RequiresConfirmation()
    {
        var skill = new CleanupSkill();

        Assert.True(skill.RequiresConfirmation);
        Assert.False(string.IsNullOrWhiteSpace(skill.ConfirmationPrompt));
        Assert.Contains("очисти мусор", skill.Triggers);
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
