using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    [InitializeOnLoad]
    public static class StationRoadRepair
    {
        const string Output = "Assets/ValetGetaway/Generated/StationRoadRepair";
        static StationRoadRepair() { EditorApplication.update += Requests; }
        static void Requests()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            const string request = "Library/InspectStationRoad.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            Inspect();
        }

        [MenuItem("Valet Getaway/Roads/Inspect Station Roads")]
        public static void Inspect()
        {
            Physics.SyncTransforms();
            var report = new StringBuilder(SceneManager.GetActiveScene().path + "\n");
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) report.AppendLine("ROOT " + root.name);
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include))
            {
                if (!c.enabled || c.bounds.max.x < 380 || c.bounds.min.x > 580 || c.bounds.max.z < -210 || c.bounds.min.z > -70) continue;
                report.AppendLine($"COLLIDER {Path(c.transform)} center={c.bounds.center:F2} size={c.bounds.size:F2} type={c.GetType().Name}");
            }
            foreach (var ai in Object.FindObjectsByType<PoliceCarAI>(FindObjectsInactive.Include))
                report.AppendLine($"CAR {ai.name} position={ai.transform.position:F2} forward={ai.transform.forward:F2}");
            var director = Object.FindAnyObjectByType<PoliceDispatchDirector>();
            if (director != null)
            {
                var so = new SerializedObject(director);
                foreach (string field in new[] { "aislePoints", "stationExit", "bridgeRoute" })
                {
                    var a = so.FindProperty(field);
                    for (int i = 0; i < a.arraySize; i++)
                    {
                        var t = a.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                        report.AppendLine($"ROUTE {field} {i}: {(t == null ? "MISSING" : t.position.ToString("F2"))}");
                    }
                }
            }
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/StationRoadInspection.txt", report.ToString());
            Debug.Log("Station road inspection written to Logs/StationRoadInspection.txt");
        }
        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

        public static void Batch()
        {
            EditorSceneManager.OpenScene(IslandCityBuilder.ScenePath);
            Apply();
            EditorSceneManager.OpenScene(IslandCityBuilder.ScenePath);
            Inspect();
            StationRoadValidation.RunBatch();
        }

        [MenuItem("Valet Getaway/Roads/Repair Station Access and Dispatch")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != IslandCityBuilder.ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open the island in Edit mode first.");
            Directory.CreateDirectory("Logs/SceneBackups");
            EditorSceneManager.SaveScene(scene, "Logs/SceneBackups/BeforeStationAccess_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", true);
            Directory.CreateDirectory(Output); AssetDatabase.Refresh();
            var roads = GameObject.Find("02 Roads - traced from reference").transform;
            foreach (string name in new[] { "Police station frontage", "Police station car park entrance", "Police station response link" })
            {
                var old = roads.Find(name + " [Broken Vector road mesh]");
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            }
            IslandCityBuilder.BeginRoadEdit(roads);
            Road("Police station frontage", 12, new Vector3(399,0,-98), new Vector3(430,0,-98), new Vector3(458,0,-94), new Vector3(474,0,-80));
            Road("Police station car park entrance", 9, new Vector3(399,0,-126), new Vector3(399,0,-112), new Vector3(399,0,-98));
            Road("Police station response link", 12, new Vector3(399,0,-98), new Vector3(383,0,-80), new Vector3(372,0,-62), new Vector3(365,0,-40), new Vector3(362,0,-18), new Vector3(350,0,-4));
            var entrance = roads.Find("Police station car park entrance [Broken Vector road mesh]").GetComponent<MeshFilter>().sharedMesh;
            var vertices = entrance.vertices;
            for (int i=0;i<vertices.Length;i++) vertices[i].y += .26f * Mathf.Clamp01((-110f-vertices[i].z)/9f);
            entrance.vertices=vertices; entrance.RecalculateNormals(); entrance.RecalculateBounds(); EditorUtility.SetDirty(entrance);

            // Re-trim just the affected junctions using existing saved meshes, without rebuilding the island.
            var surfaces = new List<IslandRoadJunctions.Surface>();
            var changed = new List<MeshFilter>();
            var region = new Bounds(new Vector3(414,.3f,-65),new Vector3(130,2,132));
            foreach (var filter in roads.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.name.EndsWith("[Broken Vector road mesh]") || !filter.GetComponent<Renderer>().bounds.Intersects(region)) continue;
                var original=filter.sharedMesh;
                var mesh=Object.Instantiate(original); mesh.name=original.name + " junction repair";
                AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Output+"/"+filter.name+".asset"));
                filter.sharedMesh=mesh;
                var surface=new IslandRoadJunctions.Surface { Mesh=mesh, Collider=filter.GetComponent<MeshCollider>(), CurbTriangles=new bool[mesh.triangles.Length/3] };
                var v=mesh.vertices; var tri=mesh.triangles;
                for(int i=0;i<tri.Length;i+=3)
                {
                    Vector3 a=v[tri[i]], b=v[tri[i+1]], c=v[tri[i+2]];
                    bool curb=Mathf.Max(a.y,Mathf.Max(b.y,c.y))>.231f;
                    surface.CurbTriangles[i/3]=curb;
                    if(Mathf.Abs(Vector3.Cross(b-a,c-a).y)<.00001f) continue;
                    var footprint=new IslandRoadJunctions.Quad(new Vector2(a.x,a.z),new Vector2(b.x,b.z),new Vector2(c.x,c.z));
                    surface.Full.Add(footprint); if(!curb) surface.Asphalt.Add(footprint);
                }
                surfaces.Add(surface); changed.Add(filter);
            }
            IslandRoadJunctions.Trim(surfaces);

            var precinct=GameObject.Find("07 Police station - precinct").transform;
            var gate=precinct.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="PD_GateArm_Arm");
            if(gate!=null && gate.GetComponent<Renderer>().bounds.size.y<1f)
            {
                var bounds=gate.GetComponent<Renderer>().bounds;
                Undo.RecordObject(gate,"Raise emergency exit gate");
                gate.RotateAround(new Vector3(bounds.min.x,bounds.center.y,bounds.center.z),Vector3.forward,85);
                PrefabUtility.RecordPrefabInstancePropertyModifications(gate);
            }

            var director=Object.FindAnyObjectByType<PoliceDispatchDirector>();
            var so=new SerializedObject(director);
            var cars=so.FindProperty("responseCars"); var aisles=so.FindProperty("aislePoints");
            float[] bays={393,403,408,413,423,428,418,423,428};
            for(int i=0;i<cars.arraySize;i++)
            {
                var ai=(PoliceCarAI)cars.GetArrayElementAtIndex(i).objectReferenceValue;
                Undo.RecordObject(ai.transform,"Park police beside the clear central aisle");
                ai.transform.position=new Vector3(bays[i],ai.transform.position.y,i<6 ? -124f : -148f);
                ParkFacingAisle(ai.transform);
                var point=(Transform)aisles.GetArrayElementAtIndex(i).objectReferenceValue;
                point.position=new Vector3(399,.52f,-135f);
                Clearance(point,new Vector3(ai.transform.position.x,.52f,point.position.z));
            }
            var displayCars=precinct.Cast<Transform>().Where(t=>t.name.Contains("PatrolSedan") || t.name.Contains("PoliceSUV") || t.name.Contains("UnmarkedSedan")).OrderBy(t=>t.name).ToArray();
            for(int i=0;i<displayCars.Length;i++)
            {
                Undo.RecordObject(displayCars[i],"Clear the police parking aisle");
                displayCars[i].SetPositionAndRotation(new Vector3(394.5f+i*5f,.49f,-148),Quaternion.identity);
                PrefabUtility.RecordPrefabInstancePropertyModifications(displayCars[i]);
            }
            var patrol=Object.FindAnyObjectByType<PoliceVehicleController>();
            var lead=patrol.policeCarAI.transform;
            Vector3 delta=new Vector3(418f,lead.position.y,-124f)-lead.position;
            Undo.RecordObject(patrol.transform,"Place patrol in its clear departure bay");
            patrol.transform.position+=delta;
            PrefabUtility.RecordPrefabInstancePropertyModifications(patrol.transform);
            ParkFacingAisle(lead);
            var routing=director.transform.Find("Dispatch route points");
            var leadPoint=routing.Find("Patrol aisle entry");
            if(leadPoint==null) { leadPoint=new GameObject("Patrol aisle entry").transform; leadPoint.SetParent(routing); }
            leadPoint.position=new Vector3(399,.52f,-135f);
            Clearance(leadPoint,new Vector3(lead.position.x,.52f,-135f));
            Vector3[] exit={new Vector3(399,.52f,-106),new Vector3(395,.24f,-94),new Vector3(383,.24f,-80),new Vector3(372,.24f,-62),new Vector3(365,.24f,-40),new Vector3(362,.24f,-18),new Vector3(359,.24f,-6),new Vector3(350,.24f,-4)};
            var exits=so.FindProperty("stationExit");
            for(int i=0;i<exits.arraySize;i++) ((Transform)exits.GetArrayElementAtIndex(i).objectReferenceValue).position=exit[Mathf.Min(i,exit.Length-1)];
            Physics.SyncTransforms();
            RoadBarrierFix.FixBarriers();
            var navigation=Object.FindAnyObjectByType<NavMeshSurface>();
            if(navigation != null)
            {
                string navigationPath=AssetDatabase.GetAssetPath(navigation.navMeshData);
                navigation.BuildNavMesh();
                if(!string.IsNullOrEmpty(navigationPath))
                {
                    // Keep the existing navigation asset GUID used by the saved scene.
                    var saved=AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(navigationPath);
                    EditorUtility.CopySerialized(navigation.navMeshData,saved); navigation.navMeshData=saved; EditorUtility.SetDirty(saved);
                }
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllLines("Logs/StationRepairDependencies.txt",AssetDatabase.GetDependencies(scene.path,true));
            Inspect();
            File.WriteAllText("Logs/StationRoadRepair.txt", "Station road repaired: direct west-side response connection, open exit gate, backed-in fleet, current parking aisle routes, blended parking transition, trimmed junction overlaps and drivable road collision. Play-mode validation pending.");
        }

        static void Road(string name,float width,params Vector3[] points)
        {
            IslandCityBuilder.Road(name,width,points.Select(p=>new Vector2(p.x+500,240-p.z)).ToArray());
        }
        static void Clearance(Transform parent,Vector3 point)
        {
            var t=parent.Find("Bay clearance");
            if(t==null) { t=new GameObject("Bay clearance").transform; t.SetParent(parent); }
            t.position=point;
        }
        static void ParkFacingAisle(Transform car)
        {
            Undo.RecordObject(car,"Face police car toward parking aisle");
            // Reverse the nose-in bays once; rerunning the repair must not flip them back.
            bool bottom=car.position.z < -143;
            bool upper=car.position.z > -125;
            car.rotation=Quaternion.Euler(0,bottom || !upper && car.position.z > -135 ? 0 : 180,0);
            Physics.SyncTransforms();
            var hull=car.GetComponent<BoxCollider>();
            car.position+=Vector3.up*(.52f-hull.bounds.min.y);
            PrefabUtility.RecordPrefabInstancePropertyModifications(car);
        }
    }
}
