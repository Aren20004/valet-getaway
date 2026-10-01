using UnityEngine;

namespace ValetGetaway
{
    // The built-in font shader draws through walls. Bind its changing atlas to a
    // depth-tested material so only the visible building faces show their labels.
    [ExecuteAlways, RequireComponent(typeof(TextMesh))]
    public sealed class BuildingLabel : MonoBehaviour
    {
        [SerializeField] Shader labelShader;
        Material material;
        TextMesh textMesh;

        public void Configure(Shader shader)
        {
            labelShader=shader;
            BindAtlas();
        }
        void OnEnable()
        {
            textMesh=GetComponent<TextMesh>();
            Font.textureRebuilt+=OnFontRebuilt;
            BindAtlas();
        }
        void OnFontRebuilt(Font font)
        {
            if(textMesh!=null && textMesh.font==font) BindAtlas();
        }
        void BindAtlas()
        {
            if(textMesh==null) textMesh=GetComponent<TextMesh>();
            if(labelShader==null || textMesh.font==null) return;
            if(material==null) material=new Material(labelShader) { name="Building label (font atlas)",hideFlags=HideFlags.HideAndDontSave };
            material.mainTexture=textMesh.font.material.mainTexture;
            GetComponent<MeshRenderer>().sharedMaterial=material;
        }
        void OnDisable()
        {
            Font.textureRebuilt-=OnFontRebuilt;
            if(material==null) return;
            if(Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            material=null;
        }
    }
}
