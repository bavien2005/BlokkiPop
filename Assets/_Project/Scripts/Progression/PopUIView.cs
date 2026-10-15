using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PopUIPage { Home, Hud, Tutorial, Journey, Level, Daily, Rewards, Pause, Victory, Failure, ReplaceRun, InvalidSave }
[Serializable]
public sealed class PopUIElement
{
    public string key;
    public RectTransform rect;
    public TMP_Text text;
    public Button button;
    public Graphic graphic;
    [Tooltip("Disable to keep the text authored on this object. Leave enabled for scores, goals and translated configuration labels.")]
    public bool updateText = true;
}
/// <summary>Serialized references to real designer-owned UI. No runtime layout generation.</summary>
public sealed class PopUIView : MonoBehaviour
{
    public PopUIPage page;
    public TMP_Text title;
    public List<PopUIElement> elements = new();
    public PopUIElement Element(string key)
    {
        PopUIElement value = elements.Find(item => item != null && item.key == key);
        if (value == null || value.rect == null) throw new InvalidOperationException(name + ": missing UI binding '" + key + "'.");
        return value;
    }
    public void Text(string key, string value)
    { PopUIElement item = Element(key); if (item.updateText && item.text != null) item.text.text = value; }
    public void Visible(string key, bool value) => Element(key).rect.gameObject.SetActive(value);
}
