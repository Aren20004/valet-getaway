using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ValetGetaway.Editor
{
    [InitializeOnLoad]
    internal static class PoliceAndMarinaRequest
    {
        static PoliceAndMarinaRequest() { EditorApplication.update += Update; }
        static void Update()
        {
            const string request = "Library/PoliceAndMarina.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request);
            try { IslandCityBuilder.ApplyPoliceAndMarinaFix(); }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/PoliceAndMarinaFix.txt", exception.ToString());
                Debug.LogException(exception);
            }
        }
    }
}
