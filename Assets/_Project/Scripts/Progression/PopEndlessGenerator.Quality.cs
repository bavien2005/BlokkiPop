using System.Collections.Generic;
using UnityEngine;

/// <summary>Bounded, deterministic structural scoring. This is not a proof of optimality or fun.</summary>
public static partial class PopEndlessGenerator
{
    public struct PuzzleMetrics
    {
        public int setupMoves, multiClears, remainingCells, pocketCells, distinctShapes;
        public float quality;
        public override string ToString() => $"Setup {setupMoves} | Double+ {multiClears} | Left {remainingCells} | Pockets {pocketCells} | Shapes {distinctShapes}";
    }

    private static int[] SelectLanes(int wave, int count, bool transpose, PopEndlessSettings settings, bool simpleAlternative, ref PopRandom random)
    {
        int first = random.Range(0, 9 - count), offset = transpose ? 8 : 0;
        var lanes = new int[count];
        bool breathing = settings.breathingWaveInterval > 0 && wave % settings.breathingWaveInterval == 0;
        int pattern = settings.alternateLanePatterns && !breathing && !simpleAlternative ? (wave - 1) % 3 : 0;
        for (int i = 0; i < count; ++i)
            lanes[i] = pattern == 2 && i % 2 == 1 ? (offset == 0 ? 8 : 0) + (first + i) % 8
                : offset + (pattern == 1 ? (first + i * 2) % 8 : first + i);
        return lanes;
    }

    private static bool TouchesLane(int x, int y, int[] lanes)
    {
        foreach (int lane in lanes) if (lane < 8 ? y == lane : x == lane - 8) return true;
        return false;
    }

    public static PuzzleMetrics Evaluate(PopLevelDefinition level, PopEndlessSettings settings, int difficulty = 0)
    {
        var result = new PuzzleMetrics();
        var occupied = new bool[64]; var bubbles = new bool[64];
        foreach (var cell in level.startingCells)
        { int i = cell.position.y * 8 + cell.position.x; occupied[i] = true; bubbles[i] = cell.bubble; }
        int lines = 0, popped = 0, score = 0, combo = 0, best = 0, multi = 0;
        foreach (var move in level.verifiedSolution)
        {
            int before = lines;
            if (!PopLevelValidation.ResolveMove(occupied, bubbles, move.shape, move.origin, new PopScoringSettings(),
                ref lines, ref popped, ref score, ref combo, ref best, ref multi))
            { result.quality = float.NegativeInfinity; return result; }
            if (lines == before) ++result.setupMoves;
            if (lines - before >= 2) ++result.multiClears;
            if (popped >= level.targetBubbles) break;
        }
        // Count only tiny empty pockets using a fixed 64-cell flood; no extra shape-placement search.
        var visited = new bool[64]; var queue = new int[64];
        for (int i = 0; i < 64; ++i)
        {
            if (occupied[i]) { ++result.remainingCells; continue; }
            if (visited[i]) continue;
            int read = 0, write = 1; queue[0] = i; visited[i] = true;
            while (read < write)
            {
                int at = queue[read++], x = at % 8, y = at / 8;
                Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
            }
            if (write <= 3) result.pocketCells += write;
            void Visit(int x, int y)
            {
                if (x < 0 || x > 7 || y < 0 || y > 7) return;
                int next = y * 8 + x;
                if (occupied[next] || visited[next]) return;
                visited[next] = true; queue[write++] = next;
            }
        }
        result.distinctShapes = new HashSet<int>(level.pieceSequence).Count;
        result.quality = level.verifiedSolution.Count * (2 + difficulty) + result.distinctShapes * 2
            + result.multiClears * Mathf.Clamp(settings.multiClearWeight, 0, 8)
            + Mathf.Min(result.setupMoves, difficulty + 1) * Mathf.Clamp(settings.setupMoveWeight, 0, 4)
            - result.pocketCells * Mathf.Clamp01(settings.leftoverPocketPenalty) - result.remainingCells * .08f
            - level.startingCells.Count * .06f;
        return result;
    }
}
