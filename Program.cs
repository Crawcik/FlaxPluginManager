global using System;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Avalonia;

namespace FlaxPlugMan;

public class Program
{
	public static string[] Args { get; private set;}

	private static void Main(string[] args) 
	{
		Args = args;
		BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
	}
	
	public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();

	public static string GetOSCommand()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "explorer";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "xdg-open";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "open";
		return null;
	}
}