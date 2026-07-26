using Fenrir.Core.Models;

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
    /// Ключевые фразы, по которым активируется навык.
    /// Пока самая простая логика: содержит ли команда одно из этих слов.
    /// </summary>
    string[] Triggers { get; }

    /// <summary>
    /// Выполняет команду и возвращает результат.
    /// </summary>
    /// <param name="command">Полный текст команды от пользователя.</param>
    /// <returns>Результат выполнения.</returns>
    Task<CommandResult> ExecuteAsync(string command);
}