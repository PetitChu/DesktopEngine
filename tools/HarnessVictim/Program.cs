using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HarnessVictim;

// Usage: HarnessVictim.exe <x> <y> <width> <height> <logPath>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length < 5) { Console.Error.WriteLine("need: x y w h logPath"); return; }
        int x = int.Parse(args[0]), y = int.Parse(args[1]), w = int.Parse(args[2]), h = int.Parse(args[3]);
        string log = args[4];
        File.WriteAllText(log, ""); // truncate

        ApplicationConfiguration.Initialize();
        var form = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(x, y),
            Size = new Size(w, h),
            BackColor = Color.LightGreen,
            ShowInTaskbar = false,
            TopMost = false,
        };
        form.MouseDown += (_, e) =>
        {
            var sp = form.PointToScreen(e.Location);
            File.AppendAllText(log, $"{sp.X},{sp.Y}{Environment.NewLine}");
        };
        Application.Run(form);
    }
}
