using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ValetGetaway.Editor
{
    public static class HotelSceneSetup
    {
        private static void FixStartingView()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            const string flag = "Valet.Hotel.StartView.v2";
            if (SessionState.GetBool(flag, false)) return;
            Transform root = GameObject.Find("Valet Getaway Prototype").transform;
            Transform point = root.Find("Hotel Return Point");
            if (point == null) return;
            Transform hotel = root.Find("Test Area/Buildings/Hotel");
            hotel.localScale = new Vector3(19f, 13f, 19f);
            hotel.position = new Vector3(-20f, 6.7f, -20f);
            point.SetPositionAndRotation(new Vector3(-8f, 0.1f, -4f), Quaternion.Euler(0, 180, 0));
            var player = Object.FindAnyObjectByType<ThirdPersonPlayerController>();
            player.transform.SetPositionAndRotation(point.position, point.rotation);
            Object.FindAnyObjectByType<OrbitFollowCamera>().SetTarget(player.transform, true);
            EditorSceneManager.MarkSceneDirty(point.gameObject.scene);
            EditorSceneManager.SaveScene(point.gameObject.scene);
            SessionState.SetBool(flag, true);
            Debug.Log("VALET HOTEL START VIEW SAVED");
        }

        [MenuItem("Valet Getaway/Set Up Hotel Gameplay %#h")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Object.FindAnyObjectByType<HotelGameSession>() != null) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != TestCitySceneBuilder.ScenePath) return;
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/ValetTestCity_BeforeHotel.unity", true);
            Transform root = GameObject.Find("Valet Getaway Prototype").transform;
            var player = Object.FindAnyObjectByType<ThirdPersonPlayerController>();
            var camera = Object.FindAnyObjectByType<OrbitFollowCamera>();
            var delivery = Object.FindAnyObjectByType<BuyerDeliveryManager>();
            var car = Object.FindAnyObjectByType<VehicleInteraction>();
            Transform area = root.Find("Test Area");
            Transform hotel = root.Find("Test Area/Buildings/Hotel");
            Material green = Material("Hotel Green", new Color(0.08f, 0.38f, 0.31f));
            Material white = Material("Signs White", new Color(0.94f, 0.97f, 0.98f));
            Material yellow = AssetDatabase.LoadAssetAtPath<Material>("Assets/ValetGetaway/Materials/Accent.mat");
            Material glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/ValetGetaway/Materials/Glass.mat");
            hotel.GetComponent<Renderer>().sharedMaterial = green;
            // World-space additions keep the existing hotel's transform and prefab editable.
            Transform entrance = Group("Hotel Entrance and Valet Lane", area);
            Block("Entrance Doors", entrance, new Vector3(-10.3f, 2f, -13f), new Vector3(0.3f, 4f, 4f), glass);
            Block("Entrance Canopy", entrance, new Vector3(-8f, 4.2f, -13f), new Vector3(6f, 0.4f, 9f), green);
            Block("Canopy Pillar A", entrance, new Vector3(-6.5f, 2f, -17f), new Vector3(0.3f, 4f, 0.3f), white);
            Block("Canopy Pillar B", entrance, new Vector3(-6.5f, 2f, -9f), new Vector3(0.3f, 4f, 0.3f), white);
            Sign("HOTEL", entrance, new Vector3(-10.2f, 6f, -20f), Quaternion.Euler(0, -90, 0), 1.2f, Color.white);
            Sign("VALET", entrance, new Vector3(-4f, 0.04f, -7f), Quaternion.Euler(90, 0, 0), 0.6f, Color.yellow);
            for (int i = 0; i < 3; i++)
            {
                float z = -12f - 9f * i;
                Block("Valet Bay Stripe", entrance, new Vector3(-4, 0.025f, z - 3), new Vector3(3f, 0.03f, 0.15f), yellow, false);
            }
            for (int floor = 0; floor < 3; floor++)
            for (int window = 0; window < 4; window++)
                Block("Hotel Window", entrance, new Vector3(-10.35f, 3f + floor * 3f, -27f + window * 4f), new Vector3(0.12f, 1.7f, 2f), glass, false);

            Transform returnPoint = Group("Hotel Return Point", root);
            returnPoint.position = new Vector3(-8f, 0.3f, -7f);
            player.transform.SetPositionAndRotation(new Vector3(-8f, 0.3f, -12f), Quaternion.Euler(0, 90, 0));
            camera.SetTarget(player.transform, true);
            Transform arrivals = Group("Hotel Guest Arrivals", root);
            HotelGuestCar[] guests = new HotelGuestCar[3];
            string[] names = { "Alex", "Morgan", "Jamie" };
            Color[] colors = { new Color(0.88f, 0.17f, 0.10f), new Color(0.12f, 0.5f, 0.75f), new Color(0.85f, 0.8f, 0.65f) };
            GameObject template = car.gameObject;
            GameObject[] cars = { template, Object.Instantiate(template), Object.Instantiate(template) };
            for (int i = 0; i < cars.Length; i++)
            {
                GameObject vehicle = cars[i];
                vehicle.name = $"Guest Car {i + 1} - {names[i]}";
                vehicle.transform.SetParent(arrivals);
                Vector3 queue = new Vector3(-4f, 0.12f, -12f - 9f * i);
                vehicle.transform.SetPositionAndRotation(queue, Quaternion.identity);
                vehicle.transform.Find("Car Body - Replace Me").GetComponent<Renderer>().sharedMaterial = Material($"Guest Car {i + 1}", colors[i]);
                GameObject guest = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                guest.name = $"Guest {names[i]}";
                guest.transform.SetParent(arrivals);
                guest.transform.position = queue + new Vector3(-3.2f, 1.1f, 0f);
                guest.GetComponent<Renderer>().sharedMaterial = white;
                Block("Guest Face", guest.transform, guest.transform.position + new Vector3(0, 0.45f, 0.46f), new Vector3(0.28f, 0.18f, 0.12f), glass, false);
                var guestCar = vehicle.AddComponent<HotelGuestCar>();
                guestCar.Configure(guest.transform, new Vector3(-4f, 0.12f, -72f), queue, i * 9f, 2500 + i * 1500, names[i]);
                guests[i] = guestCar;
            }

            Transform stopsRoot = Group("Bus Stops", area);
            Vector3[] locations = { new Vector3(-7f, 0, 2f), new Vector3(8f, 0, 35f), new Vector3(-48f, 0, 35f), new Vector3(48f, 0, -35f), new Vector3(48f, 0, 35f) };
            HotelBusStop[] stops = new HotelBusStop[locations.Length];
            Material cyan = Material("Bus Cyan", new Color(0.04f, 0.63f, 0.73f));
            for (int i = 0; i < locations.Length; i++)
            {
                Vector3 p = locations[i];
                Transform stop = Group($"Bus Stop {i + 1}", stopsRoot);
                stop.position = p;
                Block("Shelter Roof", stop, p + new Vector3(0, 3.2f, 0), new Vector3(4, 0.25f, 2.8f), cyan);
                Block("Left Post", stop, p + new Vector3(-1.8f, 1.6f, 1), new Vector3(0.15f, 3.2f, 0.15f), white);
                Block("Right Post", stop, p + new Vector3(1.8f, 1.6f, 1), new Vector3(0.15f, 3.2f, 0.15f), white);
                Block("Bench", stop, p + new Vector3(0, 0.6f, 0.7f), new Vector3(2.8f, 0.3f, 0.6f), cyan);
                Sign("BUS", stop, p + new Vector3(0, 3.8f, -0.1f), Quaternion.identity, 0.75f, Color.cyan);
                stops[i] = stop.gameObject.AddComponent<HotelBusStop>();
            }
            var session = Group("Hotel Game Session", root).gameObject.AddComponent<HotelGameSession>();
            session.Configure(player, camera, delivery, returnPoint, guests, stops);
            var serialized = new SerializedObject(delivery);
            GameObject beacon = (GameObject)serialized.FindProperty("marker").objectReferenceValue;
            beacon.transform.localScale = new Vector3(0.6f, 2.5f, 0.6f);
            ((LineRenderer)serialized.FindProperty("parkingBoundary").objectReferenceValue).widthMultiplier = 0.25f;
            if (!AssetDatabase.IsValidFolder("Assets/ValetGetaway/Prefabs/Vehicles")) AssetDatabase.CreateFolder("Assets/ValetGetaway/Prefabs", "Vehicles");
            PrefabUtility.SaveAsPrefabAsset(stops[0].gameObject, "Assets/ValetGetaway/Prefabs/Buildings/BusStop.prefab");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = entrance.gameObject;
            Debug.Log("VALET HOTEL SCENE SAVED: hotel entrance, 3 arriving guests, 5 bus stops, wallet HUD.");
            FixStartingView();
        }

        private static Transform Group(string name, Transform parent)
        {
            Transform result = new GameObject(name).transform;
            result.SetParent(parent, false);
            return result;
        }

        private static Material Material(string name, Color color)
        {
            string path = $"Assets/ValetGetaway/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Block(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collider = true)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, true);
            block.transform.position = position;
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(block.GetComponent<Collider>());
        }

        private static void Sign(string text, Transform parent, Vector3 position, Quaternion rotation, float size, Color color)
        {
            GameObject sign = new GameObject(text + " Sign", typeof(TextMesh));
            sign.transform.SetParent(parent);
            sign.transform.SetPositionAndRotation(position, rotation);
            TextMesh label = sign.GetComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sign.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.text = text; label.fontSize = 64; label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = color;
        }
    }
}
