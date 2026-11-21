using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Core
{
    /// <summary>
    /// Материалы для плейсхолдеров. Работает и в URP, и во встроенном пайплайне.
    /// Cached* методы переиспользуют материал одного цвета (меньше draw call'ов и мусора).
    /// </summary>
    public static class MaterialFactory
    {
        private static readonly Dictionary<Color, Material> _litCache = new();
        private static readonly Dictionary<Color, Material> _transparentCache = new();

        public static bool IsScriptablePipeline =>
            GraphicsSettings.currentRenderPipeline != null || GraphicsSettings.defaultRenderPipeline != null;

        public static Shader LitShader
        {
            get
            {
                Shader s = IsScriptablePipeline ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
                return s != null ? s : Shader.Find("Sprites/Default");
            }
        }

        public static Shader UnlitShader
        {
            get
            {
                Shader s = IsScriptablePipeline ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Color");
                return s != null ? s : Shader.Find("Sprites/Default");
            }
        }

        /// <summary>Непрозрачный освещаемый материал (новый экземпляр).</summary>
        public static Material CreateLit(Color color, float smoothness = 0.15f)
        {
            var m = new Material(LitShader) { color = color };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        /// <summary>Неосвещаемый непрозрачный материал (огонь, светящиеся детали).</summary>
        public static Material CreateUnlit(Color color) => new Material(UnlitShader) { color = color };

        /// <summary>Прозрачный неосвещаемый материал (лучи лута, VFX, растворение трупов).</summary>
        public static Material CreateTransparent(Color color) => new Material(Shader.Find("Sprites/Default")) { color = color };

        public static Material Lit(Color color)
        {
            if (!_litCache.TryGetValue(color, out var m) || m == null)
            {
                m = CreateLit(color);
                _litCache[color] = m;
            }
            return m;
        }

        public static Material Transparent(Color color)
        {
            if (!_transparentCache.TryGetValue(color, out var m) || m == null)
            {
                m = CreateTransparent(color);
                _transparentCache[color] = m;
            }
            return m;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _litCache.Clear();
            _transparentCache.Clear();
        }
    }
}
