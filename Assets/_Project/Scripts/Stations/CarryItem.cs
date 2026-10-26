using UnityEngine;

/// <summary>
/// A pick-up/drop prop for one specific carry order. Only the crew member currently holding that
/// order can pick it up; standing at the order's deliverTo station while carrying it counts as
/// holding Interact there, so delivery uses the same work-progress path as any other station.
/// Owner: Rabib.
/// </summary>
public class CarryItem : MonoBehaviour
{
    [SerializeField] OrderData order;
    [SerializeField] float pickupRange = 1.5f;
    [SerializeField] Vector3 carryOffset = new Vector3(0f, 1.2f, 0.6f);

    Vector3 homePosition;
    Transform carrier;
    ICrewActor carrierActor;
    Station deliveryStation;

    public bool IsCarried => carrier != null;

    void Awake() => homePosition = transform.position;

    void OnEnable() => GameEvents.OrderIssued += OnOrderIssued;
    void OnDisable() => GameEvents.OrderIssued -= OnOrderIssued;

    void OnOrderIssued(ActiveOrder active)
    {
        if (active.Data != order) return;
        Drop();
        transform.position = homePosition;
    }

    void Update()
    {
        if (carrier == null) return;

        transform.position = carrier.position + carrier.TransformDirection(carryOffset);

        if (deliveryStation != null)
        {
            bool inRange = Vector3.Distance(transform.position, deliveryStation.Position) <= pickupRange;
            deliveryStation.Hold(carrierActor, inRange);
        }
    }

    public bool TryPickUp(ICrewActor actor, Transform actorTransform)
    {
        if (carrier != null) return false;
        if (Vector3.Distance(transform.position, actorTransform.position) > pickupRange) return false;

        var active = OrderDirector.Instance?.GetOrderFor(actor);
        if (active == null || active.Data != order) return false;

        carrier = actorTransform;
        carrierActor = actor;
        deliveryStation = StationRegistry.Get(order.deliverTo) as Station;
        return true;
    }

    public void Drop()
    {
        deliveryStation?.Hold(carrierActor, false);
        carrier = null;
        carrierActor = null;
        deliveryStation = null;
    }
}
