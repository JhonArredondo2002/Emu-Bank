using PokeBank.Core;

namespace PokeBank.App;

/// <summary>Registra errores en %AppData%\PokeBank\logs\errores.log para poder diagnosticarlos.</summary>
public static class CrashLog
{
    public static string FilePath { get; } = Path.Combine(BankStore.DefaultRoot, "logs", "errores.log");

    public static void Write(Exception ex, string context = "")
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Si ni siquiera se puede escribir el log, no hay más que hacer.
        }
    }
}
