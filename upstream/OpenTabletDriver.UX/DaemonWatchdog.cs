using System;
using System.Diagnostics;
using System.IO;
using OpenTabletDriver.Desktop.Interop;
using OpenTabletDriver.Interop;
using OpenTabletDriver.Plugin;

namespace OpenTabletDriver.UX
{
    public sealed class DaemonWatchdog : IDisposable
    {
        public event EventHandler DaemonExited;
        private Process daemonProcess;
        private readonly static ProcessStartInfo startInfo = SystemInterop.CurrentPlatform switch
        {
            PluginPlatform.Windows => new ProcessStartInfo
            {
                FileName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "OTD-Touchpad-Experimental" ? Environment.ProcessPath : Path.Join(AppContext.BaseDirectory, "OpenTabletDriver.Daemon.exe"),
                Arguments = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "OTD-Touchpad-Experimental" ? "--asus-daemon" : "",
                WorkingDirectory = AppContext.BaseDirectory,
                CreateNoWindow = true
            },
            PluginPlatform.MacOS => new ProcessStartInfo
            {
                FileName = Path.Join(AppContext.BaseDirectory, "OpenTabletDriver.Daemon")
            },
            _ => new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = Path.Join(AppContext.BaseDirectory, "OpenTabletDriver.Daemon.dll")
            }
        };
        public static bool CanExecute => File.Exists(startInfo.FileName) || File.Exists(startInfo.Arguments);

        public void Start()
        {
            if (SystemInterop.CurrentPlatform == PluginPlatform.Windows || SystemInterop.CurrentPlatform == PluginPlatform.MacOS)
                startInfo.Environment["ASUS_OTD_UI_PARENT"] = Environment.ProcessId.ToString();
            this.daemonProcess = new Process()
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            this.daemonProcess.Exited += (_, e) =>
            {
                DaemonExited?.Invoke(this, EventArgs.Empty);
            };
            this.daemonProcess.Start();
        }

        public void Stop()
        {
            DaemonExited = null;
            if (this.daemonProcess != null)
            {
                try
                {
                    if (!this.daemonProcess.HasExited)
                        this.daemonProcess.Kill();
                }
                catch (InvalidOperationException)
                {
                }
            }

            this.daemonProcess?.Dispose();
            this.daemonProcess = null;
        }

        public static void StopLocalDaemon()
        {
            if (SystemInterop.CurrentPlatform != PluginPlatform.Windows)
                return;
            var expected = Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "OpenTabletDriver.Daemon.exe"));
            foreach (var process in Process.GetProcessesByName("OpenTabletDriver.Daemon"))
            {
                using (process)
                {
                    try
                    {
                        if (string.Equals(process.MainModule?.FileName, expected, StringComparison.OrdinalIgnoreCase))
                            process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (System.ComponentModel.Win32Exception ex)
                    {
                        Log.Exception(ex);
                    }
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
