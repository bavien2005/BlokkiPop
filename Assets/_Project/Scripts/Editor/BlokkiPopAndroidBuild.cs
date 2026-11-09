#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Repeatable, debug-signed Android APK for native ad QA. Never use this output for Play submission.</summary>
public static class BlokkiPopAndroidBuild
{
    private const string DevelopmentApkPath = "Build/QA/BlokkiPop-Development-QA.apk";

    [MenuItem("Blokki Pop/Build/Build Android Development QA")]
    public static void BuildDevelopmentQa()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes are configured for the Android build.");

        string outputPath = Path.GetFullPath(DevelopmentApkPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException("Invalid build output path."));

        bool originalBundleSetting = EditorUserBuildSettings.buildAppBundle;
        try
        {
            // An APK is intentional here: ADB installs it directly on the QA phone.
            EditorUserBuildSettings.buildAppBundle = false;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Android Development QA build failed: {report.summary.result}.");

            Debug.Log($"[Build] Development QA APK created: {outputPath} ({report.summary.totalSize:N0} bytes).");
        }
        finally
        {
            EditorUserBuildSettings.buildAppBundle = originalBundleSetting;
        }
    }
}
#endif
