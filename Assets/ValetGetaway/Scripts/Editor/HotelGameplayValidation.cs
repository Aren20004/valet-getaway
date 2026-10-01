using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ValetGetaway.Editor
{
    [InitializeOnLoad]
    public static class HotelGameplayValidation
    {
        private const string Key = "Valet.Hotel.Validation.2";
        private static double started;
        private static int phase;
        private static HotelGameSession session;
        private static HotelGuestCar firstGuest;
        private static Vector3 carStart;
        private static bool moved;
        private static int expectedPayout;
        private static bool previousRunInBackground;

        static HotelGameplayValidation()
        {
            EditorApplication.playModeStateChanged += PlayState;
        }

        [MenuItem("Valet Getaway/Validate Hotel Gameplay %#t")]
        public static void Run()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".running", true);
            EditorApplication.isPlaying = true;
        }

        private static void PlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(Key + ".running", false);
                return;
            }
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key + ".running", false)) return;
            phase = 0; moved = false; started = EditorApplication.timeSinceStartup;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 90) throw new TimeoutException($"Guest timed out: phase={phase}, state={firstGuest?.State}, position={firstGuest?.transform.position}");
                if (phase == 0)
                {
                    session = UnityEngine.Object.FindAnyObjectByType<HotelGameSession>();
                    if (session == null || Time.time < 1f) return;
                    firstGuest = session.Guests[0];
                    carStart = firstGuest.transform.position;
                    Assert(session.Money == 0 && session.Guests.Length == 3 && session.BusStops.Length == 5, "Scene setup incomplete");
                    Assert(!firstGuest.GetComponent<VehicleInteraction>().CanEnter, "Unclaimed guest car is enterable");
                    phase = 1;
                }
                if (phase == 1)
                {
                    moved |= Vector3.Distance(carStart, firstGuest.transform.position) > 4f;
                    if (firstGuest.State != HotelGuestCar.ArrivalState.Waiting) return;
                    Assert(moved, "Guest car did not visibly drive into hotel");
                    Assert(firstGuest.Guest.gameObject.activeSelf, "Arrived guest not visible");
                    var car = firstGuest.GetComponent<VehicleInteraction>();
                    session.Player.TeleportTo(firstGuest.Guest.position + new Vector3(-0.8f, -1f, -0.5f), Quaternion.identity);
                    Assert(firstGuest.HandOverKeys(session), "Guest did not hand over keys");
                    Assert(!firstGuest.HandOverKeys(session), "Duplicate keys accepted");
                    session.Player.TeleportTo(car.transform.position + new Vector3(-1.8f, 0, 0), Quaternion.identity);
                    car.EnterVehicle();
                    Assert(session.Player.IsDriving && session.Delivery.HasDelivery, "Car entry and buyer assignment failed");
                    Assert(!session.Delivery.BuyerDiscovered && !session.TakeBus(), "Buyer revealed early or bus accepted a driver");

                    var buyers = Field<Transform[]>(session.Delivery, "buyers");
                    var buyer = buyers[Field<int>(session.Delivery, "selectedBuyer")];
                    foreach (var npc in buyers)
                    {
                        npc.GetComponent<BuyerWander>().enabled = false;
                        npc.GetComponent<NavMeshAgent>().isStopped = true;
                    }
                    buyer.GetComponent<NavMeshAgent>().Warp(new Vector3(0, 1, 28));
                    var body = car.GetComponent<Rigidbody>();
                    body.position = new Vector3(0, 0.12f, 25); car.transform.position = body.position;
                    body.linearVelocity = Vector3.zero;
                    var camera = session.ViewCamera;
                    camera.transform.position = new Vector3(0, 3, 18);
                    camera.transform.LookAt(buyer.position);
                    Physics.SyncTransforms();
                    typeof(BuyerDeliveryManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session.Delivery, null);
                    Assert(session.Delivery.BuyerDiscovered, "Visible buyer not discovered");
                    Assert(!session.Delivery.TrySell(car), "Sold from inside vehicle");
                    car.ExitVehicle();
                    session.Player.TeleportTo(buyer.position + new Vector3(1.6f, -1f, 0), Quaternion.identity);
                    Physics.SyncTransforms();
                    body.linearVelocity = Vector3.forward * 4f;
                    Assert(!session.Delivery.TrySell(car), "Moving car accepted for sale");
                    body.linearVelocity = Vector3.zero;
                    body.position = new Vector3(0, 0.12f, 18); car.transform.position = body.position;
                    Assert(!session.Delivery.TrySell(car), "Distant car accepted for sale");
                    body.position = new Vector3(0, 0.12f, 25); car.transform.position = body.position;
                    Physics.SyncTransforms();
                    VehicleDamageValidation.Impact(car, 10f);
                    expectedPayout = car.SalePrice;
                    Assert(expectedPayout < firstGuest.SalePrice, "Damage did not lower sale offer");
                    Assert(session.Delivery.TrySell(car), "Park and talk sale failed");
                    Assert(session.Money == expectedPayout && session.ReservedCar == null, "Incorrect sale payment or reservation");
                    Assert(!session.Delivery.TrySell(car) && session.Money == expectedPayout, "Duplicate payment");
                    session.Player.TeleportTo(session.BusStops[1].transform.position + Vector3.back * 2f + Vector3.up * 0.1f, Quaternion.identity);
                    Assert(session.TakeBus(), "Bus failed to return player");
                    Assert(Vector3.Distance(session.Player.transform.position, Field<Transform>(session, "hotelReturn").position) < 0.1f, "Wrong bus destination");
                    Assert(!session.TakeBus(), "Bus cooldown failed");
                    phase = 2;
                }
                if (phase == 2 && firstGuest.State == HotelGuestCar.ArrivalState.Waiting)
                {
                    session.Player.TeleportTo(firstGuest.Guest.position + new Vector3(-0.8f, -1f, -0.5f), Quaternion.identity);
                    Assert(firstGuest.HandOverKeys(session), $"Replacement guest cannot hand over keys: state={firstGuest.State}, talk={firstGuest.CanTalk(session)}, reserved={session.ReservedCar}, delivery={session.Delivery.HasDelivery}");
                    Assert(session.Money == expectedPayout, "Money did not survive return loop");
                    Assert(firstGuest.GetComponent<VehicleInteraction>().Condition == 1f, "Replacement car was not repaired");
                    Debug.Log("VALET HOTEL VALIDATION PASSED: visible arrival, waiting guest, key handover, drive, discovery, parking/speed gates, sale payment, duplicate prevention, bus teleport, replacement guest.");
                    Stop();
                }
            }
            catch (Exception error) { Debug.LogError("VALET HOTEL VALIDATION FAILED: " + error); Stop(); }
        }

        private static void Stop()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key + ".running", false);
            Application.runInBackground = previousRunInBackground;
            EditorApplication.isPlaying = false;
        }
    }
}
