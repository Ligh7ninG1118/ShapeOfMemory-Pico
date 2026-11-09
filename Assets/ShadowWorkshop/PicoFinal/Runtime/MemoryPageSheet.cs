using CloudGarden.ShadowPuzzle;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShapeOfMemory
{
    // The turning page as drawn (MemoryBook): one bendable two-sided sheet (PageBend) printed with the two pages it
    // carries, and the shade it casts on the standing pages. A page's content is built as usual on a print root, rendered
    // once from straight in front into that side's print (a private layer and an orthographic camera that never renders
    // by itself), then hidden: the sheet shows the print, so its text, plates and tiles bend with the paper. The sheet's
    // material is in Resources (ShapeOfMemory/Book page), which keeps its shader in builds.
    sealed class PageSheet
    {
        // An unnamed layer nothing else uses; the print camera sees only it.
        public const int PrintLayer=31;
        // About a millimetre of page per texel, near what the headset resolves at the book's distance.
        public const int PrintWidth=1024,PrintHeight=960;
        // The shade: drawn this far in front of the standing pages (in front of their content, behind the sheet at rest),
        // darkest where the sheet is close, fading over Reach of gap.
        public const float ShadowOffset=.0085f,ShadowStrength=.5f,ShadowReach=.22f;
        const int ShadowColumns=24;
        // Toward the light the sheet is shaded by (stage space): from the front and above.
        static readonly Vector3 LightFrom=new Vector3(0,.8f,-.6f).normalized;

        public Renderer Renderer{get;}
        readonly Transform book;
        readonly BookShape shape;
        readonly Mesh mesh;
        readonly Material material;
        readonly Camera camera;
        readonly RenderTexture[] prints=new RenderTexture[2];
        readonly Mesh[] shadows=new Mesh[2];
        readonly Renderer[] shadowRenderers=new Renderer[2];
        readonly Vector3[] positions=new Vector3[PageBend.Vertices],normals=new Vector3[PageBend.Vertices],edge=new Vector3[PageBend.Rows+1];
        readonly Color32[] crease=new Color32[PageBend.Vertices];
        readonly Color32[][] shade={new Color32[(ShadowColumns+1)*(PageBend.Rows+1)],new Color32[(ShadowColumns+1)*(PageBend.Rows+1)]};
        // Each page's direction from the spine and the way it faces (left 0, right 1).
        readonly Vector3[] page=new Vector3[2],facing=new Vector3[2];
        readonly float[] along=new float[PageBend.Columns+1],gap=new float[PageBend.Columns+1];

        public PageSheet(Transform book,MemoryKit kit,BookShape shape,Texture rightPaper,Texture leftPaper)
        {
            this.book=book;this.shape=shape;
            for(int k=0;k<2;k++){page[k]=shape.Direction(k==1);facing[k]=shape.Facing(k==1);}
            for(int side=0;side<2;side++)
            {
                prints[side]=new RenderTexture(PrintWidth,PrintHeight,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default)
                    {name=side==1?"Book right print":"Book left print",useMipMap=true,autoGenerateMips=true,filterMode=FilterMode.Trilinear,anisoLevel=4,wrapMode=TextureWrapMode.Clamp};
                kit.Owned.Add(prints[side]);
            }
            var template=Resources.Load<Material>("ShapeOfMemory/Book page");
            material=template?new Material(template):new Material(Shader.Find("ShapeOfMemory/Book Page"));material.name="Book page";kit.Owned.Add(material);
            material.SetTexture("_FrontPaper",rightPaper);material.SetTexture("_BackPaper",leftPaper);
            material.SetTexture("_FrontPrint",prints[1]);material.SetTexture("_BackPrint",prints[0]);
            mesh=new Mesh{name="Turning page"};mesh.MarkDynamic();kit.Owned.Add(mesh);
            Bend(0,0,true,0);
            mesh.vertices=positions;mesh.normals=normals;mesh.colors32=crease;mesh.uv=PageBend.Uvs();mesh.triangles=PageBend.Triangles();
            float w=MemoryBook.Width,h=MemoryBook.Height;
            mesh.bounds=new Bounds(shape.Corner+shape.Up*h*.5f,Vector3.one*(w*2.4f+h));
            Renderer=Part("Turning page",mesh,material);

            var shadowTemplate=Resources.Load<Material>("ShapeOfMemory/Book page shadow");
            var shadowMaterial=shadowTemplate?new Material(shadowTemplate):new Material(Shader.Find("ShapeOfMemory/Book Page Shadow"));shadowMaterial.name="Book page shadow";kit.Owned.Add(shadowMaterial);
            for(int k=0;k<2;k++)
            {
                var points=new Vector3[(ShadowColumns+1)*(PageBend.Rows+1)];
                for(int j=0;j<=PageBend.Rows;j++)for(int i=0;i<=ShadowColumns;i++)
                    points[j*(ShadowColumns+1)+i]=shape.Corner+page[k]*(w*i/ShadowColumns)+shape.Up*(h*j/PageBend.Rows)+facing[k]*ShadowOffset;
                var triangles=new int[ShadowColumns*PageBend.Rows*6];int n=0;
                for(int j=0;j<PageBend.Rows;j++)for(int i=0;i<ShadowColumns;i++){int a=j*(ShadowColumns+1)+i,up=a+ShadowColumns+1;triangles[n++]=a;triangles[n++]=up;triangles[n++]=a+1;triangles[n++]=a+1;triangles[n++]=up;triangles[n++]=up+1;}
                shadows[k]=new Mesh{name=k==1?"Right page shade":"Left page shade"};shadows[k].MarkDynamic();kit.Owned.Add(shadows[k]);
                shadows[k].vertices=points;shadows[k].colors32=shade[k];shadows[k].triangles=triangles;shadows[k].RecalculateBounds();
                shadowRenderers[k]=Part(shadows[k].name,shadows[k],shadowMaterial);
            }

            camera=new GameObject("Print camera",typeof(Camera)).GetComponent<Camera>();camera.transform.SetParent(book,false);
            camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.cullingMask=1<<PrintLayer;camera.allowHDR=false;camera.allowMSAA=false;camera.useOcclusionCulling=false;
            var data=camera.GetUniversalAdditionalCameraData();
            data.allowXRRendering=false;data.renderPostProcessing=false;data.renderShadows=false;data.antialiasing=AntialiasingMode.None;
            data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;
            Show(false);
        }
        Renderer Part(string name,Mesh shape,Material look)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(book,false);
            go.GetComponent<MeshFilter>().sharedMesh=shape;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=look;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            return r;
        }
        public void Show(bool on){Renderer.enabled=on;foreach(var r in shadowRenderers)r.enabled=on;}

        // Renders a print root's content into a side's print (front 1: a right page; back 0: a left page), then hides it.
        public void Print(Transform face,int side)
        {
            var target=prints[side];
            // A blank page (what Build replaced is only destroyed at the end of the frame, inactive meanwhile).
            bool printed=false;foreach(Transform child in face)printed|=child.gameObject.activeSelf;
            if(!printed){face.gameObject.SetActive(false);Clear(target);return;}
            face.gameObject.SetActive(true);
            foreach(var t in face.GetComponentsInChildren<Transform>(true))t.gameObject.layer=PrintLayer;
            foreach(var text in face.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();
            float scale=face.lossyScale.y;
            camera.transform.SetPositionAndRotation(face.position-face.forward*(.5f*scale),face.rotation);
            camera.orthographicSize=MemoryBook.Height*.5f*scale;camera.aspect=MemoryBook.Width/MemoryBook.Height;
            camera.nearClipPlane=.4f*scale;camera.farClipPlane=.6f*scale;
            var request=new RenderPipeline.StandardRequest{destination=target};
            if(RenderPipeline.SupportsRenderRequest(camera,request))RenderPipeline.SubmitRenderRequest(camera,request);
            else{camera.targetTexture=target;camera.Render();camera.targetTexture=null;}
            face.gameObject.SetActive(false);
        }
        static void Clear(RenderTexture target){var active=RenderTexture.active;RenderTexture.active=target;GL.Clear(true,true,Color.clear);RenderTexture.active=active;}

        // Bends the sheet to a turn's root and tip (progress), held by row grab (0..1 up the page), and shades the pages
        // under it.
        public void Pose(float root,float tip,bool forward,float grab=0)
        {
            Bend(root,tip,forward,grab);
            mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetColors(crease);
            for(int k=0;k<2;k++){Shade(k);shadows[k].SetColors(shade[k]);}
            var light=book.TransformDirection(LightFrom);
            material.SetVector("_LightDir",light);material.SetFloat("_LightOnPage",Vector3.Dot(book.TransformDirection(facing[1]).normalized,light));
        }
        void Bend(float root,float tip,bool forward,float grab)
        {
            int leave=forward?1:0;
            PageBend.Shape(root,tip,shape.Corner,page[leave],facing[leave],shape.Up,shape.Opening,MemoryBook.Width,MemoryBook.Height,shape.Lift,grab,positions,normals,crease,edge);
        }
        // A standing page's shade: at each point, how near the sheet comes to it (a row's points against the same row of the
        // page), counting only the part of the sheet over this page (past the spine's own offset: lying on the other page,
        // the sheet shades nothing here).
        void Shade(int k)
        {
            var origin=shape.Corner;Vector3 on=page[k],off=facing[k];float w=MemoryBook.Width;var colors=shade[k];
            for(int j=0;j<=PageBend.Rows;j++)
            {
                for(int i=0;i<=PageBend.Columns;i++){var d=positions[j*(PageBend.Columns+1)+i]-origin;d-=shape.Up*Vector3.Dot(d,shape.Up);along[i]=Vector3.Dot(d,on);gap[i]=Vector3.Dot(d,off);}
                for(int c=0;c<=ShadowColumns;c++)
                {
                    float x=w*c/ShadowColumns,near=float.MaxValue;
                    for(int i=0;i<=PageBend.Columns;i++){if(along[i]<=PageBend.Rest*1.5f)continue;float a=along[i]-x,b=Mathf.Max(0,gap[i]);near=Mathf.Min(near,a*a+b*b);}
                    if(near==float.MaxValue){colors[j*(ShadowColumns+1)+c]=new Color32(255,255,255,0);continue;}
                    float dark=ShadowStrength*Mathf.Exp(-Mathf.Sqrt(near)/ShadowReach)*Mathf.Clamp01((x-.03f)/.03f);
                    colors[j*(ShadowColumns+1)+c]=new Color32(255,255,255,(byte)(255*dark));
                }
            }
        }
    }
}
