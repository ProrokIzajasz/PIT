using System.Runtime.InteropServices;
using PIT.Core.Input;

namespace PIT.Infrastructure.Input;

public sealed class NativeInputService : IInputService
{
    private const int InputMouse = 0;
    private const int InputKeyboard = 1;

    private const uint KeyEventFKeyUp = 0x0002;

    private const uint MouseEventFLeftDown = 0x0002;
    private const uint MouseEventFLeftUp = 0x0004;
    private const uint MouseEventFRightDown = 0x0008;
    private const uint MouseEventFRightUp = 0x0010;
    private const uint MouseEventFMiddleDown = 0x0020;
    private const uint MouseEventFMiddleUp = 0x0040;

    public async Task PressKeyAsync(string key, string? modifiers, int holdMilliseconds, CancellationToken cancellationToken = default)
    {
        await KeyDownAsync(key, modifiers, cancellationToken);

        if (holdMilliseconds > 0)
        {
            await Task.Delay(holdMilliseconds, cancellationToken);
        }

        await KeyUpAsync(key, modifiers, cancellationToken);
    }

    public Task KeyDownAsync(string key, string? modifiers, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var modifierKeys = ResolveModifiers(modifiers).ToList();

        if (!TryResolveVirtualKey(key, out var keyCode))
        {
            throw new InvalidOperationException($"Nieznany klawisz: {key}");
        }

        foreach (var modifier in modifierKeys)
        {
            SendKeyboardInput(modifier, isKeyUp: false);
        }

        SendKeyboardInput(keyCode, isKeyUp: false);

        return Task.CompletedTask;
    }

    public Task KeyUpAsync(string key, string? modifiers, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var modifierKeys = ResolveModifiers(modifiers).ToList();

        if (!TryResolveVirtualKey(key, out var keyCode))
        {
            throw new InvalidOperationException($"Nieznany klawisz: {key}");
        }

        SendKeyboardInput(keyCode, isKeyUp: true);

        foreach (var modifier in modifierKeys.AsEnumerable().Reverse())
        {
            SendKeyboardInput(modifier, isKeyUp: true);
        }

        return Task.CompletedTask;
    }

    public Task MoveMouseAsync(int x, int y, MouseMoveMode mode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (mode == MouseMoveMode.Absolute)
        {
            SetCursorPos(x, y);
            return Task.CompletedTask;
        }

        if (!GetCursorPos(out var point))
        {
            throw new InvalidOperationException("Nie udało się odczytać pozycji kursora.");
        }

        SetCursorPos(point.X + x, point.Y + y);

        return Task.CompletedTask;
    }

    public async Task ClickMouseAsync(MouseButton button, int? x, int? y, int downUpDelayMilliseconds, CancellationToken cancellationToken = default)
    {
        await MouseDownAsync(button, x, y, cancellationToken);

        if (downUpDelayMilliseconds > 0)
        {
            await Task.Delay(downUpDelayMilliseconds, cancellationToken);
        }

        await MouseUpAsync(button, null, null, cancellationToken);
    }

    public async Task MouseDownAsync(MouseButton button, int? x, int? y, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (x.HasValue && y.HasValue)
        {
            SetCursorPos(x.Value, y.Value);
            await Task.Delay(20, cancellationToken);
        }

        var down = button switch
        {
            MouseButton.Left => MouseEventFLeftDown,
            MouseButton.Right => MouseEventFRightDown,
            MouseButton.Middle => MouseEventFMiddleDown,
            _ => throw new InvalidOperationException($"Nieznany przycisk myszy: {button}")
        };

        SendMouseInput(down);
    }

    public async Task MouseUpAsync(MouseButton button, int? x, int? y, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (x.HasValue && y.HasValue)
        {
            SetCursorPos(x.Value, y.Value);
            await Task.Delay(20, cancellationToken);
        }

        var up = button switch
        {
            MouseButton.Left => MouseEventFLeftUp,
            MouseButton.Right => MouseEventFRightUp,
            MouseButton.Middle => MouseEventFMiddleUp,
            _ => throw new InvalidOperationException($"Nieznany przycisk myszy: {button}")
        };

        SendMouseInput(up);
    }

