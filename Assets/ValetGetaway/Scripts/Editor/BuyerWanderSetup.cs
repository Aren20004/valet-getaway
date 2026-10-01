using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ValetGetaway.Editor
{
    public static class BuyerWanderSetup
    {
        [MenuItem("Valet Getaway/Set Up Buyer Walking")]
        private static void Apply()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().path != TestCitySceneBuilder.ScenePath) return;
            EditorApplication.update -= Apply;
            SessionState.SetBool("ValetBuyerWanderSetup.v3", true);
            BuyerDeliveryManager manager = Object.FindAnyObjectByType<BuyerDeliveryManager>();
            var serialized = new SerializedObject(manager);
            SerializedProperty buyers = serialized.FindProperty("buyers");
            for (int i = 0; i < buyers.arraySize; i++)
            {
                Transform buyer = (Transform)buyers.GetArrayElementAtIndex(i).objectReferenceValue;
                if (buyer.GetComponent<BuyerWander>() == null) buyer.gameObject.AddComponent<BuyerWander>();
                buyer.GetComponent<NavMeshAgent>().enabled = false;
                if (buyer.Find("Facing Marker") != null) continue;
                GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cube);
                face.name = "Facing Marker";
                face.transform.SetParent(buyer, false);
                face.transform.localPosition = new Vector3(0f, 0.45f, 0.45f);
                face.transform.localScale = new Vector3(0.28f, 0.18f, 0.12f);
                Object.DestroyImmediate(face.GetComponent<Collider>());
                face.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/ValetGetaway/Materials/Glass.mat");
            }
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            EditorSceneManager.SaveScene(manager.gameObject.scene);
            Debug.Log("VALET WANDER SETUP SAVED");
        }
    }
}
