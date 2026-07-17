using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace HylianHeroCompanion;

public static class SettingsStore
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        BrandIdentity.TechnicalName);

    private static readonly string LegacyDirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        BrandIdentity.LegacyTechnicalName);

    private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");
    private static readonly string StatusPath = Path.Combine(DirectoryPath, "status.json");
    private static readonly string LegacyFilePath = Path.Combine(LegacyDirectoryPath, "settings.json");

    public static CompanionSettings? Load()
    {
        try
        {
            var sourcePath = File.Exists(FilePath)
                ? FilePath
                : File.Exists(LegacyFilePath)
                    ? LegacyFilePath
                    : null;

            if (sourcePath is null)
            {
                return null;
            }

            var settings = JsonSerializer.Deserialize<CompanionSettings>(File.ReadAllText(sourcePath));
            if (settings is not null && sourcePath == LegacyFilePath)
            {
                Save(settings);
            }

            return settings;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(CompanionSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Position persistence should never stop the companion.
        }
    }

    public static void WriteStatus(CompanionStatus status)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(StatusPath, JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Diagnostics must never interrupt animation.
        }
    }
}

public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = BrandIdentity.TechnicalName;
    private const string LegacyValueName = BrandIdentity.LegacyTechnicalName;

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            var value = key?.GetValue(ValueName) as string
                ?? key?.GetValue(LegacyValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
    }

    public static void SetEnabled(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{executablePath}\"");
            key.DeleteValue(LegacyValueName, false);
        }
        else
        {
            key.DeleteValue(ValueName, false);
            key.DeleteValue(LegacyValueName, false);
        }
    }
}
