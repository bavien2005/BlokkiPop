using UnityEngine;

public class Cell : MonoBehaviour
{
    [SerializeField] private Sprite normal;

    [SerializeField] private Sprite highlight;


    private SpriteRenderer spriteRenderer;


    private Color baseColor = Color.white;

    private void Awake()
    {
        spriteRenderer = gameObject.GetComponent<SpriteRenderer>();

    }

    public void SetBaseColor(Color color)
    {
        this.baseColor = color;
    }



    public void Normal()
    {
        gameObject.SetActive(true);
        spriteRenderer.sprite = normal;

        spriteRenderer.color = new Color(
            baseColor.r,
            baseColor.g,
            baseColor.b,
            1f);
    }


    public void Highlight(Color highlightColor)
    {
        gameObject.SetActive(true);
        spriteRenderer.color = new Color(
            highlightColor.r,
            highlightColor.g,
            highlightColor.b,
            1f);
        spriteRenderer.sprite = highlight;
    }

    public void Hover()
    {
        gameObject.SetActive(true);
        spriteRenderer.color = new Color(
             baseColor.r,
             baseColor.g,
             baseColor.b,
             0.5f);
        spriteRenderer.sprite = normal;
    }

    public void Hide()
    {
        gameObject?.SetActive(false);
    }
}
