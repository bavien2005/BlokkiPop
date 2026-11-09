using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class PopGameplaySetup
{
    static PopGameplaySetup() { EditorApplication.delayCall += EnsureConfiguration; }

    public static void EnsureConfiguration()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += EnsureConfiguration; return; }
        if (AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath) != null) return;
        const string folder = "Assets/_Project/Resources/Gameplay";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Project/Resources", "Gameplay");
        PopGameplayConfiguration config = PopGameplayConfiguration.CreateDefaults();
        config.ui.bodyFont = TMP_Settings.defaultFontAsset;
        config.ui.headingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PopRuntimeFontSetup.RuntimeFontPath) ?? config.ui.bodyFont;
        if (!Validate(config, out string reason)) { UnityEngine.Object.DestroyImmediate(config); throw new InvalidOperationException(reason); }
        AssetDatabase.CreateAsset(config, PopGameplayConfiguration.AssetPath); AssetDatabase.SaveAssets();
        Debug.Log("[Gameplay] Created Inspector configuration: 20 verified Journey levels, Daily bank, lesson and themes.");
    }

    [MenuItem("Tools/Blokki Pop/Gameplay/Select Configuration")]
    public static void SelectConfiguration()
    { EnsureConfiguration(); Selection.activeObject = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath); }
    [MenuItem("Tools/Blokki Pop/Gameplay/Validate All Levels")]
    public static void ValidateMenu()
    {
        EnsureConfiguration(); PopGameplayConfiguration config = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
        if (Validate(config, out string reason)) Debug.Log("[Gameplay] " + reason, config); else Debug.LogError("[Gameplay] " + reason, config);
    }

    public static bool Validate(PopGameplayConfiguration config, out string reason)
    {
        reason = "Missing configuration.";
        if (config == null || config.contentRevision < 1 || config.runs == null || config.ui == null || config.text == null ||
            config.daily == null || config.endless == null || config.tutorial == null || config.authoredScoring == null || config.journeyLevels == null ||
            config.themes == null || config.themes.Count == 0 || config.popEffects == null || config.popEffects.Count == 0 || config.journeyLevels.Count == 0) return false;
        var ids = new HashSet<string>();
        BlockColorPalette palette = AssetDatabase.LoadAssetAtPath<BlockColorPalette>("Assets/_Project/ScriptableObjects/BlockColorPalette.asset");
        foreach (PopLevelDefinition level in config.journeyLevels)
        {
            if (level == null || !ids.Add(level.id)) { reason = "Duplicate/missing Journey identity."; return false; }
            if (!PopLevelValidation.Validate(level, config.authoredScoring, out reason)) { reason = level.id + ": " + reason; return false; }
            foreach (PopStartingCell cell in level.startingCells)
                if (palette == null || cell.colorId >= palette.Count) { reason = level.id + ": color index not in BlockColorPalette."; return false; }
        }
        if (!PopLevelValidation.Validate(config.tutorial.lesson, config.authoredScoring, out reason)) { reason = "Tutorial: " + reason; return false; }
        if (!PopLevelValidation.Validate(config.tutorial.endlessLesson, config.authoredScoring, out reason)) { reason = "Endless lesson: " + reason; return false; }
        if (config.tutorial.endlessLesson.id == config.tutorial.lesson.id || config.tutorial.endlessInstructions == null ||
            config.tutorial.endlessInstructions.Length < config.tutorial.endlessLesson.verifiedSolution.Count)
        { reason = "Endless lesson needs a separate ID and an instruction for every placement."; return false; }
        foreach (var cell in config.tutorial.endlessLesson.startingCells)
            if (cell.colorId >= palette.Count) { reason = "Endless lesson uses an unknown color."; return false; }
        if (config.tutorial.stepInstructions == null || config.tutorial.stepInstructions.Length < config.tutorial.lesson.verifiedSolution.Count)
        { reason = "Tutorial needs an instruction for every verified placement."; return false; }
        foreach (PopStartingCell cell in config.tutorial.lesson.startingCells)
            if (cell.colorId >= palette.Count) { reason = "Tutorial uses an unknown color."; return false; }
        if (config.daily.levelIds == null || config.daily.enabled && (config.daily.levelIds.Count == 0 || string.IsNullOrWhiteSpace(config.daily.scheduleRevision)))
        { reason = "Daily needs a revision and a non-empty bank."; return false; }
        foreach (string id in config.daily.levelIds)
            if (!ids.Contains(id)) { reason = "Daily references unknown Journey level " + id; return false; }
        ids.Clear();
        foreach (PopThemeDefinition theme in config.themes)
            if (theme == null || string.IsNullOrWhiteSpace(theme.id) || !ids.Add(theme.id) || theme.blockColors == null || theme.blockColors.Length == 0 ||
                theme.requiredJourneyStars < 0 || theme.requiredJourneyStars > config.journeyLevels.Count * 3 || theme.requiredDailyWins < 0 || theme.requiredClassicBest < 0)
            { reason = "Invalid/duplicate theme or requirements."; return false; }
        PopThemeDefinition first = config.themes[0];
        if (first.requiredJourneyStars != 0 || first.requiredDailyWins != 0 || first.requiredClassicBest != 0)
        { reason = "The first/default theme must be unlocked for everyone."; return false; }
        ids.Clear();
        foreach (PopEffectDefinition effect in config.popEffects)
            if (effect == null || string.IsNullOrWhiteSpace(effect.id) || !ids.Add(effect.id) || effect.requiredSingleClearLines < 0 ||
                effect.requiredSingleClearLines > 16 || effect.requiredBestCombo < 0 || effect.particleMultiplier < 1 || effect.particleMultiplier > 3 ||
                effect.speedMultiplier < .5f || effect.speedMultiplier > 2 || effect.lifetimeMultiplier < .5f || effect.lifetimeMultiplier > 2 ||
                effect.tintStrength < 0 || effect.tintStrength > 1)
            { reason = "Invalid effect identity, requirement or particle tuning."; return false; }
        if (config.popEffects[0].requiredSingleClearLines != 0 || config.popEffects[0].requiredBestCombo != 0)
        { reason = "The first effect must be available to everyone."; return false; }
        if (config.ui.levelColumns < 1 || config.ui.levelsPerPage < 1 || config.ui.referenceResolution.x < 1 || config.ui.referenceResolution.y < 1 ||
            config.daily.calendarDaysToShow < 7 || config.daily.calendarDaysToShow > 35 || config.daily.celebrationEveryWins < 1 ||
            config.authoredScoring.placementPointsPerCell < 0 || config.authoredScoring.pointsPerLine < 1 || config.authoredScoring.maximumComboMultiplier < 1 ||
            config.authoredScoring.comboBonusPerChain < 0 || config.runs.timedComboWindowSeconds <= 0 || config.runs.feverStartCombo < 2)
        { reason = "Invalid UI, scoring, daily or combo settings."; return false; }
        if (string.IsNullOrWhiteSpace(config.endless.generatorRevision) || config.endless.generatorRevision.Length > 128 ||
            config.endless.minimumMoves < 3 || config.endless.minimumMoves > config.endless.maximumMoves || config.endless.maximumMoves < 7 || config.endless.maximumMoves > 9 ||
            config.endless.candidateAttempts < 4 || config.endless.candidateAttempts > 64 || config.endless.wavesPerDifficultyStep < 1 ||
            config.endless.minimumSlack < 0 || config.endless.minimumSlack > config.endless.startingSlack || config.endless.startingSlack < 1 || config.endless.startingSlack > 5 ||
            config.endless.maximumTargetLanes < 1 || config.endless.maximumTargetLanes > 5 || config.endless.bubblesPerLane < 1 || config.endless.bubblesPerLane > 4 ||
            config.endless.multiClearWeight < 0 || config.endless.multiClearWeight > 8 || config.endless.setupMoveWeight < 0 || config.endless.setupMoveWeight > 4 ||
            config.endless.leftoverPocketPenalty < 0 || config.endless.leftoverPocketPenalty > 1 ||
            config.endless.transitionSeconds < .35f || config.endless.transitionSeconds > 2f)
        { reason = "Invalid Endless Pop revision, move limits, generation budget or difficulty settings."; return false; }
        reason = config.journeyLevels.Count + " Journey levels and tutorial have verified three-star wins; Daily/theme references are valid.";
        return true;
    }
}

