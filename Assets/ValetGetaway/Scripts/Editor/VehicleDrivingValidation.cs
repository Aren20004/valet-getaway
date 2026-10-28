using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    [InitializeOnLoad]
    public static class VehicleDrivingValidation
    {
        const string Key = "Valet.DrivingValidation";
        const string Request = "Library/ValidateDriving.request";
        static readonly string[] Cases = { "player forward curbs", "player reverse curbs", "player diagonal curbs", "police forward curbs", "police diagonal curbs", "player wall collision", "police wall collision", "player braking", "player airborne momentum", "player brake then reverse", "player island sidewalks", "police island sidewalks", "player island grass to road", "player island jump ramp", "police island jump ramp" };
        static readonly List<string> Results = new List<string>();
        static Keyboard keyboard;
        static InputSettings.BackgroundBehavior oldBackgroundInput;
        static InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
        static bool oldBackground;
        static GameObject fixture;
        static Rigidbody body;
        static ArcadeVehicleController driver;
        static VehicleInteraction condition;
        static ThirdPersonPlayerController player;
        static PoliceVehicleController unit;
        static Vector3 origin, direction;
        static double started;
        static float caseStarted, maxHeight, maxGroundGap;
        static Vector3 peakPosition;
        static int test;
        static bool ready;

        static VehicleDrivingValidation()
        {
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.update += ProcessRequest;
        }

        static void ProcessRequest()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool islandOnly = File.ReadAllText(Request).Contains("island-only");
            bool repairRamp = File.ReadAllText(Request).Contains("repair-ramp");
            File.Delete(Request);
            if (repairRamp) IslandCityBuilder.FixRampSupportClearance();
            Run();
            SessionState.SetInt(Key + ".start", islandOnly ? 10 : 0);
        }

        [MenuItem("Valet Getaway/Validate Vehicle Driving")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetInt(Key + ".start", 0);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        static void OnPlay(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                ready = false; test = SessionState.GetInt(Key + ".start", 0); Results.Clear(); started = EditorApplication.timeSinceStartup;
                oldBackground = Application.runInBackground; Application.runInBackground = true;
                oldBackgroundInput = InputSystem.settings.backgroundBehavior;
                oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                keyboard = InputSystem.AddDevice<Keyboard>();
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.ExitingPlayMode) Cleanup();
        }

        static void Check(bool value, string detail)
        {
            if (!value) throw new InvalidOperationException(Cases[test] + ": " + detail);
        }

        static GameObject Block(string name, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name; block.transform.SetParent(fixture.transform);
            block.transform.SetPositionAndRotation(position, rotation); block.transform.localScale = scale;
            return block;
        }

        static void BeginCase()
        {
            if (fixture != null) { fixture.SetActive(false); Object.Destroy(fixture); }
            fixture = new GameObject("Driving validation track (Play mode only)");
            origin = new Vector3(0, 80, 0);
            float trackAngle = test == 2 || test == 4 ? 35f : 0f;
            var trackRotation = Quaternion.Euler(0, trackAngle, 0);
            direction = trackRotation * Vector3.forward;
            bool island = test >= 10;
            bool ramp = test >= 13;
            if (!island) Block("Ground", origin - Vector3.up * .5f, new Vector3(120, 1, 120), Quaternion.identity);
            else
            {
                origin = ramp ? new Vector3(102f, 0f, -62f) : new Vector3(0f, 0f, -62f);
                direction = ramp ? Vector3.right : test == 12 ? Vector3.back : Vector3.forward;
                trackRotation = Quaternion.LookRotation(direction);
            }
            bool wall = test == 5 || test == 6;
            if (test < 7)
            {
                if (wall) Block("Wall", origin + direction * 1.5f + Vector3.up * 1.5f, new Vector3(24, 3, 3), trackRotation);
                else
                {
                    Block("Sidewalk out", origin + direction * 2.5f + Vector3.up * .175f, new Vector3(24, .35f, 5), trackRotation);
                    Block("Sidewalk return from grass", origin + direction * 13.5f + Vector3.up * .175f, new Vector3(24, .35f, 5), trackRotation);
                }
            }
            bool cop = test == 3 || test == 4 || test == 6 || test == 11 || test == 14;
            var source = cop ? unit.policeCarAI.GetComponent<BoxCollider>() : Object.FindAnyObjectByType<VehicleInteraction>().GetComponent<BoxCollider>();
            var car = new GameObject(cop ? "Police driving test car" : "Player driving test car");
            car.transform.SetParent(fixture.transform);
            var hull = car.AddComponent<BoxCollider>(); hull.size = source.size; hull.center = source.center;
            float bottom = hull.center.y - hull.size.y * .5f;
            Vector3 start = origin - direction * (ramp ? 37f : island ? 16f : 8f);
            float floor = 0f;
            if (island && Physics.Raycast(start + Vector3.up * 45f, Vector3.down, out var ground, 50f)) floor = ground.point.y;
            car.transform.SetPositionAndRotation(start + Vector3.up * (floor - bottom + .03f + (test == 8 ? 5f : 0f)),
                test == 1 ? Quaternion.Euler(0, 180, 0) : trackRotation);
            body = car.AddComponent<Rigidbody>(); body.mass = 1100f; body.linearDamping = .12f; body.angularDamping = 4f;
            if (cop)
            {
                var target = new GameObject("Police driving target").transform;
                target.SetParent(fixture.transform); target.position = origin + direction * 75f + Vector3.up;
                if (player.IsDriving) player.ExitVehicle(origin + Vector3.right * 45f + Vector3.up * 2f, Quaternion.identity);
                player.EnterVehicle(target);
                car.AddComponent<PoliceCarAI>().player = player;
            }
            else
            {
                if (player.IsDriving) player.ExitVehicle(origin + Vector3.right * 45f + Vector3.up * 2f, Quaternion.identity);
                player.TeleportTo(origin + Vector3.right * 45f + Vector3.up * 2f, Quaternion.identity);
                driver = car.AddComponent<ArcadeVehicleController>(); driver.HasDriver = true;
                condition = car.AddComponent<VehicleInteraction>();
                var session = Object.FindAnyObjectByType<HotelGameSession>();
                condition.Initialize(player, driver, Object.FindAnyObjectByType<OrbitFollowCamera>(), car.transform, car.transform, session.Delivery);
            }
            if (test == 7 || test == 9) body.linearVelocity = direction * 12f;
            if (test == 8) body.linearVelocity = direction * 4f;
            var keys = test == 1 || test == 9 ? new KeyboardState(UnityEngine.InputSystem.Key.S)
                : test == 7 ? new KeyboardState(UnityEngine.InputSystem.Key.Space) : new KeyboardState(UnityEngine.InputSystem.Key.W);
            InputSystem.QueueStateEvent(keyboard, keys);
            Physics.SyncTransforms();
            caseStarted = Time.time; maxHeight = 0f; maxGroundGap = 0f;
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 95) throw new TimeoutException("Driving validation timed out.");
                if (!ready)
                {
                    if (Time.time < 1f) return;
                    unit = Object.FindAnyObjectByType<PoliceVehicleController>(); player = unit.player;
                    unit.enabled = false; unit.policeCarAI.enabled = false;
                    unit.policeOfficer.SetActive(false);
                    ready = true; BeginCase(); return;
                }
                float elapsed = Time.time - caseStarted;
                float progress = Vector3.Dot(body.position - origin, direction);
                float bottom = body.GetComponent<BoxCollider>().bounds.min.y - origin.y;
                if (bottom > maxHeight) { maxHeight = bottom; peakPosition = body.position; }
                if (test >= 10 && test < 13)
                {
                    float supportingHeight = float.NegativeInfinity;
                    foreach (var hit in Physics.RaycastAll(body.position + Vector3.up * 10f, Vector3.down, 25f))
                        if (hit.collider.attachedRigidbody == null && hit.point.y <= body.position.y + .5f)
                            supportingHeight = Mathf.Max(supportingHeight, hit.point.y);
                    if (supportingHeight > float.NegativeInfinity)
                        maxGroundGap = Mathf.Max(maxGroundGap, bottom + origin.y - supportingHeight);
                }
                Check(!float.IsNaN(body.position.y) && body.linearVelocity.magnitude < 50f, "unstable physics");
                if (test < 5 || test >= 10)
                {
                    Check(elapsed < (test >= 13 ? 12f : 9f), "stuck at " + body.position + ", velocity=" + body.linearVelocity);
                    if (progress < (test >= 13 ? 56f : test >= 10 ? 16f : 19f)) return;
                    if (test >= 13) Check(body.position.y > 6f && body.linearVelocity.y > 1f, "ramp did not launch the vehicle: " + body.position + " velocity=" + body.linearVelocity);
                    else
                    {
                        Check(maxHeight > .25f && (test >= 10 ? maxGroundGap < .65f : maxHeight < 1.2f), "curb caused an incorrect climb or launch: " + maxHeight + "; gap=" + maxGroundGap + "; peak=" + peakPosition + "; end=" + body.position);
                        Check(Vector3.Dot(body.transform.up, Vector3.up) > .95f, "vehicle lost its upright attitude");
                        if (test < 3 || test == 10 || test == 12) Check(condition.Condition > .999f, "ordinary curbs damaged the car: " + condition.Condition);
                    }
                }
                else if (test == 5 || test == 6)
                {
                    if (elapsed < 2.5f) return;
                    Check(progress < 0f && maxHeight < .6f, "vehicle climbed or passed through the wall");
                    if (test == 5) Check(condition.Condition < 1f, "real wall collision did not damage the vehicle");
                }
                else if (test == 7)
                {
                    if (elapsed < 1.5f) return;
                    Check(Mathf.Abs(driver.Speed) < 1f && bottom > -.1f, "braking did not stop the car on the ground");
                }
                else if (test == 8)
                {
                    if (elapsed < .3f) return;
                    Check(!driver.Grounded && body.linearVelocity.y < -1f && Mathf.Abs(driver.Speed - 4f) < .5f, "airborne throttle changed launch momentum or grounded on itself");
                }
                else if (test == 9)
                {
                    if (elapsed < 2f) return;
                    Check(driver.Speed < -1f && driver.Speed > -13f, "brake-to-reverse transition failed");
                }
                Results.Add(Cases[test] + " passed"); Debug.Log("DRIVING CHECK: " + Results[Results.Count - 1]);
                test++;
                if (test == Cases.Length) Finish(true, string.Join("; ", Results)); else BeginCase();
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        static void Cleanup()
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldBackgroundInput;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            Application.runInBackground = oldBackground;
        }

        static void Finish(bool success, string detail)
        {
            Cleanup();
            string report = "VALET DRIVING VALIDATION " + (success ? "PASSED: " : "FAILED: ") + detail;
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/VehicleDrivingValidation.txt", report);
            if (success) Debug.Log(report); else Debug.LogError(report);
            EditorApplication.isPlaying = false;
        }
    }
}
