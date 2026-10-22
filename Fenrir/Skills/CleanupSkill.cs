using System.Globalization;
using System.Text;
using Fenrir.Core.Models;
using Fenrir.Core.Services;

namespace Fenrir.Core.Skills;

public sealed class CleanupSkill : ISkill
{
    public string Name => "cleanup";

    public string[] Triggers => new[]
    {
        "очисти мусор", "очистить мусор", "почисти мусор",
        "очистка мусора", "убери мусор"
    };

    public bool RequiresConfirmation => true;

    public string ConfirmationPrompt =>
        "Вы уверены? Корзина и временные папки будут очищены безвозвратно. Скажите да или нет.";

    public Task<CommandResult> ExecuteAsync(string command)
    {
        var report = new CleanupService().Clean();

        foreach (var warning in report.Warnings)
            Console.WriteLine($"⚠️ {warning}");

        var message = new StringBuilder();
        message.Append($"Очистка завершена. Удалено {report.DeletedFiles} файлов и {report.DeletedDirectories} папок, освобождено {FormatSize(report.FreedBytes)}.");

        if (report.SkippedEntries > 0)
            message.Append($" Пропущено {report.SkippedEntries} занятых или недоступных объектов.");

        if (report.RecycleBinCleared)
            message.Append(" Корзина очищена.");
        else
            message.Append($" Корзину очистить не удалось: {report.RecycleBinError}.");

        var text = message.ToString();
        return Task.FromResult(report.HasChanges ? CommandResult.Ok(text) : CommandResult.Fail(text));
    }

    private static string FormatSize(long bytes)
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");

        if (bytes >= 1024 * 1024)
            return $"{(bytes / (1024.0 * 1024.0)).ToString("0.0", culture)} МБ";

        if (bytes >= 1024)
            return $"{(bytes / 1024.0).ToString("0.0", culture)} КБ";

        return $"{bytes} байт";
    }
}
