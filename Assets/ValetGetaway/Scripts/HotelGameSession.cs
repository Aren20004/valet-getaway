using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ValetGetaway
{
    public sealed class HotelGameSession : MonoBehaviour
    {
        [SerializeField] private ThirdPersonPlayerController player;
        [SerializeField] private OrbitFollowCamera followCamera;
        [SerializeField] private BuyerDeliveryManager delivery;
        [SerializeField] private Transform hotelReturn;
        [SerializeField] private HotelGuestCar[] guests;
        [SerializeField] private HotelBusStop[] busStops;
        private VehicleInteraction currentCar;
        private string notification;
        private float notificationUntil;
        private float busReadyAt;
        private bool islandControls;
        private void Start() => islandControls=Object.FindAnyObjectByType<IslandRecovery>()!=null;
        public ThirdPersonPlayerController Player => player;
        public BuyerDeliveryManager Delivery => delivery;
        public Camera ViewCamera => followCamera.GetComponent<Camera>();
        public VehicleInteraction ReservedCar { get; private set; }
        public int Money { get; private set; }
        public string Notification => Time.time < notificationUntil ? notification : "";
        public Transform SpeechTarget { get; private set; }
        public string SpeechText { get; private set; }
        public HotelGuestCar[] Guests => guests;
        public HotelBusStop[] BusStops => busStops;

        public void Configure(ThirdPersonPlayerController p, OrbitFollowCamera camera, BuyerDeliveryManager jobs,
            Transform returnPoint, HotelGuestCar[] arrivals, HotelBusStop[] stops)
        {
            player = p; followCamera = camera; delivery = jobs; hotelReturn = returnPoint; guests = arrivals; busStops = stops;
        }

        public void Reserve(VehicleInteraction car) => ReservedCar = car;
        public void SetCurrentCar(VehicleInteraction car) => currentCar = car;
        public void Notify(string message) { SpeechTarget = null; SpeechText = ""; notification = message; notificationUntil = Time.time + 5f; }
        public void NotifySpeech(Transform speaker, string message)
        {
            SpeechTarget = speaker;
            SpeechText = message;
            notification = message;
            notificationUntil = Time.time + 5f;
        }

        public HotelGuestCar NearbyGuest()
        {
            foreach (HotelGuestCar guest in guests) if (guest.CanTalk(this)) return guest;
            return null;
        }

        public HotelBusStop NearbyBus()
        {
            foreach (HotelBusStop stop in busStops) if (stop.IsNearby(player)) return stop;
            return null;
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
            Interact();
        }

        public void Interact()
        {
            if (player.IsDriving) { if (currentCar != null) currentCar.ExitVehicle(); return; }
            if (delivery.CanTalk)
            {
                if (!delivery.TrySell(delivery.AssignedCar)) Notify("Buyer: Park the car closer and bring it to a stop.");
                return;
            }
            HotelGuestCar guest = NearbyGuest();
            if (guest != null)
            {
                if (!guest.HandOverKeys(this)) Notify("Finish your current car delivery first.");
                return;
            }
            if (NearbyBus() != null) { TakeBus(); return; }
            if (ReservedCar != null && ReservedCar.CanEnter) ReservedCar.EnterVehicle();
        }

        public bool TakeBus()
        {
            if (player.IsDriving || NearbyBus() == null || Time.time < busReadyAt) return false;
            busReadyAt = Time.time + 1f;
            player.TeleportTo(hotelReturn.position, hotelReturn.rotation);
            followCamera.SetTarget(player.transform, true);
            Notify("Returned to the hotel.");
            return true;
        }

        public void PayForSale(VehicleInteraction car)
        {
            Money += car.SalePrice;
            Notify($"Car sold! +${car.SalePrice:N0} ({car.Condition:P0} condition)");
            if (ReservedCar == car) ReservedCar = null;
            currentCar = null;
            HotelGuestCar guest = car.GetComponent<HotelGuestCar>();
            if (guest != null) { guest.MarkSold(); StartCoroutine(NextGuest(guest)); }
        }

        private IEnumerator NextGuest(HotelGuestCar guest)
        {
            yield return new WaitForSeconds(12f);
            guest.ScheduleArrival(0f);
        }

        public string Objective => delivery.HasDelivery ? islandControls && !delivery.BuyerDiscovered ? "Drive across the bridge and look for a buyer in the city" : delivery.Objective : ReservedCar != null
            ? "Keys received. Take the guest's car." : Money > 0 && Vector3.Distance(player.transform.position, hotelReturn.position) > 20f
            ? "Take a bus back to the hotel" : "Speak to a guest at the hotel valet line";

        public string Prompt
        {
            get
            {
                if (player.IsDriving) return delivery.BuyerDiscovered && delivery.CanDeliver
                    ? "E  Exit and speak to buyer" : "WASD  Drive   |   SPACE  Brake   |   E  Exit"+(islandControls ? "   |   R  Recover" : "");
                if (delivery.CanTalk) return delivery.CanDeliver ? "E  Sell car to buyer" : "Buyer: Park closer and stop the car";
                if (NearbyGuest() != null) return ReservedCar == null && !delivery.HasDelivery ? "E  Speak to guest and take keys" : "Finish your current delivery first";
                if (NearbyBus() != null) return "E  Take bus to hotel";
                if (ReservedCar != null && ReservedCar.CanEnter) return "E  Enter guest's car";
                return "WASD  Walk   |   SHIFT  Run   |   Mouse  Look";
            }
        }
    }
}
