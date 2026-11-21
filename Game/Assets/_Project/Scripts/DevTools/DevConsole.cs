using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// Дев-консоль на ` / ~ / Ё. Показывает лог Unity (с цветом по типу), выполняет команды [DevCommand].
    /// Сделана на IMGUI: ноль настроек, работает при любом Input Handling.
    /// </summary>
    public sealed class DevConsole : MonoBehaviour
    {
        public static DevConsole Instance { get; private set; }

        private const int MaxLines = 400;
        private const string InputControl = "DevConsoleInput";

        private readonly List<string> _lines = new();
        private readonly List<string> _history = new();
        private int _historyIndex = -1;
        private string _input = "";
        private bool _open;
        private bool _focusInput;
        private Vector2 _scroll;
        private bool _scrollToBottom;
        private GUIStyle _style;
        private List<string> _completions = new();
        private int _toggleFrame = -1;

        public bool IsOpen => _open;

        private void Awake()
        {
            Instance = this;
            Application.logMessageReceived += OnLog;
            GameInput.RegisterPointerBlocker(IsPointerOverConsole);
            Print("<color=#ffd34d>Дев-консоль.</color> help — список команд. Закрыть: ` или Esc.");
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            GameInput.UnregisterPointerBlocker(IsPointerOverConsole);
            if (Instance == this) Instance = null;
            GameInput.TextInputActive = false;
        }

        private Rect ConsoleRect => new(0, 0, Screen.width, Screen.height * 0.45f);

        private bool IsPointerOverConsole()
        {
            if (!_open) return false;
            var m = GameInput.MousePosition;
            return ConsoleRect.Contains(new Vector2(m.x, Screen.height - m.y));
        }

        public void Print(string line)
        {
            _lines.Add(line);
            if (_lines.Count > MaxLines) _lines.RemoveRange(0, _lines.Count - MaxLines);
            _scrollToBottom = true;
        }

        public void Clear() => _lines.Clear();

        public void Toggle(bool? open = null)
        {
            _open = open ?? !_open;
            GameInput.TextInputActive = _open;
            _focusInput = _open;
            _completions.Clear();
        }

        private void OnLog(string message, string stack, LogType type)
        {
            string color = type switch
            {
                LogType.Error or LogType.Exception or LogType.Assert => "#ff6666",
                LogType.Warning => "#ffcc55",
                _ => "#c8c8c8",
            };
            Print($"<color={color}>{Escape(message)}</color>");
        }

        private static string Escape(string s) => s?.Replace("<", "‹").Replace(">", "›") ?? "";

        private void Submit()
        {
            string line = _input.Trim();
            _input = "";
            _completions.Clear();
            if (line.Length == 0) return;
            _history.Remove(line);
            _history.Add(line);
            _historyIndex = -1;
            Print($"<color=#88ccff>> {Escape(line)}</color>");
            string result = DevCommands.Execute(line);
            if (!string.IsNullOrEmpty(result)) Print(result);
        }

        private void Autocomplete()
        {
            IEnumerable<string> ids = Services.TryGet<ContentDatabase>(out var db) ? db.AllIds() : Enumerable.Empty<string>();
            _completions = DevCommands.Complete(_input, ids);
            if (_completions.Count == 0) return;
            string common = _completions[0];
            foreach (var c in _completions)
            {
                int n = 0;
                while (n < common.Length && n < c.Length && char.ToLowerInvariant(common[n]) == char.ToLowerInvariant(c[n])) n++;
                common = common.Substring(0, n);
            }
            int lastSpace = _input.LastIndexOf(' ');
            string head = lastSpace >= 0 ? _input.Substring(0, lastSpace + 1) : "";
            string completed = _completions.Count == 1 ? _completions[0] + " " : common;
            if (completed.Length >= _input.Length - head.Length) _input = head + completed;
            _focusInput = true;
        }

        private static bool IsToggleChar(char c) => c == '`' || c == '~' || c == 'ё' || c == 'Ё';

        private void OnGUI()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                // Клавиша ` (на русской раскладке Ё) приходит двумя событиями: keyCode и символ.
                // Переключаем один раз за кадр, оба события съедаем, чтобы символ не попал в поле ввода.
                if (e.keyCode == KeyCode.BackQuote || IsToggleChar(e.character))
                {
                    if (Time.frameCount != _toggleFrame)
                    {
                        _toggleFrame = Time.frameCount;
                        Toggle();
                    }
                    e.Use();
                    return;
                }
            }
            if (!_open) return;

            if (e.type == EventType.KeyDown)
            {
                switch (e.keyCode)
                {
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        Submit();
                        e.Use();
                        break;
                    case KeyCode.Escape:
                        Toggle(false);
                        e.Use();
                        return;
                    case KeyCode.Tab:
                        Autocomplete();
                        e.Use();
                        break;
                    case KeyCode.UpArrow:
                        if (_history.Count > 0)
                        {
                            _historyIndex = _historyIndex < 0 ? _history.Count - 1 : Mathf.Max(0, _historyIndex - 1);
                            _input = _history[_historyIndex];
                        }
                        e.Use();
                        break;
                    case KeyCode.DownArrow:
                        if (_historyIndex >= 0)
                        {
                            _historyIndex++;
                            if (_historyIndex >= _history.Count) { _historyIndex = -1; _input = ""; }
                            else _input = _history[_historyIndex];
                        }
                        e.Use();
                        break;
                }
            }

            _style ??= new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 14 };
            var rect = ConsoleRect;
            GUI.color = new Color(1f, 1f, 1f, 0.95f);
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, GUIContent.none);
            GUI.color = Color.white;

            var logRect = new Rect(8, 8, rect.width - 16, rect.height - 48);
            GUILayout.BeginArea(logRect);
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var l in _lines) GUILayout.Label(l, _style);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (_scrollToBottom && e.type == EventType.Repaint)
            {
                _scroll.y = float.MaxValue;
                _scrollToBottom = false;
            }

            if (_completions.Count > 1)
            {
                string hint = string.Join("  ", _completions.Take(12)) + (_completions.Count > 12 ? "  …" : "");
                GUI.Label(new Rect(8, rect.height - 58, rect.width - 16, 20), $"<color=#999999>{hint}</color>", _style);
            }

            GUI.SetNextControlName(InputControl);
            _input = GUI.TextField(new Rect(8, rect.height - 34, rect.width - 16, 26), _input);
            if (_focusInput)
            {
                GUI.FocusControl(InputControl);
                var editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                editor.MoveTextEnd();
                _focusInput = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
