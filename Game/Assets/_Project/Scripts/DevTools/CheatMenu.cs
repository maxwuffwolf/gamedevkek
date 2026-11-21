using System.Linq;
using Game.Core;
using Game.Data;
using Game.Gameplay.Stats;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>F1: панель с кнопками для частых проверок (всё это — обёртки над командами консоли).</summary>
    public sealed class CheatMenu : MonoBehaviour
    {
        private bool _open;
        private Rect _window = new(20, 60, 380, 640);
        private Vector2 _itemScroll;
        private string _itemFilter = "";
        private int _rarityIndex = 2;
        private int _monsterIndex;
        private int _spawnCount = 3;
        private int _spawnLevel = 1;
        private string _last = "";

        private void Awake() => GameInput.RegisterPointerBlocker(IsPointerOver);
        private void OnDestroy() => GameInput.UnregisterPointerBlocker(IsPointerOver);

        private bool IsPointerOver()
        {
            if (!_open) return false;
            var m = GameInput.MousePosition;
            return _window.Contains(new Vector2(m.x, Screen.height - m.y));
        }

        private void Update()
        {
            if (GameInput.Down(GameKey.F1) && !GameInput.TextInputActive) _open = !_open;
        }

        private void Run(string command) => _last = DevCommands.Execute(command) ?? "";

        private void OnGUI()
        {
            if (!_open) return;
            _window = GUILayout.Window(91234, _window, DrawWindow, "Читы (F1)");
        }

        private void DrawWindow(int id)
        {
            if (!Services.TryGet<ContentDatabase>(out var db)) return;

            GUILayout.BeginHorizontal();
            CombatCheats.God = GUILayout.Toggle(CombatCheats.God, " God");
            CombatCheats.OneShot = GUILayout.Toggle(CombatCheats.OneShot, " One-shot");
            Gameplay.AI.MonsterBrain.DebugLabels = GUILayout.Toggle(Gameplay.AI.MonsterBrain.DebugLabels, " ИИ");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Лечить")) Run("heal");
            if (GUILayout.Button("Убить всех")) Run("killall");
            if (GUILayout.Button("+1 уровень")) Run($"level {Services.Get<Gameplay.Progression.PlayerProfile>().Level + 1}");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Лут x8")) Run("give.random 8");
            if (GUILayout.Button("+8 ячеек")) Run("inv.unlock 8");
            if (GUILayout.Button("F5 данные")) Run("data.reload");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Квест: цели")) Run("quest.complete q_wolves");
            if (GUILayout.Button("Сброс квестов")) Run("quest.reset");
            if (GUILayout.Button("Новая игра")) Run("save.wipe");
            GUILayout.EndHorizontal();

            GUILayout.Label($"Скорость времени: {Time.timeScale:0.0}");
            Time.timeScale = GUILayout.HorizontalSlider(Time.timeScale, 0.1f, 3f);

            // --- спавн мобов
            GUILayout.Space(6);
            GUILayout.Label("<b>Спавн у курсора</b>", new GUIStyle(GUI.skin.label) { richText = true });
            var monsters = db.Monsters.Keys.OrderBy(k => k).ToArray();
            if (monsters.Length > 0)
            {
                _monsterIndex = Mathf.Clamp(_monsterIndex, 0, monsters.Length - 1);
                _monsterIndex = GUILayout.SelectionGrid(_monsterIndex, monsters, 3);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Кол-во {_spawnCount}", GUILayout.Width(80));
                _spawnCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(_spawnCount, 1, 15));
                GUILayout.Label($"Ур. {_spawnLevel}", GUILayout.Width(50));
                _spawnLevel = Mathf.RoundToInt(GUILayout.HorizontalSlider(_spawnLevel, 1, 30));
                GUILayout.EndHorizontal();
                if (GUILayout.Button("Заспавнить")) Run($"spawn {monsters[_monsterIndex]} {_spawnCount} {_spawnLevel}");
            }

            // --- выдача предметов
            GUILayout.Space(6);
            GUILayout.Label("<b>Выдать предмет</b>", new GUIStyle(GUI.skin.label) { richText = true });
            var rarities = db.RaritiesOrdered.Select(r => r.Id).ToArray();
            if (rarities.Length > 0)
            {
                _rarityIndex = Mathf.Clamp(_rarityIndex, 0, rarities.Length - 1);
                _rarityIndex = GUILayout.SelectionGrid(_rarityIndex, rarities, rarities.Length);
            }
            _itemFilter = GUILayout.TextField(_itemFilter);
            _itemScroll = GUILayout.BeginScrollView(_itemScroll, GUILayout.Height(160));
            foreach (var item in db.Items.Values.OrderBy(i => i.Id))
            {
                if (_itemFilter.Length > 0 && !item.Id.Contains(_itemFilter) && !(item.Name ?? "").ToLowerInvariant().Contains(_itemFilter.ToLowerInvariant())) continue;
                if (GUILayout.Button($"{item.Name}  ({item.Id})") && rarities.Length > 0) Run($"give {item.Id} {rarities[_rarityIndex]}");
            }
            GUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_last))
                GUILayout.Label(_last, new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
    }
}
