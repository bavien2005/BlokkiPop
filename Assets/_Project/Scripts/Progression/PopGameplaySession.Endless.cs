using System;
using UnityEngine;

public sealed partial class PopGameplaySession
{
    [Header("Endless Pop runtime (saved separately from Classic)")]
    [SerializeField] private int endlessWave, waveStartMoves, waveStartBubbles;
    [SerializeField] private bool endlessPendingWave;
    private PopRandom endlessRandom;
    private float endlessTransitionAt;
    public bool IsEndless => mode == PopGameMode.EndlessPop;
    public bool IsTransitioning => IsEndless && endlessPendingWave && IsActive;
    public int EndlessWave => endlessWave;
    public int WaveMovesRemaining => definition == null ? 0 : Math.Max(0, definition.moveLimit - (moves - waveStartMoves));
    public int WaveBubblesRemaining => definition == null ? 0 : Math.Max(0, definition.targetBubbles - (bubblesPopped - waveStartBubbles));
    public int DeckCursor => deckCursor;

    public bool IsEndlessLesson => mode == PopGameMode.Tutorial && definition != null && definition.id == configuration.tutorial.endlessLesson.id;
    public bool UsesEndlessPresentation => IsEndless || IsEndlessLesson;

    public void PlayEndless(bool resume)
    {
        // Never interrupt Continue or an existing player's run with onboarding.
        if (!resume && configuration.tutorial.automaticOnFirstEndless && !store.Progress.endlessTutorialComplete &&
            store.Progress.endlessBestWave == 0 && SavedRun(PopGameMode.EndlessPop) == null)
        { PlayEndlessLesson(true); return; }
        StartMode(PopGameMode.EndlessPop, "", "", resume);
    }

    public void PlayEndlessLesson(bool startRunAfter = false)
    {
        afterTutorialMode = PopGameMode.EndlessPop; afterTutorialLevel = afterTutorialDate = "";
        hasAfterTutorial = startRunAfter;
        StartMode(PopGameMode.Tutorial, configuration.tutorial.endlessLesson.id, "", false);
    }

    private void StartFreshEndless()
    {
        endlessWave = 1; endlessPendingWave = false;
        endlessRandom = new PopRandom(configuration.endless.fixedSeed == 0 ? unchecked((uint)DateTime.UtcNow.Ticks) : (uint)configuration.endless.fixedSeed);
        // New run has a new board. Continuing/advancing a wave never clears old occupancy.
        board.ApplyStartingCells(null);
        PrepareEndlessWave();
    }

    private void PrepareEndlessWave()
    {
        var current = new PopRunSnapshot { occupiedColors = new int[64], bubbleTargets = new bool[64] };
        board.Export(current);
        definition = PopEndlessGenerator.Generate(current.occupiedColors, endlessWave, board.ColorCount, configuration.endless, ref endlessRandom);
        levelId = definition.id; deckCursor = 0; waveStartMoves = moves; waveStartBubbles = bubblesPopped;
        board.ApplyStartingCells(definition.startingCells);
        endlessPendingWave = false;
    }

    private bool CompleteEndlessPlacement()
    {
        if (WaveBubblesRemaining == 0)
        {
            endlessPendingWave = true;
            endlessTransitionAt = Time.unscaledTime + Mathf.Clamp(configuration.endless.transitionSeconds, .35f, 2f);
            if (configuration.bubbleFeedback.enabled && configuration.bubbleFeedback.waveClearSound)
                AudioManager.Instance?.PlayPopWaveCue(configuration.bubbleFeedback.soundGain);
            RecordEndlessBest();
            SaveCurrentRun(); store.Flush(); Notify();
            return true; // Do NOT deal another old-wave tray or report no-moves here.
        }
        if (WaveMovesRemaining == 0) { GameFlowManager._instance?.GameOver(); return true; }
        return false;
    }

    private void TickEndlessTransition()
    {
        if (!IsTransitioning || board == null || tray == null || Time.timeScale <= 0 || PopGameplayUI.IsModalOpen || AdsPrivacyPanel.IsModalOpen) return;
        if (Time.unscaledTime < endlessTransitionAt) return;
        ++endlessWave;
        PrepareEndlessWave(); tray.DealNextTray();
        SaveCurrentRun(); store.Flush(); Notify();
        if (!tray.HasPlayablePiece) GameFlowManager._instance?.GameOver();
    }

    private void RecordEndlessBest()
    {
        store.Progress.endlessBestWave = Math.Max(store.Progress.endlessBestWave, endlessPendingWave ? endlessWave : endlessWave - 1);
        store.Progress.endlessBestScore = Math.Max(store.Progress.endlessBestScore, GameFlowManager._instance?.Score ?? 0);
        store.Progress.endlessBestBubbles = Math.Max(store.Progress.endlessBestBubbles, bubblesPopped);
        store.SaveProgress();
    }

    private void ExportEndless(PopRunSnapshot snapshot)
    {
        snapshot.endlessWave = endlessWave; snapshot.waveStartMoves = waveStartMoves; snapshot.waveStartBubbles = waveStartBubbles;
        snapshot.endlessRandomState = endlessRandom.State; snapshot.endlessPendingWave = endlessPendingWave;
        snapshot.endlessRevision = configuration.endless.generatorRevision; snapshot.endlessDefinition = definition;
    }

    private void RestoreEndless(PopRunSnapshot snapshot)
    {
        levelId = snapshot.levelId;
        endlessWave = snapshot.endlessWave; waveStartMoves = snapshot.waveStartMoves; waveStartBubbles = snapshot.waveStartBubbles;
        endlessRandom = new PopRandom(snapshot.endlessRandomState); endlessPendingWave = snapshot.endlessPendingWave;
        endlessTransitionAt = Time.unscaledTime + Mathf.Clamp(configuration.endless.transitionSeconds, .35f, 2f);
    }
}
