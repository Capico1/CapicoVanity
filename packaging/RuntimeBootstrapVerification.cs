using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

static class RuntimeBootstrapVerification
{
    delegate bool EnumWindow(IntPtr window, IntPtr state);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindow callback, IntPtr state);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr window, EnumWindow callback, IntPtr state);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);

    static int Main(string[] args)
    {
        using (var process = Process.Start(new ProcessStartInfo(args[0]) {
            UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden
        }))
        {
            var watch = Stopwatch.StartNew();
            bool declined = false;
            while (!process.HasExited && watch.Elapsed.TotalSeconds < 15)
            {
                EnumWindows((window, state) => {
                    uint pid;
                    GetWindowThreadProcessId(window, out pid);
                    if (pid != process.Id) return true;
                    var text = new StringBuilder();
                    EnumChildWindows(window, (child, ignored) => {
                        var caption = new StringBuilder(2048);
                        GetWindowText(child, caption, caption.Capacity);
                        text.Append(caption);
                        return true;
                    }, IntPtr.Zero);
                    if (text.ToString().Contains(".NET Framework 4.7.2"))
                    {
                        SendMessage(window, 0x111, (IntPtr)7, IntPtr.Zero); // WM_COMMAND / IDNO
                        declined = true;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
                Thread.Sleep(30);
            }
            if (!process.HasExited) { process.Kill(); throw new Exception("Missing-runtime dialog did not close"); }
            if (!declined || process.ExitCode != 2) throw new Exception("Missing-runtime decline behavior failed");
            Console.WriteLine("PASS: Native bootstrap shows the runtime guidance and exits after No");
            return 0;
        }
    }
}
