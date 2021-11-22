using System;
using System.Diagnostics;
using System.IO;

namespace UninstallAction
{
    class Program
    {
        static void Main(string[] args)
        {
            using (Process RegisterProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                CreateNoWindow = true,
                Arguments = $"-Command \"regedit /s \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\Restore_Folder.reg")}\"\"",
            }))
            {
                RegisterProcess.WaitForExit();
            }

            using (Process RegisterProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                CreateNoWindow = true,
                Arguments = $"-Command \"regedit /s \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\Restore_WIN_E.reg")}\"\"",
            }))
            {
                RegisterProcess.WaitForExit();
            }
        }
    }
}
