using UnityEngine;

namespace ValetGetaway
{
    public sealed class HotelGuestCar : MonoBehaviour
    {
        public enum ArrivalState { Scheduled, Arriving, Waiting, HandedOver, Sold }
        [SerializeField] private Transform guest;
        [SerializeField] private Vector3 entryPoint;
        [SerializeField] private Vector3 queuePoint;
        [SerializeField] private float arrivalDelay;
        [SerializeField] private float arrivalSpeed = 5f;
        [SerializeField] private int salePrice = 2500;
        [SerializeField] private string guestName = "Guest";
        private Rigidbody body;
        private ArcadeVehicleController controller;
        private VehicleInteraction interaction;
        private float readyAt;
        private Renderer[] visuals;
        private bool reachedApproach;
        [SerializeField] private Vector3[] arrivalRoute;
        [SerializeField] private Vector3 hotelDoor = new Vector3(-10f,1.2f,-13f);
        [SerializeField] private float parkingHeading;
        [SerializeField] private Vector3 guestOffset = new Vector3(-3.2f,1f,0);
        private int routeIndex;
        [SerializeField] private int initialArrivalWaypoint;
        public ArrivalState State { get; private set; }
        public bool KeysReceived => State == ArrivalState.HandedOver;
        public int SalePrice => salePrice;
        public string GuestName => guestName;
        public Transform Guest => guest;
        public Vector3 QueuePoint => queuePoint;
        public Vector3[] ArrivalRoute => arrivalRoute;

        public void ConfigureRoute(Vector3[] route, Vector3 door, float heading, Vector3 offset, int initialWaypoint=0)
        {
            arrivalRoute=route; hotelDoor=door; parkingHeading=heading; guestOffset=offset;
            initialArrivalWaypoint=initialWaypoint; arrivalSpeed=8f;
        }

        public void Configure(Transform npc, Vector3 entry, Vector3 queue, float delay, int price, string customer)
        {
            guest = npc; entryPoint = entry; queuePoint = queue;
            arrivalDelay = delay; salePrice = price; guestName = customer;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            controller = GetComponent<ArcadeVehicleController>();
            interaction = GetComponent<VehicleInteraction>();
            visuals = GetComponentsInChildren<Renderer>();
        }

        private void Start()
        {
            ScheduleArrival(arrivalDelay);
            if(arrivalRoute!=null && initialArrivalWaypoint>0 && initialArrivalWaypoint<arrivalRoute.Length-1)
            {
                body.position=arrivalRoute[initialArrivalWaypoint]; routeIndex=initialArrivalWaypoint+1;
                body.rotation=Quaternion.LookRotation(arrivalRoute[routeIndex]-body.position);
                transform.SetPositionAndRotation(body.position,body.rotation);
            }
        }

        public void ScheduleArrival(float delay)
        {
            gameObject.SetActive(true);
            State = ArrivalState.Scheduled;
            readyAt = Time.time + delay;
            reachedApproach = false;
            routeIndex = 1;
            controller.HasDriver = false;
            controller.enabled = false;
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true;
            bool routed=arrivalRoute!=null && arrivalRoute.Length>1;
            body.position = routed ? arrivalRoute[0] : new Vector3(0f, entryPoint.y, entryPoint.z);
            body.rotation = routed ? Quaternion.LookRotation(arrivalRoute[1]-arrivalRoute[0]) : Quaternion.identity;
            transform.SetPositionAndRotation(body.position, body.rotation);
            foreach (Renderer visual in visuals) visual.enabled = false;
            GetComponent<Collider>().enabled = false;
            guest.gameObject.SetActive(false);
        }

        private void FixedUpdate()
        {
            if (State == ArrivalState.Scheduled && Time.time >= readyAt)
            {
                // Wait until the spawn lane is free before introducing another car.
                if (Physics.OverlapBox(body.position + Vector3.up * 0.6f, new Vector3(1.2f, 0.5f, 2.5f)).Length > 0) return;
                foreach (Renderer visual in visuals) visual.enabled = true;
                GetComponent<Collider>().enabled = true;
                guest.gameObject.SetActive(true);
                State = ArrivalState.Arriving;
            }
            if (State != ArrivalState.Arriving) return;
            guest.position = transform.TransformPoint(new Vector3(-0.45f, 1.05f, 0f));
            guest.rotation = transform.rotation;
            // Approach beside the queue so replacement guests can reach an empty bay.
            Vector3 approach = new Vector3(0f, queuePoint.y, queuePoint.z);
            if (Vector3.Distance(body.position, approach) < 0.08f) reachedApproach = true;
            Vector3 destination = reachedApproach ? queuePoint : approach;
            if(arrivalRoute!=null && arrivalRoute.Length>1)
            {
                while(routeIndex<arrivalRoute.Length && Vector3.Distance(body.position,arrivalRoute[routeIndex])<.08f) routeIndex++;
                destination=routeIndex<arrivalRoute.Length ? arrivalRoute[routeIndex] : queuePoint;
            }
            Vector3 direction = destination - body.position;
            if (direction.magnitude < 0.08f) { Arrive(); return; }
            foreach (RaycastHit hit in Physics.BoxCastAll(body.position + Vector3.up * 0.6f, new Vector3(0.9f, 0.35f, 1.8f),
                direction.normalized, body.rotation, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform == guest || hit.transform.IsChildOf(guest)) continue;
                return;
            }
            body.MovePosition(Vector3.MoveTowards(body.position, destination, arrivalSpeed * Time.fixedDeltaTime));
            body.MoveRotation(Quaternion.LookRotation(direction));
        }

        private void Arrive()
        {
            State = ArrivalState.Waiting;
            body.position = queuePoint;
            body.rotation = Quaternion.Euler(0,parkingHeading,0);
            guest.position = queuePoint + body.rotation*guestOffset;
            guest.rotation = body.rotation*Quaternion.Euler(0,90,0);
        }

        public bool CanTalk(HotelGameSession session) => State == ArrivalState.Waiting && !session.Player.IsDriving &&
            Vector3.Distance(session.Player.transform.position + Vector3.up, guest.position) < 3f;

        public bool HandOverKeys(HotelGameSession session)
        {
            if (!CanTalk(session) || session.ReservedCar != null || session.Delivery.HasDelivery) return false;
            State = ArrivalState.HandedOver;
            interaction.PrepareForGuest();
            body.isKinematic = false;
            controller.enabled = true;
            session.Reserve(interaction);
            session.Notify($"{guestName}: Here are the keys. Take care of my car!");
            return true;
        }

        private void Update()
        {
            if (State != ArrivalState.HandedOver || !guest.gameObject.activeSelf) return;
            Vector3 direction = hotelDoor - guest.position;
            if (direction.magnitude < 0.2f) { guest.gameObject.SetActive(false); return; }
            guest.position = Vector3.MoveTowards(guest.position, hotelDoor, 1.5f * Time.deltaTime);
            guest.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(direction, Vector3.up));
        }

        public void MarkSold()
        {
            State = ArrivalState.Sold;
            guest.gameObject.SetActive(false);
        }
    }
}
