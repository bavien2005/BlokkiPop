#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the board frame and bed as Single/Full Rect sprites and rebuilds their generated meshes.
/// This runs once per Editor session so Unity cannot retain stale Multiple/Tight sprite IDs.
/// </summary>
[InitializeOnLoad]
internal static class BoardSpriteImportRecovery
{
    private const string SessionKey = "BlockPuzzel.BoardSpriteImportRecovery.v3";

    static BoardSpriteImportRecovery()
    {
        EditorApplication.delayCall += ReimportBoardSprite;
    }

    private static void ReimportBoardSprite()
    {
        EditorApplication.delayCall -= ReimportBoardSprite;

        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);

        ConfigureSprite("Assets/_Project/Sprites/board-rounded.png", new Vector4(104, 104, 104, 104));
        ConfigureSprite("Assets/_Project/Sprites/board-bg-rounded.png", new Vector4(40, 40, 40, 40));
    }

    private static void ConfigureSprite(string spritePath, Vector4 border)
    {
        if (AssetImporter.GetAtPath(spritePath) is not TextureImporter importer)
        {
            Debug.LogWarning($"Could not find the board sprite importer at '{spritePath}'.");
            return;
        }

        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);

        bool changed = false;
        if (settings.spriteMode != (int)SpriteImportMode.Single)
        {
            settings.spriteMode = (int)SpriteImportMode.Single;
            changed = true;
        }
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            changed = true;
        }
        if (settings.spriteAlignment != (int)SpriteAlignment.Center || settings.spritePivot != new Vector2(0.5f, 0.5f))
        {
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            changed = true;
        }
        if (settings.spriteBorder != border)
        {
            settings.spriteBorder = border;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            changed = true;
        }

        if (changed)
        {
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return;
        }

        // The .meta may already be corrected while this Editor still has the old generated mesh cached.
        AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);
    }
}
#endif
