using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PokeBank.App;

/// <summary>Diálogo simple de Sí / No con un texto largo desplazable.</summary>
public sealed class ConfirmDialog : Window
{
    public ConfirmDialog() : this("Confirmar", string.Empty) { }

    public ConfirmDialog(string title, string message, string yes = "Sí", string no = "Cancelar")
    {
        Title = title;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        var yesButton = new Button { Content = yes, Classes = { "accent" } };
        var noButton = new Button { Content = no };
        yesButton.Click += (_, _) => Close(true);
        noButton.Click += (_, _) => Close(false);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 12, 0, 0),
            Children = { noButton, yesButton },
        };
        DockPanel.SetDock(buttons, Dock.Bottom);

        Content = new DockPanel
        {
            Margin = new Avalonia.Thickness(16),
            Children =
            {
                buttons,
                new ScrollViewer
                {
                    MaxHeight = 480,
                    Content = new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                },
            },
        };
    }

    public static Task<bool> AskAsync(Window owner, string title, string message, string yes = "Sí") =>
        new ConfirmDialog(title, message, yes).ShowDialog<bool>(owner);
}
