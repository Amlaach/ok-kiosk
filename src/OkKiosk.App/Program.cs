using OkKiosk.App.Core;
using OkKiosk.App.UI;

namespace OkKiosk.App;

public static class Program
{
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    public static int Main(string[] args)
    {
        var security = SecurityLoader.Load();

        // Delegate SYSTEM service execution to the security binary
        if (args.Contains("--service") || args.Contains("--install-service"))
            return security.RunServiceMain(args);

        _singleInstanceMutex = new Mutex(true, "Local\\OkKiosk_SingleInstance_Mutex", out bool createdNew);
        if (!createdNew)
            return 0;

        ApplicationConfiguration.Initialize();
        var config = AppConfig.Load();
        using var mainForm = new KioskLauncherForm(security, config);
        using var tray = new TrayManager(mainForm, security, config);

        if (!args.Contains("--background"))
            Application.Run(mainForm);
        else
            Application.Run();

        GC.KeepAlive(_singleInstanceMutex);
        return 0;
    }
}
