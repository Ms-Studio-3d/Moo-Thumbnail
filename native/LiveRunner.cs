using System;
using System.IO;
namespace MooThumbnail
{
    internal static class LiveRunner
    {
        [STAThread]
        private static void Main()
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".test-data", "native"));
            Directory.CreateDirectory(root);
            using (var writer = new StreamWriter(Path.Combine(root, "live-native.log"), false))
            {
                writer.AutoFlush = true; Console.SetOut(writer); Console.SetError(writer);
                int code = Tests.Main(new[] { "--live", root }); Console.WriteLine("EXIT " + code); Environment.ExitCode = code;
            }
        }
    }
}
