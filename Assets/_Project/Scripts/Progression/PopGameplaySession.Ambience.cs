using System;
using UnityEngine;

public sealed partial class PopGameplaySession
{
    [Header("Automatic backgrounds — per-run cosmetic state")]
    [SerializeField] private PopAmbientRunState ambience = new();
    private PopAmbientConfiguration ambientConfiguration;
    private bool attemptedAmbientLoad;
    public PopAmbientConfiguration AmbientConfiguration
    {
        get
        {
            if (!attemptedAmbientLoad) { ambientConfiguration = Resources.Load<PopAmbientConfiguration>(PopAmbientConfiguration.ResourcePath); attemptedAmbientLoad = true; }
            return ambientConfiguration;
        }
    }
    public bool UsesAutomaticBackgrounds => AmbientConfiguration != null && AmbientConfiguration.enabled;
    public PopAmbientRunState Ambience => ambience;
    public bool HasScoreAmbience => IsActive && AmbientConfiguration != null && AmbientConfiguration.AppliesTo(mode);

    private void InitializeAmbience(PopAmbientRunState saved = null)
    {
        ambience = saved?.Copy() ?? new PopAmbientRunState { randomState = unchecked((uint)DateTime.UtcNow.Ticks) | 1u };
        if (ambience.stage < 0 || AmbientConfiguration == null || ambience.stage > AmbientConfiguration.StageFor(GameFlowManager._instance?.Score ?? 0) ||
            ambience.stage > 0 && AmbientConfiguration.Find(ambience.sceneId) == null)
        { ambience.stage = 0; ambience.sceneId = ""; }
        UpdateAmbientScore(GameFlowManager._instance?.Score ?? 0);
    }

    public void UpdateAmbientScore(int score)
    {
        if (!HasScoreAmbience || GameFlowManager._instance != null && GameFlowManager._instance.IsGameOver) return;
        if (ambience.Advance(AmbientConfiguration, score)) Notify();
    }

    private void EndAmbience() { ambience.stage = 0; ambience.sceneId = ""; }
}
