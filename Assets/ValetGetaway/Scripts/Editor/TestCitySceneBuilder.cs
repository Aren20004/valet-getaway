using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ValetGetaway.Editor
{
    public static class TestCitySceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/ValetTestCity.unity";

        [InitializeOnLoadMethod]
        private static void ScheduleFirstBuild()
        {
            if (!File.Exists(ScenePath)) EditorApplication.update += BuildWhenReady;
        }

        private static void BuildWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= BuildWhenReady;
            if (!File.Exists(ScenePath)) Build();
        }

        [MenuItem("Valet Getaway/Open Test City")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath)) Build();
            else
            {
                Scene scene = SceneManager.GetSceneByPath(ScenePath);
                if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
            }
        }

        private static void Build()
        {
            Directory.CreateDirectory("Assets/ValetGetaway/Materials");
            Directory.CreateDirectory("Assets/ValetGetaway/Prefabs/Buildings");
            Directory.CreateDirectory("Assets/ValetGetaway/Prefabs/NPCs");
            AssetDatabase.Refresh();
            Scene original = SceneManager.GetActiveScene();
            bool keepOriginal = original.isDirty;
            Scene city = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(city);
            PrototypeBootstrap.BuildPrototype();
            GameObject root = GameObject.Find("Valet Getaway Prototype");
            Transform hotel = root.transform.Find("Test Area/Buildings/Hotel");
            PrefabUtility.SaveAsPrefabAssetAndConnect(hotel.gameObject, "Assets/ValetGetaway/Prefabs/Buildings/HotelBlock.prefab", InteractionMode.AutomatedAction);
            Transform buyer = root.transform.Find("Buyer NPCs/Buyer NPC 1");
            PrefabUtility.SaveAsPrefabAssetAndConnect(buyer.gameObject, "Assets/ValetGetaway/Prefabs/NPCs/BuyerNPC.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(city);
            EditorSceneManager.SaveScene(city, ScenePath);
            AssetDatabase.SaveAssets();
            if (!keepOriginal) EditorSceneManager.CloseScene(original, true);
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(Vector3.zero, Quaternion.Euler(60f, 0f, 0f), 125f);
            Debug.Log("VALET CITY SAVED: 160 x 160, sixteen buildings, six buyers, serialized gameplay references.");
        }
    }
}
