using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PokeBank.Core;

namespace PokeBank.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Names.Init("es");

        // Cualquier error no controlado en la interfaz se registra y se muestra, en vez de cerrar la app.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            CrashLog.Write(e.Exception, "Error no controlado en la interfaz");
            e.Handled = true;
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: MainWindow main })
                main.ShowError(e.Exception);
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
