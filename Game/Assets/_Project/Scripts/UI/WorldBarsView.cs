using System.Collections.Generic;
using Game.Gameplay.Actors;
using Game.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Полоски здоровья и имена над мобами (имя видно при наведении или после урона).</summary>
    public sealed class WorldBarsView : MonoBehaviour
    {
        private sealed class Bar
        {
            public RectTransform Root;
            public RectTransform Fill;
            public Text Name;
        }

        private readonly Dictionary<Actor, Bar> _bars = new();
        private readonly List<Actor> _remove = new();
        private RectTransform _root;
        private RectTransform _canvas;

        public static WorldBarsView Create(RectTransform canvas)
        {
            var rt = UiKit.Rect("WorldBars", canvas);
            rt.Stretch();
            var view = rt.gameObject.AddComponent<WorldBarsView>();
            view._root = rt;
            view._canvas = canvas;
            return view;
        }

        private Bar CreateBar()
        {
            var root = UiKit.Image(_root, "Bar", new Color(0f, 0f, 0f, 0.55f)).Round(UiTheme.RadiusPill).rectTransform;
            root.sizeDelta = new Vector2(72f, 7f);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            var fill = UiKit.Image(root, "Fill", UiTheme.Enemy).Round(UiTheme.RadiusPill).rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(1f, 1f);
            fill.offsetMax = new Vector2(-1f, -1f);
            var name = UiKit.Text(root, "Name", "", 14, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(220f, 18f));
            return new Bar { Root = root, Fill = fill, Name = name };
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var hovered = GameWorld.PlayerController != null ? GameWorld.PlayerController.HoveredEnemy : null;

            foreach (var a in Actor.All)
            {
                if (a.Team != Team.Monster) continue;
                if (!_bars.TryGetValue(a, out var bar))
                {
                    bar = CreateBar();
                    _bars[a] = bar;
                }
                bool visible = !a.IsDead;
                Vector3 screen = cam.WorldToScreenPoint(a.HeadPosition);
                if (screen.z < 0f) visible = false;
                bar.Root.gameObject.SetActive(visible);
                if (!visible) continue;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out var local);
                bar.Root.anchoredPosition = local;
                bar.Fill.anchorMax = new Vector2(Mathf.Clamp01(a.Health01), 1f);
                bool showName = a == hovered || Time.time - a.LastDamagedTime < 3f;
                bar.Name.text = showName ? $"{a.DisplayName} <color=#b9b5ad>{a.Level}</color>" : "";
            }

            _remove.Clear();
            foreach (var kv in _bars)
                if (kv.Key == null || !Actor.All.Contains(kv.Key)) _remove.Add(kv.Key);
            foreach (var a in _remove)
            {
                Destroy(_bars[a].Root.gameObject);
                _bars.Remove(a);
            }
        }
    }
}
