using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ValetGetaway.Editor
{
    // Coordinates trace the supplied 812 x 454 reference, extended beyond its crop.
    // All geometry is generated in the editor and saved as editable scene objects/assets.
    public static class IslandCityBuilder
    {
        const string Pack = "Assets/BrokenVector/LowPolyRoadPack/";
        const string Generated = "Assets/ValetGetaway/IslandCity";
        public const string ScenePath = "Assets/Scenes/ValetIslandCity.unity";
        static readonly List<List<Vector2>> paths = new List<List<Vector2>>();
        static readonly List<float> widths = new List<float>();
        static readonly List<Rect> lots = new List<Rect>();
        static Transform roads, buildings, terrain, props;
        static Material grass, sand, water, stone, roof, glass, gold;
        static Material[] districts;
        static int meshIndex;
        static Vector2[] cityBoundary;
        static readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
        static Vector3 W(Vector2 p, float y=0) => new Vector3(p.x-500, y, 240-p.y);
        static Vector2[] Points(params float[] v) { var a=new Vector2[v.Length/2]; for(int i=0;i<a.Length;i++) a[i]=new Vector2(v[i*2],v[i*2+1]); return a; }

        [MenuItem("Valet Getaway/Open Island City")]
        public static void Open()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Valet Getaway/Build Reference Island City")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play mode before building.");
            if (!Directory.Exists(Pack)) throw new InvalidOperationException("Import Broken Vector Low Poly Road Pack first.");
            // Never overwrite an unsaved scene or silently replace a previously edited layout.
            if (File.Exists(ScenePath)) { Directory.CreateDirectory("Logs/SceneBackups"); File.Copy(ScenePath,"Logs/SceneBackups/ValetIslandCity_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity",true); }
            foreach (var s in GetLoadedScenes()) if (s.isDirty) throw new InvalidOperationException("Save open scene changes before building the island layout.");
            Directory.CreateDirectory(Generated+"/Meshes"); Directory.CreateDirectory(Generated+"/Materials"); Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            paths.Clear(); widths.Clear(); lots.Clear(); converted.Clear(); meshIndex=0;
            roads=Group("02 Roads - traced from reference"); buildings=Group("03 Buildings - coordinated districts"); terrain=Group("01 Islands and water"); props=Group("04 Trees and waterfront");
            grass=Mat("Island meadow",new Color(.46f,.59f,.26f)); sand=Mat("Warm shoreline",new Color(.72f,.65f,.43f)); water=Mat("Harbour blue",new Color(.13f,.26f,.39f));
            stone=Mat("Pale pavement",new Color(.64f,.66f,.60f)); roof=Mat("Ivory roofs",new Color(.83f,.85f,.78f)); glass=Mat("Blue glass",new Color(.15f,.31f,.39f)); gold=Mat("Hotel sandstone",new Color(.73f,.55f,.31f));
            districts=new[]{Mat("Residential sage",new Color(.61f,.73f,.63f)),Mat("Commercial blue",new Color(.55f,.68f,.76f)),Mat("Downtown cream",new Color(.79f,.76f,.65f))};
            Box("Water",new Vector2(530,230),new Vector3(2000,2,1400),-8,water,terrain,false);
            Island("City island",Points(-70,-90, 210,-105, 370,-75, 452,-15, 500,35, 534,82, 548,126, 623,140, 658,185, 666,208, 646,233, 650,275, 628,305, 637,349, 645,424, 592,470, 485,512, 300,525, 126,501, 25,463, -51,380, -83,248, -94,70));
            Island("Hotel island",Points(816,-80, 989,-90, 1114,14, 1140,170, 1118,361, 1035,468, 918,488, 822,424, 742,350, 726,308, 744,264, 754,198, 773,103, 779,18));

            Road("West arterial and bridge approach",20,Points(36,-65,36,80,43,110,64,141,66,184,83,225,111,243,180,247,350,247,520,247,650,247));
            Road("Northwest loop",12,Points(200,-52,200,12,150,12,125,23,115,50,115,88,125,111,151,118,172,136,181,173));
            Road("Northwest connector",12,Points(151,118,160,95,182,85,237,85,264,78,279,54,284,36,304,29,393,29,423,35,443,56,452,81,480,94,496,113,499,135,483,147,435,147));
            Road("North winding boulevard",18,Points(245,114,305,114,331,109,359,88,381,80,403,83,424,98,435,119,435,178,438,206,453,228,478,242,510,247));
            Road("West cross street",18,Points(65,174,145,174,245,174,267,171,287,160,302,142,309,115));
            Road("Inner S bend",14,Points(350,302,350,221,350,180,359,158,378,148,436,148));
            Road("Hotel bridge connection",20,Points(770,247,818,247,852,247));
            Road("South waterfront curve",14,Points(510,247,548,252,567,267,575,291,575,348,573,380,562,399,537,411,488,415,444,422,428,439,422,482));
            Road("South cross avenue",14,Points(198,302,290,302,400,302,496,302,575,302,632,302));
            Road("Lower downtown grid west",14,Points(198,285,198,349,198,414,198,493));
            Road("Lower downtown grid center",10,Points(282,302,282,410,282,493));
            Road("Lower downtown grid east",12,Points(350,302,350,412,350,493));
            Road("Lower cross street",12,Points(156,479,156,439,169,419,195,413,280,413,350,413,383,405,402,384,404,346,404,302));
            Road("Valet city ramp west",10,Points(145,247,171,257,198,285));
            Road("Valet city ramp east",10,Points(198,285,222,259,249,247));
            Road("Parking access",10,Points(453,302,458,328,477,337,493,337));
            Road("Waterfront slip",10,Points(575,290,594,268,621,251,642,247));
            Road("Hotel arrival loop",14,Points(852,247,872,269,878,299,902,319,953,319,981,302,991,277,985,249,965,228,920,222,882,225,852,247));
            Road("Hotel parking access",10,Points(970,316,1007,334,1065,334));
            Bridge();

            // Landmark footprints follow the image; smaller homes fill curved street edges.
            Lot(96,141,25,47,18,0); Lot(216,135,55,45,22,1); Lot(174,218,48,36,20,1);
            Lot(215,213,21,40,32,1); Lot(246,216,22,34,16,1); Lot(321,214,38,39,30,2);
            Lot(401,178,44,42,25,2); Lot(392,216,46,32,23,2); Lot(281,277,66,37,26,2);
            Lot(445,277,100,37,22,2); Lot(378,332,43,37,24,2); Lot(457,368,44,43,18,1);
            Lot(243,329,49,42,20,2); Lot(166,331,48,92,27,1); Lot(239,373,47,30,15,1);
            Lot(318,352,48,77,9,2); Lot(500,204,24,48,19,0); Lot(532,204,26,48,21,0);
            Lot(567,205,29,48,23,0); Lot(603,205,25,48,20,0); Lot(557,165,79,16,13,0);
            Lot(555,329,20,38,19,1); Lot(314,64,20,37,14,0); Lot(345,58,18,35,15,0);
            Lot(373,54,18,22,12,0); Lot(409,59,23,25,14,0);
            HomesAlong(paths[2],12); HomesAlong(paths[3],18); HomesAlong(paths[4],18); HomesAlong(paths[5],14); HomesAlong(paths[7],14);
            Parking(new Vector2(513,350),45,66,props);
            Hotel();
            Trees();
            LightAndCamera();
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(scene,ScenePath);
            Validate(); Capture(); ValidateSaved();
            Debug.Log("VALET ISLAND BUILD PASSED: saved editable roads, city island, hotel island, bridge, coordinated buildings. Scene: "+ScenePath);
        }

        static IEnumerable<Scene> GetLoadedScenes() { for(int i=0;i<SceneManager.sceneCount;i++) yield return SceneManager.GetSceneAt(i); }
        static Transform Group(string name) => new GameObject(name).transform;
        static Material Mat(string name, Color color)
        {
            string path=Generated+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
            m.color=color; m.SetFloat("_Smoothness",.12f); EditorUtility.SetDirty(m); return m;
        }
        static Material Convert(Material source)
        {
            if(source==null) return roof;
            if(converted.TryGetValue(source,out var m)) return m;
            m=Mat("Pack - "+source.name,Color.white);
            if(source.HasProperty("_MainTex")) m.SetTexture("_BaseMap",source.GetTexture("_MainTex"));
            converted[source]=m; return m;
        }
        static GameObject Box(string name,Vector2 p,Vector3 size,float bottom,Material material,Transform parent,bool collision=true)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.transform.SetParent(parent);
            o.transform.position=W(p,bottom+size.y/2); o.transform.localScale=size; o.GetComponent<Renderer>().sharedMaterial=material;
            if(!collision) Object.DestroyImmediate(o.GetComponent<Collider>()); return o;
        }
        static void Island(string name,Vector2[] outline)
        {
            var center=Vector2.zero; foreach(var p in outline) center+=p; center/=outline.Length;
            var inner=new Vector2[outline.Length]; for(int i=0;i<inner.Length;i++) inner[i]=Vector2.Lerp(outline[i],center,.028f);
            if(name=="City island") cityBoundary=inner;
            // The outlines are star-shaped around their centroids, so a fan is sufficient.
            Fan(name+" - sand shoreline",outline,-1.0f,sand);
            Fan(name+" - grass",inner,0,grass);
        }
        static void Fan(string name,Vector2[] outline,float y,Material mat)
        {
            Vector2 center=Vector2.zero; foreach(var p in outline) center+=p; center/=outline.Length;
            var verts=new Vector3[outline.Length+1]; verts[0]=W(center,y);
            var tris=new int[outline.Length*3];
            for(int i=0;i<outline.Length;i++) { verts[i+1]=W(outline[i],y); tris[3*i]=0; tris[3*i+1]=i+1; tris[3*i+2]=(i+1)%outline.Length+1; }
            MeshObject(name,verts,tris,null,new[]{mat},terrain);
        }
        static GameObject MeshObject(string name,Vector3[] vertices,int[] triangles,Vector2[] uv,Material[] mats,Transform parent)
        {
            var mesh=new Mesh { name=name, indexFormat=IndexFormat.UInt32 }; mesh.vertices=vertices; mesh.triangles=triangles; if(uv!=null) mesh.uv=uv; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,Generated+"/Meshes/"+(meshIndex++).ToString("D4")+".asset");
            var o=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider)); o.transform.SetParent(parent);
            o.GetComponent<MeshFilter>().sharedMesh=mesh; o.GetComponent<MeshRenderer>().sharedMaterials=mats; o.GetComponent<MeshCollider>().sharedMesh=mesh; return o;
        }
        static List<Vector2> Smooth(Vector2[] p)
        {
            var result=new List<Vector2>();
            for(int i=0;i<p.Length-1;i++)
            {
                Vector2 a=p[Mathf.Max(0,i-1)],b=p[i],c=p[i+1],d=p[Mathf.Min(p.Length-1,i+2)];
                int steps=Mathf.Max(4,Mathf.CeilToInt(Vector2.Distance(b,c)/2));
                for(int j=0;j<steps;j++) { float t=(float)j/steps; result.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)); }
            }
            result.Add(p[p.Length-1]); return result;
        }
        static void Road(string name,float width,Vector2[] controls)
        {
            var line=Smooth(controls); paths.Add(line); widths.Add(width);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Prefabs/Freeway Straight 2.prefab");
            var source=prefab.GetComponent<MeshFilter>().sharedMesh; var bounds=source.bounds;
            float[] distances=new float[line.Count]; for(int i=1;i<line.Count;i++) distances[i]=distances[i-1]+Vector2.Distance(line[i-1],line[i]);
            int tiles=Mathf.CeilToInt(distances[distances.Length-1]/5f);
            var verts=new List<Vector3>(); var uv=new List<Vector2>(); var tris=new List<int>();
            var sv=source.vertices; var su=source.uv; var st=source.triangles;
            for(int tile=0;tile<tiles;tile++)
            {
                int offset=verts.Count;
                for(int i=0;i<sv.Length;i++)
                {
                    float t=(sv[i].z-bounds.min.z)/bounds.size.z;
                    float distance=(tile+t)*distances[distances.Length-1]/tiles;
                    int k=Array.BinarySearch(distances,distance); if(k<0) k=~k; k=Mathf.Clamp(k,1,line.Count-1);
                    Vector2 tangent=(line[k]-line[k-1]).normalized;
                    Vector2 point=Vector2.Lerp(line[k-1],line[k],Mathf.InverseLerp(distances[k-1],distances[k],distance));
                    point+=new Vector2(-tangent.y,tangent.x)*((sv[i].x-bounds.center.x)/bounds.size.x*width);
                    verts.Add(W(point,.22f+(sv[i].y-bounds.max.y)*.35f)); uv.Add(su[i]);
                }
                foreach(int t in st) tris.Add(offset+t);
            }
            MeshObject(name+" [Broken Vector road mesh]",verts.ToArray(),tris.ToArray(),uv.ToArray(),new[]{Convert(prefab.GetComponent<Renderer>().sharedMaterial)},roads);
        }
        static void Bridge()
        {
            Road("Bridge carriageway",18,Points(650,247,770,247));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+"Prefabs/Highway Bridge 2.prefab");
            var o=(GameObject)PrefabUtility.InstantiatePrefab(prefab); o.name="05 Hotel bridge - Broken Vector truss";
            var mesh=Object.Instantiate(o.GetComponent<MeshFilter>().sharedMesh); var b=mesh.bounds; var v=mesh.vertices;
            for(int i=0;i<v.Length;i++) v[i]=W(new Vector2(Mathf.Lerp(650,770,(v[i].z-b.min.z)/b.size.z),247+(v[i].x-b.center.x)/b.size.x*22),.22f+(v[i].y-7.5f));
            mesh.vertices=v; mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.name="Hotel bridge adapted span";
            AssetDatabase.CreateAsset(mesh,Generated+"/Meshes/HotelBridge.asset");
            o.GetComponent<MeshFilter>().sharedMesh=mesh; o.GetComponent<MeshCollider>().sharedMesh=mesh;
            var rr=o.GetComponent<Renderer>(); var materials=rr.sharedMaterials; for(int i=0;i<materials.Length;i++) materials[i]=Convert(materials[i]); rr.sharedMaterials=materials;
        }
        static void Lot(float x,float y,float width,float depth,float height,int district,float rotation=0)
        {
            lots.Add(new Rect(x-width/2-2,y-depth/2-2,width+4,depth+4));
            var group=new GameObject("Building "+lots.Count+" - "+(district==0?"residential":district==1?"commercial":"downtown")).transform; group.SetParent(buildings);
            Box("Footprint",new Vector2(x,y),new Vector3(width+3,.25f,depth+3),0,stone,group);
            Box("Facade",new Vector2(x,y),new Vector3(width,height,depth),.25f,districts[district],group);
            if(district==0 && width<16 && depth<18)
            {
                float h=height+.25f;
                MeshObject("Pitched ivory roof",new[]{W(new Vector2(x-width/2-.4f,y-depth/2-.4f),h),W(new Vector2(x+width/2+.4f,y-depth/2-.4f),h),W(new Vector2(x+width/2+.4f,y+depth/2+.4f),h),W(new Vector2(x-width/2-.4f,y+depth/2+.4f),h),W(new Vector2(x-width/2-.4f,y),h+3),W(new Vector2(x+width/2+.4f,y),h+3)},new[]{0,1,5,0,5,4,4,5,2,4,2,3,0,4,3,1,2,5},null,new[]{roof},group);
            }
            else Box("Ivory roof",new Vector2(x,y),new Vector3(width+.7f,.6f,depth+.7f),height+.25f,roof,group);
            // Repeated window bands make the scale readable from street level.
            for(float floor=3;floor<height-1;floor+=4)
            {
                Box("South windows",new Vector2(x,y+depth/2+.02f),new Vector3(width*.78f,1.6f,.08f),floor,glass,group,false);
                Box("North windows",new Vector2(x,y-depth/2-.02f),new Vector3(width*.78f,1.6f,.08f),floor,glass,group,false);
            }
            group.RotateAround(W(new Vector2(x,y)),Vector3.up,rotation);
        }
        static bool Clear(Vector2 p,float radius)
        {
            if(!Inside(p,cityBoundary)) return false;
            for(int j=0;j<paths.Count;j++) foreach(var q in paths[j]) if(Vector2.Distance(p,q)<radius+widths[j]/2+2) return false;
            foreach(var lot in lots) if(new Rect(lot.xMin-radius,lot.yMin-radius,lot.width+2*radius,lot.height+2*radius).Contains(p)) return false;
            return p.x>75 && p.x<615 && p.y>24 && p.y<475;
        }
        static bool Inside(Vector2 p, Vector2[] polygon)
        {
            bool inside=false;
            for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)
                if((polygon[i].y>p.y)!=(polygon[j].y>p.y) && p.x<(polygon[j].x-polygon[i].x)*(p.y-polygon[i].y)/(polygon[j].y-polygon[i].y)+polygon[i].x) inside=!inside;
            return inside;
        }
        static void HomesAlong(List<Vector2> line,float width)
        {
            for(int i=8;i<line.Count-4;i+=9)
            {
                Vector2 tangent=(line[i+1]-line[i-1]).normalized;
                foreach(int side in new[]{-1,1})
                {
                    var p=line[i]+new Vector2(-tangent.y,tangent.x)*side*(width/2+12);
                    if(Clear(p,9)) Lot(p.x,p.y,11,13,7+(i%3)*2,0,Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg);
                }
            }
        }
        static void Parking(Vector2 center,float width,float depth,Transform parent)
        {
            var asphalt=Mat("Parking asphalt",new Color(.22f,.25f,.26f));
            Box("Parking court",center,new Vector3(width,.15f,depth),.05f,asphalt,parent);
            for(float y=-depth/2+4;y<depth/2-2;y+=5)
                foreach(int side in new[]{-1,1}) Box("Parking line",center+new Vector2(side*(width/2-5),y),new Vector3(9,.03f,.15f),.22f,roof,parent,false);
        }
        static void Hotel()
        {
            var hotel=Group("06 Hotel island - arrival precinct");
            Box("Hotel terrace",new Vector2(924,168),new Vector3(145,1,83),0,stone,hotel);
            Box("Hotel main wing",new Vector2(924,160),new Vector3(103,31,43),1,gold,hotel);
            Box("Hotel west wing",new Vector2(866,175),new Vector3(23,24,59),1,gold,hotel);
            Box("Hotel east wing",new Vector2(982,175),new Vector3(23,24,59),1,gold,hotel);
            Box("Main roof",new Vector2(924,160),new Vector3(106,1,46),32,roof,hotel);
            for(int side=-1;side<=1;side+=2) Box("Wing roof",new Vector2(924+side*58,175),new Vector3(25,1,61),25,roof,hotel);
            for(int floor=0;floor<7;floor++) for(int column=0;column<15;column++)
                Box("Hotel window",new Vector2(878+column*6.6f,181.55f),new Vector3(3.8f,2.2f,.1f),3+floor*4,glass,hotel,false);
            Box("Lobby glazing",new Vector2(924,183),new Vector3(21,5,.4f),1,glass,hotel);
            Box("Entrance canopy",new Vector2(924,193),new Vector3(32,1,23),7,roof,hotel);
            foreach(float x in new[]{911f,937f}) Box("Canopy pillar",new Vector2(x,201),new Vector3(.8f,7,.8f),0,gold,hotel);
            Box("Entrance walk",new Vector2(924,207),new Vector3(32,.2f,14),.1f,stone,hotel);
            Parking(new Vector2(1037,371),82,58,hotel);
            Road("Hotel parking entrance",10,Points(1037,334,1037,348));
            var pool=GameObject.CreatePrimitive(PrimitiveType.Cylinder); pool.name="Arrival fountain"; pool.transform.SetParent(hotel); pool.transform.position=W(new Vector2(931,272),.8f); pool.transform.localScale=new Vector3(26,1,26); pool.GetComponent<Renderer>().sharedMaterial=stone;
            var inner=GameObject.CreatePrimitive(PrimitiveType.Cylinder); inner.name="Fountain water"; inner.transform.SetParent(hotel); inner.transform.position=W(new Vector2(931,272),1.82f); inner.transform.localScale=new Vector3(23,.02f,23); inner.GetComponent<Renderer>().sharedMaterial=water;
            for(int i=0;i<3;i++) Box("Valet bay "+(i+1),new Vector2(896+i*12,209),new Vector3(9,.1f,6),.25f,roof,hotel,false);
            var landscape=Mat("Hotel garden foliage",new Color(.24f,.38f,.24f));
            foreach(var p in Points(845,201, 844,174, 844,145, 1006,144, 1006,174, 1006,203, 903,268, 915,293, 949,293, 958,267, 819,275, 814,300, 816,328, 848,352, 886,366, 1012,406, 1039,410, 1066,406))
            {
                Box("Garden planter",p,new Vector3(7,1,7),0,stone,hotel);
                var crown=GameObject.CreatePrimitive(PrimitiveType.Sphere); crown.name="Hotel garden tree"; crown.transform.SetParent(hotel); crown.transform.position=W(p,5); crown.transform.localScale=new Vector3(7,9,7); crown.GetComponent<Renderer>().sharedMaterial=landscape; Object.DestroyImmediate(crown.GetComponent<Collider>());
            }
        }
        static void Trees()
        {
            var leaf=Mat("Foliage olive",new Color(.29f,.41f,.17f)); var trunk=Mat("Tree trunks",new Color(.35f,.30f,.20f));
            var random=new System.Random(67288);
            for(int i=0;i<210;i++)
            {
                var p=new Vector2(70+(float)random.NextDouble()*550,30+(float)random.NextDouble()*440);
                if(!Clear(p,5)) continue;
                Box("Tree trunk",p,new Vector3(1,4,1),0,trunk,props,false);
                var o=GameObject.CreatePrimitive(PrimitiveType.Sphere); o.name="Tree crown"; o.transform.SetParent(props); o.transform.position=W(p,5); o.transform.localScale=new Vector3(8,10,8); o.GetComponent<Renderer>().sharedMaterial=leaf; Object.DestroyImmediate(o.GetComponent<Collider>());
            }
        }
        static void LightAndCamera()
        {
            var sun=new GameObject("Sun").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=1.1f; sun.shadows=LightShadows.Soft; sun.transform.rotation=Quaternion.Euler(50,-30,0);
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.3f,.34f,.4f); RenderSettings.fog=false;
            var camera=new GameObject("City overview camera").AddComponent<Camera>(); camera.tag="MainCamera"; camera.orthographic=true; camera.orthographicSize=365; camera.farClipPlane=3000; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.16f,.28f,.39f);
            camera.transform.SetPositionAndRotation(W(new Vector2(525,218),45),Quaternion.Euler(90,0,0));
            if(SceneView.lastActiveSceneView!=null) SceneView.lastActiveSceneView.LookAt(W(new Vector2(525,218)),Quaternion.Euler(65,0,0),650);
        }
        public static void Validate()
        {
            var missing=0; var meshCount=0;
            foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) { meshCount++; foreach(var m in r.sharedMaterials) if(m==null || m.shader==null) missing++; }
            if(missing>0 || meshCount<100 || GameObject.Find("05 Hotel bridge - Broken Vector truss")==null) throw new InvalidOperationException("Island content validation failed");
            File.WriteAllText("Logs/IslandValidation.txt","Saved scene: "+ScenePath+"\nMesh renderers: "+meshCount+"\nMissing materials: "+missing+"\nRoad routes: "+paths.Count+"\nBuildings: "+lots.Count+"\nVisual-layout scene only; hotel gameplay migration remains separate.\n");
        }
        public static void ValidateSaved()
        {
            EditorSceneManager.OpenScene(ScenePath);
            int meshes=0;
            foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if(filter.sharedMesh==null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(filter.sharedMesh))) throw new InvalidOperationException("Unsaved or missing mesh on "+filter.name);
                meshes++;
            }
            Physics.SyncTransforms();
            for(float x=652;x<770;x+=5)
            {
                bool deck=false;
                foreach(var hit in Physics.RaycastAll(W(new Vector2(x,247),50),Vector3.down,100))
                    if(hit.collider.name.StartsWith("Bridge carriageway") && Mathf.Abs(hit.point.y-.22f)<.5f) deck=true;
                if(!deck) throw new InvalidOperationException("Missing bridge deck collider at "+x);
            }
            Debug.Log("VALET ISLAND SAVED-SCENE VALIDATION PASSED: "+meshes+" persistent meshes; continuous bridge carriageway collision.");
        }
        public static void Capture()
        {
            Directory.CreateDirectory("Logs/Previews"); var camera=Camera.main;
            Render(camera,"Logs/Previews/IslandCityOverview.png",1800,1050);
            camera.transform.SetPositionAndRotation(W(new Vector2(300,220),45),Quaternion.Euler(90,0,0)); camera.orthographicSize=285;
            Render(camera,"Logs/Previews/IslandCityReferenceView.png",1200,900);
            camera.transform.position=W(new Vector2(1000,480),180); camera.transform.LookAt(W(new Vector2(910,210),6)); camera.orthographicSize=145;
            Render(camera,"Logs/Previews/HotelIsland.png",1400,900);
        }
        static void Render(Camera camera,string path,int w,int h)
        {
            ShaderUtil.allowAsyncCompilation=false;
            var rt=new RenderTexture(w,h,24); var previous=RenderTexture.active; camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            camera.Render();
            var image=new Texture2D(w,h,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,w,h),0,0); image.Apply(); File.WriteAllBytes(path,image.EncodeToPNG());
            camera.targetTexture=null; RenderTexture.active=previous; Object.DestroyImmediate(image); Object.DestroyImmediate(rt);
        }
    }
}
