using Avalonia;

namespace FlaxPlugMan;

public class Program
{
    private static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}