using Avalonia.Controls;
using Avalonia.Layout;
using PKHeX.Core;
using PokeBank.Core;
using Option = PokeBank.Core.Option;
using EvolutionOption = PokeBank.Core.EvolutionOption;

namespace PokeBank.App;

/// <summary>
/// Ficha editable de un Pokémon. Trabaja sobre una copia: solo devuelve el resultado si se pulsa "Guardar cambios".
/// Uso: var editado = await new EditorWindow(pk, sav).ShowDialog&lt;PKM?&gt;(owner);
/// </summary>
public partial class EditorWindow : Window
{
    private static readonly string[] StatNames = ["PS", "Ataque", "Defensa", "Velocidad", "At. Esp.", "Def. Esp."];

    private readonly PKM _working;
    private readonly HashSet<int> _allowedSpecies;
    private readonly NumericUpDown[] _ivBoxes = new NumericUpDown[6];
    private readonly NumericUpDown[] _evBoxes = new NumericUpDown[6];
    private readonly TextBlock[] _statTexts = new TextBlock[6];
    private string _nicknameShown = string.Empty;
    private bool _loading;

    /// <summary>Solo para el cargador XAML / diseñador de Avalonia (evita la advertencia AVLN3001). No usar en código.</summary>
    public EditorWindow()
    {
        InitializeComponent();
        _working = null!;
        _allowedSpecies = [];
    }

    public EditorWindow(PKM pokemon, SaveFile context, string? title = null)
    {
        InitializeComponent();
        _working = pokemon.Clone();
        if (title is not null)
            Title = title;

        // Listas filtradas por la partida (solo especies, movimientos y objetos que existen en ese juego).
        var sources = PokemonEdits.SourcesFor(context);
        var species = PokemonEdits.ToOptions(sources.Species);
        _allowedSpecies = species.Select(s => s.Value).Where(v => v > 0).ToHashSet();

        _loading = true;
        SpeciesCombo.ItemsSource = species;
        NatureCombo.ItemsSource = PokemonEdits.ToOptions(sources.Natures);
        ItemCombo.ItemsSource = PokemonEdits.ToOptions(sources.Items);
        var moves = PokemonEdits.ToOptions(sources.Moves);
        foreach (var combo in MoveCombos)
            combo.ItemsSource = moves;
        BuildStatsGrid();
        AdaptToGeneration();
        _loading = false;

        WireEvents();
        LoadFromPokemon();
    }

    /// <summary>Gen 1-2 no tienen naturalezas ni habilidades, y Gen 1 tampoco objetos.</summary>
    private void AdaptToGeneration()
    {
        var format = _working.Format;
        NatureLabel.IsVisible = NatureCombo.IsVisible = format >= 3;
        AbilityLabel.IsVisible = AbilityText.IsVisible = format >= 3;
        ItemLabel.IsVisible = ItemCombo.IsVisible = format >= 2;
        if (format <= 2)
            ShinyCheck.Content = "Shiny ★ (ajusta los DVs)";
    }

    private ComboBox[] MoveCombos => [Move1Combo, Move2Combo, Move3Combo, Move4Combo];

    private void BuildStatsGrid()
    {
        StatsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        AddCell(new TextBlock { Text = "", }, 0, 0);
        var evLabel = _working.MaxEV > 255 ? "Stat Exp" : "EV";
        var ivLabel = _working.Format <= 2 ? "DV" : "IV";
        AddCell(new TextBlock { Text = $"{ivLabel} (0-{_working.MaxIV})", FontWeight = Avalonia.Media.FontWeight.SemiBold }, 0, 1);
        AddCell(new TextBlock { Text = $"{evLabel} (0-{_working.MaxEV})", FontWeight = Avalonia.Media.FontWeight.SemiBold }, 0, 2);
        AddCell(new TextBlock { Text = "Stat", FontWeight = Avalonia.Media.FontWeight.SemiBold }, 0, 3);

        for (int i = 0; i < 6; i++)
        {
            int stat = i;
            int row = i + 1;
            StatsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            AddCell(new TextBlock { Text = StatNames[i] }, row, 0);

            _ivBoxes[i] = new NumericUpDown { Minimum = 0, Maximum = _working.MaxIV, Increment = 1, FormatString = "0" };
            _ivBoxes[i].ValueChanged += (_, e) => OnChange(() => PokemonEdits.SetIV(_working, stat, (int)(e.NewValue ?? 0)));
            AddCell(_ivBoxes[i], row, 1);

            // Gen 1-2 usan "Stat Exp" (0-65535) en vez de EVs (0-255).
            _evBoxes[i] = new NumericUpDown { Minimum = 0, Maximum = _working.MaxEV, Increment = _working.MaxEV > 255 ? 256 : 4, FormatString = "0" };
            _evBoxes[i].ValueChanged += (_, e) => OnChange(() => PokemonEdits.SetEV(_working, stat, (int)(e.NewValue ?? 0)));
            AddCell(_evBoxes[i], row, 2);

            _statTexts[i] = new TextBlock { FontWeight = Avalonia.Media.FontWeight.Bold };
            AddCell(_statTexts[i], row, 3);
        }
    }

