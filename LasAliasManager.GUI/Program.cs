using Avalonia;
using System;
using System.Text;

namespace LasAliasManager.GUI;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Register encoding provider for legacy encodings
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
