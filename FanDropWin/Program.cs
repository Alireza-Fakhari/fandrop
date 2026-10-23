using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace FanDropWin
{
    static class Native
    {
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int i, int v);
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        public const int VK_LBUTTON = 0x01, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
        public static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            using var mutex = new System.Threading.Mutex(true, "FanDropWin.Single", out bool first);
            if (!first) return;
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var overlay = new FanOverlay();
            var argv = Environment.GetCommandLineArgs();
            if (argv.Length == 5 && argv[1] == "--preview") { overlay.RenderPreview(argv[2], int.Parse(argv[3]), argv[4]); return; }
            var watcher = new DragWatcher(overlay);
            using var tray = new WinForms.NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath),
                Text = "FanDrop â€“ drag a file, hold Shift / Ctrl / Alt",
                Visible = true,
                ContextMenuStrip = new WinForms.ContextMenuStrip()
            };
            tray.ContextMenuStrip.Items.Add("How to use", null, (_, _) => MessageBox.Show(
                "Drag any file from Explorer and, while dragging, hold:\n\n" +
                "  Shift  â†’  Convert (PNG, JPG, MP4, MP3 â€¦)\n" +
                "  Ctrl   â†’  Actions (resize, compress, mute, trim â€¦)\n" +
                "  Alt    â†’  Tools (copy path, ZIP, reveal, recycle)\n\n" +
                "A fan appears under the cursor. Drop the file on a wedge.\n" +
                "Results go to Downloads\\FanDrop.", "FanDrop"));
            tray.ContextMenuStrip.Items.Add("Open output folder", null, (_, _) => System.Diagnostics.Process.Start("explorer.exe", Actions.OutputDir));
            tray.ContextMenuStrip.Items.Add("Open log", null, (_, _) => { Log.Write("log opened"); System.Diagnostics.Process.Start("notepad.exe", Log.Path); });
            tray.ContextMenuStrip.Items.Add(new WinForms.ToolStripSeparator());
            tray.ContextMenuStrip.Items.Add("Quit", null, (_, _) => app.Shutdown());
            tray.ShowBalloonTip(3000, "FanDrop is running", "Drag a file and hold Shift, Ctrl or Alt.", WinForms.ToolTipIcon.Info);
            Log.Write("FanDrop started");
            app.Run();
        }
    }

    /// Watches for a mouse drag with a modifier held and shows the fan under the cursor.
    class DragWatcher
    {
        readonly FanOverlay overlay;
        readonly DispatcherTimer timer;
        Native.POINT downAt; bool wasDown;

        public DragWatcher(FanOverlay o)
        {
            overlay = o;
            timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Normal, Tick, Dispatcher.CurrentDispatcher);
            timer.Start();
        }

        void Tick(object s, EventArgs e)
        {
            bool down = Native.Down(Native.VK_LBUTTON);
            Native.GetCursorPos(out var p);
            if (down && !wasDown) downAt = p;
            wasDown = down;

            if (!down) { if (overlay.IsVisible && !overlay.Busy) overlay.Hide(); return; }
            if (overlay.IsVisible) return;

            bool moved = Math.Abs(p.X - downAt.X) + Math.Abs(p.Y - downAt.Y) > 12;
            FanMode? mode = Native.Down(Native.VK_SHIFT) ? FanMode.Convert
                          : Native.Down(Native.VK_CONTROL) ? FanMode.Actions
                          : Native.Down(Native.VK_MENU) ? FanMode.Tools : null;
            if (moved && mode != null) overlay.ShowAt(p.X, p.Y, mode.Value);
        }
    }
}

