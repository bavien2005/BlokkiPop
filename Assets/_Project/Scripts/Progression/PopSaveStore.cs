using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PopLevelRecord
{
    public string id;
    public int stars;
    public int bestMoves;
    public int bestScore;
}

[Serializable]
public sealed class PopProgressData
{
    public int schema = 1;
    public bool tutorialComplete;
    public bool endlessTutorialComplete;
    public string selectedTheme = "sky";
    public string selectedEffect = "classic";
    public int largestSingleClear, bestCombo;
    public int endlessBestWave, endlessBestScore, endlessBestBubbles;
    public List<PopLevelRecord> journey = new();
    // Daily identities include revision + UTC date; retries never grant a second completion.
    public List<PopLevelRecord> daily = new();
}

[Serializable]
public sealed class PopRunSnapshot
{
    public int schema = 1;
    public int contentRevision;
    public string rulesSignature;
    public PopGameMode mode;
    public string levelId;
    public string utcDate;
    public string dailyRevision;
    public int score;
    public int lines;
    public int moves;
    public int bubblesPopped;
    public int maxCombo;
    public int largestClear;
    public int combo;
    public float comboSecondsRemaining;
    public bool reviveUsed;
    public int deckCursor;
    public uint generatorState;
    public int dangerStreak;
    public int[] occupiedColors;
    public bool[] bubbleTargets;
    public int[] trayShapes;
    public int[] trayColors;
    public bool[] trayAvailable;
    public int endlessWave, waveStartMoves, waveStartBubbles;
    public uint endlessRandomState;
    public bool endlessPendingWave;
    public string endlessRevision;
    public PopLevelDefinition endlessDefinition;
    // Optional cosmetic extension: absent in old saves, never part of puzzle/RNG validation.
    public PopAmbientRunState ambience;
}

/// <summary>Own versioned keys only. Never DeleteAll, and never touches the legacy HighScore key.</summary>
public sealed class PopSaveStore
{
    private const string ProgressKey = "blokkipop.progression.v1";
    private const string RunPrefix = "blokkipop.run.v1.";
    public PopProgressData Progress { get; private set; }
    public bool RecoveredBackup { get; private set; }
    private bool dirty;

    public PopSaveStore()
    {
        Progress = Read<PopProgressData>(ProgressKey, ValidProgress) ?? new PopProgressData();
    }

    public PopRunSnapshot LoadRun(PopGameMode mode)
    {
        if (mode == PopGameMode.Tutorial) return null;
        PopRunSnapshot run = Read<PopRunSnapshot>(RunPrefix + mode, ValidRun);
        return run != null && run.mode == mode ? run : null;
    }

    public void SaveRun(PopRunSnapshot run)
    {
        if (run == null || run.mode == PopGameMode.Tutorial || !ValidRun(run)) return;
        Write(RunPrefix + run.mode, run, ValidRun);
    }

    public void ClearRun(PopGameMode mode)
    {
        if (mode == PopGameMode.Tutorial) return;
        PlayerPrefs.DeleteKey(RunPrefix + mode);
        PlayerPrefs.DeleteKey(RunPrefix + mode + ".backup");
        dirty = true;
    }

    public void SaveProgress() => Write(ProgressKey, Progress, ValidProgress);

    public bool RecordClearAchievement(PopGameMode mode, int clearedLines, int combo)
    {
        if (mode == PopGameMode.Tutorial || !Enum.IsDefined(typeof(PopGameMode), mode) || clearedLines < 0 || clearedLines > 16 || combo < 0) return false;
        int largest = Math.Max(Progress.largestSingleClear, clearedLines), best = Math.Max(Progress.bestCombo, combo);
        if (largest == Progress.largestSingleClear && best == Progress.bestCombo) return false;
        Progress.largestSingleClear = largest; Progress.bestCombo = best; SaveProgress(); return true;
    }

    public void Flush()
    {
        if (!dirty) return;
        PlayerPrefs.Save();
        dirty = false;
    }

    public static PopLevelRecord Find(List<PopLevelRecord> records, string id) => records.Find(record => record.id == id);

    public static void RecordWin(List<PopLevelRecord> records, string id, int stars, int moves, int score)
    {
        PopLevelRecord record = Find(records, id);
        if (record == null)
        {
            record = new PopLevelRecord { id = id, stars = 0, bestMoves = int.MaxValue };
            records.Add(record);
        }
        record.stars = Math.Max(record.stars, Mathf.Clamp(stars, 1, 3));
        record.bestMoves = Math.Min(record.bestMoves, Math.Max(1, moves));
        record.bestScore = Math.Max(record.bestScore, Math.Max(0, score));
    }

    private T Read<T>(string key, Func<T, bool> validate) where T : class
    {
        T value = Parse(PlayerPrefs.GetString(key, ""), validate);
        if (value != null) return value;
        value = Parse(PlayerPrefs.GetString(key + ".backup", ""), validate);
        if (value != null) RecoveredBackup = true;
        return value;
    }

