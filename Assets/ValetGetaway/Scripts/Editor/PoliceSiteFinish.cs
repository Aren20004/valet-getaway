// One-click finish for the police station swap in the island scene:
//   1. the new vehicle prefabs are built,
//   2. the precinct is turned to face the road,
//   3. every police car becomes a new model and the old ones go,
//   4. the hotel's old car park and its dead-end entrance road are removed,
//   5. a new approach road is laid from the existing network to the precinct's own car park.
//
// Valet Getaway > Police Station > Finish Site (all of the above)
//
// Roads are built through IslandCityBuilder.Road, the project's own road system, using the same prologue
// ApplyPoliceAndMarinaFix uses when it reroutes the marina. The scene is backed up to Logs/SceneBackups first.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    public static class PoliceSiteFinish
    {
        const string PoliceRoot = "07 Police station - precinct";
        const string HotelRoot = "06 Hotel island - arrival precinct";
        const string RoadRoot = "02 Roads - traced from reference";
        const string ApproachRoad = "Police station approach";
        const string FrontageRoad = "Police station frontage";
        const string CarParkRoad = "Police station car park entrance";
        const string OldEntranceRoad = "Hotel parking entrance";

        /// <summary>Where the approach road leaves the existing network (Unity world). On the route the dispatch
        /// director already drives: PoliceDispatchSetup's first station exit sits at (537, 0.24, -98).</summary>
        static readonly Vector3 Junction = new(537f, 0.24f, -98f);

        /// <summary>Blender-space point for IslandCityBuilder.Road, which works in the reference image's coordinates.</summary>
        static Vector2 B(Vector3 unity) => new(unity.x + 500f, 240f - unity.z);

        [MenuItem("Valet Getaway/Police Station/Finish Site (rotate, new cars, road)", priority = 216)]
        public static void FinishSite()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != IslandCityBuilder.ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[PoliceStation] Open ValetIslandCity in Edit mode first.");
                return;
            }
            var policeRoot = GameObject.Find(PoliceRoot);
            if (policeRoot == null)
            {
                Debug.LogError($"[PoliceStation] No '{PoliceRoot}'. Run Build Site first.");
                return;
            }
            var roadRoot = GameObject.Find(RoadRoot);
            if (roadRoot == null) { Debug.LogError($"[PoliceStation] No '{RoadRoot}' in the scene."); return; }

            Directory.CreateDirectory("Logs/SceneBackups");
            EditorSceneManager.SaveScene(scene, "Logs/SceneBackups/BeforePoliceFinish_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Finish police station site");

            var notes = new List<string>();

            // 1 + 2. New prefabs, then turn the precinct so the entrance faces the road.
            PoliceVehicleSetup.BuildVehiclePrefabs();
            PoliceStationSite.RotateSite(180f);
            notes.Add("rotated 180°");

            // 3. Every car becomes a new model; strays are destroyed.
            PoliceVehicleSetup.RebuildFleet(mixed: true);
            notes.Add("fleet rebuilt");

            // 4. The hotel's old car park (the lot the fleet used to sit on) and its now-pointless entrance road.
            notes.Add(RemoveOldParking() + " old car-park piece(s) removed");
            notes.Add(RemoveRoad(roadRoot.transform, OldEntranceRoad) ? "old entrance road removed" : "old entrance road not found");

            // 5. Roads: one past the entrance, one into the car park.
            notes.Add(BuildApproachRoads(policeRoot, roadRoot.transform));

            Physics.SyncTransforms();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);

            string report = "Police station site finished: " + string.Join("; ", notes) + ".";
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/PoliceSiteFinish.txt", report);
            Debug.Log("[PoliceStation] " + report);
            Selection.activeGameObject = policeRoot;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        [MenuItem("Valet Getaway/Police Station/Rebuild Approach Roads", priority = 219)]
        public static void RebuildRoads()
        {
            StationRoadRepair.Apply();
        }

        // ---------------- Pieces ----------------

        /// <summary>The hotel's generated car park: "Parking court" plus its "Parking line" markings.</summary>
        static int RemoveOldParking()
        {
            var hotel = GameObject.Find(HotelRoot);
            if (hotel == null) return 0;
            var doomed = hotel.GetComponentsInChildren<Transform>(true)
                .Where(t => t != null && (t.name == "Parking court" || t.name == "Parking line"))
                .Select(t => t.gameObject).ToList();
            foreach (var go in doomed) Undo.DestroyObjectImmediate(go);
            return doomed.Count;
        }

        static bool RemoveRoad(Transform roadRoot, string name)
        {
            var road = roadRoot.Find(name + " [Broken Vector road mesh]");
            if (road == null) return false;
            Undo.DestroyObjectImmediate(road.gameObject);
            return true;
        }

        /// <summary>
        /// Two roads, because the precinct needs two things from the network: a frontage that runs past the entrance
        /// the way a street does, and a spur off it into the car park. The frontage is held on the junction's own
        /// line, which is clear of the buildings, so neither road is driven through the station.
        /// </summary>
        static string BuildApproachRoads(GameObject policeRoot, Transform roadRoot)
        {
            var lot = Bounds(policeRoot, "ParkingLot");
            if (lot == null) return "no PD_ParkingLot found, no roads built";
            var site = Bounds(policeRoot, null);
            if (site == null) return "the precinct has no renderers, no roads built";

            Bounds lotBounds = lot.Value, siteBounds = site.Value;
            float lane = Junction.z;                                     // the frontage runs on the junction's line
            bool lotIsNorth = lotBounds.center.z < lane;                 // which way the spur drops off it

            // The frontage has to clear the buildings, or it is laid straight through them.
            float clear = lotIsNorth ? siteBounds.max.z + 4f : siteBounds.min.z - 4f;
            if (lotIsNorth ? lane < clear : lane > clear) lane = clear;

            RemoveRoad(roadRoot, ApproachRoad);    // the old single road, if an earlier run laid one
            RemoveRoad(roadRoot, FrontageRoad);
            RemoveRoad(roadRoot, CarParkRoad);     // idempotent: both are laid fresh every time

            // 1. Frontage: from the junction, along the front of the site, finishing past the car park.
            float far = lotBounds.center.x + Mathf.Sign(lotBounds.center.x - Junction.x) * (lotBounds.extents.x + 8f);
            Vector3 start = new(Junction.x, Junction.y, lane);
            Vector3 finish = new(far, Junction.y, lane);
            IslandCityBuilder.BeginRoadEdit(roadRoot);
            IslandCityBuilder.Road(FrontageRoad, 10f,
                new[] { B(Junction), B(start), B(Vector3.Lerp(start, finish, .5f)), B(finish) });
            Register("Police frontage road");

            // 2. Car park spur: straight off the frontage into the tarmac.
            Vector3 gate = new(lotBounds.center.x, Junction.y, lane);
            Vector3 inside = new(lotBounds.center.x, Junction.y,
                lotIsNorth ? lotBounds.max.z - 2f : lotBounds.min.z + 2f);
            IslandCityBuilder.BeginRoadEdit(roadRoot);
            IslandCityBuilder.Road(CarParkRoad, 9f, new[] { B(gate), B(Vector3.Lerp(gate, inside, .5f)), B(inside) });
            Register("Police car park entrance");

            return $"frontage road laid {Vector3.Distance(start, finish):0} m along z={lane:0.0}, " +
                   $"car park spur {Vector3.Distance(gate, inside):0} m into the lot at {inside}";
        }

        static void Register(string label)
        {
            foreach (var created in IslandCityBuilder.RoadEditResults())
                Undo.RegisterCreatedObjectUndo(created, label);
        }

        /// <summary>World bounds of the named part of the precinct, or of the whole thing when no name is given.</summary>
        static Bounds? Bounds(GameObject policeRoot, string contains)
        {
            Bounds bounds = default;
            bool any = false;
            foreach (var t in policeRoot.GetComponentsInChildren<Transform>(true))
            {
                if (contains != null && t.name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (var r in t.GetComponents<Renderer>())
                {
                    if (!any) { bounds = r.bounds; any = true; }
                    else bounds.Encapsulate(r.bounds);
                }
            }
            return any ? bounds : null;
        }
    }
}
