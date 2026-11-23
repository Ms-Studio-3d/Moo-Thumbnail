using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Moo-Thumbnail")]
[assembly: AssemblyProduct("Moo-Thumbnail")]
[assembly: AssemblyDescription("YouTube Thumbnail Downloader")]
[assembly: AssemblyCompany("Moo-Thumbnail")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: TargetFramework(".NETFramework,Version=v4.8")]

namespace MooThumbnail
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
            {
                if (key == null || Convert.ToInt32(key.GetValue("Release", 0)) < 528040)
                {
                    MessageBox.Show("يحتاج Moo-Thumbnail إلى .NET Framework 4.8 أو أحدث. ثبّته من موقع Microsoft ثم شغّل البرنامج.", "Moo-Thumbnail", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            using (var service = new ThumbnailService())
            using (var form = new MainForm(service, new DesktopActions())) Application.Run(form);
        }
    }
}
