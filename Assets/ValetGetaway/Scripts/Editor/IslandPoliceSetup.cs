using System;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    [InitializeOnLoad]
    public static class IslandPoliceSetup
    {
        const string PrefabPath = "Assets/ValetGetaway/Prefabs/Police/PoliceUnit.prefab";
        const string RequestPath = "Library/IntegrateIslandPolice.request";

        static IslandPoliceSetup() { EditorApplication.update += ProcessRequest; }

        static void ProcessRequest()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath);
            try
            {
                AddToOpenIsland();
                IslandPoliceValidation.Run();
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/IslandPoliceSetup.txt", exception.ToString());
                Debug.LogException(exception);
            }
        }

        [MenuItem("Valet Getaway/Add Police to Open Island")]
        public static void AddToOpenIsland()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != IslandCityBuilder.ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open ValetIslandCity in Edit mode first.");
            var player = Object.FindAnyObjectByType<ThirdPersonPlayerController>();
            var geometry = GameObject.Find("Island Map Geometry");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (player == null || geometry == null || prefab == null)
                throw new InvalidOperationException("Island player, map geometry, or PoliceUnit prefab is missing.");
            if (Object.FindAnyObjectByType<PoliceVehicleController>() != null)
                throw new InvalidOperationException("The scene already contains a police unit.");

            Directory.CreateDirectory("Logs/SceneBackups");
            EditorSceneManager.SaveScene(scene, "Logs/SceneBackups/BeforePolice_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);
            var surface = geometry.GetComponent<NavMeshSurface>();
            if (surface == null) throw new InvalidOperationException("Island navigation surface is missing.");
            // Bake the edited station and waterfront layout; dynamic vehicles remain outside this root.
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Island navigation bake failed.");
            if (!AssetDatabase.Contains(surface.navMeshData))
                AssetDatabase.CreateAsset(surface.navMeshData, AssetDatabase.GenerateUniqueAssetPath("Assets/ValetGetaway/IslandCity/IslandPoliceNavigation.asset"));

            var unit = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(unit, "Add island police unit");
            unit.name = "PoliceUnit - Station Patrol";
            unit.transform.SetPositionAndRotation(new Vector3(504.13556f, .24f, -120.18249f), Quaternion.identity);
            var controller = unit.GetComponent<PoliceVehicleController>();
            var foot = unit.GetComponentInChildren<PoliceFootAI>();
            var car = unit.GetComponentInChildren<PoliceCarAI>();
            if (controller == null || foot == null || car == null)
                throw new InvalidOperationException("Police prefab has missing scripts.");
            controller.player = player;
            foot.player = player;
            car.player = player;
            controller.policeOfficer = foot.gameObject;
            controller.policeCarAI = car;
            var body = car.GetComponent<Rigidbody>();
            body.mass = 1100f;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = .3f;
            body.angularDamping = 3f;
            var agent = foot.GetComponent<NavMeshAgent>();
            agent.enabled = false;
            if (!NavMesh.SamplePosition(foot.transform.position, out var spawn, 5f, agent.areaMask))
                throw new InvalidOperationException("No walkable pavement at the police spawn.");
            foot.transform.position = spawn.position + Vector3.up * agent.baseOffset;
            agent.enabled = true;
            if (!NavMesh.SamplePosition(controller.vehicleAccessPoint.position, out var door, 3f, agent.areaMask))
                throw new InvalidOperationException("Police car door is not beside walkable pavement.");
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(spawn.position, door.position, agent.areaMask, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("The police officer cannot reach the car door.");
            foreach (var component in unit.GetComponentsInChildren<Component>(true))
                if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorUtility.SetDirty(surface);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = unit;
            SceneView.RepaintAll();
            const string result = "VALET ISLAND POLICE SETUP PASSED: saved PoliceUnit prefab instance beside station parking, player references assigned, navigation rebuilt, officer-to-car route complete.";
            File.WriteAllText("Logs/IslandPoliceSetup.txt", result);
            Debug.Log(result);
        }
    }
}
