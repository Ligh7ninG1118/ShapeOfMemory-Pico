using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CloudGarden.ShadowPuzzle
{
    // The room corner shared by the game (Torch2World) and the designers' gym (Torch2Gym): table, the two walls in their
    // frames, the lamps on their stands and the orb. Each lit wall is a texture of its lamp's spot (smooth, so low
    // resolution is fine) plus a mesh of the pile's outline projected onto the wall, drawn just in front with the same
    // spot dimmed: sharp at any distance, and overlapping triangles show the same colour. Runs in edit mode for the gym.
    public sealed class Torch2Room
    {
        public static readonly Color Dark=new Color(.065f,.07f,.08f),Warm=new Color(1f,.90f,.72f),FrameColor=new Color(.16f,.17f,.18f);
        // Resources path of the lit walls' shader: URP Unlit plus the lamp's slide.
        public const string SlideShader="ShadowArt/ProjectedSlide";
        // Resources path of the orb rings' see-through material (a Resources asset, so builds keep its transparent
        // variant). Tints set the alpha: RingAlpha at rest, RingLitAlpha while a hand rolls them.
        public const string RingMaterial="ShadowArt/Orb ring";
        public const float RingAlpha=.4f,RingLitAlpha=.65f;
        const int W=Torch2Projection.Width,H=Torch2Projection.Height;
        public static Vector2 WallSize=>new Vector2(W*Torch2Projection.Pixel,H*Torch2Projection.Pixel);
        public readonly Renderer[] frames=new Renderer[2];
        // The lit walls (every wall) and the shadow quads in front of the lit ones, for a host that frames them its own way.
        public readonly Renderer[] wallQuads=new Renderer[2],shadowQuads=new Renderer[2];
        public GameObject Table{get;private set;}
        // The colour of a wall where no lamp reaches (and of its shadows). Unlit walls take it at once, lit ones at the
        // next Light.
        public Color Base
        {
            get=>baseColor;
            set{baseColor=value;for(int i=0;i<pixels.Length;i++)pixels[i]=value;for(int k=0;k<2;k++)if(walls[k]&&!shaded[k]){walls[k].SetPixels32(pixels);walls[k].Apply(false,false);}}
        }
        Color baseColor=Dark;
        // Lit lamps only: the beam that lights the pile and the glowing lens (Lamps dims both).
        readonly Light[] beams=new Light[2];
        readonly Renderer[] lenses=new Renderer[2];
        readonly List<GameObject> lampModels=new List<GameObject>();
        // Each lamp's head: at the light, its forward aimed at the pile's rest position (a host can hang its own model on it).
        public readonly Transform[] lampHeads=new Transform[2];
        public readonly Vector3[] wallCentres=new Vector3[2];
        public readonly Quaternion[] wallFacing=new Quaternion[2];
        readonly Transform root;
        readonly Material surface,screen,glow;
        readonly List<Object> owned;
        readonly MaterialPropertyBlock property=new MaterialPropertyBlock();
        readonly Texture2D[] walls=new Texture2D[2],shaded=new Texture2D[2];
        // Slide materials: lit wall k at k, its shadow at 2 + k.
        readonly Material[] slides=new Material[4];
        readonly Mesh[] shadows=new Mesh[2];
        readonly Color32[] pixels=new Color32[W*H];
        readonly List<Vector2> outline=new List<Vector2>();
        readonly List<Vector3> shadowVertices=new List<Vector3>();
        readonly List<Vector2> shadowUVs=new List<Vector2>();
        int[] shadowIndices=new int[0];

        // lamps: the walls this level lights; the others stay dark.
        public Torch2Room(Transform root,Material surface,Material screen,Material glow,int[] lamps,List<Object> owned)
        {
            this.root=root;this.surface=surface;this.screen=screen;this.glow=glow;this.owned=owned;
            Table=Box("Sculptor table",new Vector3(0,-.035f,.04f),new Vector3(1.4f,.06f,1.0f),new Color(.085f,.14f,.19f));
            var size=WallSize;
            for(int k=0;k<2;k++)
            {
                var centre=Torch2Projection.WallOrigin[k]+Torch2Projection.WallRight[k]*(size.x*.5f)+Vector3.up*(size.y*.5f);
                var facing=Quaternion.LookRotation(-Torch2Projection.WallNormal[k],Vector3.up);wallCentres[k]=centre;wallFacing[k]=facing;
                var frame=Box("Wall frame "+k,centre-Torch2Projection.WallNormal[k]*.02f,new Vector3(size.x+.03f,size.y+.03f,.02f),FrameColor);frame.transform.localRotation=facing;frames[k]=frame.GetComponent<Renderer>();
                bool lit=System.Array.IndexOf(lamps,k)>=0;var slide=lit?Resources.Load<Shader>(SlideShader):null;
                walls[k]=Texture("Torch2 wall "+k,W,H);var quad=Quad("Shadow wall "+k,centre,size,walls[k],slide);quad.transform.localRotation=facing;wallQuads[k]=quad.GetComponent<Renderer>();
                for(int i=0;i<pixels.Length;i++)pixels[i]=Dark;walls[k].SetPixels32(pixels);walls[k].Apply(false,false);
                if(lit)
                {
                    shaded[k]=Texture("Torch2 wall shade "+k,W,H);shadows[k]=new Mesh{name="Torch2 shadow "+k};shadows[k].MarkDynamic();owned.Add(shadows[k]);
                    var shadow=Quad("Shadow "+k,Vector3.zero,Vector2.one,shaded[k],slide);shadow.transform.localScale=Vector3.one;
                    shadow.GetComponent<MeshFilter>().sharedMesh=shadows[k];var r=shadow.GetComponent<Renderer>();r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;shadowQuads[k]=r;
                    if(slide){slides[k]=quad.GetComponent<Renderer>().sharedMaterial;slides[2+k]=r.sharedMaterial;}
                }
                Lamp(k,lit);
            }
            RestMarker=Cylinder("Rest marker",new Vector3(Torch2Projection.Home.x,.002f,Torch2Projection.Home.z),new Vector3(.20f,.002f,.20f),new Color(.20f,.27f,.30f));
        }
        // The disc on the table under the orb's rest position.
        public GameObject RestMarker{get;private set;}
        void Lamp(int k,bool lit)
        {
            // Fixed lamp on a stand, aimed at the pile's rest position.
            var position=Torch2Projection.Lamps[k];var head=new GameObject("Lamp "+k).transform;head.SetParent(root,false);
            head.localPosition=position;head.localRotation=Quaternion.LookRotation(Torch2Projection.Home-position,Vector3.up);lampHeads[k]=head;
            lampModels.Add(Cylinder("Lamp stand",new Vector3(position.x,position.y*.5f,position.z),new Vector3(.012f,position.y*.5f,.012f),new Color(.12f,.12f,.13f)));
            lampModels.Add(Cylinder("Lamp foot",new Vector3(position.x,.006f,position.z),new Vector3(.09f,.006f,.09f),new Color(.12f,.12f,.13f)));
            var body=Cylinder("Lamp body",new Vector3(0,0,-.06f),new Vector3(.05f,.06f,.05f),lit?new Color(.30f,.30f,.32f):new Color(.16f,.16f,.17f),head);body.transform.localRotation=Quaternion.Euler(90,0,0);lampModels.Add(body);
            var lens=Cylinder("Lamp lens",Vector3.zero,new Vector3(.046f,.002f,.046f),lit?Color.white:new Color(.2f,.2f,.2f),head);lens.transform.localRotation=Quaternion.Euler(90,0,0);lampModels.Add(lens);
            if(!lit)return;
            lens.GetComponent<Renderer>().sharedMaterial=glow;lenses[k]=lens.GetComponent<Renderer>();
            var spot=new GameObject("Lamp beam").AddComponent<Light>();spot.transform.SetParent(head,false);spot.type=LightType.Spot;
            spot.spotAngle=Torch2Projection.HalfAngle*2+8;spot.range=2.5f;spot.intensity=BeamIntensity;spot.color=Warm;spot.shadows=LightShadows.None;beams[k]=spot;
        }
        const float BeamIntensity=2.2f;
        // Whether lamp k lights its wall in this level.
        public bool Lit(int k)=>beams[k]!=null;
        // Only the light: the stands, bodies and lenses go (a host that shows the lamps by what they light).
        public void RemoveLampModels()
        {
            foreach(var model in lampModels)Kill(model);
            lampModels.Clear();for(int k=0;k<2;k++)lenses[k]=null;
        }
        // The walls (frames, lit walls, shadows) shown or hidden, for a host that brings them in its own way.
        public void ShowWalls(bool on)
        {
            for(int k=0;k<2;k++){if(frames[k])frames[k].enabled=on;if(wallQuads[k])wallQuads[k].enabled=on;if(shadowQuads[k])shadowQuads[k].enabled=on;}
        }
        // 0 = lamps off (the walls go dark through Light's colour), 1 = on.
        public void Lamps(float level)
        {
            for(int k=0;k<2;k++)
            {
                if(beams[k]){beams[k].intensity=BeamIntensity*level;beams[k].enabled=level>0;}
                if(lenses[k])Tint(lenses[k],Color.Lerp(new Color(.18f,.18f,.19f),Color.white,level));
            }
        }
        // The lit walls, and the same walls with 90% of the lamp blocked for the shadow meshes to show.
        public void Light(Torch2Projection p,Color light)
        {
            for(int j=0;j<p.walls.Length;j++)
            {
                var bright=p.brightness[j];int k=p.walls[j];
                for(int i=0;i<pixels.Length;i++)pixels[i]=Base+(light-Base)*bright[i];
                walls[k].SetPixels32(pixels);walls[k].Apply(false,false);
                for(int i=0;i<pixels.Length;i++)pixels[i]=Base+(light-Base)*(bright[i]*.1f);
                shaded[k].SetPixels32(pixels);shaded[k].Apply(false,false);
            }
        }
        // What a lit wall's lamp projects (Torch2Cue): cropped to fill the wall at the slide's own aspect. Without a slide
        // shader (missing resource) the walls stay plain.
        public void Slide(int wall,Texture texture)
        {
            if(!slides[wall]||!texture)return;
            float aspect=texture.width/(float)texture.height,wallAspect=W/(float)H;
            var rect=aspect>wallAspect?new Vector4(wallAspect/aspect,1,(1-wallAspect/aspect)*.5f,0):new Vector4(1,aspect/wallAspect,0,(1-aspect/wallAspect)*.5f);
            foreach(var m in new[]{slides[wall],slides[2+wall]}){m.SetTexture("_Slide",texture);m.SetVector("_SlideRect",rect);}
        }
        // blur: as a fraction of the slide's width (0 = sharp); mix: how strongly the slide tints the lamp light.
        public void Focus(int wall,float blur,float saturation,float mix)
        {
            if(!slides[wall])return;
            foreach(var m in new[]{slides[wall],slides[2+wall]}){m.SetFloat("_SlideBlur",blur);m.SetFloat("_SlideSaturation",saturation);m.SetFloat("_SlideMix",mix);}
        }
        // The pile's shadow on every lit wall for this pose (workbench space).
        public void Shadows(Torch2Projection p,Quaternion rotation,Vector3 position)
        {
            var size=WallSize;
            for(int j=0;j<p.walls.Length;j++)
            {
                int k=p.walls[j];p.Outline(j,rotation,position,outline);
                var origin=Torch2Projection.WallOrigin[k]+Torch2Projection.WallNormal[k]*.003f;var right=Torch2Projection.WallRight[k];
                shadowVertices.Clear();shadowUVs.Clear();
                foreach(var q in outline){shadowVertices.Add(origin+right*q.x+Vector3.up*q.y);shadowUVs.Add(new Vector2(q.x/size.x,q.y/size.y));}
                if(shadowIndices.Length<outline.Count){shadowIndices=new int[Mathf.NextPowerOfTwo(outline.Count)];for(int i=0;i<shadowIndices.Length;i++)shadowIndices[i]=i;}
                var mesh=shadows[k];mesh.Clear();mesh.indexFormat=outline.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;mesh.SetVertices(shadowVertices);mesh.SetUVs(0,shadowUVs);
                mesh.SetIndices(shadowIndices,0,outline.Count,MeshTopology.Triangles,0,false);
                mesh.bounds=new Bounds(origin+right*(size.x*.5f)+Vector3.up*(size.y*.5f),new Vector3(size.x+.01f,size.y+.01f,size.x+.01f));
            }
        }
        // The orb around the pile: three great circles to roll it by and a hub ball to carry it by. Neither casts a shadow.
        public Renderer[] Orb(Transform parent,out Renderer hub)
        {
            var ring=Ring(Torch2Input.OrbRadius,.0025f);var turns=new[]{Quaternion.identity,Quaternion.Euler(90,0,0),Quaternion.Euler(0,0,90)};var rings=new Renderer[3];
            var see=Resources.Load<Material>(RingMaterial);
            for(int i=0;i<3;i++)
            {
                var go=new GameObject("Orb ring",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localRotation=turns[i];
                go.GetComponent<MeshFilter>().sharedMesh=ring;rings[i]=go.GetComponent<MeshRenderer>();rings[i].sharedMaterial=see?see:surface;rings[i].shadowCastingMode=ShadowCastingMode.Off;
            }
            var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.name="Hub";ball.transform.SetParent(parent,false);ball.transform.localScale=Vector3.one*.06f;Kill(ball.GetComponent<Collider>());
            hub=ball.GetComponent<Renderer>();hub.sharedMaterial=surface;hub.shadowCastingMode=ShadowCastingMode.Off;
            return rings;
        }
        // A thin ring of radius r in the XZ plane, tube radius t, faces outward.
        Mesh Ring(float r,float t,int segments=96,int sides=6)
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var tri=new List<int>();
            for(int i=0;i<=segments;i++)
            {
                float a=i*Mathf.PI*2/segments;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                for(int j=0;j<=sides;j++){float b=j*Mathf.PI*2/sides;var normal=dir*Mathf.Cos(b)+Vector3.up*Mathf.Sin(b);v.Add(dir*r+normal*t);n.Add(normal);}
            }
            for(int i=0;i<segments;i++)for(int j=0;j<sides;j++){int a=i*(sides+1)+j,b=a+sides+1;tri.Add(a);tri.Add(a+1);tri.Add(b);tri.Add(a+1);tri.Add(b+1);tri.Add(b);}
            var mesh=new Mesh{name="Torch2 orb ring"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetTriangles(tri,0);mesh.RecalculateBounds();owned.Add(mesh);return mesh;
        }
        public Texture2D Texture(string name,int w,int h){var t=new Texture2D(w,h,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};owned.Add(t);return t;}
        public GameObject Quad(string name,Vector3 position,Vector2 size,Texture2D texture,Shader shader=null)
        {
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name=name;quad.transform.SetParent(root,false);quad.transform.localPosition=position;quad.transform.localScale=new Vector3(size.x,size.y,1);Kill(quad.GetComponent<Collider>());
            var material=shader?new Material(shader){name=name}:new Material(screen);owned.Add(material);material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",texture);material.SetFloat("_Cull",0);quad.GetComponent<Renderer>().sharedMaterial=material;return quad;
        }
        public GameObject Box(string name,Vector3 p,Vector3 size,Color color,bool collider=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=surface;Tint(go.GetComponent<Renderer>(),color);if(!collider)Kill(go.GetComponent<Collider>());return go;
        }
        public GameObject Cylinder(string name,Vector3 p,Vector3 scale,Color color,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent?parent:root,false);go.transform.localPosition=p;go.transform.localScale=scale;
            var r=go.GetComponent<Renderer>();r.sharedMaterial=surface;r.shadowCastingMode=ShadowCastingMode.Off;Tint(r,color);Kill(go.GetComponent<Collider>());return go;
        }
        public void Tint(Renderer r,Color color){property.Clear();property.SetColor("_BaseColor",color);r.SetPropertyBlock(property);}
        public static void Kill(Object o){if(!o)return;if(Application.isPlaying)Object.Destroy(o);else Object.DestroyImmediate(o);}
    }
}
