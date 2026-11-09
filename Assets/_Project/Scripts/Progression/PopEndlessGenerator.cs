using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PopEndlessSettings
{
    public bool replaceJourneyHomeButton = true;
    public bool resumeRun = true;
    public string generatorRevision = "endless-pop-v1";
    [Range(4, 64)] public int candidateAttempts = 28;
    [Range(3, 9)] public int minimumMoves = 6;
    [Range(7, 9)] public int maximumMoves = 9;
    [Range(1, 5)] public int startingSlack = 3;
    [Range(0, 3)] public int minimumSlack = 1;
    [Min(1)] public int wavesPerDifficultyStep = 4;
    [Range(1, 5)] public int maximumTargetLanes = 4;
    [Range(1, 4)] public int bubblesPerLane = 2;
    [Min(0)] public int breathingWaveInterval = 5;
    [Header("Puzzle rhythm (future waves only; saved puzzles keep their deck)")]
    public bool alternateLanePatterns = true;
    [Range(0, 8)] public float multiClearWeight = 3;
    [Range(0, 4)] public float setupMoveWeight = 1.5f;
    [Range(0, 1)] public float leftoverPocketPenalty = .3f;
    [Range(.35f, 2f)] public float transitionSeconds = .7f;
    [Tooltip("0 chooses a fresh seed for each new run. Non-zero is reproducible QA.")]
    public int fixedSeed;
    public string title = "ENDLESS POP";
    public string play = "CHƠI ENDLESS POP";
    public string resume = "TIẾP TỤC ENDLESS POP";
    public string newRun = "VÁN ENDLESS POP MỚI";
    [TextArea] public string description = "Xóa hết bóng trước khi hết lượt. Giữ bàn qua các đợt. Xem trước toàn bộ khối; mỗi khay có thể đặt theo thứ tự tùy ý.";
    [TextArea] public string replaceWarning = "Bắt đầu Endless Pop mới? Ván Endless Pop đang lưu sẽ bị thay thế. Classic, kỷ lục và giao diện đã mở vẫn giữ nguyên.";
}

/// <summary>
/// Constructive puzzle generation on the ACTUAL leftover board. Reserve non-overlapping
/// legal pieces as gaps, complete selected lanes around them, then replay the witness.
/// Every accepted deck has a real win under the three-piece-tray rule. No live objects,
/// global Random, unbounded search, hidden rerolls, or board deletion.
/// </summary>
public static partial class PopEndlessGenerator
{
    public static PopLevelDefinition Generate(int[] colors, int wave, int paletteCount, PopEndlessSettings settings, ref PopRandom random)
    {
        if (colors == null || colors.Length != 64) throw new ArgumentException("Expected 8x8 board.");
        int difficulty = Mathf.Min(6, Math.Max(0, wave - 1) / Math.Max(1, settings.wavesPerDifficultyStep));
        if (settings.breathingWaveInterval > 0 && wave % settings.breathingWaveInterval == 0) difficulty = Math.Max(0, difficulty - 2);
        PopLevelDefinition best = null; float bestQuality = float.NegativeInfinity;
        for (int attempt = 0; attempt < Mathf.Clamp(settings.candidateAttempts, 4, 64); ++attempt)
        {
            PopLevelDefinition candidate = Candidate(colors, wave, paletteCount, difficulty, settings, attempt % 3 == 2, ref random);
            if (candidate == null || !Validate(candidate, out _)) continue;
            float quality = Evaluate(candidate, settings, difficulty).quality;
            if (quality > bestQuality) { best = candidate; bestQuality = quality; }
        }
        // Crowded/irregular boards get a verified row-completion fallback, never a reset.
        return best ?? Fallback(colors, wave, paletteCount, settings, ref random);
    }

