using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>20 authored layouts/decks, not unrestricted random difficulty. Every entry has a playable witness.</summary>
public static class PopLevelCatalog
{
    public static void FillDefaults(PopGameplayConfiguration config)
    {
        config.journeyLevels.Clear();
        config.daily.levelIds.Clear();
        for (int index = 0; index < 20; ++index)
        {
            PopLevelDefinition level;
            if (index == 0) level = Rows(new[] { 1 }, 1, 2, false);
            else if (index == 1) level = Rows(new[] { 3 }, 3, 1, false);
            else if (index == 2) level = Rows(new[] { 1, 5 }, 2, 3, false);
            else if (index == 3) level = DoubleRows(2, false, false);
            else if (index == 4)
            {
                level = Rows(new[] { 1, 3, 5 }, 2, 2, false);
                level.targetCombo = 3;
            }
            else if (index < 10)
                level = Rows(index % 2 == 0 ? new[] { 1, 3, 5 } : new[] { 2, 6 },
                    2 + index % 3, index % 3, true);
            else if (index < 15) level = DoubleRows(index % 4, true, true);
            else level = Cross(index - 15);

            Transform(level, index % 2 != 0, index % 4 >= 2);
            level.id = "journey-" + (index + 1).ToString("00");
            level.title = index < 5 ? new[] { "First Pop", "A New Direction", "Make Room", "Double Delight", "Keep the Chain" }[index]
                : index < 10 ? "Bubble Trail " + (index - 4)
                : index < 15 ? "Double Trouble " + (index - 9) : "Crossroads " + (index - 14);
            level.description = index < 5 ? "Plan your placement and clear the marked lanes."
                : "Pop the bubbles by clearing their rows or columns. Pieces do not rotate.";
            level.threeStarMoves = level.verifiedSolution.Count;
            level.twoStarMoves = level.threeStarMoves + 2;
            level.moveLimit = level.threeStarMoves + (index < 5 ? 5 : index < 15 ? 4 : 3);
            // Extra fixed pieces allow alternate plans, but never change the verified starting deck.
            foreach (int filler in new[] { FindShape(new[,] { { 1, 1 } }), FindShape(new[,] { { 1 } }),
                         FindShape(new[,] { { 1, 1 }, { 1, 1 } }) })
                level.pieceSequence.Add(filler);
            config.journeyLevels.Add(level);
            if (index >= 5) config.daily.levelIds.Add(level.id);
        }

        config.tutorial.lesson = Tutorial();
        config.themes = new List<PopThemeDefinition>
        {
            Theme("sky", "Sky", 0, Color.white, new Color(0.04f, 0.73f, 0.78f),
                new[] { "35CFD9", "FFD75B", "A38CFF", "FF86B8", "67DEAF", "FF9965", "71B9FF", "D793F6" }),
            Theme("sunset", "Sunset", 10, new Color(1f, 0.76f, 0.74f), new Color(0.91f, 0.39f, 0.37f),
                new[] { "FFB26B", "FFE19B", "FF7D85", "CF8ACB", "EEA6A1", "FEC58C", "DB93B5", "FFC96E" }),
            Theme("night-glow", "Night Glow", 30, new Color(0.28f, 0.28f, 0.58f), new Color(0.57f, 0.37f, 0.87f),
                new[] { "62E7F2", "B295FF", "FF87DA", "71F3B3", "F6DD82", "7CB6FF", "D5A4FF", "FF9FAD" })
        };
        config.popEffects = new List<PopEffectDefinition>
        {
            new() { id = "classic", title = "Classic Pop", description = "Your original clear particles." },
            new() { id = "gold-burst", title = "Golden Burst", description = "Clear two lines in one move to unlock a radial golden burst.",
                requiredSingleClearLines = 2, radialBurst = true, particleMultiplier = 1.5f, speedMultiplier = 1.15f,
                lifetimeMultiplier = 1.15f, tint = new Color(1f, .82f, .25f), tintStrength = .7f }
        };
    }

    private static PopThemeDefinition Theme(string id, string title, int stars, Color tint, Color accent, string[] hexes)
    {
        var colors = new Color[hexes.Length];
        for (int i = 0; i < colors.Length; ++i) ColorUtility.TryParseHtmlString("#" + hexes[i], out colors[i]);
        return new PopThemeDefinition
        {
            id = id, title = title, requiredJourneyStars = stars, blockColors = colors,
            backgroundTint = tint, accent = accent, clearEffect = Color.Lerp(accent, Color.white, 0.35f),
            description = stars == 0 ? "Your bright everyday board." : "Earn " + stars + " Journey stars to unlock.",
            bubbleColor = new Color(1f, 0.87f, 0.28f)
        };
    }

