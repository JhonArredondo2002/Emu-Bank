using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PKHeX.Core;
using PokeBank.Core;

namespace PokeBank.App;

/// <summary>
/// Apariencia de cada juego: un degradado con sus colores y, si el usuario la eligió, una imagen propia.
/// Las imágenes se guardan en %AppData%\PokeBank\Fondos con el código del juego (p. ej. "E.png", "HG.jpg").
/// </summary>
public static class GameTheme
{
    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];

    public static string Folder { get; } = Path.Combine(BankStore.DefaultRoot, "Fondos");

    public static IBrush GradientFor(GameVersion version)
    {
        var info = GameNames.Get(version);
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse(info.ColorA), 0),
                new GradientStop(Color.Parse(info.ColorB), 1),
            },
        };
    }

    /// <summary>Imagen propia del juego, o null si no hay.</summary>
    public static Bitmap? LoadBackground(GameVersion version)
    {
        var file = FindFile(version);
        if (file is null)
            return null;
        try
        {
            using var stream = File.OpenRead(file); // se lee a memoria para no dejar el archivo bloqueado
            return new Bitmap(stream);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, $"Fondo {file}");
            return null;
        }
    }

    /// <summary>Copia una imagen elegida por el usuario como fondo de ese juego (reemplaza la anterior).</summary>
    public static void SetBackground(GameVersion version, string imagePath)
    {
        Directory.CreateDirectory(Folder);
        RemoveBackground(version);
        var ext = Path.GetExtension(imagePath).ToLowerInvariant();
        File.Copy(imagePath, Path.Combine(Folder, version + ext), overwrite: true);
    }

    public static void RemoveBackground(GameVersion version)
    {
        foreach (var ext in Extensions)
        {
            var file = Path.Combine(Folder, version + ext);
            if (File.Exists(file))
                File.Delete(file);
        }
    }

    public static bool HasBackground(GameVersion version) => FindFile(version) is not null;

    private static string? FindFile(GameVersion version) =>
        Extensions.Select(ext => Path.Combine(Folder, version + ext)).FirstOrDefault(File.Exists);
}
