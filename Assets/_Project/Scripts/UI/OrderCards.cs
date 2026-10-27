using UnityEngine;

/// <summary>
/// Puts each issued order on a free card (at most 2 are active at once) and stamps it when it's resolved.
/// Owner: Samprity. Listens to GameEvents.OrderIssued / OrderResolved only.
/// </summary>
public class OrderCards : MonoBehaviour
{
    public OrderCardUI[] cards = new OrderCardUI[0];

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
