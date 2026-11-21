using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// Команда дев-консоли. Вешается на static-метод в любой сборке Game.*:
    /// [DevCommand("give", "Выдать предмет: give &lt;id&gt; [rarity] [ilvl]")]
    /// public static string Give(string id, string rarity = "rare", int ilvl = 1) { ... }
    /// Параметры: string, int, float, bool (on/off), enum. Можно по имени: spawn wolf 5 lvl=3.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DevCommandAttribute : Attribute
    {
        public readonly string Name;
        public readonly string Help;

        public DevCommandAttribute(string name, string help = "")
        {
            Name = name;
            Help = help;
        }
    }

    public static class DevCommands
    {
        private sealed class Command
        {
            public string Name;
            public string Help;
            public MethodInfo Method;
        }

        private static Dictionary<string, Command> _commands;

        private static Dictionary<string, Command> Commands
        {
            get
            {
                if (_commands == null) Build();
                return _commands;
            }
        }

        public static IEnumerable<string> Names => Commands.Keys.OrderBy(k => k);

        public static string Help(string name = null)
        {
            if (!string.IsNullOrEmpty(name))
                return Commands.TryGetValue(name, out var c) ? $"{c.Name}: {c.Help}" : $"Нет команды \"{name}\"";
            var sb = new StringBuilder("Команды (Tab — автодополнение, ↑/↓ — история):\n");
            foreach (var c in Commands.Values.OrderBy(c => c.Name))
                sb.AppendLine($"  <color=#ffd34d>{c.Name}</color>  {c.Help}");
            return sb.ToString().TrimEnd();
        }

        public static string Execute(string line)
        {
            var tokens = Tokenize(line);
            if (tokens.Count == 0) return null;
            if (!Commands.TryGetValue(tokens[0].ToLowerInvariant(), out var cmd))
                return $"<color=#ff6666>Неизвестная команда \"{tokens[0]}\". help — список.</color>";

            var parameters = cmd.Method.GetParameters();
            var args = new object[parameters.Length];
            var positional = new List<string>();
            var named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in tokens.Skip(1))
            {
                int eq = t.IndexOf('=');
                if (eq > 0) named[t.Substring(0, eq)] = t.Substring(eq + 1);
                else positional.Add(t);
            }

            int pos = 0;
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                string raw = null;
                if (named.TryGetValue(p.Name, out var n)) raw = n;
                else if (pos < positional.Count) raw = positional[pos++];

                if (raw == null)
                {
                    if (p.HasDefaultValue) { args[i] = p.DefaultValue; continue; }
                    return $"<color=#ff6666>Не хватает аргумента \"{p.Name}\". {cmd.Help}</color>";
                }
                if (!TryConvert(raw, p.ParameterType, out args[i]))
                    return $"<color=#ff6666>Аргумент \"{p.Name}\": не удалось понять \"{raw}\" как {p.ParameterType.Name}</color>";
            }

            try
            {
                var result = cmd.Method.Invoke(null, args);
                return result as string ?? "ok";
            }
            catch (TargetInvocationException e)
            {
                Debug.LogException(e.InnerException ?? e);
                return $"<color=#ff6666>Ошибка: {(e.InnerException ?? e).Message}</color>";
            }
        }

        /// <summary>Варианты автодополнения для текущей строки.</summary>
        public static List<string> Complete(string line, IEnumerable<string> ids)
        {
            var tokens = Tokenize(line);
            bool newToken = line.EndsWith(" ");
            if (tokens.Count == 0) return Names.ToList();
            if (tokens.Count == 1 && !newToken)
                return Names.Where(n => n.StartsWith(tokens[0], StringComparison.OrdinalIgnoreCase)).ToList();
            string prefix = newToken ? "" : tokens[tokens.Count - 1];
            return ids.Distinct().Where(i => i.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(i => i).ToList();
        }

        private static bool TryConvert(string raw, Type type, out object value)
        {
            value = null;
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null) type = underlying;
            var inv = CultureInfo.InvariantCulture;
            if (type == typeof(string)) { value = raw; return true; }
            if (type == typeof(int) && int.TryParse(raw, NumberStyles.Integer, inv, out var i)) { value = i; return true; }
            if (type == typeof(long) && long.TryParse(raw, NumberStyles.Integer, inv, out var l)) { value = l; return true; }
            if (type == typeof(float) && float.TryParse(raw.Replace(',', '.'), NumberStyles.Float, inv, out var f)) { value = f; return true; }
            if (type == typeof(bool))
            {
                switch (raw.ToLowerInvariant())
                {
                    case "1": case "on": case "true": case "yes": case "вкл": value = true; return true;
                    case "0": case "off": case "false": case "no": case "выкл": value = false; return true;
                }
                return false;
            }
            if (type.IsEnum)
            {
                try
                {
                    value = Enum.Parse(type, raw, true);
                    return true;
                }
                catch { return false; }
            }
            return false;
        }

        private static List<string> Tokenize(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(line)) return result;
            var sb = new StringBuilder();
            bool quoted = false;
            foreach (char c in line.Trim())
            {
                if (c == '"') { quoted = !quoted; continue; }
                if (char.IsWhiteSpace(c) && !quoted)
                {
                    if (sb.Length > 0) { result.Add(sb.ToString()); sb.Clear(); }
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0) result.Add(sb.ToString());
            return result;
        }

        private static void Build()
        {
            _commands = new Dictionary<string, Command>(StringComparer.OrdinalIgnoreCase);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.GetName().Name.StartsWith("Game")) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in types)
                foreach (var m in t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var attr = m.GetCustomAttribute<DevCommandAttribute>();
                    if (attr == null) continue;
                    _commands[attr.Name.ToLowerInvariant()] = new Command { Name = attr.Name, Help = attr.Help, Method = m };
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _commands = null;
    }
}
