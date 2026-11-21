using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Gameplay.Actors
{
    /// <summary>
    /// Модели-заглушки из примитивов, пока нет настоящих (KayKit/Quaternius/свои из Blender).
    /// Чтобы заменить: создай ActorVisual с префабом и тем же id, что в поле "prefab" в JSON.
    /// </summary>
    public static class PlaceholderModels
    {
        public static GameObject Build(PlaceholderDef def, Transform parent)
        {
            def ??= new PlaceholderDef();
            var root = new GameObject("Model");
            root.transform.SetParent(parent, false);
            var rig = root.AddComponent<PlaceholderRig>();
            Color main = ColorUtil.Parse(def.Color, Color.gray);
            Color accent = ColorUtil.Parse(def.Accent, Color.gray * 0.6f);

            switch ((def.Model ?? "").ToLowerInvariant())
            {
                case "humanoid": BuildHumanoid(rig, main, accent, weapon: true); break;
                case "npc": BuildHumanoid(rig, main, accent, weapon: false); break;
                case "wolf": BuildWolf(rig, main, accent); break;
                default: BuildBlob(rig, main); break;
            }
            root.transform.localScale = Vector3.one * Mathf.Max(0.1f, def.Scale);
            return root;
        }

        private static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Lit(color);
            return go.transform;
        }

        private static Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        private static void BuildHumanoid(PlaceholderRig rig, Color main, Color accent, bool weapon)
        {
            var t = rig.transform;
            var body = Pivot("Body", t, Vector3.zero);
            rig.Body = body;
            Part(PrimitiveType.Capsule, body, new Vector3(0f, 0.85f, 0f), new Vector3(0.7f, 0.55f, 0.55f), main);
            Part(PrimitiveType.Cube, body, new Vector3(0f, 0.72f, 0f), new Vector3(0.74f, 0.12f, 0.58f), accent); // пояс
            var head = Part(PrimitiveType.Sphere, body, new Vector3(0f, 1.55f, 0f), Vector3.one * 0.5f, new Color(0.95f, 0.8f, 0.65f));
            rig.Head = head;
            Part(PrimitiveType.Sphere, head, new Vector3(-0.2f, 0.08f, 0.42f), Vector3.one * 0.18f, Color.black);
            Part(PrimitiveType.Sphere, head, new Vector3(0.2f, 0.08f, 0.42f), Vector3.one * 0.18f, Color.black);

            var legL = Pivot("LegL", t, new Vector3(-0.16f, 0.45f, 0f));
            var legR = Pivot("LegR", t, new Vector3(0.16f, 0.45f, 0f));
            Part(PrimitiveType.Cube, legL, new Vector3(0f, -0.22f, 0f), new Vector3(0.2f, 0.45f, 0.22f), accent);
            Part(PrimitiveType.Cube, legR, new Vector3(0f, -0.22f, 0f), new Vector3(0.2f, 0.45f, 0.22f), accent);
            rig.Legs = new[] { legL, legR };

            var armL = Pivot("ArmL", body, new Vector3(-0.45f, 1.15f, 0f));
            Part(PrimitiveType.Cube, armL, new Vector3(0f, -0.25f, 0f), new Vector3(0.16f, 0.5f, 0.16f), main * 0.85f);

            var weaponPivot = Pivot("WeaponPivot", body, new Vector3(0.45f, 1.1f, 0.1f));
            rig.WeaponPivot = weaponPivot;
            Part(PrimitiveType.Cube, weaponPivot, new Vector3(0f, -0.2f, 0f), new Vector3(0.16f, 0.45f, 0.16f), main * 0.85f);
            if (weapon)
            {
                Part(PrimitiveType.Cube, weaponPivot, new Vector3(0f, -0.4f, 0.45f), new Vector3(0.07f, 0.07f, 0.95f), new Color(0.75f, 0.78f, 0.82f));
                Part(PrimitiveType.Cube, weaponPivot, new Vector3(0f, -0.4f, 0.0f), new Vector3(0.28f, 0.08f, 0.08f), new Color(0.45f, 0.32f, 0.2f));
            }
            else
            {
                // посох
                Part(PrimitiveType.Cylinder, weaponPivot, new Vector3(0f, -0.15f, 0.1f), new Vector3(0.07f, 0.9f, 0.07f), new Color(0.45f, 0.3f, 0.18f));
                Part(PrimitiveType.Sphere, weaponPivot, new Vector3(0f, 0.8f, 0.1f), Vector3.one * 0.2f, new Color(0.5f, 0.9f, 1f));
                // шляпа
                Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.05f, 1.3f), accent);
                Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 0.3f, 0.7f), accent);
            }
            rig.WeaponRest = weaponPivot.localRotation;
        }

        private static void BuildWolf(PlaceholderRig rig, Color main, Color accent)
        {
            var t = rig.transform;
            var body = Pivot("Body", t, Vector3.zero);
            rig.Body = body;
            Part(PrimitiveType.Cube, body, new Vector3(0f, 0.62f, -0.05f), new Vector3(0.5f, 0.45f, 1.1f), main);
            Part(PrimitiveType.Cube, body, new Vector3(0f, 0.68f, 0.35f), new Vector3(0.56f, 0.5f, 0.45f), accent); // грива
            var head = Pivot("Head", body, new Vector3(0f, 0.85f, 0.65f));
            rig.Head = head;
            Part(PrimitiveType.Cube, head, Vector3.zero, new Vector3(0.38f, 0.36f, 0.4f), main);
            Part(PrimitiveType.Cube, head, new Vector3(0f, -0.07f, 0.3f), new Vector3(0.22f, 0.18f, 0.3f), accent);
            Part(PrimitiveType.Cube, head, new Vector3(0f, -0.04f, 0.46f), new Vector3(0.1f, 0.08f, 0.06f), Color.black); // нос
            Part(PrimitiveType.Cube, head, new Vector3(-0.12f, 0.24f, -0.05f), new Vector3(0.1f, 0.16f, 0.06f), main, new Vector3(0f, 0f, 10f));
            Part(PrimitiveType.Cube, head, new Vector3(0.12f, 0.24f, -0.05f), new Vector3(0.1f, 0.16f, 0.06f), main, new Vector3(0f, 0f, -10f));
            Part(PrimitiveType.Cube, head, new Vector3(-0.11f, 0.06f, 0.2f), new Vector3(0.07f, 0.05f, 0.02f), new Color(1f, 0.85f, 0.2f));
            Part(PrimitiveType.Cube, head, new Vector3(0.11f, 0.06f, 0.2f), new Vector3(0.07f, 0.05f, 0.02f), new Color(1f, 0.85f, 0.2f));
            Part(PrimitiveType.Cube, body, new Vector3(0f, 0.78f, -0.75f), new Vector3(0.12f, 0.12f, 0.5f), accent, new Vector3(-25f, 0f, 0f)); // хвост

            var legs = new Transform[4];
            int i = 0;
            foreach (var z in new[] { 0.35f, -0.4f })
            foreach (var x in new[] { -0.17f, 0.17f })
            {
                var leg = Pivot("Leg", t, new Vector3(x, 0.45f, z));
                Part(PrimitiveType.Cube, leg, new Vector3(0f, -0.22f, 0f), new Vector3(0.13f, 0.45f, 0.13f), main * 0.85f);
                legs[i++] = leg;
            }
            rig.Legs = legs;
        }

        private static void BuildBlob(PlaceholderRig rig, Color main)
        {
            rig.Body = Pivot("Body", rig.transform, Vector3.zero);
            Part(PrimitiveType.Sphere, rig.Body, new Vector3(0f, 0.5f, 0f), Vector3.one, main);
            Part(PrimitiveType.Sphere, rig.Body, new Vector3(-0.2f, 0.7f, 0.42f), Vector3.one * 0.15f, Color.black);
            Part(PrimitiveType.Sphere, rig.Body, new Vector3(0.2f, 0.7f, 0.42f), Vector3.one * 0.15f, Color.black);
        }
    }
}
