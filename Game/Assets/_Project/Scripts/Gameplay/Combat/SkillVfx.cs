using Game.Core;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>Простые процедурные эффекты: веер удара на земле.</summary>
    public static class SkillVfx
    {
        public static void Swing(Vector3 origin, Vector3 forward, float radius, float angle, Color color)
        {
            var go = new GameObject("VFX_Swing");
            go.transform.position = origin;
            go.transform.rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward : Vector3.forward);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildFan(radius, angle, Mathf.Max(6, Mathf.CeilToInt(angle / 10f)));
            color.a = 0.45f;
            mr.sharedMaterial = MaterialFactory.CreateTransparent(color);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<FadeAndDie>().Init(0.22f, mr.sharedMaterial, mf.sharedMesh);
        }

        public static Mesh BuildFan(float radius, float angle, int segments)
        {
            var mesh = new Mesh { name = "Fan" };
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            float start = -angle * 0.5f;
            for (int i = 0; i <= segments; i++)
            {
                float a = (start + angle * i / segments) * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
            }
            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
