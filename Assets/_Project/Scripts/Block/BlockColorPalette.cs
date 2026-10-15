using UnityEngine;

[CreateAssetMenu(
    fileName = "BlockColorPalette",
    menuName = "Block Puzzle/Block Color Palette"
)]
public class BlockColorPalette : ScriptableObject
{
    [SerializeField] private Color[] colors;

    public int Count => colors == null ? 0 : colors.Length;

    public Color GetColor(int colorId)
    {
        if (colorId < 0 || colorId >= Count)
            return Color.white;

        PopThemeDefinition theme = Application.isPlaying ? PopGameplaySession.Instance?.SelectedTheme : null;
        return theme?.blockColors != null && theme.blockColors.Length > 0
            ? theme.blockColors[colorId % theme.blockColors.Length] : colors[colorId];
    }

    public int GetRandomColorId()
    {
        if (Count == 0)
            return -1;

        return Random.Range(0, Count);
    }
}
