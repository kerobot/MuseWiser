using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MuseWiser.Models;

namespace MuseWiser.Services.Settings;

/// <summary>
/// <see cref="DisplaySettings"/> を JSON ファイル（%AppData%\MuseWiser\settings.json）として
/// 読み込み・保存する <see cref="ISettingsService"/> の実装。
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MuseWiser");

        _filePath = Path.Combine(folder, "settings.json");
    }

    public DisplaySettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var settings = JsonSerializer.Deserialize<DisplaySettings>(json, SerializerOptions);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // 読み込みに失敗した場合は既定値を使用する。
            _logger.LogWarning(ex, "表示設定の読み込みに失敗したため、既定値を使用します。");
        }

        return new DisplaySettings();
    }

    public void Save(DisplaySettings settings)
    {
        try
        {
            var folder = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "表示設定の保存に失敗しました。");
        }
    }
}
