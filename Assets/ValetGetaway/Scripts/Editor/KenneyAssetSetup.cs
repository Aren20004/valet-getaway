using System.IO;
using UnityEditor;
using UnityEngine;

namespace ValetGetaway.Editor
{
    public sealed class KenneyAssetSetup : AssetPostprocessor
    {
        private static readonly string[] Packs = { "CityKitRoads", "CarKit", "CityKitCommercial" };

        [InitializeOnLoadMethod]
        private static void Schedule() => EditorApplication.update += Prepare;

        private static void Prepare()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= Prepare;
            foreach (string pack in Packs)
            {
                string root = "Assets/Kenney/" + pack;
                string path = root + "/KenneyPalette.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) continue;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(root + "/Models/FBX format/Textures/colormap.png");
                if (texture == null) continue;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) { Debug.LogError("Kenney setup requires the project's URP Lit shader."); return; }
                var material = new Material(shader);
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 0.2f);
                AssetDatabase.CreateAsset(material, path);
                foreach (string model in Directory.GetFiles(root, "*.fbx", SearchOption.AllDirectories))
                    AssetDatabase.ImportAsset(model, ImportAssetOptions.ForceUpdate);
                Debug.Log("VALET KENNEY IMPORT READY: " + pack);
            }
            AssetDatabase.SaveAssets();
        }

        private Material OnAssignMaterialModel(Material material, Renderer renderer)
        {
            foreach (string pack in Packs)
                if (assetPath.StartsWith("Assets/Kenney/" + pack + "/"))
                    return AssetDatabase.LoadAssetAtPath<Material>("Assets/Kenney/" + pack + "/KenneyPalette.mat");
            return null;
        }
    }
}
