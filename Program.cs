global using System;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
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
}