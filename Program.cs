using Avalonia;
using System;

namespace AutomationContent;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Services.ConfigLoader.LoadEnv();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
