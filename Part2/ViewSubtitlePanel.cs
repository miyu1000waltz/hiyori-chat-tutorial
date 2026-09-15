using TMPro;
using UnityEngine;

/// <summary>
/// 字幕パネル(View)。Show()で渡されたテキストを画面下部に表示する。
/// </summary>
public class ViewSubtitlePanel : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private Vector2 anchoredPosition = new Vector2(0f, 350f);
    [SerializeField] private Vector2 sizeDelta = new Vector2(1000f, 200f);
    [SerializeField] private float fontSize = 100f;
    [SerializeField] private Color textColor = new Color32(0x2B, 0x2B, 0x2B, 0xFF);

    private TMP_Text _text;

    void Awake()
    {
        // 画面下端を基準(Anchor/Pivot: Bottom-Center)に配置。
        // Constant Pixel Size Canvasのため、AnchoredPositionはそのまま実画面ピクセル数。
        var rect              = GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0f);
        rect.anchorMax        = new Vector2(0.5f, 0f);
        rect.pivot            = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta        = sizeDelta;

        _text                = gameObject.AddComponent<TextMeshProUGUI>();
        _text.text           = "";
        _text.fontSize       = fontSize;
        _text.color          = textColor;
        _text.alignment      = TextAlignmentOptions.Center;
        _text.overflowMode   = TextOverflowModes.Truncate;
        _text.raycastTarget  = false;  // 下にあるInputFieldなどへのクリックをブロックしないようにする

        if (fontAsset != null)
            _text.font = fontAsset;
    }

    public void Show(string text)
    {
        if (_text == null) return;
        _text.text = text;
    }
}
