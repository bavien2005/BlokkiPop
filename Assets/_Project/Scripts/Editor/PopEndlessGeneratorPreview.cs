using UnityEditor;
using UnityEngine;

/// <summary>Paint a carry-over board and inspect/replay the actual generated puzzle without Play.</summary>
public sealed class PopEndlessGeneratorPreview : EditorWindow
{
    private int seed = 293, wave = 1, step, popped;
    private int[] carry;
    private bool[] occupied, targets;
    private PopLevelDefinition puzzle;
    private string status;
    private Vector2 scroll;
    [MenuItem("Tools/Blokki Pop/Gameplay/Preview Endless Pop Generator")]
    public static void Open() => GetWindow<PopEndlessGeneratorPreview>("Endless Pop Preview");
    private void OnEnable() { carry = new int[64]; for (int i = 0; i < 64; ++i) carry[i] = -1; }
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        var config = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
        EditorGUILayout.HelpBox("Click the input grid to paint leftover blocks. Generate copies this board; it never changes your actual scene, run or save. The second grid replays the verified solution.", MessageType.Info);
        seed = EditorGUILayout.IntField("Seed", seed); wave = EditorGUILayout.IntSlider("Wave", wave, 1, 100);
        if (GUILayout.Button("Select Endless settings in Inspector")) Selection.activeObject = config;
        EditorGUILayout.LabelField("Carry-over input (click to paint)");
        DrawGrid(false);
        if (GUILayout.Button("Generate and verify"))
        {
            var random = new PopRandom((uint)seed); puzzle = PopEndlessGenerator.Generate(carry, wave, 8, config.endless, ref random);
            status = PopEndlessGenerator.Validate(puzzle, out string reason) ? reason : "INVALID: " + reason;
            RestartReplay();
        }
        if (puzzle != null)
        {
            EditorGUILayout.HelpBox(status + "\nMoves: " + puzzle.moveLimit + " | Bubbles: " + puzzle.targetBubbles + " | Replayed: " + step + " | Popped: " + popped, MessageType.Info);
            EditorGUILayout.LabelField(PopEndlessGenerator.Evaluate(puzzle, config.endless).ToString());
            DrawGrid(true);
            EditorGUILayout.LabelField("Entire fixed deck (groups of three)");
            for (int first = 0; first < puzzle.pieceSequence.Count; first += 3)
            {
                EditorGUILayout.BeginHorizontal();
                for (int i = first; i < Mathf.Min(first + 3, puzzle.pieceSequence.Count); ++i)
                { Rect slot = GUILayoutUtility.GetRect(85, 75); DrawShape(slot, puzzle.pieceSequence[i]); }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Replay one solution move") && step < puzzle.verifiedSolution.Count)
            {
                var move = puzzle.verifiedSolution[step++]; int lines = 0, score = 0, combo = 0, best = 0, multi = 0;
                PopLevelValidation.ResolveMove(occupied, targets, move.shape, move.origin, config.authoredScoring, ref lines, ref popped, ref score, ref combo, ref best, ref multi);
            }
            if (GUILayout.Button("Restart preview replay")) RestartReplay();
        }
        EditorGUILayout.EndScrollView();
    }
    private void RestartReplay()
    {
        occupied = new bool[64]; targets = new bool[64]; step = popped = 0;
        foreach (var cell in puzzle.startingCells) { int i = cell.position.y * 8 + cell.position.x; occupied[i] = true; targets[i] = cell.bubble; }
    }
    private void DrawGrid(bool replay)
    {
        for (int y = 7; y >= 0; --y)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < 8; ++x)
            {
                int i = y * 8 + x; Color previous = GUI.backgroundColor;
                GUI.backgroundColor = replay ? targets[i] ? Color.yellow : occupied[i] ? Color.cyan : Color.gray : carry[i] >= 0 ? Color.cyan : Color.gray;
                if (GUILayout.Button(replay && targets[i] ? "O" : "", GUILayout.Width(30), GUILayout.Height(30)) && !replay) carry[i] = carry[i] < 0 ? 0 : -1;
                GUI.backgroundColor = previous;
            }
            EditorGUILayout.EndHorizontal();
        }
    }
    private static void DrawShape(Rect slot, int index)
    {
        int[,] shape = Polyominos.Get(index); float size = Mathf.Min((slot.width - 10) / shape.GetLength(1), (slot.height - 10) / shape.GetLength(0));
        Vector2 start = slot.center - new Vector2(shape.GetLength(1), shape.GetLength(0)) * size * .5f;
        for (int y = 0; y < shape.GetLength(0); ++y) for (int x = 0; x < shape.GetLength(1); ++x)
            if (shape[y, x] > 0) EditorGUI.DrawRect(new Rect(start.x + x * size, start.y + (shape.GetLength(0) - 1 - y) * size, size - 2, size - 2), Color.cyan);
    }
}
