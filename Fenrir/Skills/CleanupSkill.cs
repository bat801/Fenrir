using System.Text;
using Fenrir.Core.Models;
using Fenrir.Core.Services;
using Fenrir.Core.Utilities;

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
        message.Append($"Очистка завершена. Удалено {RussianPlural.Files(report.DeletedFiles)} и {RussianPlural.Folders(report.DeletedDirectories)}, освобождено {ByteSizeFormatter.Format(report.FreedBytes)}.");

        if (report.SkippedEntries > 0)
            message.Append($" Пропущено {RussianPlural.Objects(report.SkippedEntries)}: занятые или недоступные.");

        if (report.RecycleBinCleared)
            message.Append(" Корзина очищена.");
        else
            message.Append($" Корзину очистить не удалось: {report.RecycleBinError}.");

        var text = message.ToString();
        return Task.FromResult(report.HasChanges ? CommandResult.Ok(text) : CommandResult.Fail(text));
    }
}
