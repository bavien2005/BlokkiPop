using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct BoardRiskReport
{
    public readonly float Danger;
    public readonly int FreeCells;
    public readonly int EmptyRegions;
    public readonly int SmallPocketCells;
    public readonly float ShapeAvailability;

    public BoardRiskReport(float danger, int freeCells, int emptyRegions, int smallPocketCells, float shapeAvailability)
    {
        Danger = danger;
        FreeCells = freeCells;
        EmptyRegions = emptyRegions;
        SmallPocketCells = smallPocketCells;
        ShapeAvailability = shapeAvailability;
    }
}

public readonly struct SimulatedPlacement
{
    public readonly int Column;
    public readonly int Row;
    public readonly int ClearedLines;
    public readonly float Quality;

    public SimulatedPlacement(int column, int row, int clearedLines, float quality)
    {
        Column = column;
        Row = row;
        ClearedLines = clearedLines;
        Quality = quality;
    }
}

public readonly struct TraySimulationResult
{
    public readonly bool HasPlayableOrder;
    public readonly int ClearedLines;
    public readonly int RemainingFreeCells;
    public readonly float Quality;

    public TraySimulationResult(bool hasPlayableOrder, int clearedLines, int remainingFreeCells, float quality)
    {
        HasPlayableOrder = hasPlayableOrder;
        ClearedLines = clearedLines;
        RemainingFreeCells = remainingFreeCells;
        Quality = quality;
    }
}

/// <summary>
/// Pure board math used while selecting a tray. It operates on copied bool grids,
/// never Cell objects or the live Board, so generation cannot affect gameplay.
/// </summary>
public static class BoardSimulation
{
    // Reused scratch storage keeps repeated simulated floods from allocating a
    // Queue and visited grid for every possible placement on a mobile device.
    private static readonly bool[,] floodVisited = new bool[Board.Size, Board.Size];
    private static readonly Vector2Int[] floodQueue = new Vector2Int[Board.Size * Board.Size];

    public static BoardRiskReport Analyze(bool[,] state)
    {
        int freeCells = 0;
        for (int row = 0; row < Board.Size; ++row)
        {
            for (int column = 0; column < Board.Size; ++column)
            {
                if (!state[row, column])
                    ++freeCells;
            }
        }

        GetEmptyRegionData(state, out int regions, out int smallPocketCells);

        int shapesWithMoves = 0;
        for (int shapeIndex = 0; shapeIndex < Polyominos.Lenght; ++shapeIndex)
        {
            if (CountAvailablePlacements(state, shapeIndex) > 0)
                ++shapesWithMoves;
        }

        float availability = Polyominos.Lenght > 0
            ? (float)shapesWithMoves / Polyominos.Lenght
            : 0f;
        float fullness = Mathf.InverseLerp(16f, 56f, Board.Size * Board.Size - freeCells);
        float fragmentation = freeCells > 0
            ? Mathf.Clamp01((float)smallPocketCells / freeCells * 0.72f + Mathf.InverseLerp(1f, 10f, regions) * 0.28f)
            : 1f;
        float scarcity = 1f - availability;
        float danger = Mathf.Clamp01(fullness * 0.46f + fragmentation * 0.30f + scarcity * 0.24f) * 100f;

        return new BoardRiskReport(danger, freeCells, regions, smallPocketCells, availability);
    }

    public static int CountAvailablePlacements(bool[,] state, int shapeIndex)
    {
        int[,] shape = Polyominos.Get(shapeIndex);
        int rows = shape.GetLength(0);
        int columns = shape.GetLength(1);
        int count = 0;

        for (int row = 0; row <= Board.Size - rows; ++row)
        {
            for (int column = 0; column <= Board.Size - columns; ++column)
            {
                if (CanPlace(state, shape, column, row))
                    ++count;
            }
        }

        return count;
    }

    public static int CountLineClearingPlacements(bool[,] state, int shapeIndex)
    {
        int[,] shape = Polyominos.Get(shapeIndex);
        int rows = shape.GetLength(0);
        int columns = shape.GetLength(1);
        int count = 0;
        for (int row = 0; row <= Board.Size - rows; ++row)
        {
            for (int column = 0; column <= Board.Size - columns; ++column)
            {
                if (CanPlace(state, shape, column, row) &&
                    CountLinesAfterPlacement(state, shape, column, row) > 0)
                    ++count;
            }
        }

        return count;
    }

    public static List<SimulatedPlacement> GetPlacements(bool[,] state, int shapeIndex)
    {
        int[,] shape = Polyominos.Get(shapeIndex);
        int rows = shape.GetLength(0);
        int columns = shape.GetLength(1);
        List<SimulatedPlacement> placements = new();

        for (int row = 0; row <= Board.Size - rows; ++row)
        {
            for (int column = 0; column <= Board.Size - columns; ++column)
            {
                if (!CanPlace(state, shape, column, row))
                    continue;

                bool[,] next = ApplyPlacement(state, shapeIndex, column, row, out int clearedLines);
                GetEmptyRegionData(next, out int regions, out int smallPockets);
                float quality = clearedLines * 100f - regions * 1.7f - smallPockets * 0.55f;
                placements.Add(new SimulatedPlacement(column, row, clearedLines, quality));
            }
        }

        placements.Sort((left, right) => right.Quality.CompareTo(left.Quality));
        return placements;
    }

