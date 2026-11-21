using Game.Core;
using Game.Data;
using Game.Gameplay.Actors;
using Game.Gameplay.AI;
using Game.Gameplay.Stats;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>F3: FPS и сводка. Всегда: красная плашка при ошибках в данных, подписи ИИ (ai.debug on).</summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        private bool _show;
        private float _fps;
        private GUIStyle _style;

        private void Update()
        {
            if (GameInput.Down(GameKey.F3) && !GameInput.TextInputActive) _show = !_show;
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
        }

        private void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.label) { richText = true, fontSize = 14 };

            if (Services.TryGet<ContentDatabase>(out var db) && db.Errors.Count > 0)
            {
                GUI.color = new Color(1f, 0.3f, 0.3f, 0.9f);
                GUI.Box(new Rect(10, Screen.height * 0.5f, 360, 26), GUIContent.none);
                GUI.color = Color.white;
                GUI.Label(new Rect(16, Screen.height * 0.5f + 3, 360, 22), $"<b>Ошибок в данных: {db.Errors.Count}</b> — открой консоль (~)", _style);
            }

            if (MonsterBrain.DebugLabels && Camera.main != null)
            {
                foreach (var a in Actor.All)
                {
                    var brain = a.GetComponent<MonsterBrain>();
                    if (brain == null) continue;
                    var p = Camera.main.WorldToScreenPoint(a.HeadPosition + Vector3.up * 0.6f);
                    if (p.z < 0) continue;
                    GUI.Label(new Rect(p.x - 60, Screen.height - p.y - 10, 160, 20), $"<color=#7fd4ff>{brain.Current}</color>", _style);
                }
            }

            if (!_show) return;
            string flags = (CombatCheats.God ? " GOD" : "") + (CombatCheats.OneShot ? " ONESHOT" : "");
            int monsters = 0;
            foreach (var a in Actor.All) if (a.Team == Team.Monster && !a.IsDead) monsters++;
            GUI.Box(new Rect(10, 30, 260, 92), GUIContent.none);
            GUI.Label(new Rect(18, 34, 250, 90),
                $"FPS: {_fps:0}\nМобов: {monsters}\nДанные: v{db?.Version}, ошибок {db?.Errors.Count}\ntimeScale: {Time.timeScale:0.##}<color=#ff6666>{flags}</color>", _style);
        }
    }
}
