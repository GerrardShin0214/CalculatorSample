using UnityEditor;
using UnityEngine;

namespace KitchenDefense.EditorTools
{
    /// <summary>
    /// Resources/PixelFx/ 에 넣은 pixelfx 시트를 픽셀 아트에 맞게 가져온다:
    /// 점 필터(번지지 않게), 무압축, 밉맵 없음, 2의 거듭제곱으로 늘리지 않음(프레임 칸이 어긋나지 않게).
    /// </summary>
    public sealed class PixelFxImport : AssetPostprocessor
    {
        const string Folder = "Assets/KitchenDefense/Resources/PixelFx/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
        }
    }
}
