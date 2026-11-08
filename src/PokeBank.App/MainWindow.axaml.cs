using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PKHeX.Core;
using PokeBank.Core;
using Transfer = PokeBank.Core.Transfer;
using TransferKind = PokeBank.Core.TransferKind;

namespace PokeBank.App;

public partial class MainWindow : Window
{
    private const int MaxPanels = 4;

    private readonly BankStore _bank = new(Path.Combine(BankStore.DefaultRoot, "Bank"));
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly SavePanel[] _panels = new SavePanel[MaxPanels];
    private IReadOnlyList<SaveCandidate> _candidates = [];

    public MainWindow()
    {
        InitializeComponent();

        for (int i = 0; i < MaxPanels; i++)
        {
            var panel = new SavePanel { Header = $"Partida {(char)('A' + i)}", Margin = new Avalonia.Thickness(0, 0, 8, 8) };
            panel.IsPathOpenElsewhere = IsPathOpenElsewhere;
            panel.SendToBankRequested += (s, _) => Guard("Enviar al banco", () => SendToBank((SavePanel)s!));
            panel.BringFromBankRequested += async (s, _) => await GuardAsync("Traer del banco", () => BringFromBankAsync((SavePanel)s!));
            panel.EditRequested += async (s, _) => await GuardAsync("Editar", () => EditSlotAsync((SavePanel)s!));
            panel.CreateRequested += async (s, _) => await GuardAsync("Crear", () => CreateAsync((SavePanel)s!));
            panel.StatusMessage += (_, msg) => SetStatus(msg);
            panel.BackgroundChanged += (_, _) => Guard("Fondo", () =>
            {
                foreach (var p in _panels)
                    p.ApplyTheme();
            });
            _panels[i] = panel;
        }

        PanelCountCombo.ItemsSource = new[] { 1, 2, 3, 4 };
        PanelCountCombo.SelectedItem = Math.Clamp(_settings.VisiblePanels, 1, MaxPanels);
        PanelCountCombo.SelectionChanged += (_, _) => Guard("Paneles", ApplyPanelCount);
        ApplyPanelCount();

        OpenFolderButton.Click += async (_, _) => await GuardAsync("Abrir carpeta", PickFolderAsync);
        EditBankButton.Click += async (_, _) => await GuardAsync("Editar banco", EditBankAsync);
        BankList.DoubleTapped += async (_, _) => await GuardAsync("Editar banco", EditBankAsync);
        DeleteButton.Click += async (_, _) => await GuardAsync("Liberar", DeleteFromBankAsync);

        Guard("Cargar banco", RefreshBank);

        if (_settings.SavesFolder is { } folder && Directory.Exists(folder))
            Opened += async (_, _) => await GuardAsync("Escanear carpeta", () => ScanFolderAsync(folder));
    }

    // ───────────────────────── utilidades ─────────────────────────

    private void SetStatus(string message) => StatusText.Text = message;

    /// <summary>Muestra un error inesperado sin cerrar la app.</summary>
    public void ShowError(Exception ex) =>
        SetStatus($"Ocurrió un error: {ex.Message}. Detalles en {CrashLog.FilePath}");

    private void Guard(string action, Action work)
    {
        try { work(); }
        catch (Exception ex)
        {
            CrashLog.Write(ex, action);
            ShowError(ex);
        }
    }

    private async Task GuardAsync(string action, Func<Task> work)
    {
        try { await work(); }
        catch (Exception ex)
        {
            CrashLog.Write(ex, action);
            ShowError(ex);
        }
    }

    private bool IsPathOpenElsewhere(string fullPath, SavePanel asking) =>
        _panels.Any(p => p != asking && string.Equals(p.Session?.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<SavePanel> OpenPanels => _panels.Where(p => p.Session is not null);

    // ───────────────────── carpeta y paneles ─────────────────────

    private void ApplyPanelCount()
    {
        var count = PanelCountCombo.SelectedItem is int n ? n : 2;
        SavesGrid.Children.Clear();
        SavesGrid.Columns = count <= 2 ? count : 2;
        SavesGrid.Rows = count <= 2 ? 1 : 2;
        for (int i = 0; i < count; i++)
            SavesGrid.Children.Add(_panels[i]);

        _settings.VisiblePanels = count;
        _settings.Save();
    }

    private async Task PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Carpeta donde guardas las partidas (.sav / .dsv)",
            AllowMultiple = false,
        });
        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (path is null)
            return;

