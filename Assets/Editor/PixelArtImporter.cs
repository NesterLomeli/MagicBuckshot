using UnityEditor;
using UnityEngine;

// makes every image in Assets/Art import as crisp pixel art (16 pixels per unit, no blur)
// so i don't have to fix the import settings by hand every time
public class PixelArtImporter : AssetPostprocessor
{
    public const int PixelsPerUnit = 16;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Art")) return;
        Apply((TextureImporter)assetImporter);
    }

    public static bool Apply(TextureImporter ti)
    {
        bool changed =
            ti.textureType != TextureImporterType.Sprite ||
            ti.spriteImportMode != SpriteImportMode.Single ||
            !Mathf.Approximately(ti.spritePixelsPerUnit, PixelsPerUnit) ||
            ti.filterMode != FilterMode.Point ||
            ti.textureCompression != TextureImporterCompression.Uncompressed ||
            ti.mipmapEnabled;

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = PixelsPerUnit;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        return changed;
    }
}
