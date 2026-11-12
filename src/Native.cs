using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Miu
{
    internal sealed class Hotkey : IDisposable
    {
        private HwndSource source;
        private readonly Action callback;
        public bool Registered { get; private set; }
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        public Hotkey(Window window, Action action)
        {
            callback = action;
            var handle = new WindowInteropHelper(window).EnsureHandle();
            source = HwndSource.FromHwnd(handle); source.AddHook(Hook);
            Registered = RegisterHotKey(handle, 7813, 0x4003, 0x20); // Control + Alt + Space, no repeats
        }
        private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled)
        {
            if (msg == 0x0312 && wp.ToInt32() == 7813) { handled = true; callback(); }
            return IntPtr.Zero;
        }
        public void Dispose()
        {
            if (source == null) return;
            if (Registered) UnregisterHotKey(source.Handle, 7813);
            source.RemoveHook(Hook); source = null;
        }
    }
}