    private static PopLevelDefinition Rows(int[] rows, int width, int gap, bool bubbles)
    {
        var level = new PopLevelDefinition { targetLines = rows.Length };
        foreach (int row in rows)
        {
            for (int column = 0; column < Board.Size; ++column)
            {
                if (column >= gap && column < gap + width) continue;
                bool bubble = bubbles && (column == (gap == 0 ? 6 : 0) || column == 7);
                Add(level, column, row, bubble);
            }
            int[,] shape = new int[1, width];
            for (int x = 0; x < width; ++x) shape[0, x] = 1;
            Move(level, shape, gap, row);
        }
        CountBubbles(level);
        return level;
    }

    private static PopLevelDefinition DoubleRows(int gap, bool bubbles, bool thirdRow)
    {
        var level = new PopLevelDefinition { targetLines = thirdRow ? 3 : 2, targetMultiClear = 2 };
        for (int row = 2; row <= 3; ++row)
            for (int column = 0; column < Board.Size; ++column)
                if (column < gap || column >= gap + 2)
                    Add(level, column, row, bubbles && (column == 7 || column == (gap == 0 ? 6 : 0)));
        Move(level, new[,] { { 1, 1 }, { 1, 1 } }, gap, 2);
        if (thirdRow)
        {
            for (int column = 0; column < Board.Size; ++column)
                if (column < 4 || column > 6) Add(level, column, 6, bubbles && (column == 0 || column == 7));
            Move(level, new[,] { { 1, 1, 1 } }, 4, 6);
        }
        CountBubbles(level);
        return level;
    }

    private static PopLevelDefinition Cross(int variant)
    {
        var level = new PopLevelDefinition { targetLines = 3, targetMultiClear = 2 };
        for (int x = 0; x < Board.Size; ++x)
            if (x != 2 && x != 3) Add(level, x, 2, x == 0);
        for (int y = 0; y < Board.Size; ++y)
            if (y != 2 && y != 3) Add(level, 2, y, y == 0 || y == 7);
        for (int x = 0; x < Board.Size; ++x)
            if (x < 4 || x > 6) Add(level, x, 6, x == 1);
        Move(level, new[,] { { 1, 1 }, { 1, 0 } }, 2, 2);
        Move(level, new[,] { { 1, 1, 1 } }, 4, 6);
        Move(level, new[,] { { 1 } }, 2, 6);
        // Later crossroads extend the dependency: clearing the column removes
        // one cell from these rows, so their final gaps are wider than they first look.
        if (variant >= 1)
        {
            for (int x = 0; x < Board.Size; ++x) if (x < 3 || x > 6) Add(level, x, 4, x == 0);
            Move(level, new[,] { { 1, 1, 1, 1, 1 } }, 2, 4); ++level.targetLines;
        }
        if (variant >= 2)
        {
            for (int x = 0; x < Board.Size; ++x) if (x < 3 || x > 6) Add(level, x, 0, x == 7);
            Move(level, new[,] { { 1, 1, 1, 1, 1 } }, 2, 0); ++level.targetLines;
        }
        if (variant >= 3)
        {
            for (int x = 0; x < Board.Size; ++x) if (x < 3 || x > 4) Add(level, x, 7, x == 0);
            Move(level, new[,] { { 1, 1, 1 } }, 2, 7); ++level.targetLines;
            level.targetCombo = 3;
        }
        if (variant == 4)
        {
            // A one-cell repair on the last lane makes the final lesson a seven-move route.
            level.startingCells.RemoveAll(cell => cell.position == new Vector2Int(7, 7));
            Move(level, new[,] { { 1 } }, 7, 7);
            level.targetCombo = 2;
        }
        CountBubbles(level);
        return level;
    }

    private static PopLevelDefinition Tutorial()
    {
        var lesson = new PopLevelDefinition { id = "tutorial-v1", title = "Learn to Pop", targetLines = 4, moveLimit = 3,
            threeStarMoves = 3, twoStarMoves = 3, description = "Three placements. Three useful skills." };
        for (int x = 0; x < Board.Size; ++x)
        {
            if (x != 2) Add(lesson, x, 1, false);
            if (x < 2 || x > 4) Add(lesson, x, 3, false);
            if (x < 3 || x > 4) { Add(lesson, x, 5, false); Add(lesson, x, 6, false); }
        }
        Move(lesson, new[,] { { 1 } }, 2, 1);
        Move(lesson, new[,] { { 1, 1, 1 } }, 2, 3);
        Move(lesson, new[,] { { 1, 1 }, { 1, 1 } }, 3, 5);
        return lesson;
    }

