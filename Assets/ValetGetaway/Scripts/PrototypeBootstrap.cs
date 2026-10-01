#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace ValetGetaway
{
    public static class PrototypeBootstrap
    {
        private static Material asphaltMaterial;
        private static Material concreteMaterial;
        private static Material playerMaterial;
        private static Material carMaterial;
        private static Material glassMaterial;
        private static Material accentMaterial;

        public static void BuildPrototype()
        {
            CreateMaterials();
            GameObject prototype = new GameObject("Valet Getaway Prototype");

            BuildTestArea(prototype.transform);
            ThirdPersonPlayerController player = BuildPlayer(prototype.transform);
            OrbitFollowCamera followCamera = BuildCamera(prototype.transform, player.transform);
            player.SetCamera(followCamera.transform);
            BuyerDeliveryManager delivery = new GameObject("Buyer NPCs", typeof(BuyerDeliveryManager)).GetComponent<BuyerDeliveryManager>();
            delivery.transform.SetParent(prototype.transform);
            delivery.Initialize(player.transform, followCamera.GetComponent<Camera>(), concreteMaterial, accentMaterial);
            BuildVehicle(prototype.transform, player, followCamera, delivery);

            new GameObject("Prototype HUD", typeof(PrototypeHud)).transform.SetParent(prototype.transform);
        }

        private static void CreateMaterials()
        {
            asphaltMaterial = CreateMaterial("Asphalt", new Color(0.12f, 0.14f, 0.16f));
            concreteMaterial = CreateMaterial("Concrete", new Color(0.45f, 0.48f, 0.50f));
            playerMaterial = CreateMaterial("Player", new Color(0.12f, 0.55f, 0.92f));
            carMaterial = CreateMaterial("Car", new Color(0.88f, 0.17f, 0.10f));
            glassMaterial = CreateMaterial("Glass", new Color(0.10f, 0.23f, 0.31f));
            accentMaterial = CreateMaterial("Accent", new Color(0.96f, 0.78f, 0.13f));
        }

        private static Material CreateMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                name = materialName,
                color = color
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            string path = $"Assets/ValetGetaway/Materials/{materialName}.mat";
            Material saved = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (saved != null)
            {
                Object.DestroyImmediate(material);
                return saved;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void BuildTestArea(Transform parent)
        {
            GameObject environment = new GameObject("Test Area");
            environment.transform.SetParent(parent);

            CreateBlock("Ground", environment.transform, new Vector3(0f, -0.5f, 0f), new Vector3(160f, 1f, 160f), asphaltMaterial);

            Transform roads = new GameObject("Road Markings").transform;
            roads.SetParent(environment.transform);
            for (int road = -1; road <= 1; road++)
            {
                for (int i = -16; i <= 16; i++)
                {
                    float along = i * 4.5f;
                    if (Mathf.Abs(along) < 7f || Mathf.Abs(Mathf.Abs(along) - 40f) < 7f) continue;
                    CreateBlock("North South Lane", roads, new Vector3(road * 40f, 0.012f, along), new Vector3(0.15f, 0.02f, 2.2f), accentMaterial, false);
                    CreateBlock("East West Lane", roads, new Vector3(along, 0.012f, road * 40f), new Vector3(2.2f, 0.02f, 0.15f), accentMaterial, false);
                }
            }

            CreateBlock("North Barrier", environment.transform, new Vector3(0f, 1f, 79f), new Vector3(160f, 2f, 0.6f), concreteMaterial);
            CreateBlock("South Barrier", environment.transform, new Vector3(0f, 1f, -79f), new Vector3(160f, 2f, 0.6f), concreteMaterial);
            CreateBlock("East Barrier", environment.transform, new Vector3(79f, 1f, 0f), new Vector3(0.6f, 2f, 160f), concreteMaterial);
            CreateBlock("West Barrier", environment.transform, new Vector3(-79f, 1f, 0f), new Vector3(0.6f, 2f, 160f), concreteMaterial);

            Transform buildings = new GameObject("Buildings").transform;
            buildings.SetParent(environment.transform);
            for (int x = 0; x < 4; x++)
            for (int z = 0; z < 4; z++)
            {
                float px = -60f + x * 40f;
                float pz = -60f + z * 40f;
                float height = 7f + ((x + z * 3) % 4) * 4f;
                CreateBlock("Sidewalk Block", buildings, new Vector3(px, 0.1f, pz), new Vector3(27f, 0.2f, 27f), concreteMaterial);
                CreateBlock(x == 1 && z == 1 ? "Hotel" : $"Building {x + 1}-{z + 1}", buildings, new Vector3(px, height / 2f + 0.2f, pz), new Vector3(19f, height, 19f), concreteMaterial);
            }

            {
                GameObject lightObject = new GameObject("Directional Light", typeof(Light));
                lightObject.transform.SetParent(environment.transform);
                lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
                Light light = lightObject.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.shadows = LightShadows.Soft;
            }
        }

        private static ThirdPersonPlayerController BuildPlayer(Transform parent)
        {
            GameObject player = new GameObject("Player");
            player.transform.SetParent(parent);
            player.transform.position = new Vector3(-3f, 0.05f, -6f);

            CharacterController characterController = player.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.42f;
            characterController.center = Vector3.up;
            characterController.stepOffset = 0.3f;

            ThirdPersonPlayerController controller = player.AddComponent<ThirdPersonPlayerController>();
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Player Visual - Replace Me";
            visual.transform.SetParent(player.transform);
            visual.transform.localPosition = Vector3.up;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = playerMaterial;

            GameObject facing = CreateBlock("Facing Marker", visual.transform, new Vector3(0f, 0.25f, 0.48f), new Vector3(0.25f, 0.25f, 0.16f), accentMaterial, false);
            facing.transform.localPosition = new Vector3(0f, 0.25f, 0.48f);
            return controller;
        }

        private static OrbitFollowCamera BuildCamera(Transform parent, Transform target)
        {
            GameObject cameraObject = new GameObject("Gameplay Camera", typeof(Camera), typeof(AudioListener), typeof(OrbitFollowCamera));
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = target.position + new Vector3(0f, 3f, -6f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 65f;
            camera.nearClipPlane = 0.1f;

            OrbitFollowCamera followCamera = cameraObject.GetComponent<OrbitFollowCamera>();
            followCamera.SetTarget(target, true);
            return followCamera;
        }

        private static void BuildVehicle(Transform parent, ThirdPersonPlayerController player, OrbitFollowCamera followCamera, BuyerDeliveryManager delivery)
        {
            GameObject car = new GameObject("Prototype Car");
            car.transform.SetParent(parent);
            car.transform.position = new Vector3(2f, 0.65f, 1f);

            BoxCollider collider = car.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.45f, 0f);
            collider.size = new Vector3(2.1f, 1.1f, 4.2f);

            Rigidbody body = car.AddComponent<Rigidbody>();
            body.mass = 1100f;
            body.linearDamping = 0.12f;
            body.angularDamping = 4f;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            ArcadeVehicleController vehicle = car.AddComponent<ArcadeVehicleController>();
            VehicleInteraction interaction = car.AddComponent<VehicleInteraction>();

            GameObject lowerBody = CreateBlock("Car Body - Replace Me", car.transform, new Vector3(0f, 0.35f, 0f), new Vector3(2f, 0.65f, 4f), carMaterial, false);
            lowerBody.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            GameObject cabin = CreateBlock("Cabin", car.transform, new Vector3(0f, 0.95f, -0.25f), new Vector3(1.65f, 0.75f, 1.9f), glassMaterial, false);
            cabin.transform.localPosition = new Vector3(0f, 0.95f, -0.25f);
            CreateBlock("Front Marker", car.transform, new Vector3(0f, 0.5f, 2.05f), new Vector3(1.3f, 0.18f, 0.08f), accentMaterial, false).transform.localPosition = new Vector3(0f, 0.5f, 2.05f);

            CreateWheel(car.transform, new Vector3(-1.02f, 0.08f, 1.25f));
            CreateWheel(car.transform, new Vector3(1.02f, 0.08f, 1.25f));
            CreateWheel(car.transform, new Vector3(-1.02f, 0.08f, -1.25f));
            CreateWheel(car.transform, new Vector3(1.02f, 0.08f, -1.25f));

            Transform seat = new GameObject("Driver Seat").transform;
            seat.SetParent(car.transform);
            seat.localPosition = new Vector3(-0.45f, 0.75f, -0.25f);

            Transform exitPoint = new GameObject("Driver Exit Point").transform;
            exitPoint.SetParent(car.transform);
            exitPoint.localPosition = new Vector3(-1.8f, -0.6f, 0f);
            exitPoint.localRotation = Quaternion.Euler(0f, -90f, 0f);

            interaction.Initialize(player, vehicle, followCamera, seat, exitPoint, delivery);
            PrototypeHud.SetVehicle(null, interaction);
        }

        private static void CreateWheel(Transform parent, Vector3 localPosition)
        {
            GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Wheel";
            wheel.transform.SetParent(parent);
            wheel.transform.localPosition = localPosition;
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(0.42f, 0.16f, 0.42f);
            Object.DestroyImmediate(wheel.GetComponent<Collider>());
            wheel.GetComponent<Renderer>().sharedMaterial = asphaltMaterial;
        }

        private static GameObject CreateBlock(string objectName, Transform parent, Vector3 position, Vector3 scale, Material material, bool worldSpace = true)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = objectName;
            block.transform.SetParent(parent);
            if (worldSpace)
            {
                block.transform.position = position;
            }
            else
            {
                block.transform.localPosition = position;
                Object.DestroyImmediate(block.GetComponent<Collider>());
            }
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }
    }
}
#endif
