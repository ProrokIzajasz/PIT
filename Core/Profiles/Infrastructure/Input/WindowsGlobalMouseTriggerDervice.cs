using System.Diagnostics;
using System.Runtime.InteropServices;
using PIT.Core.Input;

namespace PIT.Infrastructure.Input;

public sealed class WindowsGlobalMouseTriggerService : IGlobalMouseTriggerService
{
    private const int WhMouseLl = 14;

    private const int WmXButtonDown = 0x020B;

    private const int XButton1 = 0x0001;
    private const int XButton2 = 0x0002;

    private readonly LowLevelMouseProc _mouseProc;

    private IntPtr _mouseHook = IntPtr.Zero;

    public event Action<GlobalMouseTriggerButton>? Triggered;

    public bool IsRunning { get; private set; }

    public bool BlockMouse4 { get; set; }

    public bool BlockMouse5 { get; set; }

    public WindowsGlobalMouseTriggerService()
    {
        _mouseProc = MouseHookCallback;
    }

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        var moduleName = Process.GetCurrentProcess().MainModule?.ModuleName;
        var moduleHandle = string.IsNullOrWhiteSpace(moduleName)
            ? IntPtr.Zero
            : GetModuleHandle(moduleName);

        _mouseHook = SetWindowsHookEx(
            WhMouseLl,
            _mouseProc,
            moduleHandle,
            0);

        if (_mouseHook == IntPtr.Zero)
        {
            throw new InvalidOperationException("Nie udało się uruchomić globalnego hooka Mouse4/Mouse5.");
        }

        IsRunning = true;
    }

    public void Stop()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        IsRunning = false;
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam.ToInt32() == WmXButtonDown)
        {
            var data = Marshal.PtrToStructure<MouseLlHookStruct>(lParam);
            var xButton = GetHighWord(data.MouseData);

            if (xButton == XButton1)
            {
                Task.Run(() => Triggered?.Invoke(GlobalMouseTriggerButton.Mouse4));

                if (BlockMouse4)
                {
                    return 1;
                }
            }

            if (xButton == XButton2)
            {
                Task.Run(() => Triggered?.Invoke(GlobalMouseTriggerButton.Mouse5));

                if (BlockMouse5)
                {
                    return 1;
                }
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private static int GetHighWord(uint value)
    {
        return (int)((value >> 16) & 0xFFFF);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        LowLevelMouseProc callback,
        IntPtr moduleHandle,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(
        IntPtr hookHandle,
        int nCode,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string moduleName);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseLlHookStruct
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}