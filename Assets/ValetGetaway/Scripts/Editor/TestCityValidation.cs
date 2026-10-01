using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ValetGetaway.Editor
{
    // Exercises the saved scene in Play Mode; all test movement is discarded on exit.
    [InitializeOnLoad]
    public static class TestCityValidation
    {
        private const string Key = "ValetTestCity.Validation.v1";
        private static double readyAt;

        static TestCityValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayState;
        }

        [MenuItem("Valet Getaway/Validate Saved City")]
        public static void Run()
        {
            if (UnityEngine.Object.FindAnyObjectByType<HotelGameSession>() != null) { HotelGameplayValidation.Run(); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != TestCitySceneBuilder.ScenePath) return;
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".running", true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key + ".running", false)) return;
            readyAt = EditorApplication.timeSinceStartup + 1;
            EditorApplication.update += Check;
        }

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Check()
        {
            if (EditorApplication.timeSinceStartup < readyAt) return;
            EditorApplication.update -= Check;
            try
            {
                var player = UnityEngine.Object.FindAnyObjectByType<ThirdPersonPlayerController>();
                var car = UnityEngine.Object.FindAnyObjectByType<VehicleInteraction>();
                var manager = UnityEngine.Object.FindAnyObjectByType<BuyerDeliveryManager>();
                Require(player != null && car != null && manager != null, "Saved gameplay objects missing");
                Require(Field<Transform>(player, "cameraTransform") != null, "Player camera reference missing");
                var buyers = Field<Transform[]>(manager, "buyers");
                Require(buyers.Length == 6 && Array.TrueForAll(buyers, b => b != null), "Buyer references missing");
                Require(Array.TrueForAll(buyers, b => b.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh), "Wandering NPC off navigation mesh");
                var wanderUpdate = typeof(BuyerWander).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                wanderUpdate.Invoke(buyers[0].GetComponent<BuyerWander>(), null);
                Require(buyers[0].GetComponent<UnityEngine.AI.NavMeshAgent>().hasPath, "Buyer did not select a walking route");
                Require(player.GetComponentsInChildren<Renderer>().Length > 0, "Player visual missing");
                typeof(VehicleInteraction).GetMethod("EnterVehicle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(car, null);
                Require(player.IsDriving && manager.HasDelivery, "Entering car did not assign buyer");
                int first = Field<int>(manager, "selectedBuyer");
                Require(!manager.BuyerDiscovered && !Field<GameObject>(manager, "marker").activeSelf, "Buyer revealed before sighting");
                Transform selected = buyers[first];
                var view = Field<Camera>(manager, "viewCamera");
                var update = typeof(BuyerDeliveryManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                Vector3 oldCameraPosition = view.transform.position;
                Quaternion oldCameraRotation = view.transform.rotation;
                // Isolate discovery from movement while testing the visibility gates.
                selected.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
                foreach (Transform npc in buyers) npc.GetComponent<BuyerWander>().enabled = false;
                selected.position = new Vector3(0f, 1f, 25f);
                car.transform.position = new Vector3(0f, 0.1f, 18f);
                view.transform.position = new Vector3(0f, 2f, 15f);
                view.transform.rotation = Quaternion.LookRotation(Vector3.back);
                Physics.SyncTransforms();
                update.Invoke(manager, null);
                Require(!manager.BuyerDiscovered, "Buyer discovered behind camera");
                view.transform.LookAt(selected.position);
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = new Vector3(0f, 2f, 22f);
                wall.transform.localScale = new Vector3(4f, 4f, 1f);
                Physics.SyncTransforms();
                update.Invoke(manager, null);
                Require(!manager.BuyerDiscovered, "Buyer discovered through wall");
                wall.GetComponent<Collider>().enabled = false;
                UnityEngine.Object.Destroy(wall);
                Physics.SyncTransforms();
                update.Invoke(manager, null);
                Require(manager.BuyerDiscovered, "Visible nearby buyer not discovered");
                view.transform.rotation = Quaternion.LookRotation(Vector3.back);
                update.Invoke(manager, null);
                Require(manager.BuyerDiscovered && Field<GameObject>(manager, "marker").activeSelf, "Discovery not remembered");
                view.transform.SetPositionAndRotation(oldCameraPosition, oldCameraRotation);
                manager.AssignBuyer(car);
                Require(first == Field<int>(manager, "selectedBuyer"), "Buyer changed during an active job");
                var body = car.GetComponent<Rigidbody>();
                Vector3 parkingPoint = buyers[first].position + new Vector3(0, -0.9f, -3f);
                body.position = parkingPoint;
                car.transform.position = parkingPoint;
                body.linearVelocity = Vector3.forward * 5f;
                Require(!manager.CanDeliver, "Moving car accepted");
                body.linearVelocity = Vector3.zero;
                Require(manager.CanDeliver, "Stopped car inside parking area rejected");
                Require(manager.IsWaitingAtParkedCar(selected), "Parked car did not reserve buyer");
                var walkingAgent = selected.GetComponent<UnityEngine.AI.NavMeshAgent>();
                walkingAgent.enabled = true;
                walkingAgent.Warp(new Vector3(0f, 1f, 25f));
                Vector3 seatedPosition = player.transform.position;
                player.transform.position = selected.position + Vector3.forward * 30f;
                wanderUpdate.Invoke(selected.GetComponent<BuyerWander>(), null);
                Require(walkingAgent.isStopped, "Buyer walked away from parked sale car");
                player.transform.position = seatedPosition;
                Require(!manager.TrySell(car), "Sale accepted from inside car");
                typeof(VehicleInteraction).GetMethod("ExitVehicle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(car, null);
                Require(!player.IsDriving, "Exit failed");
                Require(Array.TrueForAll(player.GetComponentsInChildren<Renderer>(), r => r.enabled), "Player invisible after exit");
                Require(!manager.TrySell(car), "Sale accepted too far from buyer");
                player.GetComponent<CharacterController>().enabled = false;
                player.transform.position = buyers[first].position + Vector3.back * 1.5f;
                Physics.SyncTransforms();
                Require(manager.TrySell(car), "Park and talk sale failed");
                Require(manager.DeliveriesCompleted == 1 && !manager.HasDelivery && !car.gameObject.activeSelf, "Sale state incorrect");
                Require(!manager.TrySell(car), "Duplicate sale accepted");
                car.ResetAfterDelivery();
                Require(car.gameObject.activeSelf, "Car reset failed");
                manager.AssignBuyer(car);
                Require(Field<int>(manager, "selectedBuyer") != first, "Consecutive buyer repeated");
                Require(!manager.BuyerDiscovered, "Discovery carried over to a new job");
                Debug.Log("VALET CITY VALIDATION PASSED: walking routes, waiting beside parked car, discovery visibility, wall occlusion, remembered directions, new-job reset, enter/exit, park and talk sale.");
            }
            catch (Exception error)
            {
                Debug.LogError("VALET CITY VALIDATION FAILED: " + error);
            }
            finally
            {
                SessionState.SetBool(Key + ".running", false);
                EditorApplication.isPlaying = false;
            }
        }
    }
}
