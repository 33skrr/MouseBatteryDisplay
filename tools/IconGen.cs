using System;
using System.IO;
using MouseBatteryDisplay;

internal static class IconGen
{
    private static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : Path.Combine("assets", "app.ico");
        int percent = 87;
        if (args.Length > 1)
        {
            int.TryParse(args[1], out percent);
        }
        TrayIconFactory.SaveIco(path, percent, false, true, false);
        Console.WriteLine("ICO written: " + Path.GetFullPath(path));
        return 0;
    }
}
