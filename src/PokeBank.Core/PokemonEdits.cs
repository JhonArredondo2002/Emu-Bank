using PKHeX.Core;

namespace PokeBank.Core;

/// <summary>Una opción de lista (especie, movimiento, objeto, naturaleza...).</summary>
public sealed record Option(int Value, string Text)
{
    public override string ToString() => Text;
}

/// <summary>Una evolución posible.</summary>
public sealed record EvolutionOption(ushort Species, byte Form, string Text)
{
    public override string ToString() => Text;
}

/// <summary>
/// Ediciones sobre un Pokémon, apoyadas en PKHeX.Core.
/// Ojo: en Gen 3 y 4 la naturaleza, el género, la habilidad y el shiny salen del PID,
/// así que cambiar uno puede cambiar los otros.
/// </summary>
public static class PokemonEdits
{
    /// <summary>Listas de especies, movimientos, objetos y naturalezas válidas para una partida.</summary>
    public static FilteredGameDataSource SourcesFor(SaveFile sav) => new(sav, GameInfo.Sources);

    public static List<Option> ToOptions(IEnumerable<ComboItem> items) =>
        items.Select(i => new Option(i.Value, i.Text)).ToList();

    /// <summary>Cambia la especie (sirve para evolucionar o para elegir otra al crear).</summary>
    public static void ChangeSpecies(PKM pk, ushort species, byte form = 0)
    {
        var hadNickname = pk.IsNicknamed;
        pk.Species = species;
        pk.Form = form;
        if (!hadNickname)
            pk.ClearNickname();
        RefreshAbility(pk);
    }

    /// <summary>
    /// Vuelve a calcular la habilidad para la especie actual.
    /// Gen 1-2 no tienen habilidades; en Gen 3-4 el slot sale del PID; desde Gen 5 se guarda aparte.
    /// </summary>
    public static void RefreshAbility(PKM pk, int? index = null)
    {
        if (pk.Format <= 2)
            return;
        int slot = pk.Format <= 4
            ? (int)(pk.PID & 1)
            : index ?? Math.Max(0, pk.AbilityNumber >> 1);
        pk.RefreshAbility(slot);
    }

    /// <summary>Índice de habilidad (0, 1 u oculta 2) de un Pokémon en cualquier formato.</summary>
    public static int AbilityIndex(PKM pk) => pk.Format switch
    {
        <= 2 => 0,
        <= 4 => (int)(pk.PID & 1),
        _ => Math.Max(0, pk.AbilityNumber >> 1),
    };

    /// <summary>Aplica mote. Vacío = quitar mote (vuelve al nombre de la especie).</summary>
    public static void SetNickname(PKM pk, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            pk.ClearNickname();
        else
            pk.SetNickname(text.Trim());
    }

    /// <summary>Pone naturaleza y shiny a la vez (en Gen 3/4 ambos dependen del PID).</summary>
    public static void SetNatureAndShiny(PKM pk, Nature nature, bool shiny)
    {
        if (pk.Format <= 2)
        {
            // Gen 1-2: no hay naturaleza real y el shiny sale de los DVs (IVs).
            if (shiny && !pk.IsShiny)
                pk.SetShiny();
            else if (!shiny && pk.IsShiny)
                pk.SetUnshiny();
            return;
        }

        if (!shiny)
        {
            if (pk.IsShiny)
                pk.SetUnshiny();
            if (pk.Nature != nature)
                pk.SetNature(nature);
            for (int i = 0; i < 100 && pk.IsShiny; i++) // muy improbable, pero por si el nuevo PID salió shiny
                pk.SetNature(nature);
        }
        else
        {
            for (int i = 0; i < 10_000; i++)
            {
                if (pk.IsShiny && pk.Nature == nature)
                    break;
                pk.SetShiny();
                if (pk.Nature != nature && pk.Format >= 5)
                    pk.SetNature(nature); // desde Gen 5 la naturaleza ya no depende del PID
            }
        }
        RefreshAbility(pk);
    }

    public static void SetMoves(PKM pk, ushort m1, ushort m2, ushort m3, ushort m4)
    {
        pk.SetMoves([m1, m2, m3, m4]);
        pk.HealPP();
    }

    /// <summary>Movimientos sugeridos por PKHeX (los que aprendería por nivel).</summary>
    public static void SuggestMoves(PKM pk)
    {
        pk.SetMoveset();
        pk.HealPP();
    }

