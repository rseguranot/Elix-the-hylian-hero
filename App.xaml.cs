using System.Threading;
using System.Windows;

namespace HylianHeroCompanion;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, $"Local\\{BrandIdentity.TechnicalName}.Singleton", out _ownsMutex);
        if (!_ownsMutex)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        if (e.Args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            var exitCode = SelfTest.Run();
            Shutdown(exitCode);
            return;
        }

        var demoMode = e.Args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        var window = new MainWindow(demoMode);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex)
        {
            _singleInstance?.ReleaseMutex();
        }
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
