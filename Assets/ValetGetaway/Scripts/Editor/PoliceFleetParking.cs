// Parks the drivable police fleet in the precinct's own car park (and the SWAT yard), using the parking bays the
// PD_ set already models: each gameplay car takes over a decorative parked car's exact spot and that decorative
// model is removed, so nothing overlaps and the lot stays as full as it looks.
//
//   Valet Getaway > Police Station > Park Fleet in Lot
//
// Slots are read live from the scene, so this works whatever yaw the site ended up at. Idempotent: running it
// again just re-seats the cars in whatever bays are still free.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    public static class PoliceFleetParking
    {
        const string PoliceRoot = "07 Police station - precinct";

        static readonly string[] CarBays = { "PatrolSedan", "PoliceSUV", "UnmarkedSedan" };
        static readonly string[] SwatBays = { "SWAT_Van", "SWAT_ArmoredTruck" };

        [MenuItem("Valet Getaway/Police Station/Park Fleet in Lot", priority = 217)]
        public static void ParkFleetMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[PoliceStation] Edit mode only."); return; }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Park police fleet");
            string report = ParkFleet();
            Physics.SyncTransforms();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PoliceStation] " + report);
        }

        /// <summary>Seat every drivable police vehicle in a modelled bay; returns a one-line report.</summary>
        public static string ParkFleet()
        {
            var root = GameObject.Find(PoliceRoot);
            if (root == null) return "no precinct in the scene, fleet not parked";

            // Only the placed models themselves, not the mesh parts inside them: destroying a bay would otherwise
            // leave its own children in the list as dead references.
            var placed = new List<Transform>();
            foreach (Transform child in root.transform) placed.Add(child);

            var bays = placed.Where(t => t != null && Is(t.name, CarBays))
                .OrderBy(t => t.position.z).ThenBy(t => t.position.x).ToList();
            var swatBays = placed.Where(t => t != null && Is(t.name, SwatBays))
                .OrderBy(t => t.position.x).ToList();

            var cars = Vehicles();
            int parked = 0, removed = 0;

            foreach (var car in cars)
            {
                bool swat = car.name.IndexOf("Swat", StringComparison.OrdinalIgnoreCase) >= 0;
                var pool = swat && swatBays.Count > 0 ? swatBays : bays;
                if (pool.Count == 0) continue;

                // Nearest free bay to where the car is now, so cars barely move when they are already right.
                pool.RemoveAll(t => t == null);
                if (pool.Count == 0) continue;
                var bay = pool.OrderBy(t => (t.position - car.position).sqrMagnitude).First();
                pool.Remove(bay);

                Undo.RecordObject(car, "Park police fleet");
                car.SetPositionAndRotation(new Vector3(bay.position.x, car.position.y, bay.position.z), bay.rotation);
                Undo.DestroyObjectImmediate(bay.gameObject);
                parked++; removed++;
            }

            return $"fleet parked: {parked} vehicle(s) seated in modelled bays, {removed} decorative car(s) removed " +
                   $"({bays.Count} car bay(s) and {swatBays.Count} SWAT bay(s) still filled with scenery)";
        }

        static bool Is(string name, string[] keys) =>
            keys.Any(k => name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Every drivable police vehicle in the scene, as the transform that should be moved (the unit root
        /// when an officer rides with it, otherwise the car itself).</summary>
        static List<Transform> Vehicles()
        {
            var list = new List<Transform>();
            foreach (var ai in Object.FindObjectsByType<PoliceCarAI>(FindObjectsInactive.Include))
            {
                if (ai == null) continue;
                var t = ai.transform;
                var controller = ai.GetComponentInParent<PoliceVehicleController>(true);
                if (controller != null) t = controller.transform;
                if (!list.Contains(t)) list.Add(t);
            }
            return list;
        }
    }
}
