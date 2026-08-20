using UnityEngine;

public class Blocks : MonoBehaviour
{
    [SerializeField] private Block[] blocks;

    [SerializeField] private Board board;

    private int[] polyominoIndexes;

    [SerializeField] private BlockColorPalette colorPalette;

    private int blockCount = 0;

    void Start()
    {
        var blockWidth = (float)Board.Size / blocks.Length;
        var cellSize = (float)Board.Size / (Block.Size * blocks.Length + blocks.Length + 1);
        for (var i = 0; i < blocks.Length; ++i) {

            blocks[i].transform.localPosition = new(
                blockWidth * (i + 0.5f), 
                -0.25f - cellSize * 4.0f, 
                0.0f);
            blocks[i].transform.localScale =  new(
                cellSize, cellSize, cellSize);
            blocks[i].Initialize();
        }

        polyominoIndexes = new int[blocks.Length];
        Generate();
    }


    private void Generate()
    {
        for (var i = 0; i <blocks.Length; ++i)
        {
            polyominoIndexes[i] = Random.Range(0, Polyominos.Lenght);

            int colorId = colorPalette.GetRandomColorId();

            Color color = colorPalette.GetColor(colorId);

            blocks[i].SetColor(colorId, color);

            blocks[i].gameObject.SetActive(true);

            blocks[i].Show(polyominoIndexes[i]);

            ++blockCount;
        }
    }


    public void Remove()
    {
        --blockCount; 
        if(blockCount <= 0)
        {
            blockCount = 0;
            Generate();
        }

        var lose = true; 
        for ( var i = 0; i <blocks.Length; ++i)
        {
            if (blocks[i].gameObject.activeSelf && board.CheckPlace(polyominoIndexes[i]))
            {
                lose = false;
                break;
            }
        }

        if (lose)
        {
            Debug.Log("Thua cuộc ");
        }
    }

    public void ResetBlocksSortingOrder()
    {
        for(var i = 0; i < blocks.Length; ++i)
        {
            blocks[i].SetSortingGroup(0);
        }
    }
}
