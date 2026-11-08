using PKHeX.Core;

namespace PokeBank.Core;

/// <summary>
/// Una partida abierta: el archivo .sav/.dsv y el SaveFile que PKHeX construye a partir de él.
/// </summary>
public sealed class SaveSession
{
    /// <summary>Formatos de Pokémon soportados (juegos principales de Gen 1 a 7, sin Colosseum/XD ni Let's Go).</summary>
    private static readonly string[] SupportedFormats = ["PK1", "PK2", "PK3", "PK4", "PK5", "PK6", "PK7"];

    public static bool IsSupported(SaveFile sav) => SupportedFormats.Contains(sav.PKMType.Name);

    public string FilePath { get; }
    public SaveFile Save { get; }
    public string BackupPath { get; }

    /// <summary>Archivos que ya respaldamos en esta ejecución (para no llenar la carpeta de copias).</summary>
    private static readonly Dictionary<string, string> BackedUp = new(StringComparer.OrdinalIgnoreCase);

    private SaveSession(string filePath, SaveFile save, string backupPath)
    {
        FilePath = filePath;
        Save = save;
        BackupPath = backupPath;
    }

    /// <summary>
    /// Abre una partida y guarda una copia de seguridad antes de tocar nada.
    /// </summary>
    public static SaveSession Open(string path, string backupDirectory)
    {
        if (!SaveUtil.TryGetSaveFile(path, out var sav))
            throw new InvalidDataException("PKHeX no reconoce este archivo como una partida de Pokémon.");

        if (!IsSupported(sav))
            throw new NotSupportedException(
                $"La partida es de {GameNames.Name(sav.Version)} (Gen {sav.Generation}). Por ahora se soportan los juegos principales de Gen 1 a 7.");

        var fullPath = Path.GetFullPath(path);
        if (!BackedUp.TryGetValue(fullPath, out var backup))
        {
            Directory.CreateDirectory(backupDirectory);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            backup = Path.Combine(backupDirectory, $"{Path.GetFileNameWithoutExtension(path)}_{stamp}{Path.GetExtension(path)}");
            File.Copy(path, backup, overwrite: true);
            BackedUp[fullPath] = backup;
        }

        return new SaveSession(fullPath, sav, backup);
    }

    public static string Describe(SaveFile sav, string path) =>
        $"{GameNames.Name(sav.Version)} · Gen {sav.Generation} · {sav.OT} — {Path.GetFileName(path)}";

    public string Title => Describe(Save, FilePath);

    public int BoxCount => Save.BoxCount;
    public int SlotsPerBox => Save.BoxSlotCount;

    public string GetBoxName(int box)
    {
        if (Save is IBoxDetailName named)
        {
            var name = named.GetBoxName(box);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        return $"Caja {box + 1}";
    }

    public PKM GetSlot(int box, int slot) => Save.GetBoxSlotAtIndex(box, slot);

    public static bool IsEmpty(PKM pk) => pk.Species == 0;

    /// <summary>Saca un Pokémon de la caja y deja el espacio vacío. Devuelve el Pokémon retirado.</summary>
    public PKM Take(int box, int slot)
    {
        var pk = GetSlot(box, slot);
        if (IsEmpty(pk))
            throw new InvalidOperationException("Ese espacio está vacío.");
        Save.SetBoxSlotAtIndex(Save.BlankPKM, box, slot);
        return pk;
    }

    /// <summary>Coloca un Pokémon (ya en el formato de esta partida) en un espacio vacío.</summary>
    public void Put(PKM pk, int box, int slot)
    {
        if (pk.GetType() != Save.PKMType)
            throw new InvalidOperationException($"El Pokémon es {pk.GetType().Name} y la partida espera {Save.PKMType.Name}.");
        if (!IsEmpty(GetSlot(box, slot)))
            throw new InvalidOperationException("Ese espacio ya está ocupado.");
        Save.SetBoxSlotAtIndex(pk, box, slot);
    }

    /// <summary>Primer espacio vacío, empezando por la caja indicada.</summary>
    public (int Box, int Slot)? FindEmptySlot(int startBox = 0)
    {
        for (int i = 0; i < BoxCount; i++)
        {
            int box = (startBox + i) % BoxCount;
            for (int slot = 0; slot < SlotsPerBox; slot++)
            {
                if (IsEmpty(GetSlot(box, slot)))
                    return (box, slot);
            }
        }
        return null;
    }

    /// <summary>
    /// Hasta <paramref name="count"/> espacios vacíos, empezando en (startBox, startSlot) y recorriendo todas las cajas.
    /// </summary>
    public List<(int Box, int Slot)> FindEmptySlots(int startBox, int startSlot, int count)
    {
        var result = new List<(int Box, int Slot)>();
        int total = BoxCount * SlotsPerBox;
        int start = startBox * SlotsPerBox + startSlot;
        for (int i = 0; i < total && result.Count < count; i++)
        {
            int index = (start + i) % total;
            int box = index / SlotsPerBox;
            int slot = index % SlotsPerBox;
            if (IsEmpty(GetSlot(box, slot)))
                result.Add((box, slot));
        }
        return result;
    }

    /// <summary>Escribe la partida en su archivo original (PKHeX recalcula checksums y conserva el footer del emulador).</summary>
    public void Write()
    {
        if (Save.Generation <= 2)
            CompactBoxes();
        var data = Save.Write();
        File.WriteAllBytes(FilePath, data.ToArray());
    }

    /// <summary>
    /// En Gen 1 y 2 las cajas son listas sin huecos: si se saca el Pokémon 3 de 10, los de atrás deben correrse.
    /// </summary>
    private void CompactBoxes()
    {
        for (int box = 0; box < BoxCount; box++)
        {
            var filled = Enumerable.Range(0, SlotsPerBox)
                .Select(slot => GetSlot(box, slot))
                .Where(pk => !IsEmpty(pk))
                .ToList();
            for (int slot = 0; slot < SlotsPerBox; slot++)
                Save.SetBoxSlotAtIndex(slot < filled.Count ? filled[slot] : Save.BlankPKM, box, slot);
        }
    }
}
