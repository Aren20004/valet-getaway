using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    [InitializeOnLoad]
    public static class StationRoadValidation
    {
        const string Key="Valet.StationRoadBatch";
        static GameObject fixture;
        static float caseStart;
        static int test;
        static double deadline;
        static readonly Vector3[] Starts={new Vector3(399,.65f,-129),new Vector3(399,.6f,-94),new Vector3(390,.6f,-98),new Vector3(365,.6f,-42)};
        static readonly Vector3[] Directions={Vector3.forward,Vector3.back,Vector3.right,Vector3.forward};
        static readonly float[] Distances={35,35,60,40};
        static StationRoadValidation()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=OnPlay;
        }
        public static void RunBatch()
        {
            SessionState.SetInt(Key,1);
            deadline=EditorApplication.timeSinceStartup+240;
            File.Delete("Logs/PoliceDispatchValidation.txt");
            PoliceDispatchValidation.Run();
        }
        public static void RunRoadsBatch()
        {
            EditorSceneManager.OpenScene(IslandCityBuilder.ScenePath);
            SessionState.SetInt(Key,2); deadline=EditorApplication.timeSinceStartup+120;
            EditorApplication.isPlaying=true;
        }
        static void OnPlay(PlayModeStateChange state)
        {
            if(SessionState.GetInt(Key,0)!=2 || state!=PlayModeStateChange.EnteredPlayMode) return;
            Application.runInBackground=true;
            var fleet=Object.FindAnyObjectByType<PoliceDispatchDirector>();
            var patrol=Object.FindAnyObjectByType<PoliceVehicleController>();
            if(fleet!=null) fleet.gameObject.SetActive(false);
            if(patrol!=null) patrol.gameObject.SetActive(false);
            test=0; caseStart=-1;
        }
        static void Tick()
        {
            int phase=SessionState.GetInt(Key,0);
            if(phase==0) return;
            if(deadline==0) deadline=EditorApplication.timeSinceStartup+240;
            if(EditorApplication.timeSinceStartup>deadline) { Finish(false,"Batch timed out."); return; }
            if(phase==1)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Logs/PoliceDispatchValidation.txt")) return;
                var result=File.ReadAllText("Logs/PoliceDispatchValidation.txt");
                if(!result.Contains("VALIDATION PASSED")) { Finish(false,result); return; }
                EditorSceneManager.OpenScene(IslandCityBuilder.ScenePath);
                SessionState.SetInt(Key,2); EditorApplication.isPlaying=true; return;
            }
            if(!EditorApplication.isPlaying) return;
            if(caseStart<0)
            {
                fixture=new GameObject("Station road validation vehicle (Play mode only)");
                fixture.transform.SetPositionAndRotation(Starts[test],Quaternion.LookRotation(Directions[test]));
                var box=fixture.AddComponent<BoxCollider>(); box.size=new Vector3(2.1f,1.1f,4.2f); box.center=new Vector3(0,.55f,0);
                var body=fixture.AddComponent<Rigidbody>(); body.mass=1100;
                fixture.AddComponent<StationRoadTestDriver>();
                caseStart=Time.time;
            }
            float travel=Vector3.Dot(fixture.transform.position-Starts[test],Directions[test]);
            if(Time.time-caseStart>14 || fixture.transform.position.y<-.5f)
            { Finish(false,$"Road case {test} blocked at {fixture.transform.position}, travel={travel:F1}m, obstacle={fixture.GetComponent<StationRoadTestDriver>().LastObstacle}."); return; }
            if(travel<Distances[test]) return;
            Debug.Log($"STATION ROAD CHECK: physical crossing {test} passed ({travel:F1}m).");
            fixture.SetActive(false); Object.Destroy(fixture); fixture=null;
            test++; caseStart=-1;
            if(test==Starts.Length) Finish(true,"Four physical station road/parking/junction crossings passed.");
        }
        static void Finish(bool passed,string details)
        {
            SessionState.SetInt(Key,0);
            string report="VALET STATION ROAD VALIDATION "+(passed?"PASSED: ":"FAILED: ")+details;
            File.WriteAllText("Logs/StationRoadValidation.txt",report);
            Debug.Log(report);
            EditorApplication.isPlaying=false;
            EditorApplication.delayCall+=()=>EditorApplication.Exit(passed?0:1);
        }
    }
}
