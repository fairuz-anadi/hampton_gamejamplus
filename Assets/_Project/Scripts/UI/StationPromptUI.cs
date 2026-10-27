using UnityEngine;
using UnityEngine.UI;

/// <summary>One floating "Hold [E] Shovel coal" label over a station. Owner: Samprity.</summary>
public class StationPromptUI : MonoBehaviour
{
    public CanvasGroup group;
    public RectTransform rect;
    public Text title, action, key;
    public Image keycap;

    float alpha, target;

    public void Show(Vector2 canvasPos, string titleText, string actionText, string keyText, Color tint)
    {
        rect.anchoredPosition = canvasPos;
        title.text = titleText;
        action.text = actionText;
        key.text = keyText;
        keycap.color = tint;
        key.color = tint;
        target = 1f;
    }

    public void Hide() => target = 0f;

    public void SnapIn() { alpha = target = 1f; group.alpha = 1f; }

    void Update()
    {
        alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime * 6f);
        group.alpha = alpha;
    }
}