    private static PopLevelDefinition Candidate(int[] colors, int wave, int paletteCount, int difficulty, PopEndlessSettings settings, bool simpleAlternative, ref PopRandom random)
    {
        bool transpose = random.Range(0, 2) == 1;
        int count = Mathf.Clamp(1 + difficulty / 2, 1, Mathf.Clamp(settings.maximumTargetLanes, 1, 5));
        int[] lanes = SelectLanes(wave, count, transpose, settings, simpleAlternative, ref random);
        bool[] reserved = new bool[64];
        for (int i = 0; i < 64; ++i) reserved[i] = colors[i] >= 0;
        var witness = new List<PopSolutionMove>();
        int wantedMoves = Mathf.Clamp(2 + difficulty, 2, Mathf.Clamp(settings.maximumMoves, 7, 9) - 1);
        for (int step = 0; step < wantedMoves; ++step)
        {
            bool found = false;
            for (int sample = 0; sample < 90 && !found; ++sample)
            {
                int shapeId = random.Range(1, Polyominos.Lenght); int[,] shape = Polyominos.Get(shapeId);
                if (Polyominos.GetCellCount(shapeId) > 4 + difficulty / 2 || Polyominos.GetCellCount(shapeId) < 2) continue;
                int x = random.Range(0, 9 - shape.GetLength(1)), y = random.Range(0, 9 - shape.GetLength(0));
                bool legal = true, touchesLane = false;
                for (int r = 0; r < shape.GetLength(0); ++r) for (int c = 0; c < shape.GetLength(1); ++c)
                    if (shape[r, c] > 0)
                    { legal &= !reserved[(y + r) * 8 + x + c]; touchesLane |= TouchesLane(x + c, y + r, lanes); }
                if (!legal || !touchesLane) continue;
                for (int r = 0; r < shape.GetLength(0); ++r) for (int c = 0; c < shape.GetLength(1); ++c)
                    if (shape[r, c] > 0) reserved[(y + r) * 8 + x + c] = true;
                witness.Add(new PopSolutionMove { shape = shapeId, origin = new Vector2Int(x, y) }); found = true;
            }
            if (!found) break;
        }
        if (witness.Count < 2) return null;
        var gaps = new bool[64];
        foreach (var move in witness)
        {
            int[,] shape = Polyominos.Get(move.shape);
            for (int r = 0; r < shape.GetLength(0); ++r) for (int c = 0; c < shape.GetLength(1); ++c)
                if (shape[r, c] > 0) gaps[(move.origin.y + r) * 8 + move.origin.x + c] = true;
        }
        int[] start = (int[])colors.Clone(); bool[] bubbles = new bool[64];
        foreach (int lane in lanes)
        {
            int marked = 0; bool hasGap = false;
            for (int p = 0; p < 8; ++p)
            {
                int index = lane >= 8 ? p * 8 + lane - 8 : lane * 8 + p;
                if (gaps[index]) { hasGap = true; continue; }
                if (start[index] < 0) start[index] = (wave + lane) % Math.Max(1, paletteCount);
                ++marked;
            }
            if (!hasGap || marked == 0) return null;
            // Spread target positions across the lane instead of always marking its left/bottom edge.
            int startAt = random.Range(0, 8), markedBubbles = 0;
            for (int p = 0; p < 8 && markedBubbles < Mathf.Clamp(settings.bubblesPerLane, 1, 4); ++p)
            {
                int at = (startAt + p * 3) % 8;
                int index = lane >= 8 ? at * 8 + lane - 8 : lane * 8 + at;
                if (gaps[index]) continue;
                bubbles[index] = true; ++markedBubbles;
            }
        }
        return Build(start, bubbles, witness, wave, difficulty, settings, ref random);
    }

    private static PopLevelDefinition Fallback(int[] colors, int wave, int paletteCount, PopEndlessSettings settings, ref PopRandom random)
    {
        for (int offset = 0, first = random.Range(0, 8); offset < 8; ++offset)
        {
            int row = (first + offset) % 8; int[] start = (int[])colors.Clone();
            bool any = false; for (int x = 0; x < 8; ++x) any |= start[row * 8 + x] >= 0;
            if (!any) start[row * 8 + random.Range(0, 8)] = wave % Math.Max(1, paletteCount);
            var bubbles = new bool[64]; var moves = new List<PopSolutionMove>();
            for (int x = 0; x < 8;)
            {
                if (start[row * 8 + x] >= 0) { bubbles[row * 8 + x] = true; ++x; continue; }
                int length = 1; while (length < 5 && x + length < 8 && start[row * 8 + x + length] < 0) ++length;
                int shape = FindLine(length); moves.Add(new PopSolutionMove { shape = shape, origin = new Vector2Int(x, row) }); x += length;
            }
            var level = Build(start, bubbles, moves, wave, 0, settings, ref random);
            if (level != null && Validate(level, out _)) return level;
        }
        throw new InvalidOperationException("No legal endless wave can be constructed from this board.");
    }

    private static int FindLine(int length)
    {
        for (int i = 0; i < Polyominos.Lenght; ++i)
        { int[,] shape = Polyominos.Get(i); if (shape.GetLength(0) == 1 && shape.GetLength(1) == length && Polyominos.GetCellCount(i) == length) return i; }
        return 0;
    }

