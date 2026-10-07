#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Forces Unity to rebuild the board sprite mesh after restoring its Full Rect importer setting.
/// This runs once per Editor session so an already-open Editor cannot keep displaying the stale Tight mesh.
/// </summary>
[InitializeOnLoad]
internal static class BoardSpriteImportRecovery
{
    private const string SpritePath = "Assets/resources/Sprites/board-rounded.png";
    private const string SessionKey = "BlockPuzzel.BoardRoundedSpriteImportRecovery.v1";

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

        if (AssetImporter.GetAtPath(SpritePath) is not TextureImporter importer)
        {
            Debug.LogWarning($"Could not find the board sprite importer at '{SpritePath}'.");
            return;
        }

        TextureImporterSettings settings = new();
        importer.ReadTextureSettings(settings);

        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return;
        }

        // The .meta may already be corrected while this Editor still has the old generated mesh cached.
        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
    }
}
#endif
