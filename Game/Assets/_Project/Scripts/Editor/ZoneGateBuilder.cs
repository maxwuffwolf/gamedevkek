using Game.Gameplay.World;
using Game.Gameplay.Zones;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Собирает на сцене «врата» между зонами: каменную арку со светящейся пеленой + ZonePortal + ZoneEntrance.
    /// Портал смотрит «лицом» (ось Z) туда, где стоит точка прибытия: игрок, пришедший из другой зоны,
    /// появляется в 3.5 м перед аркой и смотрит от неё.
    /// Позже арку можно заменить своей моделью из Blender: ZonePortal и ZoneEntrance просто остаются на объекте.
    /// </summary>
    public static class ZoneGateBuilder
    {
        public enum Style
        {
            /// <summary>Холодная пелена: выход из оврага наверх, в лес.</summary>
            Ascent,
            /// <summary>Тёплая, фиолетово-золотая: спуск в овраг, где упала «звезда».</summary>
            Descent,
        }

        public static GameObject Build(Transform parent, Vector3 position, float yaw, string targetZone, string targetEntrance,
            string localEntranceId, Style style)
        {
            Color glow = style == Style.Ascent ? new Color(0.45f, 0.75f, 1.6f) : new Color(1.4f, 0.75f, 1.9f);
            Color core = style == Style.Ascent ? new Color(1.2f, 1.6f, 2.2f) : new Color(2.2f, 1.6f, 0.9f);
            Color stone = style == Style.Ascent ? new Color(0.42f, 0.42f, 0.47f) : new Color(0.36f, 0.33f, 0.38f);
            string suffix = style.ToString();

            var root = new GameObject($"Portal_to_{targetZone}");
            root.transform.SetParent(parent);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var stoneMat = ShowcaseSceneBuilder.Mat("GateStone_" + suffix, stone);
            Part(PrimitiveType.Cube, root.transform, "Pillar_L", new Vector3(-1.95f, 2.1f, 0f), new Vector3(0.9f, 4.2f, 0.9f), stoneMat, true, new Vector3(0f, 8f, -3f));
            Part(PrimitiveType.Cube, root.transform, "Pillar_R", new Vector3(1.95f, 2.1f, 0f), new Vector3(0.9f, 4.2f, 0.9f), stoneMat, true, new Vector3(0f, -6f, 3f));
            Part(PrimitiveType.Cube, root.transform, "Lintel", new Vector3(0f, 4.45f, 0f), new Vector3(5.2f, 0.8f, 1.1f), stoneMat, true, new Vector3(0f, 0f, 2f));
            Part(PrimitiveType.Cube, root.transform, "Keystone", new Vector3(0f, 5.0f, 0f), new Vector3(0.7f, 0.7f, 0.7f),
                ShowcaseSceneBuilder.UnlitMat("GateRune_" + suffix, core * 0.9f), false, new Vector3(0f, 0f, 45f));
            Part(PrimitiveType.Cylinder, root.transform, "Base", new Vector3(0f, 0.02f, 0.4f), new Vector3(6.4f, 0.02f, 4f),
                ShowcaseSceneBuilder.Mat("GateBase_" + suffix, stone * 0.7f), false, Vector3.zero);

            // пелена: толстая «плёнка» видна с обеих сторон (камера смотрит с юга, арка может стоять к ней спиной)
            Part(PrimitiveType.Cube, root.transform, "Veil", new Vector3(0f, 1.95f, 0f), new Vector3(3.0f, 3.7f, 0.06f),
                ShowcaseSceneBuilder.UnlitMat("GateVeil_" + suffix, glow), false, Vector3.zero);
            Part(PrimitiveType.Cube, root.transform, "VeilCore", new Vector3(0f, 1.8f, 0f), new Vector3(1.7f, 2.6f, 0.1f),
                ShowcaseSceneBuilder.UnlitMat("GateCore_" + suffix, core), false, Vector3.zero);

            var lightGo = new GameObject("GateLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.2f, 1.2f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(glow.r, glow.g, glow.b) / Mathf.Max(glow.r, glow.g, glow.b);
            light.range = 10f;
            light.intensity = 3.5f;
            light.shadows = LightShadows.None;
            var flicker = lightGo.AddComponent<LightFlicker>();
            flicker.BaseIntensity = 3.5f;
            flicker.Amount = 0.5f;
            flicker.Speed = 2f;

            // клик-зона: триггер (физику не блокирует, но луч мыши его ловит)
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 2.1f, 0f);
            box.size = new Vector3(3.6f, 4.2f, 1.6f);

            var portal = root.AddComponent<ZonePortal>();
            portal.TargetZone = targetZone;
            portal.TargetEntrance = targetEntrance;
            portal.ActionLabel = "Перейти";
            portal.Range = 3.2f;
            portal.LabelHeight = 5.9f;

            if (!string.IsNullOrEmpty(localEntranceId))
            {
                var entrance = new GameObject($"Entrance_{localEntranceId}", typeof(ZoneEntrance));
                entrance.transform.SetParent(root.transform, false);
                entrance.transform.localPosition = new Vector3(0f, 0f, 3.5f);
                entrance.transform.localRotation = Quaternion.identity;
                entrance.GetComponent<ZoneEntrance>().Id = localEntranceId;
            }
            return root;
        }

        private static GameObject Part(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat,
            bool keepCollider, Vector3 euler)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
    }
}
