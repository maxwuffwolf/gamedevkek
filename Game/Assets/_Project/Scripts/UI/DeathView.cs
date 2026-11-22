using Game.Gameplay.Actors;
using Game.Gameplay.World;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Затемнение экрана и таймер воскрешения.</summary>
    public sealed class DeathView : MonoBehaviour
    {
        private Image _shade;
        private Text _text;

        public static DeathView Create(RectTransform canvas)
        {
            var shade = UiKit.Image(canvas, "Death", new Color(0.06f, 0.03f, 0.06f, 0.6f));
            shade.rectTransform.Stretch();
            var view = shade.gameObject.AddComponent<DeathView>();
            view._shade = shade;
            view._text = UiKit.Text(shade.transform, "Text", "", 44, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold).Shadowed();
            view._text.rectTransform.Stretch();
            return view;
        }

        private void LateUpdate()
        {
            var p = GameWorld.Player;
            bool dead = p != null && p.IsDead;
            _shade.enabled = dead;
            _text.enabled = dead;
            if (!dead) return;
            var pc = p.GetComponent<PlayerCharacter>();
            float t = pc != null ? pc.RespawnRemaining : 0f;
            _text.text = $"Вы погибли\n<size=24>Воскрешение через {Mathf.CeilToInt(t)}…</size>";
        }
    }
}
