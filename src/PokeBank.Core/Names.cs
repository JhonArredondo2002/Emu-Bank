using PKHeX.Core;

namespace PokeBank.Core;

/// <summary>
/// Textos legibles (nombres de especie en español, descripción corta de cada Pokémon).
/// </summary>
public static class Names
{
    private static bool _initialized;

    /// <summary>Carga los textos de PKHeX en español. Llamar una vez al iniciar la app.</summary>
    public static void Init(string language = "es")
    {
        if (_initialized)
            return;
        GameInfo.CurrentLanguage = language;
        GameInfo.Strings = GameInfo.GetStrings(language);
        _initialized = true;
    }

    public static string Species(ushort species)
    {
        var list = GameInfo.Strings.specieslist;
        return species < list.Length ? list[species] : $"#{species}";
    }

    /// <summary>Ej: "Pikachu «Chispa» Nv.25 ★ ♂ [PK3 · Esmeralda]"</summary>
    public static string Describe(PKM pk)
    {
        if (pk.Species == 0)
            return "—";
        if (pk.IsEgg)
            return $"Huevo [{pk.Extension.ToUpperInvariant()}]";

        var name = Species(pk.Species);
        var nick = pk.IsNicknamed ? $" «{pk.Nickname}»" : string.Empty;
        var shiny = pk.IsShiny ? " ★" : string.Empty;
        var gender = pk.Gender switch { 0 => " ♂", 1 => " ♀", _ => string.Empty };
        return $"{name}{nick} Nv.{pk.CurrentLevel}{shiny}{gender} [{pk.Extension.ToUpperInvariant()} · {GameNames.Name(pk.Version)}]";
    }
}
