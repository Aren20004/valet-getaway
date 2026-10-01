using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using Object=UnityEngine.Object;

namespace ValetGetaway.Editor
{
    public static class IslandGameplaySetup
    {
        static Vector3 P(float x,float y,float height=.4f) => new Vector3(x-500,height,240-y);
        [MenuItem("Valet Getaway/Rebuild Playable Island City")]
        public static void Build()
        {
            IslandCityBuilder.Build();
            var island=SceneManager.GetActiveScene();
            var geometry=new GameObject("Island Map Geometry");
            foreach(var root in island.GetRootGameObjects())
                if(root!=geometry && root.name.Length>2 && char.IsDigit(root.name[0])) root.transform.SetParent(geometry.transform,true);
            var overview=Camera.main; overview.tag="Untagged"; overview.enabled=false;
            overview.gameObject.name="Disabled overview camera - editor reference";

            var original=EditorSceneManager.OpenScene(TestCitySceneBuilder.ScenePath,OpenSceneMode.Additive);
            GameObject gameplay=null;
            foreach(var root in original.GetRootGameObjects()) if(root.name=="Valet Getaway Prototype") gameplay=root;
            if(gameplay==null) throw new InvalidOperationException("Saved test-city gameplay root missing");
            SceneManager.MoveGameObjectToScene(gameplay,island);
            EditorSceneManager.CloseScene(original,true); SceneManager.SetActiveScene(island);
            gameplay.name="Island Valet Gameplay";
            var session=gameplay.GetComponentInChildren<HotelGameSession>();
            var player=session.Player; var delivery=session.Delivery;
            foreach(var stop in session.BusStops) stop.transform.SetParent(gameplay.transform,true);
            Object.DestroyImmediate(gameplay.transform.Find("Test Area").gameObject);
            var camera=gameplay.GetComponentInChildren<OrbitFollowCamera>();
            camera.GetComponent<Camera>().farClipPlane=2000;
            var returnPoint=gameplay.transform.Find("Hotel Return Point");
            returnPoint.SetPositionAndRotation(P(895,207,.4f),Quaternion.Euler(0,150,0));
            player.transform.SetPositionAndRotation(returnPoint.position,returnPoint.rotation);
            camera.SetTarget(player.transform,true);

            for(int i=0;i<session.Guests.Length;i++)
            {
                float bay=896+i*12;
                var guest=session.Guests[i]; var queue=P(bay,214,.4f);
                var route=new[]{P(802,250),P(822,250),P(842,251),P(856,256),P(868,270),P(878,299),P(902,319),P(953,319),P(981,302),P(991,277),P(985,249),P(965,228),P(941,222),P(bay,222),queue};
                guest.Configure(guest.Guest,route[0],queue,i*8,2500+i*1500,new[]{"Alex","Morgan","Jamie"}[i]);
                guest.ConfigureRoute(route,P(924,192,1.4f),0,new Vector3(-3.2f,1,0),i==0 ? 12 : i==1 ? 9 : 7);
                guest.transform.SetPositionAndRotation(queue,Quaternion.identity);
                guest.Guest.position=queue+new Vector3(-3.2f,1,0);
                guest.GetComponent<Rigidbody>().collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            }

            var surface=geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects=CollectObjects.Children; surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize=true; surface.voxelSize=.35f;
            surface.BuildNavMesh();
            const string navigation="Assets/ValetGetaway/IslandCity/IslandNavigation.asset";
            AssetDatabase.CreateAsset(surface.navMeshData,navigation);
            delivery.SetNavigationSurface(surface);
            Vector3[] buyers={P(173,150),P(322,180),P(532,247),P(226,398),P(449,435),P(-155,147)};
            for(int i=0;i<delivery.Buyers.Length;i++)
            {
                if(!NavMesh.SamplePosition(buyers[i],out var hit,18,NavMesh.AllAreas)) throw new InvalidOperationException("No buyer pavement at "+buyers[i]);
                var buyer=delivery.Buyers[i]; buyer.GetComponent<NavMeshAgent>().enabled=false;
                buyer.position=hit.position+Vector3.up;
                var path=new NavMeshPath();
                if(!NavMesh.CalculatePath(P(900,222,.22f),hit.position,NavMesh.AllAreas,path) || path.status!=NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("Buyer disconnected from hotel: "+i);
            }
            Vector3[] stops={P(878,209,0),P(180,190,0),P(328,313,0),P(587,351,0),P(373,428,0)};
            for(int i=0;i<stops.Length;i++) session.BusStops[i].transform.position=stops[i];
            var recovery=gameplay.AddComponent<IslandRecovery>(); recovery.Configure(session,returnPoint);
            session.Configure(player,camera,delivery,returnPoint,session.Guests,session.BusStops);
            EditorSceneManager.MarkSceneDirty(island); AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(island,IslandCityBuilder.ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(IslandCityBuilder.ScenePath,true),new EditorBuildSettingsScene(TestCitySceneBuilder.ScenePath,false)};
            File.WriteAllText("Logs/IslandGameplaySetup.txt","Playable island saved. 3 routed guests, 6 connected wandering buyers, 5 bus stops, baked navigation, water recovery.\n");
            Debug.Log("VALET ISLAND GAMEPLAY SETUP PASSED");
        }
    }
}
