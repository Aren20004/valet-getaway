using UnityEngine;
using UnityEngine.InputSystem;

namespace ValetGetaway
{
    public sealed class VehicleInteraction : MonoBehaviour
    {
        [SerializeField] private float interactionDistance = 3.25f;

        [SerializeField] private ThirdPersonPlayerController player;
        [SerializeField] private ArcadeVehicleController vehicle;
        [SerializeField] private OrbitFollowCamera followCamera;
        [SerializeField] private Transform seat;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private BuyerDeliveryManager delivery;
        private Vector3 startingPosition;
        private Quaternion startingRotation;
        private bool awaitingReset;
        private HotelGameSession session;
        private HotelGuestCar guest;
        public int SalePrice => guest != null ? guest.SalePrice : 2500;

        private void Start()
        {
            session = Object.FindAnyObjectByType<HotelGameSession>();
            guest = GetComponent<HotelGuestCar>();
            startingPosition = transform.position;
            startingRotation = transform.rotation;
            PrototypeHud.SetVehicle(null, this);
        }

        public bool NearBuyer => delivery != null && delivery.BuyerDiscovered && delivery.NearBuyer;
        public bool CanDeliver => delivery != null && delivery.CanDeliver;
        public bool CanTalkToBuyer => delivery != null && delivery.CanTalk;

        public bool CanEnter => player != null && !player.IsDriving && !awaitingReset && !vehicle.HasDriver &&
                                (guest == null || guest.KeysReceived) &&
                                (!delivery.HasDelivery || delivery.AssignedCar == this) &&
                                Vector3.Distance(player.transform.position, transform.position) <= interactionDistance;

        public void Initialize(
            ThirdPersonPlayerController playerController,
            ArcadeVehicleController vehicleController,
            OrbitFollowCamera cameraController,
            Transform driverSeat,
            Transform driverExit,
            BuyerDeliveryManager deliveryManager)
        {
            player = playerController;
            vehicle = vehicleController;
            followCamera = cameraController;
            seat = driverSeat;
            exitPoint = driverExit;
            delivery = deliveryManager;
            startingPosition = transform.position;
            startingRotation = transform.rotation;
        }

        private void Update()
        {
            if (session != null) return;
            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            {
                return;
            }

            if (vehicle.HasDriver)
            {
                ExitVehicle();
            }
            else if (CanTalkToBuyer)
            {
                delivery.TrySell(this);
            }
            else if (CanEnter)
            {
                EnterVehicle();
            }
        }

        public void EnterVehicle()
        {
            if (!CanEnter) return;
            vehicle.HasDriver = true;
            player.EnterVehicle(seat);
            followCamera.SetTarget(transform, true, followHeading: true);
            PrototypeHud.SetVehicle(vehicle, this);
            delivery.AssignBuyer(this);
            if (session != null) session.SetCurrentCar(this);
        }

        public void PrepareForGuest() => awaitingReset = false;

        public void CompleteDelivery()
        {
            awaitingReset = true;
            vehicle.HasDriver = false;
            PrototypeHud.SetVehicle(null, this);
            Rigidbody body = GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            gameObject.SetActive(false);
        }

        public void ResetAfterDelivery()
        {
            transform.SetPositionAndRotation(startingPosition, startingRotation);
            Rigidbody body = GetComponent<Rigidbody>();
            body.position = startingPosition;
            body.rotation = startingRotation;
            awaitingReset = false;
            gameObject.SetActive(true);
        }

        public void ExitVehicle()
        {
            vehicle.HasDriver = false;
            Vector3 exitPosition = exitPoint.position;
            if (Physics.Raycast(exitPosition + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 8f))
            {
                exitPosition.y = ground.point.y + 0.05f;
            }
            player.ExitVehicle(exitPosition, exitPoint.rotation);
            followCamera.SetTarget(player.transform, true);
            PrototypeHud.SetVehicle(null, this);
            if (session != null) session.SetCurrentCar(null);
        }
    }
}
