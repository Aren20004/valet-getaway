using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
static class IslandLivePreview
{
    static IslandLivePreview() { EditorApplication.update+=OpenWhenReady; }
    static void OpenWhenReady()
    {
        const string request="Library/OpenUpdatedIsland.request";
        if(!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) return;
        File.Delete(request);
        EditorSceneManager.OpenScene("Assets/Scenes/ValetIslandCity.unity");
        if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.LookAt(new Vector3(-100,0,-30),Quaternion.Euler(70,0,0),850);
        File.WriteAllText("Logs/IslandLivePreview.txt","Opened updated playable island scene.\n");
        Debug.Log("VALET UPDATED ISLAND OPENED - press Play to start at the hotel.");
    }
}