    public static bool[,] ApplyPlacement(bool[,] state, int shapeIndex, int column, int row, out int clearedLines)
    {
        bool[,] next = Clone(state);
        int[,] shape = Polyominos.Get(shapeIndex);
        for (int shapeRow = 0; shapeRow < shape.GetLength(0); ++shapeRow)
        {
            for (int shapeColumn = 0; shapeColumn < shape.GetLength(1); ++shapeColumn)
            {
                if (shape[shapeRow, shapeColumn] > 0)
                    next[row + shapeRow, column + shapeColumn] = true;
            }
        }

        uint fullRowMask = 0;
        uint fullColumnMask = 0;
        clearedLines = 0;

        for (int scanRow = 0; scanRow < Board.Size; ++scanRow)
        {
            bool full = true;
            for (int scanColumn = 0; scanColumn < Board.Size; ++scanColumn)
            {
                if (!next[scanRow, scanColumn])
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                fullRowMask |= 1u << scanRow;
                ++clearedLines;
            }
        }

        for (int scanColumn = 0; scanColumn < Board.Size; ++scanColumn)
        {
            bool full = true;
            for (int scanRow = 0; scanRow < Board.Size; ++scanRow)
            {
                if (!next[scanRow, scanColumn])
                {
                    full = false;
                    break;
                }
            }

            if (full)
            {
                fullColumnMask |= 1u << scanColumn;
                ++clearedLines;
            }
        }

        for (int scanRow = 0; scanRow < Board.Size; ++scanRow)
        {
            for (int scanColumn = 0; scanColumn < Board.Size; ++scanColumn)
            {
                if ((fullRowMask & (1u << scanRow)) != 0 ||
                    (fullColumnMask & (1u << scanColumn)) != 0)
                    next[scanRow, scanColumn] = false;
            }
        }

        return next;
    }

    public static bool HasPlayableOrder(bool[,] startingState, IReadOnlyList<int> tray, int maxBranchesPerShape)
    {
        return Search(startingState, tray, new bool[tray.Count], 0, Mathf.Max(1, maxBranchesPerShape));
    }

    /// <summary>
    /// Finds a legal order for all pieces and rates the best route. A completed
    /// tray is worth more when it clears lines and leaves a less dangerous board.
    /// </summary>
    public static TraySimulationResult EvaluateTray(bool[,] startingState, IReadOnlyList<int> tray, int maxBranchesPerShape)
    {
        // Tray scoring is deliberately shallower than the fast validity check:
        // it ranks a handful of promising routes without exploring an expensive
        // full search tree on mobile every time a tray is refreshed.
        int branchLimit = Mathf.Clamp(maxBranchesPerShape, 1, 4);
        bool found = false;
        int bestClears = 0;
        int bestFreeCells = 0;
        float bestQuality = float.NegativeInfinity;
        SearchBest(startingState, tray, new bool[tray.Count], 0, 0, branchLimit,
            ref found, ref bestClears, ref bestFreeCells, ref bestQuality);
        return new TraySimulationResult(found, bestClears, bestFreeCells, bestQuality);
    }

    private static void SearchBest(
        bool[,] state,
        IReadOnlyList<int> tray,
        bool[] used,
        int depth,
        int clearedSoFar,
        int maxBranches,
        ref bool found,
        ref int bestClears,
        ref int bestFreeCells,
        ref float bestQuality)
    {
        if (depth >= tray.Count)
        {
            int freeCells = CountFreeCells(state);
            GetEmptyRegionData(state, out int regions, out int smallPockets);
            float quality = clearedSoFar * 100f + freeCells * 0.45f - regions * 1.7f - smallPockets * 0.55f;
            if (!found || quality > bestQuality)
            {
                found = true;
                bestClears = clearedSoFar;
                bestFreeCells = freeCells;
                bestQuality = quality;
            }
            return;
        }

        for (int index = 0; index < tray.Count; ++index)
        {
            if (used[index])
                continue;

            List<SimulatedPlacement> placements = GetPlacements(state, tray[index]);
            int branchCount = Mathf.Min(maxBranches, placements.Count);
            for (int placementIndex = 0; placementIndex < branchCount; ++placementIndex)
            {
                SimulatedPlacement placement = placements[placementIndex];
                bool[,] next = ApplyPlacement(state, tray[index], placement.Column, placement.Row, out _);
                used[index] = true;
                SearchBest(next, tray, used, depth + 1,
                    clearedSoFar + placement.ClearedLines, maxBranches,
                    ref found, ref bestClears, ref bestFreeCells, ref bestQuality);
                used[index] = false;
            }
        }
    }

