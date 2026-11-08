using PKHeX.Core;

namespace PokeBank.Core;

/// <summary>Resultado de "recrear" un Pokémon en otro formato.</summary>
public sealed record Recreation(PKM Source, PKM Result, IReadOnlyList<string> Lost)
{
    public string Summary
    {
        get
        {
            var head = $"{Names.Describe(Source)}  →  {Result.Extension.ToUpperInvariant()}";
            return Lost.Count == 0 ? head : $"{head}\n   Se pierde: {string.Join(", ", Lost)}";
        }
    }
}

/// <summary>Cómo se puede llevar un Pokémon a una partida.</summary>
public enum TransferKind
{
    /// <summary>Mismo formato o ruta oficial (Máquina del Tiempo, Parque Compi, Pokétransfer, Poké Transporter).</summary>
    Direct,
    /// <summary>No hay ruta oficial: se puede crear una copia con lo compatible.</summary>
    Recreate,
    /// <summary>No se puede (p. ej. huevos).</summary>
    Impossible,
}

/// <summary>
/// Mueve Pokémon entre formatos.
/// Rutas oficiales (las hace PKHeX): Gen 1 → Gen 2, Gen 1/2 → Gen 7 (Consola Virtual), y Gen 3 → 4 → 5 → 6 → 7.
/// Cualquier otra combinación (bajar de generación, Gen 2 → Gen 3, etc.) se resuelve "recreando" el Pokémon.
/// </summary>
public static class Transfer
{
    static Transfer()
    {
        // Solo conversiones "reales": nada de forzar datos incompatibles.
        EntityConverter.AllowIncompatibleConversion = EntityCompatibilitySetting.DisallowIncompatible;
    }

    /// <summary>¿Existe una ruta oficial de un formato a otro?</summary>
    public static bool IsOfficialRoute(byte from, byte to) =>
        from == to
        || (from == 1 && to == 2)               // Máquina del Tiempo
        || (from <= 2 && to == 7)               // Consola Virtual → Poké Transporter
        || (from >= 3 && to > from && to <= 7); // Parque Compi, Pokétransfer, Poké Transporter, Poké Bank

    public static TransferKind Classify(PKM source, SaveFile target)
    {
        if (source.IsEgg)
            return TransferKind.Impossible;
        return IsOfficialRoute(source.Format, target.Generation) ? TransferKind.Direct : TransferKind.Recreate;
    }