    private static PopLevelDefinition Build(int[] colors, bool[] bubbles, List<PopSolutionMove> witness, int wave, int difficulty, PopEndlessSettings settings, ref PopRandom random)
    {
        if (witness.Count == 0 || witness.Count > 9) return null;
        int slack = Math.Max(Mathf.Clamp(settings.minimumSlack, 0, 3), Mathf.Clamp(settings.startingSlack, 1, 5) - difficulty / 2);
        var level = new PopLevelDefinition { id = "endless-" + wave, title = "ĐỢT " + wave, targetLines = 0,
            moveLimit = Mathf.Clamp(Math.Max(Mathf.Clamp(settings.minimumMoves, 3, 9), witness.Count + slack), witness.Count, Mathf.Clamp(settings.maximumMoves, 7, 9)),
            threeStarMoves = witness.Count, twoStarMoves = witness.Count, verifiedSolution = witness };
        // Fill only previously empty cells. Existing colors/occupancy survive every transition.
        for (int i = 0; i < 64; ++i) if (colors[i] >= 0)
        { level.startingCells.Add(new PopStartingCell { position = new Vector2Int(i % 8, i / 8), colorId = colors[i], bubble = bubbles[i] }); if (bubbles[i]) ++level.targetBubbles; }
        // Do not overstate difficulty with witness steps after the actual first win.
        var occupied = new bool[64]; for (int i = 0; i < 64; ++i) occupied[i] = colors[i] >= 0;
        var remaining = (bool[])bubbles.Clone(); int lines = 0, popped = 0, score = 0, combo = 0, best = 0, multi = 0;
        for (int i = 0; i < witness.Count; ++i)
        {
            var step = witness[i];
            if (!PopLevelValidation.ResolveMove(occupied, remaining, step.shape, step.origin, new PopScoringSettings(), ref lines, ref popped, ref score, ref combo, ref best, ref multi)) return null;
            if (popped >= level.targetBubbles) { if (i + 1 < witness.Count) witness.RemoveRange(i + 1, witness.Count - i - 1); break; }
        }
        if (popped < level.targetBubbles) return null;
        level.threeStarMoves = level.twoStarMoves = witness.Count;
        level.moveLimit = Mathf.Clamp(Math.Max(Mathf.Clamp(settings.minimumMoves, 3, 9), witness.Count + slack), witness.Count, Mathf.Clamp(settings.maximumMoves, 7, 9));
        foreach (var move in witness) level.pieceSequence.Add(move.shape);
        while (level.pieceSequence.Count < level.moveLimit) level.pieceSequence.Add(random.Range(1, Math.Min(Polyominos.Lenght, 12)));
        // Shuffle WITHIN each tray only: the witness remains available in the same groups.
        for (int first = 0; first < level.pieceSequence.Count; first += 3)
            for (int i = Math.Min(first + 2, level.pieceSequence.Count - 1); i > first; --i)
            { int j = random.Range(first, i + 1); (level.pieceSequence[i], level.pieceSequence[j]) = (level.pieceSequence[j], level.pieceSequence[i]); }
        return level.targetBubbles > 0 ? level : null;
    }

    public static bool Validate(PopLevelDefinition level, out string reason) => PopLevelValidation.Validate(level, new PopScoringSettings(), out reason);

    public static bool ValidSnapshot(PopRunSnapshot run)
    {
        var level = run.endlessDefinition;
        if (run.endlessWave < 1 || run.endlessRandomState == 0 || run.generatorState == 0 || string.IsNullOrWhiteSpace(run.endlessRevision) ||
            run.endlessRevision.Length > 128 || level == null || level.id != run.levelId || level.id != "endless-" + run.endlessWave ||
            level.moveLimit < 1 || level.moveLimit > 9 || level.startingCells == null || level.startingCells.Count > 64 ||
            level.pieceSequence == null || level.pieceSequence.Count != level.moveLimit || level.verifiedSolution == null ||
            level.verifiedSolution.Count > 9 || run.waveStartMoves < 0 || run.waveStartMoves > run.moves ||
            run.waveStartBubbles < 0 || run.waveStartBubbles > run.bubblesPopped || run.deckCursor < 1 || run.deckCursor > level.pieceSequence.Count) return false;
        int used = run.moves - run.waveStartMoves, popped = run.bubblesPopped - run.waveStartBubbles, remaining = 0, available = 0;
        for (int lane = 0; lane < 8; ++lane)
        {
            bool row = true, column = true;
            for (int p = 0; p < 8; ++p) { row &= run.occupiedColors[lane * 8 + p] >= 0; column &= run.occupiedColors[p * 8 + lane] >= 0; }
            if (row || column) return false; // A committed placement must have resolved every full line.
        }
        foreach (bool target in run.bubbleTargets) if (target) ++remaining;
        for (int i = 0; i < 3; ++i) if (run.trayAvailable[i]) ++available;
        int batchStart = (run.deckCursor - 1) / 3 * 3;
        for (int i = 0; i < 3; ++i)
        { int expected = batchStart + i < level.pieceSequence.Count ? level.pieceSequence[batchStart + i] : -1; if (run.trayShapes[i] != expected) return false; }
        return used == run.deckCursor - available && used <= level.moveLimit && popped + remaining == level.targetBubbles &&
            (run.endlessPendingWave ? remaining == 0 : remaining > 0 && used < level.moveLimit) && Validate(level, out _);
    }
}
