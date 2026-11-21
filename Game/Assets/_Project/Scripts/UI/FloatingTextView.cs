using System.Collections.Generic;
using Game.Core;
using Game.Gameplay;
using Game.Gameplay.Actors;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Всплывающие цифры урона (криты крупнее и жёлтые, урон по игроку красный).</summary>
    public sealed class FloatingTextView : MonoBehaviour
    {
        private sealed class Entry
        {
            public Text Text;
            public Vector3 World;
            public float Born;
            public Vector2 Drift;
        }

        private const float Life = 0.9f;
        private readonly List<Entry> _active = new();
        private readonly Stack<Text> _pool = new();
        private RectTransform _root;
        private RectTransform _canvas;

        public static FloatingTextView Create(RectTransform canvas)
        {
            var rt = UiKit.Rect("FloatingText", canvas);
            rt.Stretch();
            var view = rt.gameObject.AddComponent<FloatingTextView>();
            view._root = rt;
            view._canvas = canvas;
            EventBus.Subscribe<DamageDealtEvent>(view.OnDamage);
            return view;
        }

        private void OnDestroy() => EventBus.Unsubscribe<DamageDealtEvent>(OnDamage);

        private void OnDamage(DamageDealtEvent e)
        {
            if (e.Amount <= 0f || e.Target == null) return;
            bool toPlayer = e.Target.Team == Team.Player;
            var color = toPlayer ? new Color(1f, 0.35f, 0.3f) : e.Crit ? new Color(1f, 0.85f, 0.2f) : Color.white;
            int size = e.Crit ? 34 : 24;
            var t = _pool.Count > 0 ? _pool.Pop() : UiKit.Text(_root, "Dmg", "", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            t.gameObject.SetActive(true);
            t.text = e.Crit ? $"{e.Amount:0}!" : $"{e.Amount:0}";
            t.color = color;
            t.fontSize = size;
            t.rectTransform.sizeDelta = new Vector2(160f, 40f);
            _active.Add(new Entry
            {
                Text = t,
                World = e.Position,
                Born = Time.time,
                Drift = new Vector2(Random.Range(-30f, 30f), 0f),
            });
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var e = _active[i];
                float k = (Time.time - e.Born) / Life;
                if (k >= 1f)
                {
                    e.Text.gameObject.SetActive(false);
                    _pool.Push(e.Text);
                    _active.RemoveAt(i);
                    continue;
                }
                Vector3 screen = cam.WorldToScreenPoint(e.World);
                if (screen.z < 0f)
                {
                    e.Text.enabled = false;
                    continue;
                }
                e.Text.enabled = true;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out var local);
                e.Text.rectTransform.anchoredPosition = local + e.Drift * k + new Vector2(0f, 60f * k);
                var c = e.Text.color;
                c.a = 1f - k * k;
                e.Text.color = c;
                e.Text.transform.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(1.4f, 1f, k / 0.15f) : 1f);
            }
        }
    }
}
