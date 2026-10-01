using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    public static class KenneySceneSetup
    {
        private const string Request = "Library/ValetApplyKenney.request";

        [InitializeOnLoadMethod]
        private static void Schedule() => EditorApplication.update += ApplyRequested;

        private static void ApplyRequested()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Request) && AssetDatabase.LoadAssetAtPath<Material>("Assets/Kenney/CityKitCommercial/KenneyPalette.mat") == null) return;
            EditorApplication.update -= ApplyRequested;
            if (!File.Exists(Request)) return;
            File.Delete(Request);
            try { Apply(); HotelGameplayValidation.Run(); }
            catch (Exception error) { Debug.LogError("VALET KENNEY SETUP FAILED: " + error); }
        }

        [MenuItem("Valet Getaway/Apply Kenney Roads and Cars")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != TestCitySceneBuilder.ScenePath)
                throw new InvalidOperationException("Open ValetTestCity outside Play mode first.");
            Transform root = GameObject.Find("Valet Getaway Prototype").transform;
            if (root.Find("Test Area/Kenney Roads") != null)
            {
                FinalizeReplacements(root);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Capture();
                return;
            }
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/ValetTestCity_BeforeKenney.unity", true);
            var straight = Model("CityKitRoads", "road-straight");
            var crossing = Model("CityKitRoads", "road-crossroad");
            string[] models = { "sedan", "hatchback-sports", "suv-luxury" };
            var session = Object.FindAnyObjectByType<HotelGameSession>();
            if (session == null) throw new InvalidOperationException("Hotel gameplay is missing.");
            Transform roads = new GameObject("Kenney Roads").transform;
            roads.SetParent(root.Find("Test Area"), false);
            for (int x = -7; x <= 7; x++)
            for (int z = -7; z <= 7; z++)
            {
                bool northSouth = x == -4 || x == 0 || x == 4;
                bool eastWest = z == -4 || z == 0 || z == 4;
                if (!northSouth && !eastWest) continue;
                var tile = (GameObject)PrefabUtility.InstantiatePrefab(northSouth && eastWest ? crossing : straight);
                tile.name = $"Road {x * 10}, {z * 10}";
                Bounds bounds = BoundsOf(tile);
                tile.transform.localScale = new Vector3(10f / bounds.size.x, 1f, 10f / bounds.size.z);
                tile.transform.rotation = Quaternion.Euler(0f, northSouth && !eastWest ? 90f : 0f, 0f);
                tile.transform.position = new Vector3(x * 10f, 0.015f - bounds.min.y, z * 10f);
                tile.transform.SetParent(roads, true);
                foreach (var collider in tile.GetComponentsInChildren<Collider>()) collider.enabled = false;
            }
            root.Find("Test Area/Road Markings").gameObject.SetActive(false);
            for (int i = 0; i < session.Guests.Length; i++)
            {
                Transform car = session.Guests[i].transform;
                foreach (Transform child in car)
                    if (child.GetComponent<Renderer>() != null) child.gameObject.SetActive(false);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(Model("CarKit", models[i % models.Length]));
                visual.name = "Kenney " + models[i % models.Length];
                Bounds bounds = BoundsOf(visual);
                float scale = 4f / bounds.size.z;
                visual.transform.SetParent(car, false);
                visual.transform.localScale = Vector3.one * scale;
                visual.transform.localPosition = new Vector3(-bounds.center.x * scale, -0.12f - bounds.min.y * scale, -bounds.center.z * scale);
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
                Debug.Log($"VALET KENNEY CAR: {visual.name}, source bounds {bounds.size}, scale {scale}");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            AddBuildings(root);
            FinalizeReplacements(root);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Capture();
            Debug.Log("VALET KENNEY SCENE SAVED: 81 road tiles, 3 guest car models, and 16 commercial buildings.");
        }

        private static void FinalizeReplacements(Transform root)
        {
            Transform models = root.Find("Test Area/Kenney Buildings");
            if (models == null) return;
            foreach (Transform placeholder in root.Find("Test Area/Buildings"))
            {
                Transform visual = models.Find(placeholder.name == "Hotel" ? "Hotel - Kenney" : placeholder.name + " - Kenney");
                if (visual == null) continue;
                Bounds bounds = BoundsOf(visual.gameObject);
                var renderer = placeholder.GetComponent<Renderer>();
                renderer.enabled = false;
                placeholder.position = bounds.center;
                placeholder.localScale = bounds.size;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(placeholder);
            }
            foreach (var label in root.GetComponentsInChildren<TextMesh>())
            {
                label.characterSize = label.text == "HOTEL" ? 0.5f : 0.35f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }
        }

        private static void AddBuildings(Transform root)
        {
            Transform group = new GameObject("Kenney Buildings").transform;
            group.SetParent(root.Find("Test Area"), false);
            int index = 0;
            foreach (Transform placeholder in root.Find("Test Area/Buildings"))
            {
                if (placeholder.name != "Hotel" && !placeholder.name.StartsWith("Building ")) continue;
                bool hotel = placeholder.name == "Hotel";
                string modelName = hotel ? "building-k" : "building-" + (char)('a' + index++ % 14);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(Model("CityKitCommercial", modelName));
                visual.name = hotel ? "Hotel - Kenney" : placeholder.name + " - Kenney";
                Bounds bounds = BoundsOf(visual);
                float scale = 19f / Mathf.Max(bounds.size.x, bounds.size.z);
                Vector3 sizing = new Vector3(scale, hotel ? 13f / bounds.size.y : scale, scale);
                visual.transform.localScale = sizing;
                visual.transform.position = new Vector3(placeholder.position.x - bounds.center.x * sizing.x,
                    0.2f - bounds.min.y * sizing.y, placeholder.position.z - bounds.center.z * sizing.z);
                visual.transform.SetParent(group, true);
                Bounds world = BoundsOf(visual);
                placeholder.GetComponent<Renderer>().enabled = false;
                placeholder.localScale = world.size;
                placeholder.position = world.center;
                PrefabUtility.SaveAsPrefabAsset(visual, "Assets/ValetGetaway/Prefabs/Buildings/" + visual.name + ".prefab");
            }
        }

        private static GameObject Model(string pack, string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Kenney/{pack}/Models/FBX format/{name}.fbx");
            if (model == null) throw new InvalidOperationException("Missing imported Kenney model: " + name);
            return model;
        }

        private static Bounds BoundsOf(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderers: " + model.name);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        [MenuItem("Valet Getaway/Capture Scene Preview")]
        public static void Capture()
        {
            Directory.CreateDirectory("Logs/Previews");
            var cameraObject = new GameObject("Temporary Preview Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.CopyFrom(Object.FindAnyObjectByType<HotelGameSession>().ViewCamera);
            camera.transform.SetPositionAndRotation(new Vector3(47f, 105f, -100f), Quaternion.identity);
            camera.transform.LookAt(new Vector3(0, 0, 0));
            Render(camera, "Logs/Previews/KenneyCity.png");
            camera.transform.position = new Vector3(8f, 10f, -3f);
            camera.transform.LookAt(new Vector3(-10f, 2f, -20f));
            Render(camera, "Logs/Previews/KenneyHotel.png");
            Object.DestroyImmediate(cameraObject);
        }

        private static void Render(Camera camera, string path)
        {
            var target = new RenderTexture(1280, 800, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }
    }
}
