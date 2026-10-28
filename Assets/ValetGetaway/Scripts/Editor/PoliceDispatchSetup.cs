using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    [InitializeOnLoad]
    public static class PoliceDispatchSetup
    {
        const string Request = "Library/SetupPoliceWaves.request";
        static PoliceDispatchSetup() { EditorApplication.update += ProcessRequest; }
        static void ProcessRequest()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try { Install(); PoliceDispatchValidation.Run(); }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/PoliceDispatchSetup.txt", exception.ToString()); Debug.LogException(exception);
            }
        }

        static Transform Point(Transform parent, string name, Vector3 position)
        {
            var point = new GameObject(name).transform; point.SetParent(parent); point.position = position; return point;
        }

        [MenuItem("Valet Getaway/Add Police Response Fleet")]
        public static void Install()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != IslandCityBuilder.ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open the island in Edit mode first.");
            if (Object.FindAnyObjectByType<PoliceDispatchDirector>() != null)
                throw new InvalidOperationException("The island already has a police response fleet.");
            var session = Object.FindAnyObjectByType<HotelGameSession>();
            var patrol = Object.FindAnyObjectByType<PoliceVehicleController>();
            if (session == null || patrol == null) throw new InvalidOperationException("Island gameplay or the station patrol is missing.");
            Directory.CreateDirectory("Logs/SceneBackups");
            EditorSceneManager.SaveScene(scene, "Logs/SceneBackups/BeforePoliceWaves_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);

            var template = Object.Instantiate(patrol.policeCarAI.gameObject);
            template.name = "Police Response Car";
            template.transform.SetParent(null);
            template.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var ai = template.GetComponent<PoliceCarAI>(); ai.player = null; ai.enabled = false;
            ai.acceleration = 21f; ai.maxSpeed = 24f; ai.turnSpeed = 100f;
            if (template.GetComponent<VehicleGroundMotor>() == null) template.AddComponent<VehicleGroundMotor>();
            var body = template.GetComponent<Rigidbody>();
            body.mass = 1100f; body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            const string path = "Assets/ValetGetaway/Prefabs/Police/PoliceResponseCar.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(template, path);
            Object.DestroyImmediate(template);

            var root = new GameObject("Police Response Fleet");
            Undo.RegisterCreatedObjectUndo(root, "Add police response fleet");
            var routing = new GameObject("Dispatch route points").transform; routing.SetParent(root.transform);
            var positions = new[] {
                new Vector3(505,.24f,-108),new Vector3(505,.24f,-128),new Vector3(505,.24f,-138),new Vector3(505,.24f,-148),
                new Vector3(569,.24f,-108),new Vector3(569,.24f,-118),new Vector3(569,.24f,-128),new Vector3(569,.24f,-138),new Vector3(569,.24f,-148)};
            var cars = new PoliceCarAI[positions.Length]; var aisles = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                car.name = "Police Response " + (i + 1).ToString("D2");
                car.transform.SetParent(root.transform);
                car.transform.SetPositionAndRotation(positions[i], Quaternion.Euler(0, i < 4 ? 90 : 270, 0));
                cars[i] = car.GetComponent<PoliceCarAI>(); cars[i].player = session.Player;
                aisles[i] = Point(routing, "Aisle entry " + (i + 1), new Vector3(537, .24f, positions[i].z));
                PrefabUtility.RecordPrefabInstancePropertyModifications(car.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(cars[i]);
            }
            Vector3[] exitPositions = {
                new Vector3(537,.24f,-98),new Vector3(507,.24f,-94),new Vector3(470,.24f,-76),
                new Vector3(481,.24f,-62),new Vector3(491,.24f,-37),new Vector3(485,.24f,-9),new Vector3(465,.24f,12),new Vector3(420,.24f,18)};
            var exit = new Transform[exitPositions.Length];
            for (int i = 0; i < exit.Length; i++) exit[i] = Point(routing, "Station exit " + (i + 1), exitPositions[i]);
            Vector3[] bridgePositions = {
                new Vector3(382,.24f,15),new Vector3(365,.24f,2),new Vector3(339,.24f,-4),new Vector3(322,.24f,-4),
                new Vector3(280,.24f,-7),new Vector3(240,.24f,-7),new Vector3(200,.24f,-7),new Vector3(150,.24f,-7)};
            var bridge = new Transform[bridgePositions.Length];
            for (int i = 0; i < bridge.Length; i++) bridge[i] = Point(routing, "Bridge route " + (i + 1), bridgePositions[i]);
            root.AddComponent<PoliceDispatchDirector>().Configure(session, patrol, cars, aisles, exit, bridge);
            Physics.SyncTransforms();
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root; SceneView.RepaintAll();
            const string report = "VALET POLICE FLEET SETUP PASSED: nine saved response cars plus station patrol, shared response prefab, editable parking/exit/bridge route points, randomized 5-10 car dispatch waves.";
            File.WriteAllText("Logs/PoliceDispatchSetup.txt", report); Debug.Log(report);
        }
    }
}
