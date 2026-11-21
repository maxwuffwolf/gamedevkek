using System;
using System.Collections.Generic;
using UnityEngine;
#if GAME_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Core
{
    /// <summary>Клавиши, которые использует игра. Физическое расположение (на русской раскладке тоже работает).</summary>
    public enum GameKey
    {
        W, A, S, D, E, B, I, C, Q, R, F, Tab, Space, Escape, Enter, Backquote, LeftShift, LeftAlt,
        F1, F2, F3, F4, F5,
        Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0, Minus, Equals,
        UpArrow, DownArrow,
    }

    /// <summary>
    /// Единая точка чтения ввода. Работает и с новым Input System (по умолчанию в проекте),
    /// и со старым Input Manager, если переключить Active Input Handling.
    /// Ребинды позже делаются здесь, остальной код не меняется.
    /// </summary>
    public static class GameInput
    {
        /// <summary>Пока true (открыта консоль, поле ввода), геймплей игнорирует клавиатуру и мышь.</summary>
        public static bool TextInputActive;

        private static readonly List<Func<bool>> _pointerBlockers = new();

        /// <summary>UI регистрирует проверку «курсор над интерфейсом», чтобы клики не уходили в мир.</summary>
        public static void RegisterPointerBlocker(Func<bool> isBlocking) => _pointerBlockers.Add(isBlocking);
        public static void UnregisterPointerBlocker(Func<bool> isBlocking) => _pointerBlockers.Remove(isBlocking);

        public static bool IsPointerOverUI
        {
            get
            {
                foreach (var b in _pointerBlockers)
                    if (b()) return true;
                return false;
            }
        }

        public static bool GameplayBlocked => TextInputActive;

        /// <summary>WASD → вектор (x = вправо, y = вперёд), нормализован.</summary>
        public static Vector2 Move
        {
            get
            {
                if (GameplayBlocked) return Vector2.zero;
                var v = new Vector2(
                    (Held(GameKey.D) ? 1 : 0) - (Held(GameKey.A) ? 1 : 0),
                    (Held(GameKey.W) ? 1 : 0) - (Held(GameKey.S) ? 1 : 0));
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

#if GAME_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        public static bool LeftHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public static bool LeftDown => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static bool RightDown => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
        public static bool RightHeld => Mouse.current != null && Mouse.current.rightButton.isPressed;
        public static float Scroll => Mouse.current != null ? Mathf.Sign(Mouse.current.scroll.ReadValue().y) * (Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.01f ? 1f : 0f) : 0f;

        public static bool Down(GameKey k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[Map(k)].wasPressedThisFrame;
        }

        public static bool Held(GameKey k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[Map(k)].isPressed;
        }

        private static Key Map(GameKey k) => k switch
        {
            GameKey.W => Key.W, GameKey.A => Key.A, GameKey.S => Key.S, GameKey.D => Key.D,
            GameKey.E => Key.E, GameKey.B => Key.B, GameKey.I => Key.I, GameKey.C => Key.C,
            GameKey.Q => Key.Q, GameKey.R => Key.R, GameKey.F => Key.F,
            GameKey.Tab => Key.Tab, GameKey.Space => Key.Space, GameKey.Escape => Key.Escape,
            GameKey.Enter => Key.Enter, GameKey.Backquote => Key.Backquote,
            GameKey.LeftShift => Key.LeftShift, GameKey.LeftAlt => Key.LeftAlt,
            GameKey.F1 => Key.F1, GameKey.F2 => Key.F2, GameKey.F3 => Key.F3, GameKey.F4 => Key.F4, GameKey.F5 => Key.F5,
            GameKey.Digit1 => Key.Digit1, GameKey.Digit2 => Key.Digit2, GameKey.Digit3 => Key.Digit3,
            GameKey.Digit4 => Key.Digit4, GameKey.Digit5 => Key.Digit5, GameKey.Digit6 => Key.Digit6,
            GameKey.Digit7 => Key.Digit7, GameKey.Digit8 => Key.Digit8, GameKey.Digit9 => Key.Digit9,
            GameKey.Digit0 => Key.Digit0, GameKey.Minus => Key.Minus, GameKey.Equals => Key.Equals,
            GameKey.UpArrow => Key.UpArrow, GameKey.DownArrow => Key.DownArrow,
            _ => Key.None,
        };
#elif ENABLE_LEGACY_INPUT_MANAGER
        public static Vector2 MousePosition => Input.mousePosition;
        public static bool LeftHeld => Input.GetMouseButton(0);
        public static bool LeftDown => Input.GetMouseButtonDown(0);
        public static bool RightDown => Input.GetMouseButtonDown(1);
        public static bool RightHeld => Input.GetMouseButton(1);
        public static float Scroll => Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f ? Mathf.Sign(Input.mouseScrollDelta.y) : 0f;
        public static bool Down(GameKey k) => Input.GetKeyDown(Map(k));
        public static bool Held(GameKey k) => Input.GetKey(Map(k));

        private static KeyCode Map(GameKey k) => k switch
        {
            GameKey.W => KeyCode.W, GameKey.A => KeyCode.A, GameKey.S => KeyCode.S, GameKey.D => KeyCode.D,
            GameKey.E => KeyCode.E, GameKey.B => KeyCode.B, GameKey.I => KeyCode.I, GameKey.C => KeyCode.C,
            GameKey.Q => KeyCode.Q, GameKey.R => KeyCode.R, GameKey.F => KeyCode.F,
            GameKey.Tab => KeyCode.Tab, GameKey.Space => KeyCode.Space, GameKey.Escape => KeyCode.Escape,
            GameKey.Enter => KeyCode.Return, GameKey.Backquote => KeyCode.BackQuote,
            GameKey.LeftShift => KeyCode.LeftShift, GameKey.LeftAlt => KeyCode.LeftAlt,
            GameKey.F1 => KeyCode.F1, GameKey.F2 => KeyCode.F2, GameKey.F3 => KeyCode.F3, GameKey.F4 => KeyCode.F4, GameKey.F5 => KeyCode.F5,
            GameKey.Digit1 => KeyCode.Alpha1, GameKey.Digit2 => KeyCode.Alpha2, GameKey.Digit3 => KeyCode.Alpha3,
            GameKey.Digit4 => KeyCode.Alpha4, GameKey.Digit5 => KeyCode.Alpha5, GameKey.Digit6 => KeyCode.Alpha6,
            GameKey.Digit7 => KeyCode.Alpha7, GameKey.Digit8 => KeyCode.Alpha8, GameKey.Digit9 => KeyCode.Alpha9,
            GameKey.Digit0 => KeyCode.Alpha0, GameKey.Minus => KeyCode.Minus, GameKey.Equals => KeyCode.Equals,
            GameKey.UpArrow => KeyCode.UpArrow, GameKey.DownArrow => KeyCode.DownArrow,
            _ => KeyCode.None,
        };
#else
        public static Vector2 MousePosition => Vector2.zero;
        public static bool LeftHeld => false;
        public static bool LeftDown => false;
        public static bool RightDown => false;
        public static bool RightHeld => false;
        public static float Scroll => 0f;
        public static bool Down(GameKey k) => false;
        public static bool Held(GameKey k) => false;
#endif

        /// <summary>Нажатие клавиши для геймплея (игнорируется, пока открыто поле ввода).</summary>
        public static bool GameplayDown(GameKey k) => !GameplayBlocked && Down(k);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            TextInputActive = false;
            _pointerBlockers.Clear();
        }
    }
}
