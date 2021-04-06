using UnityEngine;

namespace UnityEditor.DeviceSimulation
{
    internal class OverlayPostProcessor : AssetPostprocessor
    {
        internal void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Packages/com.unity.device-simulator/Editor/SimulatorResources/DeviceAssets/DeviceOverlays"))
                return;

            TextureImporter textureImporter = assetImporter as TextureImporter;

            textureImporter.textureType = TextureImporterType.GUI;
            textureImporter.npotScale = TextureImporterNPOTScale.None;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.filterMode = FilterMode.Trilinear;
            textureImporter.maxTextureSize = 8192;
            textureImporter.isReadable = true;
            textureImporter.mipmapEnabled = false;
        }
    }
}
