using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ValetGetaway.Editor
{
    // Subtract crossing carriageways from sidewalks, and assign overlapping surfaces
    // to one route. UV interpolation preserves the imported road pack's atlas.
    public static class IslandRoadJunctions
    {
        public sealed class Surface
        {
            public Mesh Mesh;
            public MeshCollider Collider;
            public bool[] CurbTriangles;
            public int[] TriangleSegments;
            public readonly List<Quad> Full = new List<Quad>();
            public readonly List<Quad> Asphalt = new List<Quad>();
        }
        public sealed class Quad
        {
            public Vector2[] Points;
            public Rect Bounds;
            public Quad(Vector2 a,Vector2 b,Vector2 c)
            {
                Points=new[] { a,b,c };
                if(Cross(b-a,c-a)<0) System.Array.Reverse(Points);
                Bounds=BoundsOf(Points);
            }
            public Quad(Vector2 a, Vector2 b, Vector2 na, Vector2 nb, float halfWidth)
            {
                Points = new[] { a-na*halfWidth, b-nb*halfWidth, b+nb*halfWidth, a+na*halfWidth };
                float area=0;
                for(int i=0;i<4;i++) area+=Cross(Points[i],Points[(i+1)%4]);
                if(area<0) System.Array.Reverse(Points);
                Bounds=BoundsOf(Points);
            }
        }
        struct Vertex
        {
            public Vector3 Position;
            public Vector2 UV;
            public Vector2 Plan => new Vector2(Position.x,Position.z);
            public Vertex(Vector3 p,Vector2 uv) { Position=p; UV=uv; }
            public static Vertex Lerp(Vertex a,Vertex b,float t) => new Vertex(Vector3.Lerp(a.Position,b.Position,t),Vector2.Lerp(a.UV,b.UV,t));
        }
        static float Cross(Vector2 a,Vector2 b) => a.x*b.y-a.y*b.x;
        static Rect BoundsOf(IList<Vector2> p)
        {
            Vector2 min=p[0],max=p[0];
            for(int i=1;i<p.Count;i++) { min=Vector2.Min(min,p[i]); max=Vector2.Max(max,p[i]); }
            return Rect.MinMaxRect(min.x-.0001f,min.y-.0001f,max.x+.0001f,max.y+.0001f);
        }
        static Rect BoundsOf(List<Vertex> polygon)
        {
            var points=new Vector2[polygon.Count];
            for(int i=0;i<points.Length;i++) points[i]=polygon[i].Plan;
            return BoundsOf(points);
        }
        static List<Vertex> HalfPlane(List<Vertex> polygon,Vector2 a,Vector2 b,bool inside)
        {
            var result=new List<Vertex>();
            var previous=polygon[polygon.Count-1];
            float pd=Cross(b-a,previous.Plan-a);
            bool previousKept=inside ? pd>=0 : pd<0;
            foreach(var current in polygon)
            {
                float cd=Cross(b-a,current.Plan-a);
                bool kept=inside ? cd>=0 : cd<0;
                if(kept!=previousKept) result.Add(Vertex.Lerp(previous,current,pd/(pd-cd)));
                if(kept) result.Add(current);
                previous=current; pd=cd; previousKept=kept;
            }
            return result;
        }
        static void Subtract(List<Vertex> polygon,Quad quad,List<List<Vertex>> output)
        {
            if(!quad.Bounds.Overlaps(BoundsOf(polygon))) { output.Add(polygon); return; }
            var remainder=polygon;
            for(int edge=0;edge<quad.Points.Length && remainder.Count>=3;edge++)
            {
                var outside=HalfPlane(remainder,quad.Points[edge],quad.Points[(edge+1)%quad.Points.Length],false);
                if(outside.Count>=3) output.Add(outside);
                remainder=HalfPlane(remainder,quad.Points[edge],quad.Points[(edge+1)%quad.Points.Length],true);
            }
        }
        public static void Trim(IList<Surface> surfaces)
        {
            for(int road=0;road<surfaces.Count;road++)
            {
                var surface=surfaces[road];
                var positions=surface.Mesh.vertices; var uvs=surface.Mesh.uv; var triangles=surface.Mesh.triangles;
                var outputVertices=new List<Vector3>(); var outputUV=new List<Vector2>(); var outputTriangles=new List<int>();
                var asphaltCuts=new List<Quad>(); var curbCuts=new List<Quad>();
                for(int other=0;other<surfaces.Count;other++)
                {
                    if(other==road) continue;
                    // Asphalt owns all space it crosses. Earlier routes own overlapping
                    // asphalt and the remaining overlapping sidewalk corners.
                    curbCuts.AddRange(other<road ? surfaces[other].Full : surfaces[other].Asphalt);
                    if(other<road) asphaltCuts.AddRange(surfaces[other].Asphalt);
                }
                for(int t=0;t<triangles.Length;t+=3)
                {
                    var polygon=new List<Vertex>();
                    for(int k=0;k<3;k++) { int index=triangles[t+k]; polygon.Add(new Vertex(positions[index],uvs[index])); }
                    var triangleBounds=BoundsOf(polygon);
                    var fragments=new List<List<Vertex>> { polygon };
                    foreach(var quad in surface.CurbTriangles[t/3] ? curbCuts : asphaltCuts)
                    {
                        if(!quad.Bounds.Overlaps(triangleBounds)) continue;
                        var next=new List<List<Vertex>>();
                        foreach(var fragment in fragments) Subtract(fragment,quad,next);
                        fragments=next;
                        if(fragments.Count==0) break;
                    }
                    // A closed loop can intersect itself at its entrance. Treat its
                    // other segments with the same ownership rules as separate roads.
                    int segment=surface.TriangleSegments == null ? -1 : surface.TriangleSegments[t/3];
                    for(int other=0;segment>=0 && other<surface.Asphalt.Count && fragments.Count>0;other++)
                    {
                        if(other==segment || (!surface.CurbTriangles[t/3] && other>segment)) continue;
                        var quad=surface.CurbTriangles[t/3] && other<segment ? surface.Full[other] : surface.Asphalt[other];
                        if(!quad.Bounds.Overlaps(triangleBounds)) continue;
                        var next=new List<List<Vertex>>();
                        foreach(var fragment in fragments) Subtract(fragment,quad,next);
                        fragments=next;
                    }
                    foreach(var fragment in fragments)
                    {
                        for(int k=1;k<fragment.Count-1;k++)
                        {
                            if(Vector3.Cross(fragment[k].Position-fragment[0].Position,fragment[k+1].Position-fragment[0].Position).sqrMagnitude<1e-10f) continue;
                            foreach(int index in new[] { 0,k,k+1 }) { outputTriangles.Add(outputVertices.Count); outputVertices.Add(fragment[index].Position); outputUV.Add(fragment[index].UV); }
                        }
                    }
                }
                surface.Mesh.Clear(); surface.Mesh.indexFormat=IndexFormat.UInt32;
                surface.Mesh.SetVertices(outputVertices); surface.Mesh.SetUVs(0,outputUV); surface.Mesh.SetTriangles(outputTriangles,0);
                surface.Mesh.RecalculateNormals(); surface.Mesh.RecalculateBounds();
                surface.Collider.sharedMesh=null; surface.Collider.sharedMesh=surface.Mesh;
                EditorUtility.SetDirty(surface.Mesh);
            }
        }
    }
}
