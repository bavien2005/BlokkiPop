using System;
using UnityEngine;

[Serializable]
public sealed class BlockDifficultyLevel
{
    [Tooltip("Name shown in the Inspector only.")]
    public string displayName = "Level";

    [Min(0), Tooltip("This level becomes active when the score reaches this value.")]
    public int scoreThreshold;

    [Header("Shape Weights")]
    [Range(0f, 1f)] public float easyWeight = 1f;
    [Range(0f, 1f)] public float standardWeight;
    [Range(0f, 1f)] public float hardWeight;

    [Header("Board Safety")]
    [Range(0, 3), Tooltip("How many of the three new blocks must have a legal placement when the tray is generated.")]
    public int guaranteedFittingBlocks = 3;

    [Range(-1f, 1f), Tooltip("Positive values gently prefer shapes with more legal placements; negative values favor tighter fits. This is a soft bias, not a hard filter.")]
    public float boardFitBias = 0.25f;

    [Range(0f, 1f), Tooltip("How much this level prefers trays whose simulated three-piece route clears lines. Higher values make early play cleaner; lower values allow more challenging routes.")]
    public float clearOpportunityWeight = 0.7f;
}
