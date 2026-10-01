using System.Collections.Generic;
using UnityEngine;
using B=ValetGetaway.Editor.IslandCityBuilder;

namespace ValetGetaway.Editor
{
    // The full reference extends the original cropped center by 367 px west and 238 px north.
    internal static class IslandReferenceExpansion
    {
        static Vector2 P(float x,float y)=>new Vector2(x-367,y-238);
        static Vector2[] Path(params float[] coordinates)
        {
            var result=new Vector2[coordinates.Length/2];
            for(int i=0;i<result.Length;i++) result[i]=P(coordinates[2*i],coordinates[2*i+1]);
            return result;
        }
        internal static Vector2[] CityOutline()=>Path(40,115,100,70,190,40,275,25,360,27,421,20,444,72,483,91,525,122,603,110,670,105,744,136,803,175,866,210,914,262,947,315,986,334,1014,364,1001,402,987,438,1005,471,1018,520,991,599,980,675,945,744,921,781,855,830,758,848,678,881,605,889,550,921,510,951,448,963,391,938,374,889,358,826,302,805,266,771,214,754,173,759,130,732,107,695,111,635,100,581,115,522,139,478,127,443,78,421,47,373,24,310,17,245,30,180);
        internal static void AddRoads()
        {
            B.Road("Northwest scenic circuit",12,Path(155,338,155,180,155,111,201,96,257,69,292,72,341,106,389,117,405,153,410,184,441,208,430,227,410,237,402,288));
            B.Road("Western upper block",12,Path(156,167,239,167,245,199,245,288,254,335,294,376,344,426,389,449,452,449));
            B.Road("West cross avenue",14,Path(155,241,222,241,269,232,303,229,325,253,339,275,367,280,385,253,403,239));
            B.Road("West downtown connection",16,Path(155,338,176,382,246,386,341,386,394,403,432,412));
            B.Road("Western lower district",14,Path(195,383,195,468,214,507,291,520,323,561,330,597,370,616,409,644,453,650,565,651));
            B.Road("Warehouse block east",12,Path(299,520,324,516,324,477,324,444,246,410,244,339));
            B.Road("Western hill road",12,Path(439,650,439,685,402,700,304,700,263,711,248,738,255,768,284,792,321,805,364,808,404,822,440,853,449,882));
            B.Road("South city connector",14,Path(565,731,494,733,431,735,389,743,365,764,364,808));
            B.Road("Southern shore road",12,Path(449,882,488,879,530,854,564,823,600,811,632,794,653,773,638,752,603,742,565,731));
            B.Road("Southeast residential circuit",11,Path(565,731,612,724,659,701,703,690,749,706,805,696,853,670,868,645,846,620,819,619,800,638,789,674,789,720));
            B.Road("North hillside houses",10,Path(650,267,668,228,683,199,689,173,673,155,674,137));
            B.Road("North hillside outer loop",10,Path(689,173,722,180,751,201,781,224,797,254,806,279,829,292));
            B.Road("Marina quay",9,Path(284,792,289,838,323,855,382,858,393,904,438,909,449,882));
        }
        static void Block(float x,float y,float width,float depth,float height,int district)
        { var p=P(x,y); B.Lot(p.x,p.y,width,depth,height,district); }
        internal static void AddScenery()
        {
            Block(202,204,59,60,14,1); Block(208,291,55,69,19,1);
            Block(331,337,77,66,18,2); Block(218,431,38,49,23,2);
            Block(263,479,97,49,18,2); Block(329,564,55,75,21,1);
            Block(418,568,47,88,26,2); Block(389,421,46,40,16,1);
            Block(465,622,35,43,16,2); Block(543,697,43,53,23,2);
            var random=new System.Random(5112026);
            // Street-facing parcels in the outer districts fill the larger island.
            foreach(var center in Path(282,207,364,217,431,251,459,278,484,332,549,240,615,242,714,185,747,216,780,267,301,593,370,628,454,696,499,714,589,761,646,812,722,764,773,730,842,685))
            {
                for(int i=0;i<9;i++)
                {
                    var p=center+new Vector2((float)random.NextDouble()*54-27,(float)random.NextDouble()*42-21);
                    if(B.Clear(p,12)) B.Lot(p.x,p.y,16,21,10+(i%3)*2,0,(i%3-1)*15);
                }
            }
            Pond("North lake",335,157,28,42);
            Pond("City pond",407,313,14,23);
            Pond("Eastern stream",756,321,48,12);
            Pond("Eastern stream bend",813,364,17,39);
            Pond("Eastern stream mouth",855,414,14,35);
            Hill(72,235,49,146,64); Hill(368,116,48,77,52); Hill(471,197,41,45,32);
            Hill(863,265,34,80,52); Hill(918,556,49,78,63); Hill(268,614,49,46,31);
            Hill(1401,264,76,71,58); Hill(1243,265,34,53,36); Hill(1207,593,31,53,33);
            for(int i=0;i<600;i++)
            {
                var p=P(45+(float)random.NextDouble()*930,55+(float)random.NextDouble()*830);
                if(!B.Clear(p,6)) continue;
                // Skip the visible water features rather than planting into ponds.
                Vector2 full=p+new Vector2(367,238);
                if(Vector2.Distance(full,new Vector2(335,157))<46 || Vector2.Distance(full,new Vector2(407,313))<27) continue;
                var trunk=B.Box("Woodland trunk",p,new Vector3(.8f,4,.8f),0,B.Mat("Tree trunks",new Color(.35f,.30f,.2f)),B.props,false);
                Crown(p,4+(float)random.NextDouble()*3,4+(float)random.NextDouble()*3);
            }
            var pier=B.Mat("Marina concrete",new Color(.53f,.57f,.57f));
            foreach(float x in new[]{252f,280f,316f,374f})
            {
                B.Box("Marina pier",P(x,847),new Vector3(5,1,95),-.5f,pier,B.props);
                for(int i=0;i<7;i++) B.Box("Marina berth",P(x+8,805+i*12),new Vector3(16,.6f,2),-.1f,pier,B.props);
            }
            Block(399,851,24,65,9,1);
        }
        static void Pond(string name,float x,float y,float rx,float rz)
        {
            var vertices=new List<Vector3>{B.W(P(x,y),.035f)}; var triangles=new List<int>();
            for(int i=0;i<20;i++)
            {
                float angle=i*Mathf.PI*2/20, ripple=1+.12f*Mathf.Sin(i*2.7f);
                vertices.Add(B.W(P(x+Mathf.Cos(angle)*rx*ripple,y+Mathf.Sin(angle)*rz*ripple),.035f));
                triangles.AddRange(new[]{0,i+1,(i+1)%20+1});
            }
            var pond=B.MeshObject(name,vertices.ToArray(),triangles.ToArray(),null,new[]{B.water},B.terrain);
            Object.DestroyImmediate(pond.GetComponent<Collider>());
        }
        static void Hill(float x,float y,float rx,float rz,float height)
        {
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            int nx=Mathf.CeilToInt(rx*2/8),nz=Mathf.CeilToInt(rz*2/8);
            var grid=new Vector3[nx+1,nz+1];
            for(int ix=0;ix<=nx;ix++) for(int iz=0;iz<=nz;iz++)
            {
                float dx=(float)ix/nx*2-1,dz=(float)iz/nz*2-1;
                var point=P(x+dx*rx,y+dz*rz);
                float rise=Mathf.Max(0,1-Mathf.Sqrt(dx*dx+dz*dz));
                float roadMargin=Mathf.Clamp01((B.RoadClearance(point)-6)/20);
                grid[ix,iz]=B.W(point,.025f+height*rise*roadMargin);
            }
            for(int ix=0;ix<nx;ix++) for(int iz=0;iz<nz;iz++)
                foreach(var triangle in new[]{new[]{grid[ix,iz],grid[ix+1,iz],grid[ix,iz+1]},new[]{grid[ix+1,iz],grid[ix+1,iz+1],grid[ix,iz+1]}})
                {
                    if(triangle[0].y<.03f && triangle[1].y<.03f && triangle[2].y<.03f) continue;
                    foreach(var point in triangle) { triangles.Add(vertices.Count); vertices.Add(point); }
                }
            B.MeshObject("Faceted coastal hill",vertices.ToArray(),triangles.ToArray(),null,new[]{B.Mat("Hills olive",new Color(.33f,.43f,.20f))},B.terrain);
        }
        static void Crown(Vector2 p,float radius,float height)
        {
            var center=B.W(p,5);
            var v=new[]{center+Vector3.up*height,center+Vector3.right*radius,center+Vector3.forward*radius,center-Vector3.right*radius,center-Vector3.forward*radius,center-Vector3.up*2};
            var tree=B.MeshObject("Faceted woodland canopy",v,new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1},null,new[]{B.Mat("Woodland green",new Color(.27f,.42f,.16f))},B.props);
            Object.DestroyImmediate(tree.GetComponent<Collider>());
        }
    }
}
