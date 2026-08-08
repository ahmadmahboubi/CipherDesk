using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CipherDesk.App.Theming;
using CipherDesk.Core;

namespace CipherDesk.App.Services;

/// <summary>
/// User preferences, persisted as JSON under %APPDATA%\CipherDesk.
/// </summary>
/// <remarks>
/// Nothing sensitive is ever written here - no passwords, no plaintext, no recent-document history
/// that could reveal what someone encrypted. Losing or deleting the file is harmless by design.
/// </remarks>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ThemeMode Theme { get; set; } = ThemeMode.System;

    public CipherFormat PreferredFormat { get; set; } = CipherFormat.Modern;

    public int WindowWidth { get; set; } = 940;

    public int WindowHeight { get; set; } = 720;

    public bool WindowMaximized { get; set; }

    /// <summary>When true, the output box is selected automatically after a successful operation.</summary>
    public bool AutoSelectOutput { get; set; } = true;

    /// <summary>When true, a successful encryption also puts the result on the clipboard.</summary>
    public bool AutoCopyOnEncrypt { get; set; }

    [JsonIgnore]
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CipherDesk",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Preferences are a convenience; a corrupt or unreadable file must never block startup.
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, SerializerOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore: failing to persist a preference is not worth interrupting the user for.
        }
    }
}
