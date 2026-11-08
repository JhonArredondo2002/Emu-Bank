using PKHeX.Core;

namespace PokeBank.Core;

/// <summary>Una partida encontrada al escanear una carpeta.</summary>
public sealed record SaveCandidate(string Path, string Title, GameVersion Version)
{
    public override string ToString() => Title;
}

/// <summary>
/// Busca partidas compatibles (Gen 1 a 7) dentro de una carpeta y sus subcarpetas.
/// Incluye los archivos "main" de Citra (3DS), que suelen estar muy anidados.
/// </summary>
public static class SaveScanner
{
    public static List<SaveCandidate> Scan(string folder, int maxDepth = 12)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = maxDepth,
            IgnoreInaccessible = true,
        };

        var result = new List<SaveCandidate>();
        foreach (var file in Directory.EnumerateFiles(folder, "*", options))
        {
            try
            {
                // Descarta rápido lo que no tiene tamaño de partida (ROMs, imágenes, etc.).
                if (!SaveUtil.IsSizeValid(new FileInfo(file).Length))
                    continue;
                if (!SaveUtil.TryGetSaveFile(file, out var sav))
                    continue;
                if (!SaveSession.IsSupported(sav))
                    continue;

                var relative = System.IO.Path.GetRelativePath(folder, file);
                result.Add(new SaveCandidate(System.IO.Path.GetFullPath(file),
                    $"{GameNames.Name(sav.Version)} · {sav.OT} — {relative}", sav.Version));
            }
            catch
            {
                // Un archivo dañado o bloqueado no debe frenar el escaneo.
            }
        }
        return result.OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
