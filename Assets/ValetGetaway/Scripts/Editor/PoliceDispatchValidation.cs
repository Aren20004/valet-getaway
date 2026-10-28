using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    [InitializeOnLoad]
    public static class PoliceDispatchValidation
    {
        const string Key = "Valet.PoliceDispatch.Validation";
        static PoliceDispatchDirector director;
        static HotelGameSession session;
        static VehicleInteraction car;
        static Rigidbody body;
        static int stage, planned, firstWave;
        static double started;
        static float stageStarted;
        static bool oldBackground;
        static PoliceDispatchValidation() { EditorApplication.playModeStateChanged += OnPlay; }

        [MenuItem("Valet Getaway/Validate Police Response Waves")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }

        static void OnPlay(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                oldBackground = Application.runInBackground; Application.runInBackground = true;
                stage = 0; started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick; SessionState.SetBool(Key, false); Application.runInBackground = oldBackground;
            }
        }

        static void Check(bool value, string detail)
        {
            if (!value) throw new InvalidOperationException(detail);
        }

        static void MovePlayerToDoor()
        {
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            session.Player.TeleportTo(car.transform.position + Vector3.left * 2f, Quaternion.identity);
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 150)
                    throw new TimeoutException("Phase " + stage + ", launched=" + director?.LaunchedThisWave + ", departed=" + director?.DepartedCount + ", bursts=" + director?.PursuitBursts);
                if (stage == 0)
                {
                    if (Time.time < 1f) return;
                    director = Object.FindAnyObjectByType<PoliceDispatchDirector>();
                    if (SessionState.GetInt("Valet.StationRoadBatch", 0) != 0)
                    {
                        var settings = new SerializedObject(director);
                        settings.FindProperty("minimumWave").intValue = 10;
                        settings.FindProperty("maximumWave").intValue = 10;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                    }
                    session = Object.FindAnyObjectByType<HotelGameSession>();
                    // This stationary fixture measures routing, not the separate bust/damage rules.
                    PoliceChase.Ensure(session).enabled = false;
                    Check(director != null && director.FleetSize == 10, "Saved response fleet is incomplete.");
                    Check(Object.FindObjectsByType<PoliceCarAI>(FindObjectsInactive.Include).Length == 10, "Police cars are missing or duplicated.");
                    var fixture = new GameObject("Wave validation player car (Play mode only)");
                    fixture.transform.position = new Vector3(417f, .43f, 38f);
                    var hull = fixture.AddComponent<BoxCollider>(); hull.size = new Vector3(2.1f, 1.1f, 4.2f); hull.center = new Vector3(0, .45f, 0);
                    body = fixture.AddComponent<Rigidbody>(); body.mass = 1100f; body.linearDamping = .12f;
                    var controller = fixture.AddComponent<ArcadeVehicleController>();
                    var seat = new GameObject("Driver seat").transform; seat.SetParent(fixture.transform, false); seat.localPosition = Vector3.up;
                    var door = new GameObject("Exit point").transform; door.SetParent(fixture.transform, false); door.localPosition = Vector3.left * 2f;
                    car = fixture.AddComponent<VehicleInteraction>();
                    car.Initialize(session.Player, controller, Object.FindAnyObjectByType<OrbitFollowCamera>(), seat, door, session.Delivery);
                    session.Reserve(car); stageStarted = Time.time; stage = 1;
                }
                else if (stage == 1 && Time.time - stageStarted > .2f)
                {
                    MovePlayerToDoor(); car.EnterVehicle();
                    Check(session.Player.IsDriving, "Could not enter the validation car.");
                    Check(director.WaveNumber == 1 && director.WaveSize >= 5 && director.WaveSize <= 10, "Car entry did not dispatch a 5-10 car wave.");
                    planned = director.WaveSize - 1; firstWave = director.WaveNumber;
                    Check(director.PendingCount == planned && director.LaunchedThisWave == 0, "Responders did not queue with staggered departure times.");
                    stageStarted = Time.time; stage = 2;
                    Debug.Log("POLICE WAVE CHECK: new car entry queued " + director.WaveSize + " total police cars.");
                }
                else if (stage == 2 && director.LaunchedThisWave > 0)
                {
                    if (planned > 1) Check(director.PendingCount > 0, "All responders departed simultaneously.");
                    car.ExitVehicle(); MovePlayerToDoor(); car.EnterVehicle();
                    Check(director.WaveNumber == firstWave, "Re-entering the same car generated a duplicate wave.");
                    stage = 3;
                }
                else if (stage == 3)
                {
                    Check(Object.FindObjectsByType<PoliceCarAI>(FindObjectsInactive.Include).Length == 10, "Dispatch allocated an unbounded number of cars.");
                    if (director.LaunchedThisWave != planned || director.DepartedCount < planned || director.RegroupCount < 1 || director.PursuitBursts <= planned) return;
                    var patrol = Object.FindAnyObjectByType<PoliceVehicleController>();
                    if (patrol != null && (!patrol.policeCarAI.enabled || patrol.policeCarAI.transform.position.z < -105f)) return;
                    if (patrol != null) Check(!patrol.policeCarAI.GetComponent<Rigidbody>().isKinematic, "Boarded station patrol remained kinematic.");
                    Debug.Log("POLICE WAVE CHECK: all " + planned + " responders left the lot; staggered chase, regroup, and renewed pursuit passed.");
                    body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                    body.position = new Vector3(20f, .4f, -62f); body.rotation = Quaternion.identity;
                    car.transform.SetPositionAndRotation(body.position, body.rotation); Physics.SyncTransforms();
                    stageStarted = Time.time; stage = 5;
                }
                else if (stage == 5)
                {
                    bool crossed = false;
                    foreach (var ai in director.GetComponentsInChildren<PoliceCarAI>())
                        crossed |= ai.transform.position.x < 160f && ai.transform.position.x > 135f
                            && Mathf.Abs(ai.transform.position.z + 7f) < 8f && ai.transform.position.y > -.5f;
                    if (!crossed) return;
                    Debug.Log("POLICE WAVE CHECK: a reinforcement physically crossed the hotel bridge into the city.");
                    CheckFreshJobAndSale();
                }
                else if (stage == 4 && Time.time - stageStarted > .2f)
                {
                    Check(director.PendingCount == 0, "Sold car left pending reinforcements active.");
                    Finish(true, "Saved ten-car fleet, real car-entry trigger, random 5-10 car waves, staggered departures, all selected responders leaving parking, chase/regroup/re-engage cycles, physical bridge pursuit into the city, no duplicate re-entry waves, fresh-job waves, bounded reuse and sale cancellation.");
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        static void CheckFreshJobAndSale()
        {
            car.ExitVehicle(); MovePlayerToDoor(); car.PrepareForGuest(); car.EnterVehicle();
            Check(director.WaveNumber == firstWave + 1 && director.WaveSize >= 5 && director.WaveSize <= 10, "A fresh guest-car job did not create a new bounded wave.");
            for (int i = 0; i < 12; i++)
            {
                session.ReportCarTaken(car);
                Check(director.WaveSize >= 5 && director.WaveSize <= 10 && director.FleetSize == 10, "Randomized wave exceeded fleet limits.");
            }
            car.ExitVehicle(); car.CompleteDelivery(); stageStarted = Time.time; stage = 4;
        }

        static void Finish(bool passed, string detail)
        {
            if (!passed)
                foreach (var ai in Object.FindObjectsByType<PoliceCarAI>(FindObjectsInactive.Include))
                    detail += "\n" + ai.name + " at " + ai.transform.position.ToString("F2") + ", route=" + ai.HasDispatchRoute + ", target=" + ai.CurrentRouteTarget.ToString("F2") + ", enabled=" + ai.enabled;
            EditorApplication.update -= Tick; SessionState.SetBool(Key, false); Application.runInBackground = oldBackground;
            string result = "VALET POLICE DISPATCH VALIDATION " + (passed ? "PASSED: " : "FAILED: ") + detail;
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/PoliceDispatchValidation.txt", result);
            if (passed) Debug.Log(result); else Debug.LogError(result);
            EditorApplication.isPlaying = false;
        }
    }
}