    private static void Add(PopLevelDefinition level, int x, int y, bool bubble)
    {
        PopStartingCell existing = level.startingCells.Find(cell => cell.position == new Vector2Int(x, y));
        if (existing != null) { existing.bubble |= bubble; return; }
        level.startingCells.Add(new PopStartingCell { position = new Vector2Int(x, y), colorId = (x + y) % 6, bubble = bubble });
    }

    private static void CountBubbles(PopLevelDefinition level)
    {
        level.targetBubbles = level.startingCells.FindAll(cell => cell.bubble).Count;
    }

    private static void Move(PopLevelDefinition level, int[,] shape, int x, int y)
    {
        int index = FindShape(shape);
        level.pieceSequence.Add(index);
        level.verifiedSolution.Add(new PopSolutionMove { shape = index, origin = new Vector2Int(x, y) });
    }

    public static int FindShape(int[,] desired)
    {
        for (int index = 0; index < Polyominos.Lenght; ++index)
        {
            int[,] shape = Polyominos.Get(index);
            if (shape.GetLength(0) != desired.GetLength(0) || shape.GetLength(1) != desired.GetLength(1)) continue;
            bool same = true;
            for (int row = 0; row < shape.GetLength(0); ++row)
                for (int column = 0; column < shape.GetLength(1); ++column)
                    same &= (shape[row, column] > 0) == (desired[row, column] > 0);
            if (same) return index;
        }
        throw new InvalidOperationException("Authored shape is absent from the catalogue.");
    }

    private static void Transform(PopLevelDefinition level, bool mirror, bool transpose)
    {
        Vector2Int Map(Vector2Int point)
        {
            if (mirror) point.x = Board.Size - 1 - point.x;
            return transpose ? new Vector2Int(point.y, point.x) : point;
        }
        foreach (PopStartingCell cell in level.startingCells) cell.position = Map(cell.position);
        for (int i = 0; i < level.verifiedSolution.Count; ++i)
        {
            PopSolutionMove move = level.verifiedSolution[i];
            int[,] oldShape = Polyominos.Get(move.shape);
            var mapped = new List<Vector2Int>();
            Vector2Int min = new(int.MaxValue, int.MaxValue), max = new(int.MinValue, int.MinValue);
            for (int row = 0; row < oldShape.GetLength(0); ++row)
                for (int col = 0; col < oldShape.GetLength(1); ++col)
                    if (oldShape[row, col] > 0)
                    {
                        Vector2Int p = Map(move.origin + new Vector2Int(col, row));
                        mapped.Add(p); min = Vector2Int.Min(min, p); max = Vector2Int.Max(max, p);
                    }
            var shape = new int[max.y - min.y + 1, max.x - min.x + 1];
            foreach (Vector2Int point in mapped) shape[point.y - min.y, point.x - min.x] = 1;
            move.shape = FindShape(shape); move.origin = min;
            level.pieceSequence[i] = move.shape;
        }
    }
}

