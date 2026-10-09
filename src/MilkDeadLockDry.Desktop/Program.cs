namespace MilkDeadLockDry.Desktop;

internal static class Program
{
    private static void Main(string[] args)
    {
        NSApplication.Init();
        using var appDelegate = new AppDelegate();
        NSApplication.SharedApplication.Delegate = appDelegate;
        NSApplication.Main(args);
    }
}