    private void AddCell(Control control, int row, int column)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        StatsGrid.Children.Add(control);
    }

    private void WireEvents()
    {
        SpeciesCombo.SelectionChanged += (_, _) =>
        {
            if (SpeciesCombo.SelectedItem is Option o && o.Value != _working.Species)
                OnChange(() => PokemonEdits.ChangeSpecies(_working, (ushort)o.Value), reloadAll: true);
        };
        NicknameBox.LostFocus += (_, _) =>
        {
            var text = NicknameBox.Text ?? string.Empty;
            if (text != _nicknameShown)
                OnChange(() => PokemonEdits.SetNickname(_working, text), reloadAll: true);
        };
        LevelBox.ValueChanged += (_, e) => OnChange(() => _working.CurrentLevel = (byte)(e.NewValue ?? 1));
        NatureCombo.SelectionChanged += (_, _) => OnChange(ApplyNatureAndShiny, reloadAll: true);
        ShinyCheck.Click += (_, _) => OnChange(ApplyNatureAndShiny, reloadAll: true);
        ItemCombo.SelectionChanged += (_, _) =>
        {
            if (ItemCombo.SelectedItem is Option o)
                OnChange(() => _working.HeldItem = o.Value);
        };
        foreach (var combo in MoveCombos)
            combo.SelectionChanged += (_, _) => OnChange(ApplyMoves);

        SuggestMovesButton.Click += (_, _) => OnChange(() => PokemonEdits.SuggestMoves(_working), reloadAll: true);
        MaxIvButton.Click += (_, _) => OnChange(() =>
        {
            for (int i = 0; i < 6; i++)
                PokemonEdits.SetIV(_working, i, _working.MaxIV);
        }, reloadAll: true);
        ClearEvButton.Click += (_, _) => OnChange(() =>
        {
            for (int i = 0; i < 6; i++)
                PokemonEdits.SetEV(_working, i, 0);
        }, reloadAll: true);
        EvolveButton.Click += (_, _) =>
        {
            if (EvoCombo.SelectedItem is EvolutionOption evo)
                OnChange(() => PokemonEdits.ChangeSpecies(_working, evo.Species, evo.Form), reloadAll: true);
        };

        SaveButton.Click += (_, _) =>
        {
            // Por si el mote seguía con el foco puesto.
            var text = NicknameBox.Text ?? string.Empty;
            if (text != _nicknameShown)
                PokemonEdits.SetNickname(_working, text);
            _working.RefreshChecksum();
            Close(_working);
        };
        CancelButton.Click += (_, _) => Close(null);
    }

    /// <summary>Aplica un cambio sobre la copia de trabajo y refresca la ficha.</summary>
    private void OnChange(Action change, bool reloadAll = false)
    {
        if (_loading)
            return;
        change();
        if (reloadAll)
            LoadFromPokemon();
        else
            RefreshDerived();
    }

    private void ApplyNatureAndShiny()
    {
        var nature = NatureCombo.SelectedItem is Option o ? (Nature)o.Value : _working.Nature;
        PokemonEdits.SetNatureAndShiny(_working, nature, ShinyCheck.IsChecked == true);
    }

    private void ApplyMoves()
    {
        ushort Value(ComboBox c) => c.SelectedItem is Option o ? (ushort)o.Value : (ushort)0;
        PokemonEdits.SetMoves(_working, Value(Move1Combo), Value(Move2Combo), Value(Move3Combo), Value(Move4Combo));
    }

    /// <summary>Pasa todos los datos del Pokémon a los controles.</summary>
    private void LoadFromPokemon()
    {
        _loading = true;
        try
        {
            Select(SpeciesCombo, _working.Species);
            _nicknameShown = _working.IsNicknamed ? _working.Nickname : string.Empty;
            NicknameBox.Text = _nicknameShown;
            LevelBox.Value = _working.CurrentLevel;
            Select(NatureCombo, (int)_working.Nature);
            Select(ItemCombo, _working.HeldItem);
            ShinyCheck.IsChecked = _working.IsShiny;

            Select(Move1Combo, _working.Move1);
            Select(Move2Combo, _working.Move2);
            Select(Move3Combo, _working.Move3);
            Select(Move4Combo, _working.Move4);

            for (int i = 0; i < 6; i++)
            {
                _ivBoxes[i].Value = PokemonEdits.GetIV(_working, i);
                _evBoxes[i].Value = PokemonEdits.GetEV(_working, i);
            }

            var evolutions = PokemonEdits.GetEvolutions(_working, _allowedSpecies);
            EvoCombo.ItemsSource = evolutions;
            EvoCombo.SelectedIndex = evolutions.Count > 0 ? 0 : -1;
            EvoCombo.IsEnabled = EvolveButton.IsEnabled = evolutions.Count > 0;
            EvoInfoText.Text = evolutions.Count > 0 ? string.Empty : "No evoluciona (o su evolución no existe en este juego).";
        }
        finally
        {
            _loading = false;
        }
        RefreshDerived();
    }

    /// <summary>Recalcula lo que depende del resto: stats, habilidad, género, sprite y legalidad.</summary>
    private void RefreshDerived()
    {
        var stats = PokemonEdits.GetStats(_working);
        for (int i = 0; i < 6 && i < stats.Length; i++)
            _statTexts[i].Text = stats[i].ToString();

        var evTotal = Enumerable.Range(0, 6).Sum(i => PokemonEdits.GetEV(_working, i));
        EvTotalText.Text = _working.MaxEV > 255
            ? string.Empty // Gen 1-2: no hay límite total
            : $"EVs totales: {evTotal}/510" + (evTotal > 510 ? "  ⚠ pasa del máximo" : string.Empty);

        var abilities = GameInfo.Strings.Ability;
        AbilityText.Text = _working.Ability >= 0 && _working.Ability < abilities.Count ? abilities[_working.Ability] : "—";

        var gender = _working.Gender switch { 0 => "Macho ♂", 1 => "Hembra ♀", _ => "Sin género" };
        InfoText.Text = $"{gender} · {_working.Extension.ToUpperInvariant()} · OT {_working.OriginalTrainerName} ({_working.TID16:00000})";

        var (valid, report) = PokemonEdits.CheckLegality(_working);
        LegalText.Text = valid ? "✔ Legal" : "✖ PKHeX lo considera no legal (revisa el informe)";
        LegalText.Foreground = valid ? Avalonia.Media.Brushes.SeaGreen : Avalonia.Media.Brushes.IndianRed;
        ReportText.Text = report;

        _ = LoadSpriteAsync();
    }

    private async Task LoadSpriteAsync()
    {
        if (_working.IsEgg)
        {
            SpriteImage.Source = SpriteCache.Egg;
            return;
        }
        var species = _working.Species;
        var shiny = _working.IsShiny;
        var bmp = await SpriteCache.GetAsync(species, shiny);
        if (_working.Species == species && _working.IsShiny == shiny && !_working.IsEgg)
            SpriteImage.Source = bmp;
    }

    private static void Select(ComboBox combo, int value)
    {
        if (combo.ItemsSource is IEnumerable<Option> options)
        {
            int index = 0;
            foreach (var option in options)
            {
                if (option.Value == value)
                {
                    combo.SelectedIndex = index;
                    return;
                }
                index++;
            }
        }
        combo.SelectedIndex = -1;
    }
}
