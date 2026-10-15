using UnityEngine;

public partial class Blocks
{
    private PopRandom generator;
    private int[] trayColorIds;
    public string GenerationSignature
    {
        get
        {
            string settings = System.FormattableString.Invariant($"{useSmartTrayGeneration}/{trayCandidateAttempts}/{maxSimulationBranchesPerShape}/{comboDifficultyStrength:R}/{dangerDifficultyRelief:R}/{dangerFitBias:R}/{rescueDangerThreshold:R}/{rescueDangerStreakRequired}/{rescueActivationChance:R}/{rescueClearPriority:R}");
            if (difficultyLevels != null) foreach (BlockDifficultyLevel level in difficultyLevels) settings += "/" + JsonUtility.ToJson(level);
            return Hash128.Compute(settings).ToString();
        }
    }
    public bool HasPlayablePiece
    {
        get
        {
            if (blocks == null || polyominoIndexes == null || board == null) return false;
            for (int i = 0; i < blocks.Length; ++i)
                if (blocks[i].gameObject.activeSelf && polyominoIndexes[i] >= 0 && board.CheckPlace(polyominoIndexes[i])) return true;
            return false;
        }
    }

    public void InitializeGenerator(uint seed) { generator = new PopRandom(seed); highDangerTrayStreak = 0; }
    public void DealNextTray() => Generate();

    private void ApplyTray(int[] shapes, int[] colors, bool[] available)
    {
        blockCount = 0;
        trayColorIds ??= new int[blocks.Length];
        for (int i = 0; i < blocks.Length; ++i)
        {
            polyominoIndexes[i] = shapes[i];
            trayColorIds[i] = colors != null ? colors[i] : generator.Range(0, Mathf.Max(1, colorPalette.Count));
            bool visible = shapes[i] >= 0 && (available == null || available[i]);
            blocks[i].gameObject.SetActive(visible);
            if (!visible) continue;
            blocks[i].SetColor(trayColorIds[i], colorPalette.GetColor(trayColorIds[i]));
            blocks[i].Show(shapes[i]); FitBlockPreviewInTray(i, shapes[i]); ++blockCount;
        }
    }

    public bool CanImport(PopRunSnapshot run)
    {
        if (!PopSaveStore.ValidRun(run) || blocks.Length != 3) return false;
        for (int i = 0; i < 3; ++i) if (run.trayColors[i] >= colorPalette.Count) return false;
        return run.generatorState != 0;
    }

    public void Import(PopRunSnapshot run)
    {
        generator = new PopRandom(run.generatorState); highDangerTrayStreak = run.dangerStreak;
        ApplyTray(run.trayShapes, run.trayColors, run.trayAvailable);
    }

    public void Export(PopRunSnapshot run)
    {
        for (int i = 0; i < blocks.Length; ++i)
        { run.trayShapes[i] = polyominoIndexes[i]; run.trayColors[i] = trayColorIds[i]; run.trayAvailable[i] = blocks[i].gameObject.activeSelf; }
        run.generatorState = generator.State; run.dangerStreak = highDangerTrayStreak;
    }

    public void RefreshTheme()
    {
        if (trayColorIds == null) return;
        for (int i = 0; i < blocks.Length; ++i)
            if (blocks[i].gameObject.activeSelf) { blocks[i].SetColor(trayColorIds[i], colorPalette.GetColor(trayColorIds[i])); blocks[i].Show(polyominoIndexes[i]); }
    }

    /// <summary>Same placement/consume transaction as a pointer drag, also useful for automated scene validation.</summary>
    public bool TryPlaceSlot(int slot, Vector2Int origin)
    {
        if (slot < 0 || slot >= blocks.Length || !blocks[slot].gameObject.activeSelf || GameFlowManager._instance == null ||
            GameFlowManager._instance.IsGameOver || Time.timeScale <= 0 || PopGameplayUI.IsModalOpen || AdsPrivacyPanel.IsModalOpen ||
            AdsService.Instance != null && AdsService.Instance.IsShowing) return false;
        if (!board.Place(origin, polyominoIndexes[slot], trayColorIds[slot])) return false;
        blocks[slot].gameObject.SetActive(false); Remove(); return true;
    }
}
