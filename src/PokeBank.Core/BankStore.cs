using PKHeX.Core;

namespace PokeBank.Core;

/// <summary>Un Pokémon guardado en el banco (un archivo .pk3, .pk4, ... en disco).</summary>
public sealed class BankEntry(string filePath, PKM entity)
{
    public string FilePath { get; } = filePath;
    public PKM Entity { get; } = entity;
    public string Text => Names.Describe(Entity);
    public override string ToString() => Text;
}

/// <summary>
/// El banco: una carpeta con un archivo por Pokémon, en el mismo formato que exporta PKHeX.
/// Así cualquier .pkX del banco también se puede abrir con PKHeX.
/// </summary>
public sealed class BankStore
{
    public string Directory { get; }

    public BankStore(string directory)
    {
        Directory = directory;
        System.IO.Directory.CreateDirectory(directory);
    }

    public static string DefaultRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PokeBank");

    public IReadOnlyList<BankEntry> LoadAll()
    {
        var result = new List<BankEntry>();
        foreach (var file in System.IO.Directory.EnumerateFiles(Directory).Order())
        {
            var bytes = File.ReadAllBytes(file);
            var pk = EntityFormat.GetFromBytes(bytes);
            if (pk is not null && pk.Species != 0)
                result.Add(new BankEntry(file, pk));
        }
        return result;
    }

    public BankEntry Add(PKM pk)
    {
        pk.RefreshChecksum();
        var name = $"{DateTime.Now:yyyyMMdd-HHmmss}_{Guid.NewGuid():N}.{pk.Extension}";
        var path = Path.Combine(Directory, name);
        File.WriteAllBytes(path, pk.Data.ToArray());
        return new BankEntry(path, pk);
    }

    /// <summary>Sobrescribe un Pokémon del banco después de editarlo.</summary>
    public BankEntry Update(BankEntry entry, PKM edited)
    {
        edited.RefreshChecksum();
        File.WriteAllBytes(entry.FilePath, edited.Data.ToArray());
        return new BankEntry(entry.FilePath, edited);
    }

    public void Remove(BankEntry entry)
    {
        if (File.Exists(entry.FilePath))
            File.Delete(entry.FilePath);
    }
}
