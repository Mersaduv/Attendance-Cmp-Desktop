using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace AttandenceDesktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Setup simple file logging to app.log in executable directory
        var logPath = Path.Combine(AppContext.BaseDirectory, "app.log");
        Trace.Listeners.Add(new TextWriterTraceListener(logPath));
        Trace.AutoFlush = true;

        Trace.WriteLine($"\n===== Application start {DateTime.Now} =====");

        // Global exception handlers
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Trace.WriteLine($"[UnhandledException] {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Trace.WriteLine($"[UnobservedTaskException] {e.Exception}");
            e.SetObserved();
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
