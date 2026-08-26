using System.Diagnostics;
using System.Runtime.InteropServices;
using PIT.Core.Automation;
using PIT.Core.Recording;
using PitMouseButton = PIT.Core.Input.MouseButton;

namespace PIT.Infrastructure.Recording;

public sealed class WindowsInputRecorder : IInputRecorder
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;

    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;

    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int WmMButtonDown = 0x0207;
    private const int WmMButtonUp = 0x0208;

    private const int VkF12 = 0x7B;

    private readonly LowLevelProc _keyboardProc;
    private readonly LowLevelProc _mouseProc;

    private readonly HashSet<int> _pressedKeys = new();

    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;

    private DateTime _lastRecordedAtUtc;

    public bool IsRecording { get; private set; }

    public event Action<RecordedInputEvent>? InputRecorded;

    public event Action? StopRequested;

    public WindowsInputRecorder()
    {
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
    }

    public void Start()
    {
        if (IsRecording)
        {
            return;
        }

        _pressedKeys.Clear();
        _lastRecordedAtUtc = DateTime.UtcNow;

        var moduleName = Process.GetCurrentProcess().MainModule?.ModuleName;
        var moduleHandle = string.IsNullOrWhiteSpace(moduleName)
            ? IntPtr.Zero
            : GetModuleHandle(moduleName);

        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, moduleHandle, 0);
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, moduleHandle, 0);

        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            Stop();
            throw new InvalidOperationException("Nie udało się uruchomić globalnego nagrywania inputu.");
        }

        IsRecording = true;
    }

    public void Stop()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        _pressedKeys.Clear();
        IsRecording = false;
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsRecording)
        {
            var message = wParam.ToInt32();
            var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            var vkCode = (int)data.VkCode;

            if (message is WmKeyDown or WmSysKeyDown)
            {
                if (vkCode == VkF12)
                {
                    StopRequested?.Invoke();
                    return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                }

                if (_pressedKeys.Add(vkCode))
                {
                    RecordKey(vkCode, ActionKind.KeyDown);
                }
            }
            else if (message is WmKeyUp or WmSysKeyUp)
            {
                if (_pressedKeys.Remove(vkCode))
                {
                    RecordKey(vkCode, ActionKind.KeyUp);
                }
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && IsRecording)
        {
            var message = wParam.ToInt32();
            var data = Marshal.PtrToStructure<MouseLlHookStruct>(lParam);

            switch (message)
            {
                case WmLButtonDown:
                    RecordMouse(PitMouseButton.Left, data.Point.X, data.Point.Y, ActionKind.MouseDown);
                    break;

                case WmLButtonUp:
                    RecordMouse(PitMouseButton.Left, data.Point.X, data.Point.Y, ActionKind.MouseUp);
                    break;

                case WmRButtonDown:
                    RecordMouse(PitMouseButton.Right, data.Point.X, data.Point.Y, ActionKind.MouseDown);
                    break;

                case WmRButtonUp:
                    RecordMouse(PitMouseButton.Right, data.Point.X, data.Point.Y, ActionKind.MouseUp);
                    break;

                case WmMButtonDown:
                    RecordMouse(PitMouseButton.Middle, data.Point.X, data.Point.Y, ActionKind.MouseDown);
                    break;

                case WmMButtonUp:
                    RecordMouse(PitMouseButton.Middle, data.Point.X, data.Point.Y, ActionKind.MouseUp);
                    break;
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void RecordKey(int vkCode, ActionKind kind)
    {
        var keyName = ResolveKeyName(vkCode);

        if (keyName is null)
        {
            return;
        }

        var action = new ActionDefinition
        {
            Kind = kind,
            Parameters =
            {
                ["Key"] = keyName
            }
        };

        InputRecorded?.Invoke(new RecordedInputEvent
        {
            Action = action,
            DelayBeforeMilliseconds = TakeDelay(),
            DisplayName = $"{kind} {keyName}"
        });
    }

    private void RecordMouse(PitMouseButton button, int x, int y, ActionKind kind)
    {
        var action = new ActionDefinition
        {
            Kind = kind,
            Parameters =
            {
                ["Button"] = button.ToString(),
                ["X"] = x.ToString(),
                ["Y"] = y.ToString()
            }
        };

        InputRecorded?.Invoke(new RecordedInputEvent
        {
            Action = action,
            DelayBeforeMilliseconds = TakeDelay(),
            DisplayName = $"{kind} {button} X={x} Y={y}"
        });
    }

    private int TakeDelay()
    {
        var now = DateTime.UtcNow;
        var delay = (int)(now - _lastRecordedAtUtc).TotalMilliseconds;
        _lastRecordedAtUtc = now;

        return Math.Clamp(delay, 0, 60_000);
    }

    private static string? ResolveKeyName(int vkCode)
    {
        if (vkCode is >= 0x41 and <= 0x5A)
        {
            return ((char)vkCode).ToString();
        }

        if (vkCode is >= 0x30 and <= 0x39)
        {
            return ((char)vkCode).ToString();
        }

        if (vkCode is >= 0x70 and <= 0x87)
        {
            return $"F{vkCode - 0x70 + 1}";
        }

        return vkCode switch
        {
            0x20 => "Space",
            0x0D => "Enter",
            0x1B => "Esc",
            0x09 => "Tab",
            0x08 => "Backspace",
            0x2E => "Delete",
            0x2D => "Insert",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x26 => "Up",
            0x28 => "Down",
            0x25 => "Left",
            0x27 => "Right",

            0x10 => "Shift",
            0xA0 => "LShift",
            0xA1 => "RShift",
            0x11 => "Ctrl",
            0xA2 => "LCtrl",
            0xA3 => "RCtrl",
            0x12 => "Alt",
            0xA4 => "LAlt",
            0xA5 => "RAlt",

            // OEM / punctuation keys. Slash is the important one for commands such as "/warp".
            0xBA => "Semicolon",
            0xBB => "Equal",
            0xBC => "Comma",
            0xBD => "Minus",
            0xBE => "Period",
            0xBF => "Slash",
            0xC0 => "Backtick",
            0xDB => "LeftBracket",
            0xDC => "Backslash",
            0xDD => "RightBracket",
            0xDE => "Quote",
            0xE2 => "Oem102",

            _ => null
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc callback, IntPtr moduleHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string moduleName);

    private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

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
