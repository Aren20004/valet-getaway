// Police station set -> Valet Getaway island scene.
//
// The set was authored in Blender (E:\ValetGame_Police) already in this game's world space: the site origin is
// (458, 0.3, -134), exactly where IslandCityBuilder put the four-box "Police station - placeholder" west of the hotel
// car park. PD_Site_Layout.json carries every piece's Unity position and yaw, so this rebuilds the site from the
// per-asset FBX files and clears the placeholder away.
//
//   Valet Getaway > Police Station > Build Site            rebuild from the 34 part FBXs (editable, one object per piece)
//   Valet Getaway > Police Station > Build Site (one mesh)  drop PD_Site_Full.fbx instead (fewer objects, not editable)
//   Valet Getaway > Police Station > Fix Materials (URP)    repoint imported materials at the URP lit shader
//
// Idempotent: each build removes its own previous root first. Everything is undoable.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    public static class PoliceStationSite
    {
        const string Folder = "Assets/ValetGetaway/Police";
        const string Models = Folder + "/Models";
        const string LayoutPath = Folder + "/PD_Site_Layout.json";
        const string RootName = "07 Police station - precinct";
        const string PlaceholderName = "Police station - placeholder";
        const string HotelRoot = "06 Hotel island - arrival precinct";

        // Pieces that are gameplay actors or scenery people walk through: no collider, not static.
        static readonly string[] NoCollider = { "PoliceOfficer", "PoliceChief", "SWAT_Officer", "TapeStand" };

        [Serializable] class Layout { public SiteOrigin site_origin; public Placement[] placements; }
        [Serializable] class SiteOrigin { public float[] unity_world; }
        [Serializable] class Placement
        {
            public string name;
            public string asset;
            public string file;
            public float[] unity_world;
            public float[] unity_rotation_euler;
        }

        static Vector3 V(float[] a, Vector3 fallback = default) =>
            a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : fallback;

        // ---------------- Build from the part FBXs ----------------

        [MenuItem("Valet Getaway/Police Station/Build Site", priority = 200)]
        public static void BuildSite()
        {
            var layout = LoadLayout();
            if (layout == null) return;
            if (layout.placements == null || layout.placements.Length == 0)
            {
                Debug.LogError("[PoliceStation] PD_Site_Layout.json has no placements.");
                return;
            }

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build police station site");

            ClearPlaceholder();
            var root = FreshRoot();

            var missing = new List<string>();
            var byAsset = new Dictionary<string, GameObject>();
            int placed = 0, colliders = 0;

            foreach (var p in layout.placements)
            {
                if (string.IsNullOrEmpty(p.file)) continue;
                if (!byAsset.TryGetValue(p.file, out var model))
                {
                    model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/" + p.file);
                    byAsset[p.file] = model;
                    if (model == null) missing.Add(p.file);
                }
                if (model == null) continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.name = string.IsNullOrEmpty(p.name) ? Path.GetFileNameWithoutExtension(p.file) : p.name;
                instance.transform.SetPositionAndRotation(V(p.unity_world), Quaternion.Euler(V(p.unity_rotation_euler)));
                instance.transform.localScale = Vector3.one;

                bool solid = !NoCollider.Any(n => (p.asset ?? p.file).Contains(n, StringComparison.OrdinalIgnoreCase));
                if (solid) colliders += AddColliders(instance);
                SetStatic(instance, solid);
                placed++;
            }

            Finish(root, placed, colliders, missing, "parts");
            Undo.CollapseUndoOperations(group);
        }

        // ---------------- Build from the single combined FBX ----------------

        [MenuItem("Valet Getaway/Police Station/Build Site (one mesh)", priority = 201)]
        public static void BuildSiteSingle()
        {
            var layout = LoadLayout();
            if (layout == null) return;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/PD_Site_Full.fbx");
            if (model == null)
            {
                Debug.LogError($"[PoliceStation] {Models}/PD_Site_Full.fbx is missing.");
                return;
            }

            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build police station site (one mesh)");

            ClearPlaceholder();
            var root = FreshRoot();

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.name = "PD_Site_Full";
            instance.transform.SetPositionAndRotation(V(layout.site_origin?.unity_world, new Vector3(458f, 0.3f, -134f)), Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            int colliders = AddColliders(instance);
            SetStatic(instance, true);

            Finish(root, 1, colliders, new List<string>(), "one mesh");
            Undo.CollapseUndoOperations(group);
        }

        // ---------------- Orientation ----------------

        [MenuItem("Valet Getaway/Police Station/Rotate Site 180°", priority = 212)]
        public static void RotateSite180() => RotateSite(180f);

        [MenuItem("Valet Getaway/Police Station/Rotate Site 90°", priority = 213)]
        public static void RotateSite90() => RotateSite(90f);

        /// <summary>
        /// Spin the whole precinct about its own origin. The set was authored with the station entrance facing Unity -Z;
        /// if the road runs along another side of the plot, turn it until the entrance faces the road.
        /// </summary>
        public static void RotateSite(float degrees)
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                Debug.LogError($"[PoliceStation] No '{RootName}' in the scene. Build the site first.");
                return;
            }
            var layout = LoadLayout();
            var pivot = V(layout?.site_origin?.unity_world, new Vector3(458f, 0.3f, -134f));

            Undo.RegisterFullObjectHierarchyUndo(root, "Rotate police station site");
            root.transform.RotateAround(pivot, Vector3.up, degrees);
            Physics.SyncTransforms();

            float yaw = Mathf.Repeat(root.transform.eulerAngles.y, 360f);
            Debug.Log($"[PoliceStation] Rotated {degrees:0}° about {pivot}. Site yaw is now {yaw:0}°. " +
                      "The entrance faces " + Facing(yaw) + ".");
            Selection.activeGameObject = root;
            SceneView.RepaintAll();
        }

        /// <summary>Which way the entrance points, given the root's yaw (it faces -Z at yaw 0).</summary>
        static string Facing(float yaw) => (Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 90f) % 4) switch
        {
            0 => "-Z", 1 => "+X", 2 => "+Z", _ => "-X",
        };

        // ---------------- Materials ----------------

        [MenuItem("Valet Getaway/Police Station/Fix Materials (URP)", priority = 220)]
        public static void FixMaterials()
        {
            var shader = PipelineShader();
            if (shader == null) { Debug.LogError("[PoliceStation] No lit shader found for this render pipeline."); return; }

            int fixedCount = 0, looked = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Folder }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null) continue;
                looked++;
                if (m.shader == shader) continue;
                Color colour = m.HasProperty("_Color") ? m.color
                    : m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                Undo.RecordObject(m, "Fix police material");
                m.shader = shader;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
                if (m.HasProperty("_Color")) m.color = colour;
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .12f);
                EditorUtility.SetDirty(m);
                fixedCount++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[PoliceStation] Materials: {fixedCount} of {looked} repointed at {shader.name}.");
        }

        static Shader PipelineShader()
        {
            var current = GraphicsSettings.currentRenderPipeline;
            if (current != null)
            {
                string pipeline = current.GetType().Name;
                if (pipeline.Contains("Universal")) return Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (pipeline.Contains("HDRenderPipeline")) return Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            }
            return Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
        }

        // ---------------- Helpers ----------------

        static Layout LoadLayout()
        {
            if (!File.Exists(LayoutPath))
            {
                Debug.LogError($"[PoliceStation] {LayoutPath} is missing. Copy the set's PD_Site_Layout.json and export/*.fbx into {Folder}.");
                return null;
            }
            try { return JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath)); }
            catch (Exception e) { Debug.LogError("[PoliceStation] Could not read the layout: " + e.Message); return null; }
        }

        /// <summary>Delete the greybox boxes IslandCityBuilder made, wherever they are in the scene.</summary>
        static void ClearPlaceholder()
        {
            int removed = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).ToList())
            {
                if (t == null || t.name != PlaceholderName) continue;
                Undo.DestroyObjectImmediate(t.gameObject);
                removed++;
            }
            if (removed > 0) Debug.Log($"[PoliceStation] Removed {removed} greybox placeholder group(s).");
        }

        static GameObject FreshRoot()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null) Undo.DestroyObjectImmediate(existing);
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Police station root");
            return root;
        }

        static int AddColliders(GameObject instance)
        {
            int n = 0;
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                var mc = filter.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = filter.sharedMesh;
                mc.convex = false;
                n++;
            }
            return n;
        }

        static void SetStatic(GameObject instance, bool isStatic)
        {
            if (!isStatic) return;
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
        }

        static void Finish(GameObject root, int placed, int colliders, List<string> missing, string mode)
        {
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();

            string report = $"Police station site built ({mode}): {placed} object(s), {colliders} mesh collider(s).";
            if (missing.Count > 0)
                report += $" MISSING {missing.Distinct().Count()} model(s) in {Models}: {string.Join(", ", missing.Distinct())}.";
            report += $" Hotel root present: {GameObject.Find(HotelRoot) != null}.";

            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/PoliceStationSite.txt", report);
            if (missing.Count > 0) Debug.LogWarning("[PoliceStation] " + report);
            else Debug.Log("[PoliceStation] " + report);
        }
    }
}