    private static IEnumerable<ushort> ResolveModifiers(string? modifiers)
    {
        if (string.IsNullOrWhiteSpace(modifiers))
        {
            yield break;
        }

        var parts = modifiers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (!TryResolveVirtualKey(part, out var keyCode))
            {
                throw new InvalidOperationException($"Nieznany modifier: {part}");
            }

            yield return keyCode;
        }
    }

    private static bool TryResolveVirtualKey(string key, out ushort virtualKey)
    {
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var normalized = key.Trim().ToUpperInvariant();

        if (normalized.Length == 1)
        {
            var ch = normalized[0];

            if (ch is >= 'A' and <= 'Z')
            {
                virtualKey = ch;
                return true;
            }

            if (ch is >= '0' and <= '9')
            {
                virtualKey = ch;
                return true;
            }
        }

        if (normalized.StartsWith("F") && int.TryParse(normalized[1..], out var functionNumber) && functionNumber is >= 1 and <= 24)
        {
            virtualKey = (ushort)(0x70 + functionNumber - 1);
            return true;
        }

        var map = new Dictionary<string, ushort>
        {
            ["SPACE"] = 0x20,
            ["ENTER"] = 0x0D,
            ["RETURN"] = 0x0D,
            ["ESC"] = 0x1B,
            ["ESCAPE"] = 0x1B,
            ["TAB"] = 0x09,
            ["BACKSPACE"] = 0x08,
            ["DELETE"] = 0x2E,
            ["DEL"] = 0x2E,
            ["INSERT"] = 0x2D,
            ["INS"] = 0x2D,
            ["HOME"] = 0x24,
            ["END"] = 0x23,
            ["PAGEUP"] = 0x21,
            ["PAGEDOWN"] = 0x22,
            ["UP"] = 0x26,
            ["DOWN"] = 0x28,
            ["LEFT"] = 0x25,
            ["RIGHT"] = 0x27,

            ["CTRL"] = 0x11,
            ["CONTROL"] = 0x11,
            ["SHIFT"] = 0x10,
            ["ALT"] = 0x12,
            ["LCTRL"] = 0xA2,
            ["RCTRL"] = 0xA3,
            ["LSHIFT"] = 0xA0,
            ["RSHIFT"] = 0xA1,
            ["LALT"] = 0xA4,
            ["RALT"] = 0xA5,
            ["LWIN"] = 0x5B,
            ["RWIN"] = 0x5C,

            // OEM / punctuation keys. This fixes recording and replaying commands such as "/warp".
            ["/"] = 0xBF,
            ["SLASH"] = 0xBF,
            ["OEM2"] = 0xBF,
            ["OEM_2"] = 0xBF,

            ["\\"] = 0xDC,
            ["BACKSLASH"] = 0xDC,
            ["OEM5"] = 0xDC,
            ["OEM_5"] = 0xDC,

            [";"] = 0xBA,
            ["SEMICOLON"] = 0xBA,
            ["OEM1"] = 0xBA,
            ["OEM_1"] = 0xBA,

            ["="] = 0xBB,
            ["EQUAL"] = 0xBB,
            ["EQUALS"] = 0xBB,
            ["OEMPLUS"] = 0xBB,
            ["OEM_PLUS"] = 0xBB,

            [","] = 0xBC,
            ["COMMA"] = 0xBC,
            ["OEMCOMMA"] = 0xBC,
            ["OEM_COMMA"] = 0xBC,

            ["-"] = 0xBD,
            ["MINUS"] = 0xBD,
            ["OEMMINUS"] = 0xBD,
            ["OEM_MINUS"] = 0xBD,

            ["."] = 0xBE,
            ["PERIOD"] = 0xBE,
            ["DOT"] = 0xBE,
            ["OEMPERIOD"] = 0xBE,
            ["OEM_PERIOD"] = 0xBE,

            ["`"] = 0xC0,
            ["BACKTICK"] = 0xC0,
            ["GRAVE"] = 0xC0,
            ["OEM3"] = 0xC0,
            ["OEM_3"] = 0xC0,

            ["["] = 0xDB,
            ["LEFTBRACKET"] = 0xDB,
            ["OEM4"] = 0xDB,
            ["OEM_4"] = 0xDB,

            ["]"] = 0xDD,
            ["RIGHTBRACKET"] = 0xDD,
            ["OEM6"] = 0xDD,
            ["OEM_6"] = 0xDD,

            ["'"] = 0xDE,
            ["QUOTE"] = 0xDE,
            ["APOSTROPHE"] = 0xDE,
            ["OEM7"] = 0xDE,
            ["OEM_7"] = 0xDE,

            ["OEM102"] = 0xE2,
            ["OEM_102"] = 0xE2
        };

        return map.TryGetValue(normalized, out virtualKey);
    }

    private static void SendKeyboardInput(ushort virtualKey, bool isKeyUp)
    {
        var input = new Input
        {
            Type = InputKeyboard,
            Union = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    ScanCode = 0,
                    Flags = isKeyUp ? KeyEventFKeyUp : 0,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };

        var result = SendInput(1, new[] { input }, Marshal.SizeOf<Input>());

        if (result == 0)
        {
            throw new InvalidOperationException("SendInput keyboard failed.");
        }
    }

    private static void SendMouseInput(uint flags)
    {
        var input = new Input
        {
            Type = InputMouse,
            Union = new InputUnion
            {
                Mouse = new MouseInput
                {
                    Dx = 0,
                    Dy = 0,
                    MouseData = 0,
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };

        var result = SendInput(1, new[] { input }, Marshal.SizeOf<Input>());

        if (result == 0)
        {
            throw new InvalidOperationException("SendInput mouse failed.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInputStructure);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out Point point);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
