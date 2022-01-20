using Avalonia;

namespace FlaxPlugMan;

public class Program
{
    public static string[] Args { get; private set;}

    private static void Main(string[] args) 
    {
        Args = args;
        Console.WriteLine("Program is running in DEBUG mode. That means't project will not be updated!"); 
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
    
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}