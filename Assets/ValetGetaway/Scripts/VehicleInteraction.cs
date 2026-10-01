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
        [Header("Vehicle condition")]
        [SerializeField, Min(0f)] private float harmlessImpactSpeed = 3f;
        [SerializeField, Min(0f)] private float conditionLossPerMetrePerSecond = 0.045f;
        [SerializeField, Range(0f, 1f)] private float minimumValueFraction = 0.2f;
        private float condition = 1f;
        private float nextDamageAt;
        public float Condition => condition;
        public int BaseSalePrice => guest != null ? guest.SalePrice : 2500;
        public int SalePrice => Mathf.RoundToInt(BaseSalePrice * Mathf.Lerp(minimumValueFraction, 1f, condition));

        private void OnCollisionEnter(Collision collision)
        {
            if (awaitingReset || (guest != null ? !guest.KeysReceived : !vehicle.HasDriver)) return;
            // Measure closing speed into a surface, not sideways speed along it.
            // Ignore upward-facing ground contacts so ordinary road travel is free.
            float impactSpeed = 0f;
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 normal = collision.GetContact(i).normal;
                if (normal.y > 0.65f) continue;
                impactSpeed = Mathf.Max(impactSpeed, Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal)));
            }
            ApplyImpact(impactSpeed);
        }

        internal void ApplyImpact(float impactSpeed)
        {
            if (float.IsNaN(impactSpeed) || float.IsInfinity(impactSpeed) ||
                impactSpeed <= harmlessImpactSpeed || Time.time < nextDamageAt || condition <= 0f) return;
            int oldPrice = SalePrice;
            condition = Mathf.Clamp01(condition - (impactSpeed - harmlessImpactSpeed) * conditionLossPerMetrePerSecond);
            nextDamageAt = Time.time + 0.35f;
            if (session != null && oldPrice > SalePrice)
                session.Notify($"Collision! Value -${oldPrice - SalePrice:N0}. Drive carefully.");
        }

        private void ResetCondition()
        {
            condition = 1f;
            nextDamageAt = 0f;
        }

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

        public void PrepareForGuest()
        {
            awaitingReset = false;
            ResetCondition();
        }

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
            ResetCondition();
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
