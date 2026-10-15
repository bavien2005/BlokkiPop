#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Keep the game's runtime font, but do not ship every HDRP/URP demo material as a Resources asset.</summary>
public sealed class PopRuntimeFontSetup : IPreprocessBuildWithReport
{
    public const string RuntimeFontPath = "Assets/_Project/Resources/Fonts & Materials/Bangers SDF.asset";
    private const string FontGuid = "125cb55b44b24c4393181402bc6200e6";
    private const string Samples = "Assets/TextMesh Pro/Examples & Extras/Resources";
    private const string RetainedSamples = "Assets/TextMesh Pro/Examples & Extras/Sample Assets";
    public int callbackOrder => -20000;

    [MenuItem("Tools/Blokki Pop/Build/Prepare Runtime Fonts")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Stop Play/build before preparing runtime fonts.");
        if (AssetDatabase.IsValidFolder(Samples) && AssetDatabase.IsValidFolder(RetainedSamples))
            throw new InvalidOperationException("Both TMP sample folders exist. Review them manually; no sample assets were overwritten.");
        string font = AssetDatabase.GUIDToAssetPath(FontGuid);
        if (string.IsNullOrEmpty(font)) throw new InvalidOperationException("The game's Bangers font is missing.");
        if (font != RuntimeFontPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(RuntimeFontPath) != null)
                throw new InvalidOperationException("A different asset occupies the runtime-font destination. Nothing was overwritten.");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources/Fonts & Materials"))
                AssetDatabase.CreateFolder("Assets/_Project/Resources", "Fonts & Materials");
            MoveChecked(font, RuntimeFontPath);
        }
        if (AssetDatabase.IsValidFolder(Samples)) MoveChecked(Samples, RetainedSamples);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RuntimeFontPath) == null || AssetDatabase.AssetPathToGUID(RuntimeFontPath) != FontGuid)
            throw new InvalidOperationException("Runtime font validation failed after preparation.");
        Debug.Log("[Build Resources] Bangers retained with its original GUID/runtime Resources path; all TMP demo assets retained outside Resources.");
    }

    private static void MoveChecked(string source, string destination)
    {
        string root = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
        string project = Path.GetDirectoryName(Application.dataPath);
        foreach (string path in new[] { source, destination })
            if (!Path.GetFullPath(Path.Combine(project, path)).StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to move an asset outside this project's Assets directory.");
        string error = AssetDatabase.MoveAsset(source, destination);
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        if (AssetDatabase.IsValidFolder(Samples) || AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RuntimeFontPath) == null)
            throw new BuildFailedException("Run Tools > Blokki Pop > Build > Prepare Runtime Fonts before building. TMP samples must not be shipped as runtime Resources.");
    }
}
#endif
