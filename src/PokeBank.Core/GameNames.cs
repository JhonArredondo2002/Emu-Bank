using PKHeX.Core;

namespace PokeBank.Core;

/// <summary>Nombre en español, generación y colores representativos de cada versión.</summary>
public sealed record GameInfoEs(string Name, byte Generation, string ColorA, string ColorB);

/// <summary>
/// Tabla de juegos. Se busca por el nombre del enum de PKHeX (p. ej. "E", "HGSS", "SN") para no depender
/// de qué miembros existan exactamente en GameVersion.
/// </summary>
public static class GameNames
{
    private static readonly Dictionary<string, GameInfoEs> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        // Gen 1
        ["RD"] = new("Rojo", 1, "#E3350D", "#7A1F12"),
        ["GN"] = new("Verde", 1, "#3E9B48", "#1E5A27"),
        ["BU"] = new("Azul", 1, "#2A75BB", "#123E6B"),
        ["YW"] = new("Amarillo", 1, "#F8D030", "#B08A10"),
        ["RB"] = new("Rojo/Azul", 1, "#E3350D", "#2A75BB"),
        ["RBY"] = new("Rojo/Azul/Amarillo", 1, "#E3350D", "#F8D030"),
        // Gen 2
        ["GD"] = new("Oro", 2, "#DAA520", "#8A6510"),
        ["SI"] = new("Plata", 2, "#C0C0C0", "#6E7480"),
        ["C"] = new("Cristal", 2, "#4FD1D9", "#2A6F9E"),
        ["GS"] = new("Oro/Plata", 2, "#DAA520", "#C0C0C0"),
        ["GSC"] = new("Oro/Plata/Cristal", 2, "#DAA520", "#4FD1D9"),
        // Gen 3
        ["R"] = new("Rubí", 3, "#A00000", "#5A0A0A"),
        ["S"] = new("Zafiro", 3, "#0000A0", "#0A0A5A"),
        ["E"] = new("Esmeralda", 3, "#00A000", "#0A5A2A"),
        ["FR"] = new("Rojo Fuego", 3, "#FF7327", "#A0300A"),
        ["LG"] = new("Verde Hoja", 3, "#00DD00", "#2A7A10"),
        ["RS"] = new("Rubí/Zafiro", 3, "#A00000", "#0000A0"),
        ["RSE"] = new("Rubí/Zafiro/Esmeralda", 3, "#A00000", "#00A000"),
        ["FRLG"] = new("Rojo Fuego/Verde Hoja", 3, "#FF7327", "#00DD00"),
        // Gen 4
        ["D"] = new("Diamante", 4, "#AAAAFF", "#4A5AA0"),
        ["P"] = new("Perla", 4, "#FFAAAA", "#A05A6A"),
        ["Pt"] = new("Platino", 4, "#999999", "#4A3A5A"),
        ["HG"] = new("Oro HeartGold", 4, "#B69E00", "#7A3A0A"),
        ["SS"] = new("Plata SoulSilver", 4, "#C0C0E1", "#4A5A8A"),
        ["DP"] = new("Diamante/Perla", 4, "#AAAAFF", "#FFAAAA"),
        ["DPPt"] = new("Diamante/Perla/Platino", 4, "#AAAAFF", "#999999"),
        ["HGSS"] = new("Oro HeartGold/Plata SoulSilver", 4, "#B69E00", "#C0C0E1"),
        // Gen 5
        ["B"] = new("Negro", 5, "#444444", "#111111"),
        ["W"] = new("Blanco", 5, "#E1E1E1", "#8A8A8A"),
        ["B2"] = new("Negro 2", 5, "#303E51", "#0A1420"),
        ["W2"] = new("Blanco 2", 5, "#EBC5C3", "#8A5A5A"),
        ["BW"] = new("Negro/Blanco", 5, "#444444", "#E1E1E1"),
        ["B2W2"] = new("Negro 2/Blanco 2", 5, "#303E51", "#EBC5C3"),
        // Gen 6
        ["X"] = new("X", 6, "#025DA6", "#0A2A5A"),
        ["Y"] = new("Y", 6, "#EA1A3E", "#7A0A1E"),
        ["AS"] = new("Zafiro Alfa", 6, "#26649C", "#0A2A4A"),
        ["OR"] = new("Rubí Omega", 6, "#CF3025", "#6A0A0A"),
        ["XY"] = new("X/Y", 6, "#025DA6", "#EA1A3E"),
        ["ORAS"] = new("Rubí Omega/Zafiro Alfa", 6, "#CF3025", "#26649C"),
        // Gen 7
        ["SN"] = new("Sol", 7, "#F1912B", "#A0400A"),
        ["MN"] = new("Luna", 7, "#5599CA", "#1A2A5A"),
        ["US"] = new("Ultrasol", 7, "#E95B2B", "#7A1A0A"),
        ["UM"] = new("Ultraluna", 7, "#226DB5", "#0A1A4A"),
        ["SM"] = new("Sol/Luna", 7, "#F1912B", "#5599CA"),
        ["USUM"] = new("Ultrasol/Ultraluna", 7, "#E95B2B", "#226DB5"),
    };

    public static GameInfoEs Get(GameVersion version) =>
        Table.TryGetValue(version.ToString(), out var info)
            ? info
            : new GameInfoEs(version.ToString(), 0, "#777777", "#333333");

    public static string Name(GameVersion version) => Get(version).Name;

    /// <summary>Generación del juego de origen, o 0 si no se conoce.</summary>
    public static byte Generation(GameVersion version) => Get(version).Generation;
}
