using System.Text.Json;

namespace PokeBank.Core;

/// <summary>Preferencias guardadas en %AppData%\PokeBank\settings.json.</summary>
public sealed class AppSettings
{
    public string? SavesFolder { get; set; }
    public int VisiblePanels { get; set; } = 2;

    private static string FilePath => Path.Combine(BankStore.DefaultRoot, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch
        {
            // Archivo dañado: se empieza de cero.
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(BankStore.DefaultRoot);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // No es crítico.
        }
    }
}