    private void Write<T>(string key, T value, Func<T, bool> validate) where T : class
    {
        if (!validate(value)) return;
        string previous = PlayerPrefs.GetString(key, "");
        if (Parse(previous, validate) != null) PlayerPrefs.SetString(key + ".backup", previous);
        PlayerPrefs.SetString(key, JsonUtility.ToJson(value));
        dirty = true;
    }

    private static T Parse<T>(string json, Func<T, bool> validate) where T : class
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 4 * 1024 * 1024 || !json.TrimStart().StartsWith("{")) return null;
        try
        {
            T value = JsonUtility.FromJson<T>(json);
            return value != null && validate(value) ? value : null;
        }
        catch { return null; }
    }

    public static bool ValidProgress(PopProgressData data)
    {
        // Additive v1 migration: old progression records did not have an effect selection/statistics.
        if (data != null && data.selectedEffect == null) data.selectedEffect = "classic";
        if (data == null || data.schema != 1 || data.selectedTheme == null || data.selectedTheme.Length > 128 ||
            data.selectedEffect.Length > 128 || data.largestSingleClear < 0 || data.largestSingleClear > 16 || data.bestCombo < 0 ||
            data.endlessBestWave < 0 || data.endlessBestScore < 0 || data.endlessBestBubbles < 0 ||
            data.journey == null || data.daily == null || data.journey.Count > 10000 || data.daily.Count > 20000) return false;
        return ValidRecords(data.journey) && ValidRecords(data.daily);
    }

    private static bool ValidRecords(List<PopLevelRecord> records)
    {
        var ids = new HashSet<string>();
        foreach (PopLevelRecord record in records)
            if (record == null || string.IsNullOrWhiteSpace(record.id) || record.id.Length > 256 || !ids.Add(record.id) ||
                record.stars < 1 || record.stars > 3 || record.bestMoves < 1 || record.bestScore < 0) return false;
        return true;
    }

    public static bool ValidRun(PopRunSnapshot run)
    {
        if (run == null || run.schema != 1 || run.contentRevision < 1 || !Enum.IsDefined(typeof(PopGameMode), run.mode) ||
            run.mode == PopGameMode.Tutorial || string.IsNullOrWhiteSpace(run.rulesSignature) || run.rulesSignature.Length > 128 || run.score < 0 || run.lines < 0 ||
            run.moves < 0 || run.bubblesPopped < 0 || run.maxCombo < 0 || run.combo < 0 || run.largestClear < 0 ||
            run.deckCursor < 0 || run.dangerStreak < 0 || float.IsNaN(run.comboSecondsRemaining) || float.IsInfinity(run.comboSecondsRemaining) ||
            run.comboSecondsRemaining < 0f || run.occupiedColors == null || run.occupiedColors.Length != 64 ||
            run.bubbleTargets == null || run.bubbleTargets.Length != 64 || run.trayShapes == null || run.trayShapes.Length != 3 ||
            run.trayColors == null || run.trayColors.Length != 3 || run.trayAvailable == null || run.trayAvailable.Length != 3) return false;
        if (run.mode != PopGameMode.Classic && (string.IsNullOrWhiteSpace(run.levelId) || run.levelId.Length > 128)) return false;
        if (run.mode == PopGameMode.Daily && (string.IsNullOrWhiteSpace(run.utcDate) || string.IsNullOrWhiteSpace(run.dailyRevision))) return false;
        for (int i = 0; i < 64; ++i)
            if (run.occupiedColors[i] < -1 || run.occupiedColors[i] > 255 || (run.bubbleTargets[i] && run.occupiedColors[i] < 0)) return false;
        bool available = false;
        for (int i = 0; i < 3; ++i)
        {
            if (run.trayShapes[i] < -1 || run.trayShapes[i] >= Polyominos.Lenght || run.trayColors[i] < -1 || run.trayColors[i] > 255 ||
                (run.trayAvailable[i] && (run.trayShapes[i] < 0 || run.trayColors[i] < 0))) return false;
            available |= run.trayAvailable[i];
        }
        if (run.mode == PopGameMode.EndlessPop && !PopEndlessGenerator.ValidSnapshot(run)) return false;
        return available || run.mode == PopGameMode.EndlessPop && run.endlessPendingWave;
    }
}

/// <summary>Independent from Unity's visual/audio Random calls; exact state is included in unfinished runs.</summary>
public struct PopRandom
{
    public uint State;
    public PopRandom(uint seed) { State = seed == 0 ? 0x9e3779b9u : seed; }
    public uint Next()
    {
        if (State == 0) State = 0x9e3779b9u;
        State ^= State << 13; State ^= State >> 17; State ^= State << 5;
        return State;
    }
    public float Value => (Next() >> 8) / 16777216f;
    public int Range(int minimum, int maximum) => maximum <= minimum ? minimum : minimum + (int)(Next() % (uint)(maximum - minimum));
}
