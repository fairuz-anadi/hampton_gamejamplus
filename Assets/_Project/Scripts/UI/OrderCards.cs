using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Puts each issued order on a free card (at most 2 are active at once) and stamps it when it's resolved.
/// Owner: Samprity. Listens to GameEvents.OrderIssued / OrderResolved only.
/// The stack keeps a player's live orders on top, the crew's under them and stamped ones last, easing into place.
/// Notification cards fold into circles; Tab (or View on a controller) opens the player's folded cards, or folds
/// them again if they're open (Riham). Each card takes the room its current size needs.
/// </summary>
public class OrderCards : MonoBehaviour
{
    public OrderCardUI[] cards = new OrderCardUI[0];
    [Tooltip("Distance between stacked cards, in canvas units.")]
    public float spacing = 148f;
    [Tooltip("Seconds a card takes to move to its new place in the stack.")]
    [Range(0.01f, 1f)] public float reorderSmoothTime = 0.18f;

    float[] velocity = new float[0];

    void Update()
    {
        if (TabPressed() && Time.timeScale > 0f)
        {
            bool anyClosed = false;
            foreach (var c in cards) if (c.Order != null && c.ForPlayer && c.notification && !c.IsOpen) anyClosed = true;
            foreach (var c in cards) if (c.Order != null && c.ForPlayer && c.notification) c.SetOpen(anyClosed);
        }

        if (velocity.Length != cards.Length) velocity = new float[cards.Length];
        // Rank: player's live order 0, crew's live order 1, leaving or empty 2; ties keep their card order.
        var order = new int[cards.Length];
        for (int i = 0; i < cards.Length; i++) order[i] = i;
        System.Array.Sort(order, (a, b) =>
        {
            int r = Rank(cards[a]).CompareTo(Rank(cards[b]));
            return r != 0 ? r : a.CompareTo(b);
        });
        float y = 0f;
        for (int slot = 0; slot < order.Length; slot++)
        {
            int i = order[slot];
            var rt = (RectTransform)cards[i].transform;
            var p = rt.anchoredPosition;
            p.y = Mathf.SmoothDamp(p.y, y, ref velocity[i], reorderSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            rt.anchoredPosition = p;
            y -= cards[i].notification ? cards[i].StackHeight + 16f : spacing;
        }
    }

    static bool TabPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.tabKey.wasPressedThisFrame) return true;
        foreach (var pad in Gamepad.all) if (pad.selectButton.wasPressedThisFrame) return true;
        return false;
    }

    static int Rank(OrderCardUI c) => c.Order == null ? 2 : c.ForPlayer ? 0 : 1;

    void OnEnable()
    {
        GameEvents.OrderIssued += OnIssued;
        GameEvents.OrderResolved += OnResolved;
        // Orders already running when the HUD appears (e.g. after a scene reload).
        if (OrderDirector.Instance != null)
            foreach (var o in OrderDirector.Instance.Active) OnIssued(o);
    }

    void OnDisable()
    {
        GameEvents.OrderIssued -= OnIssued;
        GameEvents.OrderResolved -= OnResolved;
    }

    void OnIssued(ActiveOrder o)
    {
        foreach (var c in cards) if (c.Order == o) return;
        OrderCardUI target = null;
        foreach (var c in cards) if (c.IsFree) { target = c; break; }
        // Both busy (a card still showing its stamp): reuse the one not holding a live order.
        if (target == null) foreach (var c in cards) if (c.Order == null) { target = c; break; }
        if (target == null && cards.Length > 0) target = cards[0];
        target?.Bind(o);
    }

    void OnResolved(ActiveOrder o, Choice choice)
    {
        foreach (var c in cards) if (c.Order == o) c.Resolve(choice);
    }
}
