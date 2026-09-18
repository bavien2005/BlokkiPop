using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum PolyominoDifficulty
{
    Easy,
    Standard,
    Hard
}

public static class Polyominos
{
    // The catalogue contains every free polyomino made from one to five cells.
    // Rotations and mirror variants are generated because the player cannot
    // rotate a block while dragging it.
    private static readonly int[][,] polyominos;
    private static readonly PolyominoDifficulty[] difficulties;

    static Polyominos()
    {
        List<int[,]> generatedShapes = new();
        List<PolyominoDifficulty> generatedDifficulties = new();
        HashSet<string> catalogueKeys = new();

        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1 } }, PolyominoDifficulty.Easy);
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 1 } }, PolyominoDifficulty.Easy);
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 1, 1 } }, PolyominoDifficulty.Easy);
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 0 }, { 1, 1 } }, PolyominoDifficulty.Easy);
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 1 }, { 1, 1 } }, PolyominoDifficulty.Easy);

        // All free tetrominoes: I, O, T, L/J and S/Z.
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 1, 1, 1 } }, PolyominoDifficulty.Standard);
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 0 }, { 1, 0 }, { 1, 1 } }, PolyominoDifficulty.Standard);
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 1, 1 }, { 0, 1, 0 } }, PolyominoDifficulty.Standard);
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 0, 1, 1 }, { 1, 1, 0 } }, PolyominoDifficulty.Standard);

        // All twelve free pentominoes: F, I, L, P, N, T, U, V, W, X, Y and Z.
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 0, 1, 1 }, { 1, 1, 0 }, { 0, 1, 0 } }, PolyominoDifficulty.Hard); // F
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 1, 1, 1, 1 } }, PolyominoDifficulty.Hard); // I
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 0 }, { 1, 0 }, { 1, 0 }, { 1, 1 } }, PolyominoDifficulty.Hard); // L
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 1 }, { 1, 1 }, { 1, 0 } }, PolyominoDifficulty.Hard); // P
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 0, 1, 1, 0 }, { 1, 1, 0, 0 }, { 1, 0, 0, 0 } }, PolyominoDifficulty.Hard); // N
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 1, 1 }, { 0, 1, 0 }, { 0, 1, 0 } }, PolyominoDifficulty.Hard); // T
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 0, 1 }, { 1, 1, 1 } }, PolyominoDifficulty.Hard); // U
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 0, 0 }, { 1, 0, 0 }, { 1, 1, 1 } }, PolyominoDifficulty.Hard); // V
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 0, 0 }, { 1, 1, 0 }, { 0, 1, 1 } }, PolyominoDifficulty.Hard); // W
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 0, 1, 0 }, { 1, 1, 1 }, { 0, 1, 0 } }, PolyominoDifficulty.Hard); // X
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 0, 1 }, { 1, 1 }, { 0, 1 }, { 0, 1 } }, PolyominoDifficulty.Hard); // Y
        AddAllOrientations(generatedShapes, generatedDifficulties, catalogueKeys,
            new int[,] { { 1, 1, 0 }, { 0, 1, 0 }, { 0, 1, 1 } }, PolyominoDifficulty.Hard); // Z

        polyominos = generatedShapes.ToArray();
        difficulties = generatedDifficulties.ToArray();
    }

    public static int[,] Get(int index) => polyominos[index];

    public static PolyominoDifficulty GetDifficulty(int index) => difficulties[index];

    public static int Lenght => polyominos.Length;

    private static void AddAllOrientations(
        List<int[,]> shapes,
        List<PolyominoDifficulty> shapeDifficulties,
        HashSet<string> catalogueKeys,
        int[,] source,
        PolyominoDifficulty difficulty)
    {
        int[,] rotated = Normalize(source);
        for (int rotation = 0; rotation < 4; ++rotation)
        {
            AddIfUnique(shapes, shapeDifficulties, catalogueKeys, rotated, difficulty);
            AddIfUnique(shapes, shapeDifficulties, catalogueKeys, MirrorHorizontally(rotated), difficulty);
            rotated = RotateClockwise(rotated);
        }
    }

    private static void AddIfUnique(
        List<int[,]> shapes,
        List<PolyominoDifficulty> shapeDifficulties,
        HashSet<string> catalogueKeys,
        int[,] shape,
        PolyominoDifficulty difficulty)
    {
        int[,] normalized = Normalize(shape);
        string key = ToKey(normalized);
        if (!catalogueKeys.Add(key))
            return;

        shapes.Add(normalized);
        shapeDifficulties.Add(difficulty);
    }

    private static int[,] RotateClockwise(int[,] shape)
    {
        int rows = shape.GetLength(0);
        int columns = shape.GetLength(1);
        int[,] rotated = new int[columns, rows];

        for (int row = 0; row < rows; ++row)
        {
            for (int column = 0; column < columns; ++column)
                rotated[column, rows - 1 - row] = shape[row, column];
        }

        return Normalize(rotated);
    }

    private static int[,] MirrorHorizontally(int[,] shape)
    {
        int rows = shape.GetLength(0);
        int columns = shape.GetLength(1);
        int[,] mirrored = new int[rows, columns];

        for (int row = 0; row < rows; ++row)
        {
            for (int column = 0; column < columns; ++column)
                mirrored[row, columns - 1 - column] = shape[row, column];
        }

        return Normalize(mirrored);
    }

    private static int[,] Normalize(int[,] shape)
    {
        int rows = shape.GetLength(0);
        int columns = shape.GetLength(1);
        int minRow = rows;
        int maxRow = -1;
        int minColumn = columns;
        int maxColumn = -1;

        for (int row = 0; row < rows; ++row)
        {
            for (int column = 0; column < columns; ++column)
            {
                if (shape[row, column] <= 0)
                    continue;

                minRow = Mathf.Min(minRow, row);
                maxRow = Mathf.Max(maxRow, row);
                minColumn = Mathf.Min(minColumn, column);
                maxColumn = Mathf.Max(maxColumn, column);
            }
        }

        if (maxRow < 0)
            return new int[1, 1];

        int[,] normalized = new int[maxRow - minRow + 1, maxColumn - minColumn + 1];
        for (int row = minRow; row <= maxRow; ++row)
        {
            for (int column = minColumn; column <= maxColumn; ++column)
                normalized[row - minRow, column - minColumn] = shape[row, column];
        }

        return normalized;
    }

    private static string ToKey(int[,] shape)
    {
        StringBuilder key = new();
        int rows = shape.GetLength(0);
        int columns = shape.GetLength(1);
        key.Append(rows).Append('x').Append(columns).Append(':');

        for (int row = 0; row < rows; ++row)
        {
            for (int column = 0; column < columns; ++column)
                key.Append(shape[row, column]);
        }

        return key.ToString();
    }
}
