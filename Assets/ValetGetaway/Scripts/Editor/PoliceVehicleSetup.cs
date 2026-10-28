// Builds drivable police vehicle prefabs from the PD_ police station set, wired to the police code already in this
// project: PoliceCarAI (pursuit + dispatch routes) on top of VehicleGroundMotor, with the same Rigidbody setup
// PoliceDispatchSetup gives the response cars, plus the DriverSeat / VehicleAccessPoint transforms
// PoliceVehicleController expects when an officer walks to a car and gets in.
//
//   Valet Getaway > Police Station > Build Vehicle Prefabs     writes Prefabs/Police/Vehicles/*.prefab
//   Valet Getaway > Police Station > Use Patrol Sedan For Fleet  swaps the shared response-car prefab to the new model
//
// Models face Unity +Z (per PD_Site_Layout.json), which is what PoliceCarAI treats as forward, so no re-orienting.
// Nothing here changes PoliceCarAI, PoliceFootAI, PoliceVehicleController or PoliceDispatchDirector.

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    public static class PoliceVehicleSetup
    {
        const string Models = "Assets/ValetGetaway/Police/Models";
        const string PrefabFolder = "Assets/ValetGetaway/Prefabs/Police/Vehicles";
        const string ResponseCarPath = "Assets/ValetGetaway/Prefabs/Police/PoliceResponseCar.prefab";

        /// <summary>One police vehicle class: which model, and how it drives.</summary>
        readonly struct Spec
        {
            public readonly string Model, Prefab;
            public readonly float Mass, Acceleration, MaxSpeed, TurnSpeed;
            public Spec(string model, string prefab, float mass, float acceleration, float maxSpeed, float turnSpeed)
            { Model = model; Prefab = prefab; Mass = mass; Acceleration = acceleration; MaxSpeed = maxSpeed; TurnSpeed = turnSpeed; }
        }

        // Response-car baseline from PoliceDispatchSetup: mass 1100, accel 21, max 24, turn 100.
        // The heavier SWAT vehicles trade speed and steering for mass; the unmarked car is the quick one.
        static readonly Spec[] Specs =
        {
            new("PD_PatrolSedan.fbx",       "PoliceCar_PatrolSedan",   1100f, 21f, 24f, 100f),
            new("PD_PoliceSUV.fbx",         "PoliceCar_SUV",           1500f, 19f, 22f,  88f),
            new("PD_UnmarkedSedan.fbx",     "PoliceCar_Unmarked",      1050f, 23f, 26f, 105f),
            new("PD_SWAT_Van.fbx",          "PoliceCar_SwatVan",       2200f, 15f, 18f,  70f),
            new("PD_SWAT_ArmoredTruck.fbx", "PoliceCar_SwatTruck",     3400f, 12f, 15f,  58f),
        };

        [MenuItem("Valet Getaway/Police Station/Build Vehicle Prefabs", priority = 210)]
        public static void BuildVehiclePrefabs()
        {
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();

            int built = 0;
            var missing = new List<string>();
            foreach (var spec in Specs)
            {
                var prefab = BuildOne(spec, PrefabFolder + "/" + spec.Prefab + ".prefab");
                if (prefab == null) { missing.Add(spec.Model); continue; }
                built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report = $"Police vehicles: {built} prefab(s) in {PrefabFolder}.";
            if (missing.Count > 0) report += " MISSING model(s): " + string.Join(", ", missing) + ".";
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/PoliceVehicleSetup.txt", report);
            if (missing.Count > 0) Debug.LogWarning("[PoliceVehicles] " + report); else Debug.Log("[PoliceVehicles] " + report);
        }

        [MenuItem("Valet Getaway/Police Station/Use Patrol Sedan For Fleet", priority = 211)]
        public static void UsePatrolSedanForFleet()
        {
            if (!EditorUtility.DisplayDialog("Replace the response-car prefab?",
                    "Every car in the Police Response Fleet is an instance of\n\n" + ResponseCarPath +
                    "\n\nThis rebuilds that prefab from PD_PatrolSedan, so all of them change at once. The old prefab is " +
                    "copied to Logs/PrefabBackups first.\n\nScene-level tweaks on individual cars (position, player link) are kept.",
                    "Replace it", "Cancel"))
                return;

            if (File.Exists(ResponseCarPath))
            {
                Directory.CreateDirectory("Logs/PrefabBackups");
                string backup = "Logs/PrefabBackups/PoliceResponseCar_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".prefab";
                File.Copy(ResponseCarPath, backup, true);
                Debug.Log("[PoliceVehicles] Old prefab copied to " + backup);
            }

            var sedan = Specs.First(s => s.Prefab == "PoliceCar_PatrolSedan");
            var rebuilt = BuildOne(sedan, ResponseCarPath, "Police Response Car");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (rebuilt == null) { Debug.LogError("[PoliceVehicles] " + Models + "/" + sedan.Model + " is missing; the prefab was left alone."); return; }
            Debug.Log("[PoliceVehicles] " + ResponseCarPath + " now uses PD_PatrolSedan. Every fleet car in the island scene follows it.");
        }

        // ---------------- Swap the cars already in the scene ----------------

        [MenuItem("Valet Getaway/Police Station/Rebuild Fleet (all patrol sedans)", priority = 214)]
        public static void RebuildFleetSedans() => RebuildFleet(mixed: false);

        [MenuItem("Valet Getaway/Police Station/Rebuild Fleet (mixed vehicles)", priority = 215)]
        public static void RebuildFleetMixed() => RebuildFleet(mixed: true);

        /// <summary>
        /// Replace every car the dispatch director owns, plus the station patrol car, with the new prefabs. Each
        /// replacement keeps the old car's name, parent, sibling order, transform and player reference, and the
        /// director's own array is rewritten to point at the new <see cref="PoliceCarAI"/>s, so dispatch keeps working.
        /// Leftover instances of the old response-car prefab are removed as well.
        /// </summary>
        public static void RebuildFleet(bool mixed)
        {
            var director = Object.FindAnyObjectByType<PoliceDispatchDirector>();
            var patrol = Object.FindAnyObjectByType<PoliceVehicleController>();
            if (director == null && patrol == null)
            {
                Debug.LogError("[PoliceVehicles] No police dispatch director or station patrol in the open scene. Open the island scene.");
                return;
            }

            var prefabs = Specs.ToDictionary(s2 => s2.Prefab,
                s2 => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/" + s2.Prefab + ".prefab"));
            var missing = prefabs.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList();
            if (missing.Count > 0)
            {
                Debug.LogError("[PoliceVehicles] Build Vehicle Prefabs first — missing: " + string.Join(", ", missing));
                return;
            }

            // A mixed fleet reads better than ten identical sedans; the order repeats down the row.
            string[] rota = mixed
                ? new[] { "PoliceCar_PatrolSedan", "PoliceCar_PatrolSedan", "PoliceCar_SUV",
                          "PoliceCar_PatrolSedan", "PoliceCar_Unmarked", "PoliceCar_SUV",
                          "PoliceCar_PatrolSedan", "PoliceCar_SwatVan", "PoliceCar_SwatTruck" }
                : new[] { "PoliceCar_PatrolSedan" };

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild police fleet");

            var replaced = new List<GameObject>();
            int swapped = 0;

            if (director != null)
            {
                var so = new SerializedObject(director);
                var cars = so.FindProperty("responseCars");
                if (cars != null && cars.isArray)
                {
                    for (int i = 0; i < cars.arraySize; i++)
                    {
                        var element = cars.GetArrayElementAtIndex(i);
                        if (element.objectReferenceValue is not PoliceCarAI old) continue;
                        var fresh = Swap(old, prefabs[rota[i % rota.Length]], replaced);
                        if (fresh == null) continue;
                        element.objectReferenceValue = fresh;
                        swapped++;
                    }
                    so.ApplyModifiedProperties();
                }
            }

            // The station patrol car: PoliceVehicleController also holds the seat and door transforms.
            if (patrol != null && patrol.policeCarAI != null)
            {
                var fresh = Swap(patrol.policeCarAI, prefabs["PoliceCar_PatrolSedan"], replaced);
                if (fresh != null)
                {
                    Undo.RecordObject(patrol, "Repoint station patrol");
                    patrol.policeCarAI = fresh;
                    var seat = fresh.transform.Find("DriverSeat");
                    var door = fresh.transform.Find("VehicleAccessPoint");
                    if (seat != null) patrol.driverSeat = seat;
                    if (door != null) patrol.vehicleAccessPoint = door;
                    EditorUtility.SetDirty(patrol);
                    swapped++;
                }
            }

            // Anything still running the old prefab (strays not owned by the director).
            int strays = 0;
            foreach (var ai in Object.FindObjectsByType<PoliceCarAI>(FindObjectsInactive.Include))
            {
                if (ai == null || replaced.Contains(ai.gameObject)) continue;
                if (ai.GetComponentInChildren<MeshFilter>() == null) continue;
                bool isNew = PrefabUtility.GetCorrespondingObjectFromSource(ai.gameObject) is GameObject src &&
                             prefabs.Values.Any(p => p == src || AssetDatabase.GetAssetPath(p) == AssetDatabase.GetAssetPath(src));
                if (isNew) continue;
                Undo.DestroyObjectImmediate(ai.gameObject);
                strays++;
            }

            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(group);

            string report = $"Fleet rebuilt: {swapped} car(s) now use the new models" +
                            (mixed ? " (mixed sedans, SUVs, unmarked and SWAT)" : " (all patrol sedans)") +
                            $", {strays} stray old car(s) removed.";
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/PoliceVehicleSetup.txt", report);
            Debug.Log("[PoliceVehicles] " + report + " Save the scene to keep it.");
        }

        /// <summary>Put a new prefab instance exactly where an old car stood, and hand back its PoliceCarAI.</summary>
        static PoliceCarAI Swap(PoliceCarAI old, GameObject prefab, List<GameObject> replaced)
        {
            if (old == null || prefab == null) return null;
            var oldGo = old.gameObject;
            var t = oldGo.transform;

            var fresh = (GameObject)PrefabUtility.InstantiatePrefab(prefab, oldGo.scene);
            Undo.RegisterCreatedObjectUndo(fresh, "New police car");
            fresh.name = oldGo.name;
            fresh.transform.SetParent(t.parent, false);
            fresh.transform.SetPositionAndRotation(t.position, t.rotation);
            fresh.transform.SetSiblingIndex(t.GetSiblingIndex());

            var ai = fresh.GetComponent<PoliceCarAI>();
            if (ai != null)
            {
                ai.player = old.player;
                ai.PursuitEnabled = old.PursuitEnabled;
                ai.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(ai);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(fresh.transform);

            replaced.Add(fresh);
            Undo.DestroyObjectImmediate(oldGo);
            return ai;
        }

        // ---------------- Build one vehicle ----------------

        static GameObject BuildOne(Spec spec, string path, string rootName = null)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/" + spec.Model);
            if (model == null) return null;

            var root = new GameObject(rootName ?? spec.Prefab);
            try
            {
                // Visual: the model as a child, so the root keeps a clean pivot at ground level.
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Model";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                var bounds = LocalBounds(root, visual);

                // Body collider: the hull, lifted clear of the ground so the motor's own ground ray does the suspension.
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(bounds.center.x, Mathf.Max(bounds.center.y, bounds.size.y * .5f), bounds.center.z);
                box.size = bounds.size;

                // PoliceCarAI requires Rigidbody + VehicleGroundMotor, so adding it brings both.
                var ai = root.AddComponent<PoliceCarAI>();
                ai.player = null;                 // the dispatch/patrol setup assigns the player
                ai.acceleration = spec.Acceleration;
                ai.maxSpeed = spec.MaxSpeed;
                ai.turnSpeed = spec.TurnSpeed;
                ai.enabled = false;               // switched on when an officer gets in, as PoliceVehicleController does

                if (root.GetComponent<VehicleGroundMotor>() == null) root.AddComponent<VehicleGroundMotor>();

                var body = root.GetComponent<Rigidbody>();
                body.mass = spec.Mass;
                body.isKinematic = true;          // matches the parked response cars; the motor clears this when driving
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                // The two transforms PoliceVehicleController drives an officer to and seats them in.
                float halfWidth = bounds.extents.x;
                float seatZ = bounds.center.z + bounds.extents.z * .15f;
                Point(root.transform, "DriverSeat", new Vector3(-halfWidth * .45f, bounds.size.y * .45f, seatZ));
                Point(root.transform, "VehicleAccessPoint", new Vector3(-halfWidth - 1.1f, 0f, seatZ));

                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? PrefabFolder);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Transform Point(Transform parent, string name, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            return t;
        }

        /// <summary>The model's bounds in the root's own space, with the floor at y = 0.</summary>
        static Bounds LocalBounds(GameObject root, GameObject visual)
        {
            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(new Vector3(0f, .75f, 0f), new Vector3(2f, 1.5f, 4.5f));

            var world = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) world.Encapsulate(r.bounds);

            var min = root.transform.InverseTransformPoint(world.min);
            var max = root.transform.InverseTransformPoint(world.max);
            var local = new Bounds();
            local.SetMinMax(Vector3.Min(min, max), Vector3.Max(min, max));
            if (local.size.x < .1f || local.size.y < .1f || local.size.z < .1f)
                local = new Bounds(new Vector3(0f, .75f, 0f), new Vector3(2f, 1.5f, 4.5f));
            return local;
        }
    }
}