public sealed class PopGameplayBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => -110;
    public void OnPreprocessBuild(BuildReport report)
    {
        PopGameplaySetup.EnsureConfiguration();
        if (!PopGameplaySetup.Validate(AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath), out string reason))
            throw new BuildFailedException("Gameplay configuration: " + reason);
    }
}

[CustomEditor(typeof(PopGameplayConfiguration))]
public sealed class PopGameplayConfigurationEditor : Editor
{
    private int previewLevel, colorId;
    private bool editBubbles;
    public override void OnInspectorGUI()
    {
        var config = (PopGameplayConfiguration)target;
        EditorGUILayout.HelpBox("Edit rules, UI and content below. Permanent IDs preserve records. Increment Content Revision after changing published layouts/decks/scoring. Validation replays a real fixed-deck solution before builds.", MessageType.Info);
        if (GUILayout.Button("Validate every level and tutorial")) PopGameplaySetup.ValidateMenu();
        if (GUILayout.Button("Preview / paint / replay Endless Pop generator")) PopEndlessGeneratorPreview.Open();
        if (config.journeyLevels.Count > 0)
        {
            string[] choices = config.journeyLevels.ConvertAll(level => level == null ? "Missing" : level.id + " — " + level.title).ToArray();
            previewLevel = EditorGUILayout.Popup("Board preview / paint", Mathf.Clamp(previewLevel, 0, choices.Length - 1), choices);
            PopLevelDefinition level = config.journeyLevels[previewLevel];
            if (level != null)
            {
                BlockColorPalette palette = AssetDatabase.LoadAssetAtPath<BlockColorPalette>("Assets/_Project/ScriptableObjects/BlockColorPalette.asset");
                colorId = EditorGUILayout.IntSlider("Paint color ID", colorId, 0, Mathf.Max(0, (palette != null ? palette.Count : 6) - 1));
                editBubbles = EditorGUILayout.Toggle("Paint bubbles (occupied cells)", editBubbles);
                EditorGUILayout.HelpBox("Click a tile to add/remove an occupied cell, or toggle its bubble. Coordinates: bottom-left = (0,0). Deck IDs and solution origins remain editable in the level list below.", MessageType.None);
                DrawBoard(config, level);
                EditorGUILayout.LabelField("Fixed deck: " + string.Join(", ", level.pieceSequence));
                if (PopLevelValidation.Validate(level, config.authoredScoring, out string result)) EditorGUILayout.HelpBox(result, MessageType.Info);
                else EditorGUILayout.HelpBox(result, MessageType.Error);
                if (GUILayout.Button("Duplicate previewed level (edit ID/title/solution next)"))
                {
                    Undo.RecordObject(config, "Duplicate Journey level");
                    PopLevelDefinition copy = JsonUtility.FromJson<PopLevelDefinition>(JsonUtility.ToJson(level));
                    copy.id = "journey-" + Guid.NewGuid().ToString("N").Substring(0, 8); copy.title += " Copy";
                    config.journeyLevels.Add(copy); EditorUtility.SetDirty(config);
                }
            }
        }
        DrawDefaultInspector();
    }

