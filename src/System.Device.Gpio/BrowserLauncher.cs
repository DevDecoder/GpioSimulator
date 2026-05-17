using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DevDecoder.GpioSimulator
{
    public static class BrowserLauncher
    {
        public static void Open(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
            }
            catch
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        Process.Start("cmd", $"/c start {url.Replace("&", "^&")}");
                    }
                    catch
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss} GPIO Simulator] Please open your browser and navigate to: {url}");
                    }
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss} GPIO Simulator] Please open your browser and navigate to: {url}");
                }
            }
        }
    }
}