        _settings.SavesFolder = path;
        _settings.Save();
        await ScanFolderAsync(path);
    }

    private async Task ScanFolderAsync(string folder)
    {
        FolderText.Text = $"Buscando partidas en {folder}…";
        OpenFolderButton.IsEnabled = false;
        try
        {
            _candidates = await Task.Run(() => SaveScanner.Scan(folder));
        }
        finally
        {
            OpenFolderButton.IsEnabled = true;
        }

        FolderText.Text = $"{folder}  ·  {_candidates.Count} partidas encontradas";
        foreach (var panel in _panels)
            panel.SetCandidates(_candidates);

        // Abre automáticamente las primeras partidas en los paneles vacíos que están a la vista.
        var visible = PanelCountCombo.SelectedItem is int n ? n : 2;
        var free = _candidates.Where(c => !_panels.Any(p =>
            string.Equals(p.Session?.FilePath, c.Path, StringComparison.OrdinalIgnoreCase))).GetEnumerator();
        foreach (var panel in _panels.Take(visible).Where(p => p.Session is null))
        {
            if (!free.MoveNext())
                break;
            panel.OpenPath(free.Current.Path);
        }

        SetStatus(_candidates.Count == 0
            ? "No encontré partidas de Gen 1 a 7 en esa carpeta (se revisan hasta 12 niveles de subcarpetas)."
            : $"Encontré {_candidates.Count} partidas. Elige cuál ver en cada panel con su lista desplegable.");
    }

    // ─────────────────────────── banco ───────────────────────────

    private void RefreshBank()
    {
        var entries = _bank.LoadAll();
        BankList.ItemsSource = entries.Select(e => new BankItem(e)).ToList();
        BankInfo.Text = $"{entries.Count} Pokémon · {_bank.Directory}";
    }

    private List<BankItem> SelectedBankItems =>
        BankList.SelectedItems?.OfType<BankItem>().ToList() ?? [];

    /// <summary>Partida → banco (todos los seleccionados). Primero se escribe el banco y luego se quita de la partida.</summary>
    private void SendToBank(SavePanel panel)
    {
        var session = panel.Session;
        var slots = panel.SelectedSlots.Where(s => !s.IsEmpty).ToList();
        if (session is null || slots.Count == 0)
        {
            SetStatus("Selecciona uno o varios Pokémon de la partida (Ctrl/Shift + clic).");
            return;
        }

        int sent = 0, eggs = 0;
        foreach (var slot in slots)
        {
            if (slot.Entity.IsEgg)
            {
                eggs++;
                continue;
            }
            _bank.Add(slot.Entity.Clone());
            session.Take(slot.Box, slot.Slot);
            sent++;
        }

        if (sent > 0)
            session.Write();
        panel.RefreshSlots();
        RefreshBank();
        SetStatus($"{sent} Pokémon enviados al banco." + (eggs > 0 ? $" {eggs} huevo(s) omitidos: hay que eclosionarlos primero." : string.Empty));
    }

    /// <summary>Banco → partida (todos los seleccionados). Los de generaciones más nuevas se ofrecen para recrear.</summary>
    private async Task BringFromBankAsync(SavePanel panel)
    {
        var session = panel.Session;
        var items = SelectedBankItems;
        if (session is null)
        {
            SetStatus($"Abre primero una partida en {panel.Header}.");
            return;
        }
        if (items.Count == 0)
        {
            SetStatus("Selecciona uno o varios Pokémon del banco (Ctrl/Shift + clic).");
            return;
        }

        var ready = new List<(BankItem Item, PKM Pokemon)>();
        var recreations = new List<(BankItem Item, Recreation Recreation)>();
        var problems = new List<string>();

        foreach (var item in items)
        {
            switch (Transfer.Classify(item.Entity, session.Save))
            {
                case TransferKind.Recreate:
                    if (Transfer.TryRecreate(item.Entity, session.Save, out var rec, out var recError) && rec is not null)
                        recreations.Add((item, rec));
                    else
                        problems.Add($"{item.Text}: {recError}");
                    break;
                default:
                    if (Transfer.TryConvert(item.Entity, session.Save, out var converted, out var error) && converted is not null)
                        ready.Add((item, converted));
                    else
                        problems.Add($"{item.Text}: {error}");
                    break;
            }
        }

        if (recreations.Count > 0)
        {
            var text = new StringBuilder()
                .AppendLine($"{recreations.Count} Pokémon no tienen una ruta oficial hacia {GameNames.Name(session.Save.Version)} (Gen {session.Save.Generation}).")
                .AppendLine("Puedo crear una copia en este juego con todo lo compatible: especie, entrenador, mote, nivel, IVs, EVs,")
                .AppendLine("naturaleza/género/shiny/habilidad cuando el juego los tiene, y los movimientos, objeto y Ball que existan.")
                .AppendLine("PKHeX marcará estas copias como no legales. El original sale del banco.")
                .AppendLine();
            foreach (var (_, rec) in recreations)
                text.AppendLine("• " + rec.Summary);

            if (await ConfirmDialog.AskAsync(this, "Recrear en otra generación", text.ToString(), "Recrear"))
                ready.AddRange(recreations.Select(r => (r.Item, r.Recreation.Result)));
            else
                problems.Add($"{recreations.Count} sin recrear (cancelado).");
        }

        if (ready.Count == 0)
        {
            SetStatus(problems.Count > 0 ? string.Join("  |  ", problems) : "Nada que transferir.");
            return;
        }

        // Espacios: desde el espacio vacío seleccionado, o desde el inicio de la caja actual.
        var first = panel.SelectedSlot;
        var (startBox, startSlot) = first is { IsEmpty: true } ? (first.Box, first.Slot) : (panel.CurrentBox, 0);
        var targets = session.FindEmptySlots(startBox, startSlot, ready.Count);
        if (targets.Count < ready.Count)
            problems.Add($"Solo había {targets.Count} espacios libres; {ready.Count - targets.Count} se quedaron en el banco.");

        for (int i = 0; i < targets.Count; i++)
        {
            var (item, pokemon) = ready[i];
            var (box, slot) = targets[i];
            session.Put(pokemon, box, slot);
            _bank.Remove(item.Entry);
        }

        if (targets.Count > 0)
        {
            session.Write();
            panel.ShowBox(targets[0].Box, targets.Where(t => t.Box == targets[0].Box).Select(t => t.Slot));
        }
        RefreshBank();

        var message = $"{targets.Count} Pokémon enviados a {panel.Header}.";
        if (problems.Count > 0)
            message += "  |  " + string.Join("  |  ", problems);
        SetStatus(message);
    }

    private async Task DeleteFromBankAsync()
    {
        var items = SelectedBankItems;
        if (items.Count == 0)
        {
            SetStatus("Selecciona uno o varios Pokémon del banco.");
            return;
        }

        var list = string.Join("\n", items.Select(i => "• " + i.Text));
        if (!await ConfirmDialog.AskAsync(this, "Liberar Pokémon",
                $"Se borrarán del banco para siempre:\n\n{list}", "Liberar"))
            return;

        foreach (var item in items)
            _bank.Remove(item.Entry);
        RefreshBank();
        SetStatus($"{items.Count} Pokémon liberados del banco.");
    }

    // ─────────────────────── editar y crear ───────────────────────

    private async Task EditSlotAsync(SavePanel panel)
    {
        var session = panel.Session;
        var slot = panel.SelectedSlots.FirstOrDefault(s => !s.IsEmpty);
        if (session is null || slot is null)
        {
            SetStatus("Selecciona un Pokémon de la partida para editarlo.");
            return;
        }

        var editor = new EditorWindow(slot.Entity, session.Save, $"Editar — {Names.Describe(slot.Entity)}");
        var edited = await editor.ShowDialog<PKM?>(this);
        if (edited is null)
            return;

        session.Save.SetBoxSlotAtIndex(edited, slot.Box, slot.Slot);
        session.Write();
        panel.ShowBox(slot.Box, [slot.Slot]);
        SetStatus($"{Names.Describe(edited)} actualizado en {panel.Header}.");
    }

    private async Task CreateAsync(SavePanel panel)
    {
        var session = panel.Session;
        if (session is null)
        {
            SetStatus("Abre primero una partida.");
            return;
        }

        var first = panel.SelectedSlot;
        var (startBox, startSlot) = first is { IsEmpty: true } ? (first.Box, first.Slot) : (panel.CurrentBox, 0);
        var targets = session.FindEmptySlots(startBox, startSlot, 1);
        if (targets.Count == 0)
        {
            SetStatus("No hay espacios libres en las cajas de esa partida.");
            return;
        }

        var created = PokemonEdits.Create(session.Save, species: 1);
        var editor = new EditorWindow(created, session.Save, "Crear Pokémon");
        var result = await editor.ShowDialog<PKM?>(this);
        if (result is null)
            return;

        var (box, slot) = targets[0];
        session.Put(result, box, slot);
        session.Write();
        panel.ShowBox(box, [slot]);
        SetStatus($"{Names.Describe(result)} creado en {session.GetBoxName(box)}, espacio {slot + 1}.");
    }

    private async Task EditBankAsync()
    {
        var item = SelectedBankItems.FirstOrDefault();
        if (item is null)
        {
            SetStatus("Selecciona un Pokémon del banco.");
            return;
        }

        // El editor necesita una partida del mismo formato para saber qué especies, movimientos y objetos existen.
        var context = OpenPanels.Select(p => p.Session!).FirstOrDefault(s => s.Save.PKMType == item.Entity.GetType());
        if (context is null)
        {
            SetStatus($"Para editar un {item.Entity.Extension.ToUpperInvariant()} del banco, abre una partida de Gen {item.Entity.Format} en algún panel.");
            return;
        }

        var editor = new EditorWindow(item.Entity, context.Save, $"Editar (banco) — {item.Text}");
        var edited = await editor.ShowDialog<PKM?>(this);
        if (edited is null)
            return;

        _bank.Update(item.Entry, edited);
        RefreshBank();
        SetStatus($"{Names.Describe(edited)} actualizado en el banco.");
    }
}
