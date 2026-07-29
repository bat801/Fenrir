using System.Speech.Synthesis;

namespace Fenrir.Core.Services;

public class SpeechService
{
    private readonly SpeechSynthesizer _synth;

    public SpeechService()
    {
        _synth = new SpeechSynthesizer();
        _synth.SetOutputToDefaultAudioDevice();

        // Настраиваем русский голос
        ConfigureRussianVoice();
    }

    /// <summary>
    /// Проговаривает текст вслух.
    /// </summary>
    public void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        // Убираем эмодзи, они не проговариваются
        var cleanText = System.Text.RegularExpressions.Regex.Replace(text, @"[\p{So}\p{Cs}]", "");

        if (string.IsNullOrWhiteSpace(cleanText))
            return;

        Console.WriteLine($"🔊 Fenrir: {cleanText}");
        _synth.SpeakAsyncCancelAll(); // Остановить предыдущую речь
        _synth.SpeakAsync(cleanText); // Начать новую
    }

    /// <summary>
    /// Проговаривает и ждёт окончания (для важных сообщений).
    /// </summary>
    public void SpeakSync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var cleanText = System.Text.RegularExpressions.Regex.Replace(text, @"[\p{So}\p{Cs}]", "");

        if (string.IsNullOrWhiteSpace(cleanText))
            return;

        Console.WriteLine($"🔊 Fenrir: {cleanText}");
        _synth.SpeakAsyncCancelAll();
        _synth.Speak(cleanText);
    }

    /// <summary>
    /// Выводит список доступных голосов (для отладки).
    /// </summary>
    public void ListVoices()
    {
        Console.WriteLine("Доступные голоса:");
        foreach (var voice in _synth.GetInstalledVoices())
        {
            var info = voice.VoiceInfo;
            Console.WriteLine($"  - {info.Name} ({info.Culture}, {info.Gender}, {info.Age})");
        }
    }

    private void ConfigureRussianVoice()
    {
        // Ищем русский голос
        var russianVoice = _synth.GetInstalledVoices()
            .FirstOrDefault(v => v.VoiceInfo.Culture.Name.StartsWith("ru"));

        if (russianVoice != null)
        {
            _synth.SelectVoice(russianVoice.VoiceInfo.Name);
            Console.WriteLine($"🎤 Выбран голос: {russianVoice.VoiceInfo.Name}");
        }
        else
        {
            Console.WriteLine("⚠️ Русский голос не найден. Используется голос по умолчанию.");
            Console.WriteLine("   Установите русский языковой пакет TTS в Windows.");
        }
    }
}