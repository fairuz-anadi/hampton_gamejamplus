using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A small name tag over each crewmate's head: a dot in their seat colour and their name ("MABEL"). Fades with
/// distance and hides when they're off screen or behind the camera.
/// The player's own character has none. Built in code; spawned by GameSystems in Ship. Owner: Anadi.
/// </summary>
public class CrewNameTags : MonoBehaviour
{
    [Tooltip("Metres above the crewmate's feet.")]
    public float heightAbove = 2.15f;
    [Tooltip("Fully visible up to this distance from the camera, gone by fadeOut.")]
    public float fadeIn = 14f, fadeOut = 26f;

    class Tag { public ICrewActor actor; public Transform body; public RectTransform rect; public CanvasGroup group; public Text label; public Image dot; }

    RectTransform canvasRect;
    readonly List<Tag> tags = new List<Tag>();
    Font font;

    void Start()
    {
        var theme = UiTheme.Load();
        font = theme != null && theme.body != null ? theme.body : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvas = new GameObject("[CrewNameTags]", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5; // under the HUD and every screen
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = (RectTransform)canvas.transform;
    }

    void OnDestroy()
    {
        if (canvasRect != null) Destroy(canvasRect.gameObject);
    }

    void LateUpdate()
    {
        if (canvasRect == null) return;
        SyncTags();
        var cam = Camera.main;
        var gm = GameManager.Instance;
        bool playing = gm != null && gm.State >= GameState.Part1 && gm.State <= GameState.Iceberg
                       && !(PauseMenu.Instance != null && PauseMenu.Instance.IsPaused)
                       && !IntroCard.IsShowing && !ChartView.IsOpen; // nothing showing through the cards
        foreach (var t in tags)
        {
            if (t.body == null || cam == null || !playing) { t.group.alpha = 0f; continue; }
            Vector3 world = t.body.position + Vector3.up * heightAbove;
            Vector3 sp = cam.WorldToScreenPoint(world);
            float dist = Vector3.Distance(cam.transform.position, world);
            float a = sp.z <= 0f ? 0f : 1f - Mathf.InverseLerp(fadeIn, fadeOut, dist);
            t.group.alpha = Mathf.MoveTowards(t.group.alpha, a, Time.unscaledDeltaTime * 6f);
            if (sp.z <= 0f) continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out var local);
            t.rect.anchoredPosition = local;
        }
    }

    /// <summary>One tag per bot or other player; added when crew spawn, dropped when they're gone.</summary>
    void SyncTags()
    {
        tags.RemoveAll(t => { if (t.body != null) return false; Destroy(t.rect.gameObject); return true; });
        int humans = 0;
        foreach (var c in CrewRegistry.All) if (!c.IsBot) humans++;
        foreach (var c in CrewRegistry.All)
        {
            if (!c.IsBot && humans <= 1) continue; // solo: no tag over yourself
            if (tags.Exists(t => t.actor == c)) continue;
            var body = (c as Component)?.transform;
            if (body != null) tags.Add(Make(c, body));
        }
    }

    Tag Make(ICrewActor c, Transform body)
    {
        var rect = new GameObject("Tag", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        rect.SetParent(canvasRect, false);
        rect.sizeDelta = new Vector2(240f, 34f);
        var group = rect.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        var plate = new GameObject("Plate", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        plate.transform.SetParent(rect, false);
        plate.color = new Color(0.03f, 0.04f, 0.07f, 0.55f);
        plate.raycastTarget = false;
        var pr = plate.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(150f, 30f);

        var dot = new GameObject("Seat", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        dot.transform.SetParent(rect, false);
        dot.color = c.Seat >= 0 && c.Seat < CrewRoster.SeatColors.Length ? CrewRoster.SeatColors[c.Seat] : Color.white;
        dot.raycastTarget = false;
        var dr = dot.rectTransform;
        dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0.5f);
        dr.anchoredPosition = new Vector2(-62f, 0f);
        dr.sizeDelta = new Vector2(12f, 12f);

        var label = new GameObject("Name", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(rect, false);
        label.font = font;
        label.fontSize = 20;
        label.color = new Color(0.97f, 0.93f, 0.84f);
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.raycastTarget = false;
        label.text = CrewRegistry.CrewName(c).ToUpperInvariant();
        var lr = label.rectTransform;
        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
        lr.anchoredPosition = new Vector2(8f, 0f);
        lr.sizeDelta = new Vector2(200f, 30f);
        label.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);

        return new Tag { actor = c, body = body, rect = rect, group = group, label = label, dot = dot };
    }
}