/// <summary>Pure validation: exact fixed-deck availability, legal placements, unique bubbles and goal timing.</summary>
public static class PopLevelValidation
{
    public static bool Validate(PopLevelDefinition level, PopScoringSettings scoring, out string reason)
    {
        reason = "";
        if (level == null || string.IsNullOrWhiteSpace(level.id)) { reason = "Missing level/identity."; return false; }
        if (scoring == null || level.startingCells == null || level.pieceSequence == null || level.verifiedSolution == null ||
            level.pieceSequence.Count == 0 || level.verifiedSolution.Count == 0)
        { reason = "Missing scoring, cells, deck or playable solution."; return false; }
        if (level.moveLimit < 1 || level.threeStarMoves < 1 || level.twoStarMoves < level.threeStarMoves || level.twoStarMoves > level.moveLimit)
        { reason = "Invalid move/star thresholds."; return false; }
        if (level.targetLines < 0 || level.targetBubbles < 0 || level.targetScore < 0 || level.targetCombo < 0 || level.targetMultiClear < 0 ||
            level.targetLines + level.targetBubbles + level.targetScore + level.targetCombo + level.targetMultiClear == 0)
        { reason = "Goals must be non-negative and at least one must be positive."; return false; }
        bool[] occupied = new bool[64], bubbles = new bool[64];
        foreach (PopStartingCell cell in level.startingCells)
        {
            if (cell == null) { reason = "Null starting cell."; return false; }
            int x = cell.position.x, y = cell.position.y;
            if (x < 0 || x >= 8 || y < 0 || y >= 8 || occupied[y * 8 + x] || cell.colorId < 0)
            { reason = "Starting cell is outside the board, duplicated or has an invalid color."; return false; }
            occupied[y * 8 + x] = true; bubbles[y * 8 + x] = cell.bubble;
        }
        for (int lane = 0; lane < 8; ++lane)
        {
            bool row = true, column = true;
            for (int cell = 0; cell < 8; ++cell) { row &= occupied[lane * 8 + cell]; column &= occupied[cell * 8 + lane]; }
            if (row || column) { reason = "Starting board already has a full line; leave a gap for the player."; return false; }
        }
        foreach (int shape in level.pieceSequence)
            if (shape < 0 || shape >= Polyominos.Lenght) { reason = "Deck has an unknown shape."; return false; }
        int cursor = 0, lines = 0, popped = 0, score = 0, combo = 0, bestCombo = 0, largestClear = 0;
        var tray = new List<int>();
        foreach (PopSolutionMove move in level.verifiedSolution)
        {
            if (move == null) { reason = "Null solution move."; return false; }
            if (tray.Count == 0)
                for (int i = 0; i < 3 && cursor < level.pieceSequence.Count; ++i) tray.Add(level.pieceSequence[cursor++]);
            if (!tray.Remove(move.shape)) { reason = "Solution requests a piece absent from the current tray."; return false; }
            if (!ResolveMove(occupied, bubbles, move.shape, move.origin, scoring, ref lines, ref popped, ref score,
                    ref combo, ref bestCombo, ref largestClear))
            { reason = "Solution contains an illegal placement."; return false; }
            if (level.GoalsMet(lines, popped, score, bestCombo, largestClear))
            {
                int used = level.verifiedSolution.IndexOf(move) + 1;
                if (used > level.moveLimit || used > level.threeStarMoves) { reason = "Witness does not earn the declared three-star result."; return false; }
                reason = "Verified win in " + used + " moves; " + lines + " lines, " + popped + " bubbles, " + score + " points.";
                return true;
            }
        }
        reason = "Witness ends without meeting every goal.";
        return false;
    }

    public static bool ResolveMove(bool[] occupied, bool[] bubbles, int shapeIndex, Vector2Int origin, PopScoringSettings scoring,
        ref int lines, ref int popped, ref int score, ref int combo, ref int bestCombo, ref int largestClear)
    {
        if (shapeIndex < 0 || shapeIndex >= Polyominos.Lenght) return false;
        int[,] shape = Polyominos.Get(shapeIndex);
        int placed = 0;
        for (int row = 0; row < shape.GetLength(0); ++row)
            for (int col = 0; col < shape.GetLength(1); ++col)
                if (shape[row, col] > 0)
                {
                    int x = origin.x + col, y = origin.y + row;
                    if (x < 0 || x >= 8 || y < 0 || y >= 8 || occupied[y * 8 + x]) return false;
                    ++placed;
                }
        for (int row = 0; row < shape.GetLength(0); ++row)
            for (int col = 0; col < shape.GetLength(1); ++col)
                if (shape[row, col] > 0) occupied[(origin.y + row) * 8 + origin.x + col] = true;
        bool[] clear = new bool[64];
        int count = 0;
        for (int lane = 0; lane < 8; ++lane)
        {
            bool fullRow = true, fullColumn = true;
            for (int cell = 0; cell < 8; ++cell) { fullRow &= occupied[lane * 8 + cell]; fullColumn &= occupied[cell * 8 + lane]; }
            if (fullRow) { ++count; for (int x = 0; x < 8; ++x) clear[lane * 8 + x] = true; }
            if (fullColumn) { ++count; for (int y = 0; y < 8; ++y) clear[y * 8 + lane] = true; }
        }
        for (int i = 0; i < 64; ++i)
            if (clear[i]) { if (bubbles[i]) ++popped; occupied[i] = false; bubbles[i] = false; }
        combo = count > 0 ? combo + 1 : 0;
        bestCombo = Math.Max(bestCombo, combo); largestClear = Math.Max(largestClear, count); lines += count;
        float multiplier = Math.Min(scoring.maximumComboMultiplier, 1f + Math.Max(0, combo - 1) * scoring.comboBonusPerChain);
        score += placed * scoring.placementPointsPerCell + Mathf.RoundToInt(scoring.pointsPerLine * count * count * multiplier);
        return true;
    }
}
