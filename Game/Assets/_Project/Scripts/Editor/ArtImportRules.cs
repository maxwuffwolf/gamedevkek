using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Настройки импорта своего арта выставляются сами (см. docs/10-blender-environment.md):
    /// * палитры (`Art/Textures/*Palette*`) — без мипмапов и сжатия, Clamp: цвета соседних квадратов не смешиваются;
    /// * модели из `Art/Models/` — без камер/света из Blender, без автосоздания материалов (ставим свои M_Env_*).
    /// Срабатывает только при первом импорте файла: ручные правки в инспекторе потом не перетираются.
    /// </summary>
    public class ArtImportRules : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/_Project/Art/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot + "Textures/") || !assetPath.Contains("Palette")) return;
            if (!assetImporter.importSettingsMissing) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ArtRoot + "Models/")) return;
            if (!assetImporter.importSettingsMissing) return;

            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importAnimation = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
        }
    }
}
