// The roads are generated as one mesh each, curbs and all, and the same mesh is used for collision. Where two
// routes meet, or where a junction was laid after the trimming pass that subtracts crossing carriageways, a curb's
// vertical face ends up standing across a lane: the car stops dead against something the player cannot see.
//
//   Valet Getaway > Roads > Fix Invisible Barriers
//
// The fix is to give every road a collision mesh of its own that keeps only the surfaces you can drive on and drops
// the vertical faces. The visible mesh is untouched, so the curbs still look like curbs - they just stop being
// walls. Anything flatter than the slope the motor can climb is kept, so ramps and the bridge still work.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    public static class RoadBarrierFix
    {
        const string RoadRoot = "02 Roads - traced from reference";
        const string CollisionFolder = "Assets/ValetGetaway/Generated/RoadCollision";

        /// <summary>Steepest face still treated as road rather than wall. The motor refuses anything over 48 degrees,
        /// so a face shallower than this is drivable and a face steeper than it can only ever stop the car.</summary>
        const float DrivableNormalY = .62f;   // about 52 degrees

        [MenuItem("Valet Getaway/Roads/Fix Invisible Barriers", priority = 300)]
        public static void FixBarriers()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Roads] Edit mode only."); return; }

            var colliders = RoadColliders();
            if (colliders.Count == 0) { Debug.LogError($"[Roads] No generated road meshes found (expected under '{RoadRoot}')."); return; }

            Directory.CreateDirectory(CollisionFolder);
            AssetDatabase.Refresh();

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Fix invisible road barriers");

            int roads = 0, walls = 0, kept = 0;
            foreach (var collider in colliders)
            {
                var mesh = collider.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;

                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                var keepList = new List<int>(triangles.Length);
                int dropped = 0;

                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (normal.sqrMagnitude < 1e-12f) { dropped++; continue; }
                    // The mesh is built in world-aligned space under an unrotated root, so the local normal is the
                    // world normal; take the absolute so winding does not decide what counts as a floor.
                    normal = collider.transform.TransformDirection(normal.normalized);
                    if (Mathf.Abs(normal.y) < DrivableNormalY) { dropped++; continue; }
                    keepList.Add(triangles[t]); keepList.Add(triangles[t + 1]); keepList.Add(triangles[t + 2]);
                }

                roads++;
                kept += keepList.Count / 3;
                if (dropped == 0) continue;
                walls += dropped;

                var surface = new Mesh { name = collider.gameObject.name + " [drivable]", indexFormat = IndexFormat.UInt32 };
                surface.SetVertices(vertices);
                surface.SetTriangles(keepList, 0);
                surface.RecalculateNormals();
                surface.RecalculateBounds();

                string path = AssetDatabase.GenerateUniqueAssetPath(CollisionFolder + "/" + Safe(collider.gameObject.name) + ".asset");
                AssetDatabase.CreateAsset(surface, path);

                Undo.RecordObject(collider, "Fix invisible road barriers");
                collider.sharedMesh = null;
                collider.sharedMesh = surface;
                EditorUtility.SetDirty(collider);
            }

            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            string report = $"{roads} road mesh(es) given drivable-only collision: {walls} wall face(s) removed, " +
                            $"{kept} drivable face(s) kept. Collision meshes in {CollisionFolder}.";
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/RoadBarrierFix.txt", report);
            Debug.Log("[Roads] " + report);
        }

        /// <summary>Every generated road surface in the scene, wherever it was parented.</summary>
        static List<MeshCollider> RoadColliders()
        {
            var roads = GameObject.Find(RoadRoot);
            var list = new List<MeshCollider>();
            foreach (var collider in Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include))
            {
                if (collider == null) continue;
                // Do not strip bridge railings, supports, buildings or ramp piers.
                if (roads != null && collider.transform.IsChildOf(roads.transform)
                    && collider.gameObject.name.EndsWith("[Broken Vector road mesh]")) list.Add(collider);
            }
            return list;
        }

        static string Safe(string name)
        {
            foreach (var bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
            return name.Replace('[', '(').Replace(']', ')');
        }
    }
}
