using Fenrir.Core.Models;
using Fenrir.Core.Routing;

namespace Fenrir.Core.Skills;

/// <summary>
/// Интерфейс, который должен реализовать каждый навык Фенрира.
/// </summary>
public interface ISkill
{
    /// <summary>
    /// Уникальное имя навыка (например, "browser", "calculator").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Короткое описание для справки и уточняющих вопросов.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Фразы, по которым навык может быть выбран роутером.
    /// </summary>
    IReadOnlyList<SkillTrigger> Triggers { get; }

    /// <summary>
    /// Нужно ли подтверждение перед выполнением команды.
    /// </summary>
    bool RequiresConfirmation => false;

    /// <summary>
    /// Текст запроса подтверждения.
    /// </summary>
    string ConfirmationPrompt => string.Empty;

    /// <summary>
    /// Быстрая проверка «умею ли я это». Не должна запускать процессы,
    /// трогать файловую систему или иметь другие побочные эффекты.
    /// </summary>
    bool CanHandle(string command) => true;

    /// <summary>
    /// Выполняет команду и возвращает результат.
    /// </summary>
    /// <param name="command">Полный текст команды от пользователя.</param>
    /// <returns>Результат выполнения.</returns>
    Task<CommandResult> ExecuteAsync(string command);
}
