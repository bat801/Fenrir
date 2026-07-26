namespace Fenrir.Core.Models;

public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public static CommandResult Ok(string message) => new() { Success = true, Message = message };
    public static CommandResult Fail(string message) => new() { Success = false, Message = message };
}