namespace Fenrir.Core.Models;

public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? SpeechMessage { get; set; }

    public static CommandResult Ok(string message, string? speechMessage = null)
        => new() { Success = true, Message = message, SpeechMessage = speechMessage };

    public static CommandResult Fail(string message, string? speechMessage = null)
        => new() { Success = false, Message = message, SpeechMessage = speechMessage };
}