    public static bool TryConvert(PKM source, SaveFile target, out PKM? converted, out string error)
    {
        converted = null;

        switch (Classify(source, target))
        {
            case TransferKind.Impossible:
                error = "Los huevos no se pueden transferir. Eclosiónalo primero.";
                return false;
            case TransferKind.Recreate:
                // Importante: nunca le pedimos a PKHeX una ruta que no existe (PK4 → PK3 lo deja dando vueltas).
                error = $"De Gen {source.Format} a Gen {target.Generation} no hay transferencia oficial; hay que recrearlo.";
                return false;
        }

        var clone = source.Clone();
        if (clone.GetType() == target.PKMType)
        {
            converted = clone;
            error = string.Empty;
            return true;
        }

        var result = EntityConverter.ConvertToType(clone, target.PKMType, out var status);
        if (result is null || status != EntityConverterResult.Success)
        {
            error = Explain(status, source, target);
            return false;
        }

        converted = result;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Crea en el formato de la partida destino un Pokémon lo más parecido posible al original.
    /// Conserva lo que el formato destino soporte: especie, PID (naturaleza, género, habilidad y shiny en Gen 3-4),
    /// entrenador original, idioma, mote, nivel, IVs y EVs (re-escalados en Gen 1-2), y los movimientos,
    /// objeto y Poké Ball que existan en el juego destino. PKHeX marcará la copia como no legal.
    /// </summary>
    public static bool TryRecreate(PKM source, SaveFile target, out Recreation? recreation, out string error)
    {
        recreation = null;
        if (source.IsEgg)
        {
            error = "Los huevos no se pueden transferir. Eclosiónalo primero.";
            return false;
        }

        var gen = target.Generation;
        var sources = PokemonEdits.SourcesFor(target);
        var species = sources.Species.Select(s => s.Value).Where(v => v > 0).ToHashSet();
        if (!species.Contains(source.Species))
        {
            error = $"{Names.Species(source.Species)} no existe en {GameNames.Name(target.Version)}.";
            return false;
        }

        var lost = new List<string>();
        var pk = target.BlankPKM;

        // Entrenador primero: el shiny depende de PID + TID/SID.
        pk.Species = source.Species;
        pk.Form = 0;
        if (source.Form != 0)
            lost.Add("forma");
        pk.OriginalTrainerName = Truncate(source.OriginalTrainerName, gen >= 6 ? 12 : 7);
        pk.OriginalTrainerGender = source.OriginalTrainerGender;
        pk.TID16 = source.TID16;
        pk.SID16 = source.SID16;
        pk.Language = IsLanguageAvailable(source.Language, gen) ? source.Language : target.Language;
        if (gen >= 3)
        {
            var originGen = GameNames.Generation(source.Version);
            pk.Version = originGen >= 3 && originGen <= gen ? source.Version : target.Version;

            // En Gen 3-4 el PID guarda naturaleza, género, habilidad y shiny.
            pk.PID = source.Format >= 3 ? source.PID : (uint)Random.Shared.NextInt64(0, uint.MaxValue + 1L);
            if (gen >= 6)
                pk.EncryptionConstant = source.Format >= 6 ? source.EncryptionConstant : pk.PID;
        }

        if (source.IsNicknamed)
            pk.SetNickname(Truncate(source.Nickname, gen >= 6 ? 12 : 10));
        else
            pk.ClearNickname();

        pk.CurrentLevel = source.CurrentLevel;
        pk.MetLevel = source.CurrentLevel;

        // IVs/EVs: Gen 1-2 usan DVs 0-15 y Stat Exp 0-65535; se re-escalan.
        for (int stat = 0; stat < 6; stat++)
        {
            PokemonEdits.SetIV(pk, stat, PokemonEdits.Rescale(PokemonEdits.GetIV(source, stat), source.MaxIV, pk.MaxIV));
            PokemonEdits.SetEV(pk, stat, PokemonEdits.Rescale(PokemonEdits.GetEV(source, stat), source.MaxEV, pk.MaxEV));
        }
        if (gen <= 2 && pk.MaxEV > source.MaxEV)
            lost.Add("(EVs aproximados)");

        // Naturaleza y shiny (después de IVs: en Gen 1-2 el shiny sale de los DVs).
        PokemonEdits.SetNatureAndShiny(pk, source.Nature, source.IsShiny);
        if (gen <= 2 && source.Format >= 3)
            lost.Add("naturaleza y habilidad");
        if (gen <= 2 && source.IsShiny)
            lost.Add("(IVs ajustados para que siga siendo shiny)");

        // Movimientos que existan en el juego destino.
        var moveIds = sources.Moves.Select(m => m.Value).ToHashSet();
        var keptMoves = new List<ushort>();
        foreach (var move in new[] { source.Move1, source.Move2, source.Move3, source.Move4 })
        {
            if (move == 0)
                continue;
            if (moveIds.Contains(move))
                keptMoves.Add(move);
            else
                lost.Add($"mov. {MoveName(move)}");
        }
        while (keptMoves.Count < 4)
            keptMoves.Add(0);
        PokemonEdits.SetMoves(pk, keptMoves[0], keptMoves[1], keptMoves[2], keptMoves[3]);
        if (keptMoves.All(m => m == 0))
            PokemonEdits.SuggestMoves(pk);

        // Objeto (desde Gen 2) y Poké Ball (desde Gen 3): los IDs cambian entre generaciones, se buscan por nombre.
        if (source.HeldItem != 0)
        {
            var itemName = ItemName(source.HeldItem);
            if (gen >= 2 && FindByName(sources.Items, itemName) is { } itemId)
                pk.HeldItem = itemId;
            else
                lost.Add($"objeto {itemName}");
        }

        if (gen >= 3)
        {
            var ballName = BallName(source.Ball);
            if (source.Format >= 3 && FindByName(sources.Balls, ballName) is { } ballId)
            {
                pk.Ball = (byte)ballId;
            }
            else
            {
                pk.Ball = 4; // Poké Ball
                if (source.Format >= 3)
                    lost.Add($"{ballName} (queda en Poké Ball)");
            }
        }

        PokemonEdits.RefreshAbility(pk, PokemonEdits.AbilityIndex(source));
        pk.HealPP();
        pk.RefreshChecksum();

        lost.Add("datos de captura, cintas y concursos");
        recreation = new Recreation(source, pk, lost);
        error = string.Empty;
        return true;
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    /// <summary>Busca en una lista de PKHeX el ID que corresponde a un nombre (los IDs cambian entre generaciones).</summary>
    private static int? FindByName(IEnumerable<ComboItem> list, string name) =>
        list.Where(i => i.Value != 0 && i.Text == name).Select(i => (int?)i.Value).FirstOrDefault();

    private static string MoveName(ushort move) =>
        move < GameInfo.Strings.Move.Count ? GameInfo.Strings.Move[move] : $"#{move}";

    private static string ItemName(int item) =>
        item >= 0 && item < GameInfo.Strings.Item.Count ? GameInfo.Strings.Item[item] : $"#{item}";

    private static string BallName(byte ball) =>
        ball < GameInfo.Strings.balllist.Length ? GameInfo.Strings.balllist[ball] : $"#{ball}";

    /// <summary>
    /// Idiomas por generación: Gen 1-2 se quedan con el de la partida; Gen 3 no tiene coreano (8);
    /// el chino (9 y 10) llega en Gen 7.
    /// </summary>
    private static bool IsLanguageAvailable(int language, byte gen) => gen switch
    {
        <= 2 => false,
        3 => language is >= 1 and <= 7,
        <= 6 => language is >= 1 and <= 8,
        _ => language is >= 1 and <= 10,
    };

    private static string Explain(EntityConverterResult status, PKM pk, SaveFile target)
    {
        var from = pk.Extension.ToUpperInvariant();
        var to = target.PKMType.Name;
        var game = GameNames.Name(target.Version);
        return status switch
        {
            EntityConverterResult.NoTransferRoute => $"No existe una ruta de {from} a {to}.",
            EntityConverterResult.IncompatibleSpecies => $"{Names.Species(pk.Species)} no existe en {game}.",
            EntityConverterResult.IncompatibleForm => $"La forma de {Names.Species(pk.Species)} no existe en {game}.",
            _ => $"No se pudo convertir {from} → {to} ({status}).",
        };
    }
}