    private static int CountFreeCells(bool[,] state)
    {
        int freeCells = 0;
        for (int row = 0; row < Board.Size; ++row)
        {
            for (int column = 0; column < Board.Size; ++column)
            {
                if (!state[row, column])
                    ++freeCells;
            }
        }

        return freeCells;
    }

    private static bool Search(bool[,] state, IReadOnlyList<int> tray, bool[] used, int depth, int maxBranches)
    {
        if (depth >= tray.Count)
            return true;

        for (int index = 0; index < tray.Count; ++index)
        {
            if (used[index])
                continue;

            List<SimulatedPlacement> placements = GetPlacements(state, tray[index]);
            int branchCount = Mathf.Min(maxBranches, placements.Count);
            for (int placementIndex = 0; placementIndex < branchCount; ++placementIndex)
            {
                SimulatedPlacement placement = placements[placementIndex];
                bool[,] next = ApplyPlacement(state, tray[index], placement.Column, placement.Row, out _);
                used[index] = true;
                if (Search(next, tray, used, depth + 1, maxBranches))
                    return true;
                used[index] = false;
            }
        }

        return false;
    }

    private static bool CanPlace(bool[,] state, int[,] shape, int column, int row)
    {
        for (int shapeRow = 0; shapeRow < shape.GetLength(0); ++shapeRow)
        {
            for (int shapeColumn = 0; shapeColumn < shape.GetLength(1); ++shapeColumn)
            {
                if (shape[shapeRow, shapeColumn] > 0 && state[row + shapeRow, column + shapeColumn])
                    return false;
            }
        }

        return true;
    }

    private static int CountLinesAfterPlacement(bool[,] state, int[,] shape, int column, int row)
    {
        int clearedLines = 0;
        for (int boardRow = 0; boardRow < Board.Size; ++boardRow)
        {
            bool full = true;
            for (int boardColumn = 0; boardColumn < Board.Size; ++boardColumn)
            {
                int shapeRow = boardRow - row;
                int shapeColumn = boardColumn - column;
                bool filledByShape = shapeRow >= 0 && shapeRow < shape.GetLength(0) &&
                                     shapeColumn >= 0 && shapeColumn < shape.GetLength(1) &&
                                     shape[shapeRow, shapeColumn] > 0;
                if (!state[boardRow, boardColumn] && !filledByShape)
                {
                    full = false;
                    break;
                }
            }

            if (full)
                ++clearedLines;
        }

        for (int boardColumn = 0; boardColumn < Board.Size; ++boardColumn)
        {
            bool full = true;
            for (int boardRow = 0; boardRow < Board.Size; ++boardRow)
            {
                int shapeRow = boardRow - row;
                int shapeColumn = boardColumn - column;
                bool filledByShape = shapeRow >= 0 && shapeRow < shape.GetLength(0) &&
                                     shapeColumn >= 0 && shapeColumn < shape.GetLength(1) &&
                                     shape[shapeRow, shapeColumn] > 0;
                if (!state[boardRow, boardColumn] && !filledByShape)
                {
                    full = false;
                    break;
                }
            }

            if (full)
                ++clearedLines;
        }

        return clearedLines;
    }

    private static bool[,] Clone(bool[,] state)
    {
        bool[,] copy = new bool[Board.Size, Board.Size];
        for (int row = 0; row < Board.Size; ++row)
        {
            for (int column = 0; column < Board.Size; ++column)
                copy[row, column] = state[row, column];
        }

        return copy;
    }

    private static void GetEmptyRegionData(bool[,] state, out int regionCount, out int smallPocketCells)
    {
        Array.Clear(floodVisited, 0, floodVisited.Length);
        regionCount = 0;
        smallPocketCells = 0;

        for (int row = 0; row < Board.Size; ++row)
        {
            for (int column = 0; column < Board.Size; ++column)
            {
                if (state[row, column] || floodVisited[row, column])
                    continue;

                ++regionCount;
                int regionSize = 0;
                int head = 0;
                int tail = 0;
                floodQueue[tail++] = new Vector2Int(column, row);
                floodVisited[row, column] = true;

                while (head < tail)
                {
                    Vector2Int point = floodQueue[head++];
                    ++regionSize;
                    EnqueueEmpty(state, point.x + 1, point.y, ref tail);
                    EnqueueEmpty(state, point.x - 1, point.y, ref tail);
                    EnqueueEmpty(state, point.x, point.y + 1, ref tail);
                    EnqueueEmpty(state, point.x, point.y - 1, ref tail);
                }

                if (regionSize <= 2)
                    smallPocketCells += regionSize;
            }
        }
    }

    private static void EnqueueEmpty(bool[,] state, int column, int row, ref int tail)
    {
        if (column < 0 || column >= Board.Size || row < 0 || row >= Board.Size ||
            state[row, column] || floodVisited[row, column])
            return;

        floodVisited[row, column] = true;
        floodQueue[tail++] = new Vector2Int(column, row);
    }
}
