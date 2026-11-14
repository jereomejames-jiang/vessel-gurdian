using UnityEditor;
using UnityEngine;

// 美术组丢进 Assets/Resources 的条带素材统一按像素素材导入。
// 最关键是 npotScale=None：Unity 默认会把非 2 的幂的图缩到 2 的幂，
// 375x64 被压成 256x64 之后 5x75 的切帧直接装不下，而且一声不吭地退回占位图。
public class SpriteSheetImportSettings : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/") || !assetPath.EndsWith(".png")) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.isReadable = true;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
