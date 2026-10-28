// Districts are placed procedurally, so a building now and then ends up sitting on a carriageway: the player
// drives into a wall where the map says there is a road.
//
//   Valet Getaway > Roads > Clear Buildings Off Roads
//
// Every road surface is sampled, and any building whose collider stands on one of those samples at driving height
// is pushed straight off the road until it is clear, keeping its own orientation. A building that cannot be freed
// inside a sensible distance is switched off instead and named in the log, rather than being dragged across the
// district.

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
    public static class BuildingRoadClearance
    {
        const string RoadRoot = "02 Roads - traced from reference";
        const string BuildingRoot = "03 Buildings - coordinated districts";

        [Tooltip("Height above the road the car body occupies.")]
        const float DrivingHeight = 1.1f;
        /// <summary>Kept clear beyond the building's own skin, so the car is not scraping it.</summary>
        const float Margin = 1.2f;
        const float MaxPush = 16f;

        [MenuItem("Valet Getaway/Roads/Clear Buildings Off Roads", priority = 301)]
        public static void Clear()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Roads] Edit mode only."); return; }
            var scene = SceneManager.GetActiveScene();

            var roads = GameObject.Find(RoadRoot);
            var buildings = GameObject.Find(BuildingRoot);
            if (roads == null || buildings == null) { Debug.LogError("[Roads] Need both the road and building roots in the scene."); return; }

            var samples = RoadSamples(roads);
            if (samples.Count == 0) { Debug.LogError("[Roads] No readable road meshes to sample."); return; }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Clear buildings off roads");

            var moved = new List<string>();
            var disabled = new List<string>();

            foreach (Transform building in buildings.transform)
            {
                if (building == null || !building.gameObject.activeSelf) continue;
                var colliders = building.GetComponentsInChildren<Collider>(true)
                    .Where(c => c != null && !c.isTrigger && c.enabled).ToArray();
                if (colliders.Length == 0) continue;

                var hits = Intruding(colliders, samples);
                if (hits.Count == 0) continue;

                // Push straight away from the road it is standing on.
                Vector3 centre = Centre(colliders);
                Vector3 away = Vector3.zero;
                foreach (var point in hits) away += new Vector3(centre.x - point.x, 0f, centre.z - point.z);
                if (away.sqrMagnitude < .0001f) away = Vector3.right;
                away.Normalize();

                Vector3 start = building.position;
                float pushed = 0f;
                bool free = false;
                for (float step = 1f; step <= MaxPush; step += 1f)
                {
                    building.position = start + away * step;
                    Physics.SyncTransforms();
                    if (Intruding(colliders, samples).Count != 0) continue;
                    pushed = step + Margin;
                    building.position = start + away * pushed;
                    Physics.SyncTransforms();
                    free = Intruding(colliders, samples).Count == 0;
                    break;
                }

                if (free)
                {
                    Vector3 destination = building.position;
                    building.position = start;
                    Undo.RecordObject(building, "Clear buildings off roads");
                    building.position = destination;
                    moved.Add($"{building.name} ({pushed:0.0} m)");
                }
                else
                {
                    building.position = start;
                    Physics.SyncTransforms();
                    Undo.RecordObject(building.gameObject, "Clear buildings off roads");
                    building.gameObject.SetActive(false);
                    disabled.Add(building.name);
                }
            }

            Physics.SyncTransforms();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            string report = $"Road clearance: {moved.Count} building(s) moved off the carriageway, {disabled.Count} removed.";
            if (moved.Count > 0) report += " Moved: " + string.Join(", ", moved) + ".";
            if (disabled.Count > 0) report += " Removed: " + string.Join(", ", disabled) + ".";
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/BuildingRoadClearance.txt", report);
            Debug.Log("[Roads] " + report);
        }

        static Vector3 Centre(Collider[] colliders)
        {
            var bounds = colliders[0].bounds;
            foreach (var c in colliders.Skip(1)) bounds.Encapsulate(c.bounds);
            return bounds.center;
        }

        /// <summary>Road points this building is standing on, at the height a car occupies.</summary>
        static List<Vector3> Intruding(Collider[] colliders, List<Vector3> samples)
        {
            var hits = new List<Vector3>();
            var bounds = colliders[0].bounds;
            foreach (var c in colliders.Skip(1)) bounds.Encapsulate(c.bounds);
            bounds.Expand(new Vector3(Margin * 2f, 0f, Margin * 2f));

            foreach (var sample in samples)
            {
                Vector3 at = sample + Vector3.up * DrivingHeight;
                if (!bounds.Contains(new Vector3(at.x, bounds.center.y, at.z))) continue;
                foreach (var c in colliders)
                {
                    if ((c.ClosestPoint(at) - at).sqrMagnitude > Margin * Margin) continue;
                    hits.Add(sample);
                    break;
                }
                if (hits.Count > 64) break;     // enough to know, and to aim the push
            }
            return hits;
        }

        /// <summary>A point per road triangle, on the asphalt, to test buildings against.</summary>
        static List<Vector3> RoadSamples(GameObject roads)
        {
            var samples = new List<Vector3>();
            foreach (var collider in roads.GetComponentsInChildren<MeshCollider>(true))
            {
                var mesh = collider.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                var local = collider.transform;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (normal.sqrMagnitude < 1e-10f || Mathf.Abs(normal.normalized.y) < .62f) continue;
                    samples.Add(local.TransformPoint((a + b + c) / 3f));
                }
            }
            return samples;
        }
    }
}
