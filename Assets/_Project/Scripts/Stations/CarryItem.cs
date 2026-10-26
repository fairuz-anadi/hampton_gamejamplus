using UnityEngine;

/// <summary>
/// A pick-up/drop prop for one specific carry order. Only the crew member currently holding that
/// order can pick it up; standing at the order's deliverTo station while carrying it counts as
/// delivering there, so delivery uses the same work-progress path as any other station (and needs
/// only the carrier, even at a two-person station). When the order ends, the item goes back home.
/// Owner: Rabib.
/// </summary>
public class CarryItem : MonoBehaviour, ICarryItem
{
    [SerializeField] OrderData order;
    [SerializeField] float pickupRange = 1.5f;
    [SerializeField] Vector3 carryOffset = new Vector3(0f, 1.2f, 0.6f);

    Vector3 homePosition;
    Quaternion homeRotation;
    Transform carrier;
    ICrewActor carrierActor;
    Station deliveryStation;

    public bool IsCarried => carrier != null;
    public Vector3 Position => transform.position;
    public OrderData Order => order;

    void Awake()
    {
        homePosition = transform.position;
        homeRotation = transform.rotation;
    }

    void OnEnable()
    {
        GameEvents.OrderIssued += OnOrderIssued;
        GameEvents.OrderResolved += OnOrderResolved;
    }

    void OnDisable()
    {
        GameEvents.OrderIssued -= OnOrderIssued;
        GameEvents.OrderResolved -= OnOrderResolved;
    }

    void OnOrderIssued(ActiveOrder active)
    {
        if (active.Data == order) ReturnHome();
    }

    // Expired, refused or delivered: nobody should be left holding a dead order's item.
    void OnOrderResolved(ActiveOrder active, Choice choice)
    {
        if (active.Data == order) ReturnHome();
    }

    void Update()
    {
        if (carrier == null) return;

        transform.position = carrier.position + carrier.TransformDirection(carryOffset);

        if (deliveryStation != null)
        {
            bool inRange = Vector3.Distance(carrier.position, deliveryStation.Position) <= pickupRange + 1f;
            deliveryStation.HoldDelivery(carrierActor, inRange);
        }
    }

    public bool TryPickUp(ICrewActor actor, Transform actorTransform)
    {
        if (carrier != null) return false;
        if (Vector3.Distance(transform.position, actorTransform.position) > pickupRange + 1f) return false;

        var active = OrderDirector.Instance?.GetOrderFor(actor);
        if (active == null || active.Data != order) return false;

        carrier = actorTransform;
        carrierActor = actor;
        deliveryStation = StationRegistry.Get(order.deliverTo) as Station;
        return true;
    }

    public void Drop()
    {
        deliveryStation?.HoldDelivery(carrierActor, false);
        carrier = null;
        carrierActor = null;
        deliveryStation = null;
    }

    void ReturnHome()
    {
        Drop();
        transform.SetPositionAndRotation(homePosition, homeRotation);
    }
}
