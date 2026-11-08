using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PokeBank.Core;

namespace PokeBank.App;

/// <summary>Panel reutilizable que muestra una partida (cajas y Pokémon).</summary>
public partial class SavePanel : UserControl
{
    private bool _settingCandidates;

    public SaveSession? Session { get; private set; }

    /// <summary>La ventana principal lo usa para impedir abrir el mismo archivo en dos paneles.</summary>
    public Func<string, SavePanel, bool>? IsPathOpenElsewhere { get; set; }

    public event EventHandler? SendToBankRequested;
    public event EventHandler? BringFromBankRequested;
    public event EventHandler? EditRequested;
    public event EventHandler? CreateRequested;
    public event EventHandler<string>? StatusMessage;

    /// <summary>Cambió la imagen de fondo de un juego (para refrescar otros paneles del mismo juego).</summary>
    public event EventHandler? BackgroundChanged;

    public string Header
    {
        get => HeaderText.Text ?? string.Empty;
        set => HeaderText.Text = value;
    }

    public int CurrentBox => Math.Max(0, BoxCombo.SelectedIndex);

    /// <summary>Espacios seleccionados, en el orden en que aparecen en la caja.</summary>
    public List<SlotItem> SelectedSlots =>
        SlotList.SelectedItems?.OfType<SlotItem>().OrderBy(s => s.Slot).ToList() ?? [];

    public SlotItem? SelectedSlot => SelectedSlots.FirstOrDefault();

    public SavePanel()
    {
        InitializeComponent();
        OpenButton.Click += OnOpenFileClicked;
        ToBankButton.Click += (_, _) => SendToBankRequested?.Invoke(this, EventArgs.Empty);
        FromBankButton.Click += (_, _) => BringFromBankRequested?.Invoke(this, EventArgs.Empty);
        EditButton.Click += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);
        CreateButton.Click += (_, _) => CreateRequested?.Invoke(this, EventArgs.Empty);
        BoxCombo.SelectionChanged += (_, _) => RefreshSlots();
        SlotList.DoubleTapped += (_, _) => EditRequested?.Invoke(this, EventArgs.Empty);
        BackgroundButton.Click += OnBackgroundClicked;
        SaveCombo.SelectionChanged += (_, _) =>
        {
            if (!_settingCandidates && SaveCombo.SelectedItem is SaveCandidate c && c.Path != Session?.FilePath)
                OpenPath(c.Path);
        };
    }

    /// <summary>Lista de partidas encontradas en la carpeta elegida.</summary>
    public void SetCandidates(IReadOnlyList<SaveCandidate> candidates)
    {
        _settingCandidates = true;
        SaveCombo.ItemsSource = candidates;
        SaveCombo.IsEnabled = candidates.Count > 0;
        SaveCombo.SelectedItem = candidates.FirstOrDefault(c =>
            string.Equals(c.Path, Session?.FilePath, StringComparison.OrdinalIgnoreCase));
        _settingCandidates = false;
    }

    private async void OnOpenFileClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top is null)
                return;

            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Elige una partida guardada",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Partidas de emulador") { Patterns = ["*.sav", "*.dsv", "*.srm", "*.bin", "main"] },
                    new FilePickerFileType("Todos los archivos") { Patterns = ["*"] },
                ],
            });

            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path is not null)
                OpenPath(path);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "Abrir partida");
            StatusMessage?.Invoke(this, $"No se pudo abrir: {ex.Message}");
        }
    }

    public void OpenPath(string path)
    {
        try
        {
            if (IsPathOpenElsewhere?.Invoke(Path.GetFullPath(path), this) == true)
            {
                StatusMessage?.Invoke(this, "Esa partida ya está abierta en otro panel.");
                RestoreComboSelection();
                return;
            }

            var backups = Path.Combine(BankStore.DefaultRoot, "Backups");
            var session = SaveSession.Open(path, backups);
            Session = session;
            TitleText.Text = session.Title;
            BoxCombo.ItemsSource = Enumerable.Range(0, session.BoxCount).Select(session.GetBoxName).ToList();
            BoxCombo.SelectedIndex = 0;
            BoxCombo.IsEnabled = true;
            ToBankButton.IsEnabled = FromBankButton.IsEnabled = EditButton.IsEnabled = CreateButton.IsEnabled = true;
            BackgroundButton.IsEnabled = true;
            ApplyTheme();
            RestoreComboSelection();
            RefreshSlots();
            StatusMessage?.Invoke(this, $"{Header}: {session.Title}. Copia de seguridad en {session.BackupPath}");
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "Abrir partida");
            StatusMessage?.Invoke(this, $"No se pudo abrir: {ex.Message}");
            RestoreComboSelection();
        }
    }

    /// <summary>Pinta el fondo según el juego de la partida abierta.</summary>
    public void ApplyTheme()
    {
        if (Session is null)
            return;
        var version = Session.Save.Version;
        ThemeGradient.Background = GameTheme.GradientFor(version);
        BackgroundImage.Source = GameTheme.LoadBackground(version);
        WatermarkText.Text = GameNames.Name(version);
    }

    private async void OnBackgroundClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var session = Session;
            var top = TopLevel.GetTopLevel(this);
            if (session is null || top is null)
                return;
            var version = session.Save.Version;
            var game = GameNames.Name(version);

            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Imagen de fondo para {game}",
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.ImageAll],
            });
            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;

            if (path is null)
            {
                // Canceló: si ya había un fondo, ofrecer quitarlo.
                if (GameTheme.HasBackground(version) && top is Window owner &&
                    await ConfirmDialog.AskAsync(owner, "Quitar fondo", $"¿Quitar la imagen de fondo de {game}?", "Quitar"))
                {
                    GameTheme.RemoveBackground(version);
                    BackgroundChanged?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            GameTheme.SetBackground(version, path);
            BackgroundChanged?.Invoke(this, EventArgs.Empty);
            StatusMessage?.Invoke(this, $"Fondo de {game} actualizado.");
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "Fondo");
            StatusMessage?.Invoke(this, $"No se pudo poner el fondo: {ex.Message}");
        }
    }

    /// <summary>Deja el combo de la carpeta apuntando a la partida realmente abierta.</summary>
    private void RestoreComboSelection()
    {
        if (SaveCombo.ItemsSource is not IEnumerable<SaveCandidate> list)
            return;
        _settingCandidates = true;
        SaveCombo.SelectedItem = list.FirstOrDefault(c =>
            string.Equals(c.Path, Session?.FilePath, StringComparison.OrdinalIgnoreCase));
        _settingCandidates = false;
    }

    public void RefreshSlots()
    {
        var session = Session;
        if (session is null)
            return;

        var box = CurrentBox;
        SlotList.ItemsSource = Enumerable.Range(0, session.SlotsPerBox)
            .Select(slot => new SlotItem(box, slot, session.GetSlot(box, slot)))
            .ToList();
    }

    /// <summary>Muestra una caja y selecciona los espacios indicados.</summary>
    public void ShowBox(int box, IEnumerable<int>? selectSlots = null)
    {
        if (Session is null || box < 0 || box >= Session.BoxCount)
            return;
        BoxCombo.SelectedIndex = box;
        RefreshSlots();
        if (selectSlots is null || SlotList.ItemsSource is not List<SlotItem> items)
            return;
        SlotList.SelectedItems?.Clear();
        foreach (var s in selectSlots)
        {
            if (s >= 0 && s < items.Count)
                SlotList.SelectedItems?.Add(items[s]);
        }
    }
}
