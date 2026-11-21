using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>3D-надпись в мире (имя NPC, «!» над головой), всегда повёрнута к камере.</summary>
    public sealed class WorldLabel : MonoBehaviour
    {
        private static Font _font;
        public TextMesh Text { get; private set; }
        public float BobAmplitude;
        private Vector3 _baseLocalPos;

        public static WorldLabel Create(Transform parent, Vector3 localPos, string text, Color color, float size)
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var label = go.AddComponent<WorldLabel>();
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.text = text;
            tm.color = color;
            tm.fontSize = 64;
            tm.characterSize = size / 10f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            label.Text = tm;
            label._baseLocalPos = localPos;
            return label;
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
            if (BobAmplitude > 0f)
                transform.localPosition = _baseLocalPos + Vector3.up * (Mathf.Sin(Time.time * 3f) * BobAmplitude);
        }
    }
}
