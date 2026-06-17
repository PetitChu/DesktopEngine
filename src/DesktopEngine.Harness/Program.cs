using System;
using System.Runtime.Versioning;
using DesktopEngine.Harness;

// Usage:
//   harness verify <hostExe> <victimExe> <screenshot.png>
[SupportedOSPlatform("windows")]
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("commands: verify"); return 2; }
        switch (args[0])
        {
            case "verify" when args.Length == 4:
                return VerifyDesktop.Run(args[1], args[2], args[3]) ? 0 : 1;
            default:
                Console.Error.WriteLine("unknown command or wrong arg count");
                return 2;
        }
    }
}
