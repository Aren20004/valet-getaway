// Replaces the placeholder Box() geometry in the island city with the generated models.
//
//   Valet Getaway / City Art / 1. Configure Imports
//   Valet Getaway / City Art / 2. Place City Art
//   Valet Getaway / City Art / Undo Placement
//
// Deliberately NON-DESTRUCTIVE: placeholders are only deactivated, never deleted, and everything
// placed goes under one root so it can be removed in one step. Run step 1 once after the FBX land,
// then step 2. Nothing here hand-edits a scene file - all changes go through the editor API, which
// is the convention the rest of this project follows.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ValetGetaway.EditorTools
{
    public static class CityArtImport
    {
        const string Models = "Assets/ValetGetaway/City/Models";
        const string RootName = "10 City art - generated";
        const string PlaceholdersOff = "10 City art - replaced placeholders";

        // ---------------------------------------------------------------------------------------
        // 1. Import settings. The models are authored in metres with +Z forward, so no scaling or
        // axis conversion is wanted; Unity's default 0.01 FBX scale factor would shrink them 100x.
        // ---------------------------------------------------------------------------------------
        [MenuItem("Valet Getaway/City Art/1. Configure Imports", priority = 300)]
        public static void ConfigureImports()
        {
            if (!Directory.Exists(Models)) { Debug.LogError($"[VG] No model folder at {Models}"); return; }
            int n = 0;
            foreach (string path in Directory.GetFiles(Models, "*.fbx", SearchOption.AllDirectories))
            {
                var mi = AssetImporter.GetAtPath(path.Replace('\\', '/')) as ModelImporter;
                if (mi == null) continue;
                mi.globalScale = 1f;
                mi.useFileScale = true;
                mi.importNormals = ModelImporterNormals.Import;   // flat shading is authored in
                mi.importBlendShapes = false;
                mi.importCameras = false;
                mi.importLights = false;
                mi.importAnimation = false;
                mi.animationType = ModelImporterAnimationType.None;
                mi.meshCompression = ModelImporterMeshCompression.Medium;
                mi.isReadable = false;
                mi.addCollider = false;                           // colliders are added per use below
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
                mi.SaveAndReimport();
                n++;
            }
            Debug.Log($"[VG] Configured {n} model(s) in {Models}.");
        }

        // ---------------------------------------------------------------------------------------
        // 2. Placement
        // ---------------------------------------------------------------------------------------
        [MenuItem("Valet Getaway/City Art/2. Place City Art", priority = 301)]
        public static void Place()
        {
            UndoPlacement();   // idempotent: running twice does not stack two copies

            var root = new GameObject(RootName).transform;
            var parked = new GameObject(PlaceholdersOff).transform;
            parked.SetParent(root);
            parked.gameObject.SetActive(false);

            int buildings = PlaceBuildings(root);
            int hotel = PlaceHotel(root);
            int stops = PlaceBusStops(root);
            int hidden = HidePlaceholders();

            Debug.Log($"[VG] City art placed: {buildings} building row(s), {hotel} hotel, {stops} bus stop(s). " +
                      $"{hidden} placeholder object(s) deactivated (not deleted). " +
                      $"Remove everything with Valet Getaway > City Art > Undo Placement.");
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        }

        /// <summary>
        /// Each generated row is named VG_Bld_&lt;District&gt;_&lt;NN&gt; where NN is the 1-based index of the
        /// Lot() call it came from. The scene names its placeholders "Building N - district", so the
        /// index is the join between the two.
        /// </summary>
        static int PlaceBuildings(Transform root)
        {
            var group = new GameObject("Buildings").transform;
            group.SetParent(root);
            int placed = 0;

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!go.name.StartsWith("Building ")) continue;
                string[] bits = go.name.Split(' ');
                if (bits.Length < 2 || !int.TryParse(bits[1], out int index)) continue;

                var model = FindModel($"VG_Bld_", $"_{index:00}");
                if (model == null) continue;

                // The placeholder's Facade child carries the real footprint and world position.
                Transform facade = go.transform.Find("Facade");
                Vector3 pos = facade != null ? facade.position : go.transform.position;
                float ground = facade != null ? facade.position.y - facade.localScale.y * 0.5f : go.transform.position.y;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, group);
                inst.name = model.name;
                inst.transform.position = new Vector3(pos.x, ground, pos.z);
                inst.transform.rotation = go.transform.rotation;
                AddCollider(inst);
                placed++;
            }
            return placed;
        }

        static int PlaceHotel(Transform root)
        {
            var model = FindModel("VG_Hotel", "");
            if (model == null) return 0;
            // Terrace centre, world (924, 168) - the origin the model was authored around.
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
            inst.name = "VG_Hotel";
            inst.transform.position = new Vector3(924f, 0f, 168f);
            AddCollider(inst);
            return 1;
        }

        static int PlaceBusStops(Transform root)
        {
            var model = FindModel("VG_BusStop", "");
            if (model == null) return 0;
            var group = new GameObject("Bus stops").transform;
            group.SetParent(root);
            int n = 0;
            foreach (var stop in Object.FindObjectsByType<HotelBusStop>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, group);
                inst.name = "VG_BusStop (" + stop.name + ")";
                inst.transform.SetPositionAndRotation(stop.transform.position, stop.transform.rotation);
                AddCollider(inst);
                n++;
            }
            return n;
        }

        static void AddCollider(GameObject inst)
        {
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
                if (mf.GetComponent<Collider>() == null)
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
        }

        /// <summary>Deactivate the placeholder boxes the new art stands in for. Never deletes.</summary>
        static int HidePlaceholders()
        {
            int n = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                bool isLot = go.name.StartsWith("Building ");
                bool isHotel = go.name.StartsWith("Hotel main wing") || go.name.StartsWith("Hotel west wing")
                            || go.name.StartsWith("Hotel east wing") || go.name.StartsWith("Hotel terrace")
                            || go.name.StartsWith("Entrance canopy") || go.name.StartsWith("Canopy pillar");
                bool isStop = go.name == "Shelter Roof" || go.name == "Left Post" || go.name == "Right Post"
                           || go.name == "Bench" || go.name == "BUS Sign";
                if (!isLot && !isHotel && !isStop) continue;
                Undo.RecordObject(go, "Hide placeholder");
                go.SetActive(false);
                n++;
            }
            return n;
        }

        static GameObject FindModel(string prefix, string suffix)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Models }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string n = Path.GetFileNameWithoutExtension(p);
                if (!n.StartsWith(prefix)) continue;
                if (suffix.Length > 0 && !n.EndsWith(suffix)) continue;
                return AssetDatabase.LoadAssetAtPath<GameObject>(p);
            }
            return null;
        }

        // ---------------------------------------------------------------------------------------
        [MenuItem("Valet Getaway/City Art/Undo Placement", priority = 302)]
        public static void UndoPlacement()
        {
            int removed = 0, restored = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name != RootName) continue;
                Object.DestroyImmediate(go);
                removed++;
            }
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool isLot = go.name.StartsWith("Building ");
                bool isHotel = go.name.StartsWith("Hotel ") || go.name.StartsWith("Entrance canopy")
                            || go.name.StartsWith("Canopy pillar");
                bool isStop = go.name == "Shelter Roof" || go.name == "Left Post" || go.name == "Right Post"
                           || go.name == "Bench" || go.name == "BUS Sign";
                if ((isLot || isHotel || isStop) && !go.activeSelf) { go.SetActive(true); restored++; }
            }
            if (removed + restored > 0)
                Debug.Log($"[VG] Undo: removed {removed} art root(s), re-enabled {restored} placeholder(s).");
        }
    }
}
