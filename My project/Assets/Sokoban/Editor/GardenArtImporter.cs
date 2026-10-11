using UnityEditor;

// Rendered Blender tiles remain sharp at board scale without compression fringes.
public sealed class GardenArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Resources/GardenArt/")) return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;
        importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.filterMode=UnityEngine.FilterMode.Bilinear;
        importer.wrapMode=UnityEngine.TextureWrapMode.Clamp;
        importer.maxTextureSize=256;
    }
}