    private void DrawBoard(PopGameplayConfiguration config, PopLevelDefinition level)
    {
        for (int y = 7; y >= 0; --y)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < 8; ++x)
            {
                Vector2Int point = new(x, y); PopStartingCell cell = level.startingCells.Find(c => c.position == point);
                Color old = GUI.backgroundColor; GUI.backgroundColor = cell == null ? new Color(0.3f, 0.4f, 0.45f) : cell.bubble ? Color.yellow : config.themes[0].blockColors[cell.colorId % config.themes[0].blockColors.Length];
                if (GUILayout.Button(cell == null ? "·" : cell.bubble ? "O" : cell.colorId.ToString(), GUILayout.Width(29), GUILayout.Height(29)))
                {
                    Undo.RecordObject(config, "Paint Journey board");
                    if (editBubbles) { if (cell != null) cell.bubble = !cell.bubble; }
                    else if (cell != null) level.startingCells.Remove(cell);
                    else level.startingCells.Add(new PopStartingCell { position = point, colorId = colorId });
                    EditorUtility.SetDirty(config);
                }
                GUI.backgroundColor = old;
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Show shape ID catalogue")) PopShapeCatalogue.OpenCatalogue();
    }
}

public sealed class PopShapeCatalogue : EditorWindow
{
    private Vector2 scroll;
    public static void OpenCatalogue() => GetWindow<PopShapeCatalogue>("Piece IDs");
    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Use these IDs in Piece Sequence and Verified Solution. Shapes are fixed; there is no player rotation.", MessageType.Info);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < Polyominos.Lenght; ++i)
        {
            EditorGUILayout.BeginHorizontal("box"); EditorGUILayout.LabelField(i.ToString(), GUILayout.Width(35));
            int[,] shape = Polyominos.Get(i); string picture = "";
            for (int row = shape.GetLength(0) - 1; row >= 0; --row)
            { for (int col = 0; col < shape.GetLength(1); ++col) picture += shape[row, col] > 0 ? "[]" : "  "; picture += "\n"; }
            EditorGUILayout.LabelField(picture, GUILayout.Height(shape.GetLength(0) * 17)); EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }
}
