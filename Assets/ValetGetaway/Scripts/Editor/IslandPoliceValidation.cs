using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    [InitializeOnLoad]
    public static class IslandPoliceValidation
    {
        const string Key = "Valet.IslandPolice.Validation";
        static ThirdPersonPlayerController player;
        static PoliceVehicleController unit;
        static PoliceFootAI foot;
        static NavMeshAgent agent;
        static Rigidbody car;
        static GameObject target;
        static Vector3 officerStart;
        static float stageStarted, initialCarDistance;
        static double started;
        static int stage;
        static bool originalBackground;

        static IslandPoliceValidation() { EditorApplication.playModeStateChanged += OnPlay; }

        [MenuItem("Valet Getaway/Validate Island Police")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        static void OnPlay(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                originalBackground = Application.runInBackground;
                Application.runInBackground = true;
                started = EditorApplication.timeSinceStartup;
                stage = 0;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= OnLog;
                SessionState.SetBool(Key, false);
                Application.runInBackground = originalBackground;
            }
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) && stack.Contains("Police"))
                Finish(false, message + "\n" + stack);
        }

        static void Check(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException(detail);
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 45)
                    throw new TimeoutException("Police validation timed out in phase " + stage);
                if (stage == 0)
                {
                    if (Time.time < 1f) return;
                    unit = Object.FindAnyObjectByType<PoliceVehicleController>();
                    Check(unit != null, "Saved police unit is missing.");
                    player = unit.player;
                    foot = unit.policeOfficer.GetComponent<PoliceFootAI>();
                    agent = foot.GetComponent<NavMeshAgent>();
                    car = unit.policeCarAI.GetComponent<Rigidbody>();
                    Check(player != null && foot.player == player && unit.policeCarAI.player == player, "Police player references are incomplete.");
                    Check(agent.isOnNavMesh, "Police officer did not spawn on the island NavMesh.");
                    officerStart = foot.transform.position;
                    Check(NavMesh.SamplePosition(officerStart + Vector3.forward * 10f, out var hit, 3f, NavMesh.AllAreas), "No foot-chase target pavement.");
                    player.TeleportTo(hit.position + Vector3.up * .1f, Quaternion.identity);
                    stageStarted = Time.time;
                    stage = 1;
                }
                else if (stage == 1 && Time.time - stageStarted > 2f)
                {
                    Check(Vector3.Distance(officerStart, foot.transform.position) > .5f, "Officer did not chase the player on foot.");
                    Check(!unit.policeCarAI.enabled, "Police car started without its officer.");
                    Debug.Log("ISLAND POLICE CHECK: foot pursuit passed.");
                    // Aim into the open aisle rather than the added parked response cars.
                    car.rotation = Quaternion.Euler(0, 90, 0);
                    car.transform.rotation = car.rotation;
                    Physics.SyncTransforms();
                    target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    target.name = "Police validation target car (Play mode only)";
                    target.transform.SetPositionAndRotation(car.position + car.transform.forward * 12f + Vector3.up, car.rotation);
                    target.transform.localScale = new Vector3(2f, 2f, 4f);
                    var targetBody = target.AddComponent<Rigidbody>();
                    targetBody.isKinematic = true;
                    target.AddComponent<IslandPoliceCollisionProbe>();
                    var seat = new GameObject("Validation driver seat").transform;
                    seat.SetParent(target.transform, false);
                    player.EnterVehicle(seat);
                    initialCarDistance = Vector3.Distance(car.position, target.transform.position);
                    stageStarted = Time.time;
                    stage = 2;
                }
                else if (stage == 2)
                {
                    Check(Time.time - stageStarted < 18f, "Officer could not board the police car.");
                    if (unit.policeOfficer.activeSelf || !unit.policeCarAI.enabled) return;
                    Check(!agent.enabled && foot.transform.parent == unit.driverSeat, "Officer was not seated correctly.");
                    Debug.Log("ISLAND POLICE CHECK: officer walked to car and boarded.");
                    stageStarted = Time.time;
                    stage = 3;
                }
                else if (stage == 3)
                {
                    Check(Time.time - stageStarted < 12f, "Police car did not reach the target car.");
                    if (!target.GetComponent<IslandPoliceCollisionProbe>().Hit) return;
                    Check(Vector3.Distance(car.position, target.transform.position) < initialCarDistance - 3f, "Police vehicle did not pursue the driving player.");
                    Debug.Log("ISLAND POLICE CHECK: vehicle pursuit and physical ramming collision passed.");
                    var exit = unit.vehicleAccessPoint.position;
                    Check(NavMesh.SamplePosition(exit, out var hit, 3f, NavMesh.AllAreas), "No safe officer exit pavement.");
                    player.ExitVehicle(hit.position + Vector3.left * 3f + Vector3.up * .1f, Quaternion.identity);
                    stageStarted = Time.time;
                    stage = 4;
                }
                else if (stage == 4 && Time.time - stageStarted > .5f)
                {
                    Check(unit.policeOfficer.activeSelf && agent.enabled && agent.isOnNavMesh && foot.enabled, "Officer did not resume foot pursuit after the player exited.");
                    Check(!unit.policeCarAI.enabled, "Police vehicle kept pursuing an on-foot player.");
                    Finish(true, "Saved scene references, foot chase, walk to assigned car, boarding, vehicle pursuit, physical ramming collision, exit and resumed foot chase.");
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        static void Finish(bool success, string detail)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.SetBool(Key, false);
            Application.runInBackground = originalBackground;
            string report = "VALET ISLAND POLICE VALIDATION " + (success ? "PASSED: " : "FAILED: ") + detail;
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/IslandPoliceValidation.txt", report);
            if (success) Debug.Log(report); else Debug.LogError(report);
            EditorApplication.isPlaying = false;
        }
    }

    public sealed class IslandPoliceCollisionProbe : MonoBehaviour
    {
        public bool Hit { get; private set; }
        void OnCollisionEnter(Collision collision)
        {
            if (collision.rigidbody != null && collision.rigidbody.GetComponent<PoliceCarAI>() != null) Hit = true;
        }
    }
}
