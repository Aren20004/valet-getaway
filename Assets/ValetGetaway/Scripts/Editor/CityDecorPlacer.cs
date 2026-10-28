// Scatters the generated trees and street props along the island's roads.
//
//   Valet Getaway / City Art / 3. Place Decorations
//
// Placement data is VG_CityDecor.json, extracted from the Road(...) and garden Points(...) calls in
// IslandCityBuilder.cs - the same centrelines the roads were built from, so props sit at a known
// offset from the real kerb rather than being eyeballed. "blocks" holds every lot footprint plus the
// hotel terrace, so a prop whose kerb offset lands inside a building is dropped.
//
// Everything lands under the same "10 City art - generated" root as the buildings, so
// City Art > Undo Placement removes decorations too.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ValetGetaway.EditorTools
{
    public static class CityDecorPlacer
    {
        const string Models = "Assets/ValetGetaway/City/Models";
        const string DataPath = "Assets/ValetGetaway/City/VG_CityDecor.json";
        const string RootName = "10 City art - generated";

        // spacing in metres along each kerb
        const float LightEvery = 38f;
        const float TreeEvery = 24f;
        const float BenchEvery = 95f;
        const float BinEvery = 115f;
        const float HydrantEvery = 150f;
        const float KerbMargin = 3.4f;   // outward from the road edge

        // JsonUtility has no support for nested collections, so every coordinate list is stored
        // flat and unpacked here: pts is x,z,x,z..., blocks is centreX,centreZ,halfW,halfD...
        [Serializable] class Road { public string name; public float width; public float[] pts; }
        [Serializable] class Decor { public Road[] roads; public float[] garden; public float[] blocks; }

        static readonly string[] StreetTrees =
        {
            "VG_Tree_Broadleaf", "VG_Tree_Oak", "VG_Tree_Columnar", "VG_Tree_Flowering",
            "VG_Tree_Autumn", "VG_Tree_Birch", "VG_Tree_Cypress", "VG_Tree_Topiary",
            "VG_Tree_Willow", "VG_Tree_PalmShort", "VG_Tree_Spruce", "VG_Tree_Pine",
        };
        static readonly string[] GardenTrees =
        {
            "VG_Tree_Oak", "VG_Tree_Broadleaf", "VG_Tree_Palm", "VG_Tree_Flowering", "VG_Tree_Willow",
        };

        static float[] _blocks = new float[0];
        static int _sBlock, _sNoGround, _sHigh, _sNoModel;

        [MenuItem("Valet Getaway/City Art/3. Place Decorations", priority = 303)]
        public static void Place()
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                Debug.LogError("[VG] Run '2. Place City Art' first - decorations go under the same root.");
                return;
            }
            var existing = root.transform.Find("Decorations");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            string json = File.Exists(DataPath) ? File.ReadAllText(DataPath) : null;
            if (string.IsNullOrEmpty(json)) { Debug.LogError($"[VG] Missing {DataPath}"); return; }
            var data = JsonUtility.FromJson<Decor>(json);
            if (data == null || data.roads == null || data.roads.Length == 0)
            { Debug.LogError("[VG] Could not parse decor data."); return; }
            _blocks = data.blocks ?? new float[0];

            var group = new GameObject("Decorations").transform;
            group.SetParent(root.transform);
            var trees = Child(group, "Trees");
            var lights = Child(group, "Street lights");
            var furniture = Child(group, "Street furniture");
            var junctions = Child(group, "Junction props");

            var cache = new Dictionary<string, GameObject>();
            int nTree = 0, nLight = 0, nFurn = 0, nJunc = 0, skipped = 0;
            _sBlock = _sNoGround = _sHigh = _sNoModel = 0;
            int seed = 0;

            foreach (var road in data.roads)
            {
                var pts = ToPoints(road.pts);
                if (pts.Count < 2) continue;
                float half = road.width * 0.5f + KerbMargin;
                float len = Length(pts);
                if (len < 30f) continue;

                // one pass per side of the road
                for (int side = -1; side <= 1; side += 2)
                {
                    for (float t = 12f; t < len - 12f; t += TreeEvery)
                    {
                        seed++;
                        Vector3 p = PointAt(pts, t, out Vector3 tangent);
                        Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x) * side;
                        Vector3 at = p + normal * half;

                        bool lightHere = Mathf.Repeat(t, LightEvery) < TreeEvery && side > 0;
                        string prefab = lightHere ? "VG_StreetLight" : StreetTrees[seed % StreetTrees.Length];

                        var inst = Spawn(cache, prefab, lightHere ? lights : trees, at,
                                         Quaternion.LookRotation(-normal, Vector3.up), out bool ok);
                        if (!ok) { skipped++; continue; }
                        if (lightHere) nLight++;
                        else
                        {
                            nTree++;
                            float s = 0.85f + (seed % 7) * 0.06f;
                            inst.transform.localScale = Vector3.one * s;
                            inst.transform.rotation = Quaternion.Euler(0f, (seed * 47) % 360, 0f);
                        }
                    }

                    for (float t = 40f; t < len - 20f; t += BenchEvery)
                    {
                        seed++;
                        Vector3 p = PointAt(pts, t, out Vector3 tangent);
                        Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x) * side;
                        Spawn(cache, "VG_Bench", furniture, p + normal * (half - 0.9f),
                              Quaternion.LookRotation(-normal, Vector3.up), out bool ok);
                        if (ok) nFurn++; else skipped++;
                    }
                    for (float t = 70f; t < len - 20f; t += BinEvery)
                    {
                        seed++;
                        Vector3 p = PointAt(pts, t, out Vector3 tangent);
                        Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x) * side;
                        Spawn(cache, "VG_TrashCan", furniture, p + normal * (half - 0.7f),
                              Quaternion.identity, out bool ok);
                        if (ok) nFurn++; else skipped++;
                    }
                    for (float t = 95f; t < len - 20f; t += HydrantEvery)
                    {
                        seed++;
                        Vector3 p = PointAt(pts, t, out Vector3 tangent);
                        Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x) * side;
                        Spawn(cache, "VG_Hydrant", furniture, p + normal * (half - 0.5f),
                              Quaternion.LookRotation(-normal, Vector3.up), out bool ok);
                        if (ok) nFurn++; else skipped++;
                    }
                }

                // junction dressing at each end: a traffic light on wide roads, bollards otherwise
                for (int end = 0; end < 2; end++)
                {
                    seed++;
                    float t = end == 0 ? 6f : len - 6f;
                    Vector3 p = PointAt(pts, t, out Vector3 tangent);
                    Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x);
                    string prefab = road.width >= 16f ? "VG_TrafficLight" : "VG_Bollard";
                    Spawn(cache, prefab, junctions, p + normal * half,
                          Quaternion.LookRotation(-tangent, Vector3.up), out bool ok);
                    if (ok) nJunc++; else skipped++;
                }
            }

            // hotel garden: real trees where the placeholder spheres are
            int nGarden = 0;
            if (data.garden != null && data.garden.Length >= 2)
            {
                var garden = Child(group, "Hotel garden");
                for (int i = 0; i + 1 < data.garden.Length; i += 2)
                {
                    int k = i / 2;
                    var at = new Vector3(data.garden[i], 0f, data.garden[i + 1]);
                    // the garden sits on the hotel terrace, which IS one of the exclusion blocks
                    var inst = Spawn(cache, GardenTrees[k % GardenTrees.Length], garden, at,
                                     Quaternion.Euler(0f, (k * 61) % 360, 0f), out bool ok, ignoreBlocks: true);
                    if (!ok) { skipped++; continue; }
                    inst.transform.localScale = Vector3.one * (1.5f + (k % 4) * 0.22f);
                    nGarden++;
                }
            }

            // the 210 scattered parkland spheres: a real tree at each one's position
            int nPark = 0;
            var park = Child(group, "Parkland trees");
            // Include inactive: an earlier run may already have switched the spheres off.
            foreach (var crown in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (crown.name != "Tree crown") continue;
                var cp = crown.transform.position;
                var inst = Spawn(cache, StreetTrees[nPark % StreetTrees.Length], park, new Vector3(cp.x, 0f, cp.z),
                                 Quaternion.Euler(0f, (nPark * 73) % 360, 0f), out bool okp, ignoreBlocks: true);
                if (okp) { inst.transform.localScale = Vector3.one * (1.1f + (nPark % 5) * 0.14f); nPark++; }
                else skipped++;
            }

            int hidden = HideOldFoliage();
            Debug.Log($"[VG] Decorations placed: {nTree} tree(s), {nLight} street light(s), " +
                      $"{nFurn} bench/bin/hydrant, {nJunc} junction prop(s), {nGarden} garden tree(s), {nPark} parkland tree(s). " +
                      $"{skipped} skipped ({_sBlock} in a lot, {_sNoGround} no ground, {_sHigh} on a roof, {_sNoModel} no model). " +
                      $"{hidden} placeholder sphere(s) disabled.");
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        // -------------------------------------------------------------------------------------
        /// <summary>
        /// Drops a prop onto the ground, refusing to place it inside a building. The buildings are
        /// imported meshes with no colliders, so the overlap test is against the authored lot
        /// rectangles rather than physics; the raycast only finds the ground height.
        /// </summary>
        static GameObject Spawn(Dictionary<string, GameObject> cache, string modelName, Transform parent,
                                Vector3 at, Quaternion rot, out bool ok, bool ignoreBlocks = false)
        {
            ok = false;
            if (!ignoreBlocks && InsideBlock(at.x, at.z)) { _sBlock++; return null; }

            var model = Load(cache, modelName);
            if (model == null) { _sNoModel++; return null; }

            if (!Physics.Raycast(new Vector3(at.x, 400f, at.z), Vector3.down, out RaycastHit hit, 800f))
            { _sNoGround++; return null; }
            // standing on something tall is a roof or a wall, not a verge
            if (hit.point.y > 6f) { _sHigh++; return null; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            inst.transform.SetPositionAndRotation(hit.point, rot);
            foreach (var c in inst.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            ok = true;
            return inst;
        }

        /// <summary>Axis-aligned test against every lot footprint, grown by a 1 m clearance.</summary>
        static bool InsideBlock(float x, float z)
        {
            const float pad = 1.0f;
            for (int i = 0; i + 3 < _blocks.Length; i += 4)
            {
                if (Mathf.Abs(x - _blocks[i]) < _blocks[i + 2] + pad &&
                    Mathf.Abs(z - _blocks[i + 1]) < _blocks[i + 3] + pad) return true;
            }
            return false;
        }

        static GameObject Load(Dictionary<string, GameObject> cache, string name)
        {
            if (cache.TryGetValue(name, out var cached)) return cached;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{name}.fbx");
            if (go == null) Debug.LogWarning($"[VG] No model {name}.fbx in {Models}");
            cache[name] = go;
            return go;
        }

        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent);
            return t;
        }

        static int HideOldFoliage()
        {
            int n = 0;
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!go.activeSelf) continue;
                if (go.name != "Hotel garden tree" && go.name != "Garden planter" &&
                    go.name != "Tree trunk" && go.name != "Tree crown") continue;
                go.SetActive(false);
                n++;
            }
            return n;
        }

        // ---- polyline helpers ------------------------------------------------------------------
        static List<Vector3> ToPoints(float[] flat)
        {
            var o = new List<Vector3>();
            if (flat == null) return o;
            for (int i = 0; i + 1 < flat.Length; i += 2) o.Add(new Vector3(flat[i], 0f, flat[i + 1]));
            return o;
        }

        static float Length(List<Vector3> p)
        {
            float l = 0f;
            for (int i = 0; i < p.Count - 1; i++) l += Vector3.Distance(p[i], p[i + 1]);
            return l;
        }

        static Vector3 PointAt(List<Vector3> p, float dist, out Vector3 tangent)
        {
            tangent = Vector3.forward;
            float acc = 0f;
            for (int i = 0; i < p.Count - 1; i++)
            {
                float seg = Vector3.Distance(p[i], p[i + 1]);
                if (acc + seg >= dist)
                {
                    float f = seg < 0.0001f ? 0f : (dist - acc) / seg;
                    tangent = (p[i + 1] - p[i]).normalized;
                    return Vector3.Lerp(p[i], p[i + 1], f);
                }
                acc += seg;
            }
            tangent = (p[p.Count - 1] - p[p.Count - 2]).normalized;
            return p[p.Count - 1];
        }
    }
}