    // Orden de PKHeX: PS, Ataque, Defensa, Velocidad, At. Esp., Def. Esp.
    public static int GetIV(PKM pk, int stat) => stat switch
    {
        0 => pk.IV_HP, 1 => pk.IV_ATK, 2 => pk.IV_DEF, 3 => pk.IV_SPE, 4 => pk.IV_SPA, _ => pk.IV_SPD,
    };

    public static void SetIV(PKM pk, int stat, int value)
    {
        switch (stat)
        {
            case 0: pk.IV_HP = value; break;
            case 1: pk.IV_ATK = value; break;
            case 2: pk.IV_DEF = value; break;
            case 3: pk.IV_SPE = value; break;
            case 4: pk.IV_SPA = value; break;
            default: pk.IV_SPD = value; break;
        }
    }

    public static int GetEV(PKM pk, int stat) => stat switch
    {
        0 => pk.EV_HP, 1 => pk.EV_ATK, 2 => pk.EV_DEF, 3 => pk.EV_SPE, 4 => pk.EV_SPA, _ => pk.EV_SPD,
    };

    public static void SetEV(PKM pk, int stat, int value)
    {
        switch (stat)
        {
            case 0: pk.EV_HP = value; break;
            case 1: pk.EV_ATK = value; break;
            case 2: pk.EV_DEF = value; break;
            case 3: pk.EV_SPE = value; break;
            case 4: pk.EV_SPA = value; break;
            default: pk.EV_SPD = value; break;
        }
    }

    /// <summary>Convierte un IV/EV entre escalas (p. ej. DV 0-15 de Gen 1-2 ↔ IV 0-31; Stat Exp 0-65535 ↔ EV 0-255).</summary>
    public static int Rescale(int value, int fromMax, int toMax)
    {
        if (fromMax <= 0 || fromMax == toMax)
            return Math.Clamp(value, 0, toMax);
        return Math.Clamp((int)Math.Round(value * (double)toMax / fromMax), 0, toMax);
    }

    /// <summary>Stats reales calculados (mismo orden que GetIV/GetEV).</summary>
    public static ushort[] GetStats(PKM pk) => pk.GetStats(pk.PersonalInfo);

    /// <summary>Especies a las que puede evolucionar directamente, limitadas a las que existen en la partida.</summary>
    public static List<EvolutionOption> GetEvolutions(PKM pk, IReadOnlySet<int> allowedSpecies)
    {
        var result = new List<EvolutionOption>();
        var tree = EvolutionTree.GetEvolutionTree(pk.Context);
        foreach (var method in tree.Forward.GetForward(pk.Species, pk.Form).Span)
        {
            if (method.Species == 0 || !allowedSpecies.Contains(method.Species))
                continue;
            if (result.Any(r => r.Species == method.Species))
                continue;
            var level = method.Level > 0 ? $" (Nv. {method.Level})" : string.Empty;
            result.Add(new EvolutionOption(method.Species, method.GetDestinationForm(pk.Form),
                $"{Names.Species(method.Species)}{level}"));
        }
        return result;
    }

    /// <summary>Resumen de legalidad: (es legal, informe en texto).</summary>
    public static (bool Valid, string Report) CheckLegality(PKM pk)
    {
        try
        {
            var la = new LegalityAnalysis(pk);
            return (la.Valid, la.Report());
        }
        catch (Exception ex)
        {
            return (false, $"No se pudo analizar: {ex.Message}");
        }
    }

    /// <summary>
    /// Crea un Pokémon nuevo nivel 5 con los datos del entrenador de la partida.
    /// No inventa un lugar de captura, así que puede salir marcado como no legal; PKHeX lo indica en la ficha.
    /// </summary>
    public static PKM Create(SaveFile sav, ushort species)
    {
        var pk = sav.BlankPKM;
        pk.Species = species;
        pk.Form = 0;
        pk.Version = sav.Version;
        pk.Language = sav.Language;
        pk.OriginalTrainerName = sav.OT;
        pk.OriginalTrainerGender = sav.Gender;
        pk.TID16 = sav.TID16;
        pk.SID16 = sav.SID16;
        pk.PID = (uint)Random.Shared.NextInt64(0, uint.MaxValue + 1L);
        if (pk.Format >= 6)
            pk.EncryptionConstant = pk.PID;
        pk.ClearNickname();
        pk.CurrentLevel = 5;
        pk.MetLevel = 5;
        pk.Ball = 4; // Poké Ball

        for (int stat = 0; stat < 6; stat++)
            SetIV(pk, stat, Random.Shared.Next(0, pk.MaxIV + 1));

        RefreshAbility(pk);
        SuggestMoves(pk);
        pk.RefreshChecksum();
        return pk;
    }
}
