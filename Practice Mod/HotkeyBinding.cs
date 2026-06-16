using System;
using InputKey = UnityEngine.InputSystem.Key;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ObjImpPracticeMod;

internal enum BindingDevice
{
    Keyboard,
    Mouse
}

internal enum MouseBindingButton
{
    Left,
    Right,
    Middle,
    Back,
    Forward
}

internal readonly struct HotkeyBinding
{
    private static readonly InputKey[] CaptureKeys =
    {
        InputKey.A, InputKey.B, InputKey.C, InputKey.D, InputKey.E, InputKey.F, InputKey.G, InputKey.H, InputKey.I, InputKey.J, InputKey.K, InputKey.L, InputKey.M,
        InputKey.N, InputKey.O, InputKey.P, InputKey.Q, InputKey.R, InputKey.S, InputKey.T, InputKey.U, InputKey.V, InputKey.W, InputKey.X, InputKey.Y, InputKey.Z,
        InputKey.Digit0, InputKey.Digit1, InputKey.Digit2, InputKey.Digit3, InputKey.Digit4, InputKey.Digit5, InputKey.Digit6, InputKey.Digit7, InputKey.Digit8, InputKey.Digit9,
        InputKey.Numpad0, InputKey.Numpad1, InputKey.Numpad2, InputKey.Numpad3, InputKey.Numpad4, InputKey.Numpad5, InputKey.Numpad6, InputKey.Numpad7, InputKey.Numpad8, InputKey.Numpad9,
        InputKey.F1, InputKey.F2, InputKey.F3, InputKey.F4, InputKey.F5, InputKey.F6, InputKey.F7, InputKey.F8, InputKey.F9, InputKey.F10, InputKey.F11, InputKey.F12,
        InputKey.UpArrow, InputKey.DownArrow, InputKey.LeftArrow, InputKey.RightArrow,
        InputKey.Space, InputKey.Tab, InputKey.Enter, InputKey.NumpadEnter, InputKey.Backquote, InputKey.Minus, InputKey.Equals,
        InputKey.LeftBracket, InputKey.RightBracket, InputKey.Backslash, InputKey.Semicolon, InputKey.Quote, InputKey.Comma, InputKey.Period, InputKey.Slash,
        InputKey.Escape, InputKey.Backspace, InputKey.Insert, InputKey.Delete, InputKey.Home, InputKey.End, InputKey.PageUp, InputKey.PageDown,
        InputKey.LeftShift, InputKey.RightShift, InputKey.LeftCtrl, InputKey.RightCtrl, InputKey.LeftAlt, InputKey.RightAlt
    };

    public HotkeyBinding(InputKey key)
    {
        Device = BindingDevice.Keyboard;
        Key = key;
        MouseButton = null;
    }

    public HotkeyBinding(MouseBindingButton mouseButton)
    {
        Device = BindingDevice.Mouse;
        Key = null;
        MouseButton = mouseButton;
    }

    public BindingDevice Device { get; }

    public InputKey? Key { get; }

    public MouseBindingButton? MouseButton { get; }

    public string Serialize()
    {
        return Device switch
        {
            BindingDevice.Keyboard when Key.HasValue => $"Keyboard/{Key.Value}",
            BindingDevice.Mouse when MouseButton.HasValue => $"Mouse/{MouseButton.Value}",
            _ => "Keyboard/F2"
        };
    }

    public string DisplayName()
    {
        return Device switch
        {
            BindingDevice.Keyboard when Key.HasValue => Key.Value.ToString(),
            BindingDevice.Mouse when MouseButton.HasValue => $"Mouse {MouseButton.Value}",
            _ => "Unbound"
        };
    }

    public bool WasPressedThisFrame()
    {
        return Device switch
        {
            BindingDevice.Keyboard when Key.HasValue => Keyboard.current != null && Keyboard.current[Key.Value].wasPressedThisFrame,
            BindingDevice.Mouse when MouseButton.HasValue => GetMouseButtonControl(Mouse.current, MouseButton.Value)?.wasPressedThisFrame == true,
            _ => false
        };
    }

    public static HotkeyBinding Parse(string value, HotkeyBinding fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        string[] parts = value.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return fallback;

        if (parts[0].Equals("Keyboard", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse(parts[1], true, out InputKey key))
        {
            return new HotkeyBinding(key);
        }

        if (parts[0].Equals("Mouse", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse(parts[1], true, out MouseBindingButton mouseButton))
        {
            return new HotkeyBinding(mouseButton);
        }

        return fallback;
    }

    public static bool TryCapture(out HotkeyBinding binding)
    {
        Keyboard? keyboard = Keyboard.current;
        if (keyboard != null)
        {
            for (int i = 0; i < CaptureKeys.Length; i++)
            {
                InputKey key = CaptureKeys[i];
                KeyControl? keyControl = keyboard[key];
                if (keyControl != null && keyControl.wasPressedThisFrame)
                {
                    binding = new HotkeyBinding(key);
                    return true;
                }
            }
        }

        Mouse? mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                binding = new HotkeyBinding(MouseBindingButton.Left);
                return true;
            }
            if (mouse.rightButton.wasPressedThisFrame)
            {
                binding = new HotkeyBinding(MouseBindingButton.Right);
                return true;
            }
            if (mouse.middleButton.wasPressedThisFrame)
            {
                binding = new HotkeyBinding(MouseBindingButton.Middle);
                return true;
            }
            if (mouse.backButton.wasPressedThisFrame)
            {
                binding = new HotkeyBinding(MouseBindingButton.Back);
                return true;
            }
            if (mouse.forwardButton.wasPressedThisFrame)
            {
                binding = new HotkeyBinding(MouseBindingButton.Forward);
                return true;
            }
        }

        binding = default;
        return false;
    }

    private static ButtonControl? GetMouseButtonControl(Mouse? mouse, MouseBindingButton mouseButton)
    {
        if (mouse == null)
            return null;

        return mouseButton switch
        {
            MouseBindingButton.Left => mouse.leftButton,
            MouseBindingButton.Right => mouse.rightButton,
            MouseBindingButton.Middle => mouse.middleButton,
            MouseBindingButton.Back => mouse.backButton,
            MouseBindingButton.Forward => mouse.forwardButton,
            _ => null
        };
    }
}
