using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ShapeOfMemory.Editor
{
    // The play area as real objects in Scenes/PicoFinal.unity (2026-11-13; it used to hold only the MemoryGame director and
    // a light, everything else built at runtime). Add adds what is missing and never moves or replaces what is there, so
    // edits made in the scene stay:
    //
    //   Shape of Memory            MemoryGame: the director, its materials and its look (MemoryLook)
    //   Lighting / Key light       the table-top's key light (MemoryLighting sets it from the look)
    //   Play Area                  MemoryGame.PlayArea: its origin is the middle of the table top, +z away from the
    //                              player; in a headset the game places it from the eyes, on the desktop it stays here.
    //                              MemoryPlayArea draws the eyes, the orb, the book on the walls in the Scene view.
    //     Table                    MemoryGame.Table (the environment artist's SM_Table_01)
    //     Reading stand            MemoryGame.ReadingStand (BookStand_01); its pivot on the table top: it sinks there
    //       Book rest              MemoryGame.BookRest: the board's surface under the book's spine, at the board's tilt
    //         SM_Book_01           MemoryGame.BookModel: the book (MemoryBook.Dress opens it and prints on it)
    //     Button panel             MemoryGame.ButtonPanel: HINT / HOLD TO RESET / MENU while playing
    //   Editor only                tagged EditorOnly: left out of builds
    //     Seated player            MemorySeatedPlayer: a 1.70 m adult on a chair, eyes where the game puts them
    //
    // The cloud sea, the ruins and the sky are the look's: with an environment scene (the Env art looks) that scene opens
    // beside this one in the editor, and the look's sky is drawn round the Scene and Game views (ScenePreview below), so
    // the play area is seen against them. Edit the cloud sea and the ruins in that scene (the environment artist's).
    public static class ShapeOfMemorySceneLayout
    {
        const string Prefabs=ShapeOfMemoryBuilder.Root+"/Art/Art Assets/Prefabs";
        public const string TablePrefab=Prefabs+"/SM_Table_01.prefab",StandPrefab=Prefabs+"/BookStand_01.prefab",BookPrefab=Prefabs+"/SM_Book_01.prefab";
        public const string DevFolder=ShapeOfMemoryBuilder.Root+"/Art/Dev",SeatedPlayerModel=DevFolder+"/SeatedPlayer.fbx";
        // The fits the environment artist measured for the art props (Art/Runtime/PicoFinalArtDressing.cs, 2026-11-12), as
        // the scene's starting layout:
        //   the table: the art slab's top at its local y = 0 and its footprint 2.442 x 1.878 (without the floating chips),
        //   fitted onto the game's table box (1.78 x .06 x 1.18 at (0, -.035, .29)), turned so its ivy is on the far side;
        //   the stand: at .45 of its modelled height and depth, .957 across (its board as wide as the open book), turned so
        //   its lip faces the reader, its drawer knob kept round;
        //   the book on it: the game book's root at (0, .2421, -.2413), tilted 31.9 degrees, its underside (.042 of the
        //   game's book at FlatScale) on the board.
        static readonly Vector3 TableAt=new Vector3(0,0,.29f),TableSize=new Vector3(1.78f,.06f,1.18f);
        static readonly Vector2 TableSlab=new Vector2(2.442f,1.878f);
        const float StandScale=.45f,StandWidth=.957f;
        static readonly Vector3 StandOffset=new Vector3(0,.00225f,-.088f);
        static readonly Pose GameBookOnStand=new Pose(new Vector3(0,.2421f,-.2413f),Quaternion.Euler(-31.9f,0,0));
        // The desktop's play area: as high as a 1.70 m player sitting on a 45 cm chair would have it in a headset (the eyes
        // MemorySeatedPlayer.ModelEyeHeight over the floor, the table top StageBelow under them).
        public static float PlayAreaHeight=>MemorySeatedPlayer.ModelEyeHeight-MemoryGame.StageBelow;

        [MenuItem("MetaHackthone/Pico Final/Scene/Add missing play area pieces to the open scene")]
        public static void AddToOpenScene()
        {
            var game=Object.FindFirstObjectByType<MemoryGame>();
            if(!game){Debug.LogWarning("SHAPE_OF_MEMORY_SCENE no MemoryGame in the open scene");return;}
            int added=Add(game);
            Debug.Log($"SHAPE_OF_MEMORY_SCENE {added} pieces added to {game.gameObject.scene.name}"+(added>0?": save the scene to keep them":""));
        }

        // Adds what's missing; returns how many pieces it made.
        public static int Add(MemoryGame game)
        {
            int added=0;var scene=game.gameObject.scene;
            Undo.RecordObject(game,"Play area");
            // Lighting
            var lighting=Root(scene,"Lighting",ref added);
            var key=MemoryLighting.FindKey(scene);
            if(!key){key=new GameObject("Key light").AddComponent<Light>();key.type=LightType.Directional;key.shadows=LightShadows.None;Undo.RegisterCreatedObjectUndo(key.gameObject,"Key light");added++;}
            if(key.transform.parent!=lighting.transform){Undo.SetTransformParent(key.transform,lighting.transform,"Key light");if(key.name=="Shape of Memory lighting")key.name="Key light";}
            // The play area
            var area=game.PlayArea;
            if(!area)
            {
                area=Root(scene,"Play Area",ref added).transform;area.SetPositionAndRotation(new Vector3(0,PlayAreaHeight,0),Quaternion.identity);
                game.PlayArea=area;
            }
            if(!area.GetComponent<MemoryPlayArea>())Undo.AddComponent<MemoryPlayArea>(area.gameObject);
            if(!game.Table)
            {
                var table=Child(area,"Table",TableAt,Quaternion.identity,ref added);
                var art=Model(TablePrefab,table);
                if(art)
                {
                    float sx=TableSize.x/TableSlab.x,sz=TableSize.z/TableSlab.y;
                    art.localPosition=new Vector3(0,-.035f+TableSize.y*.5f+.001f,0);   // on the game box's top (it sits at y -.035)
                    art.localRotation=Quaternion.Euler(0,180,0);art.localScale=new Vector3(sx,Mathf.Min(sx,sz),sz);
                }
                game.Table=table;
            }
            if(!game.ReadingStand)
            {
                var stand=Child(area,"Reading stand",Vector3.zero,Quaternion.identity,ref added);
                var art=Model(StandPrefab,stand);
                if(art)
                {
                    art.localPosition=StandOffset;art.localRotation=Quaternion.Euler(0,180,0);art.localScale=new Vector3(StandWidth,StandScale,StandScale);
                    var knob=art.Find("SM_BookStandDrawerKnob_01");if(knob){var k=knob.localScale;knob.localScale=new Vector3(k.x*StandScale/StandWidth,k.y,k.z);}
                }
                game.ReadingStand=stand;
            }
            if(!game.BookRest)
            {
                var rotation=GameBookOnStand.rotation;
                var rest=Child(game.ReadingStand,"Book rest",GameBookOnStand.position-rotation*Vector3.up*(MemoryBook.Thickness*MemoryBook.FlatScale),rotation,ref added);
                game.BookRest=rest;
            }
            if(!game.BookModel)
            {
                var art=Model(BookPrefab,game.BookRest);
                if(art)
                {
                    // Where the game puts it, shut, on the rest (MemoryBook.Dress, at FlatScale).
                    float s=MemoryBook.FlatScale;
                    art.localPosition=new Vector3(0,MemoryBook.ArtHalf*MemoryBook.ArtScale*s,(MemoryBook.Height*.5f+MemoryBook.ArtFaceCentre*MemoryBook.ArtLength)*s);
                    art.localRotation=MemoryBook.ArtTurn;art.localScale=new Vector3(MemoryBook.ArtScale,MemoryBook.ArtScale,MemoryBook.ArtLength)*s;
                    BookMeshFix.Use(art);   // its spine fixed for opening (ShapeOfMemoryMeshes.cs)
                    game.BookModel=art;added++;
                }
            }
            if(!game.ButtonPanel)
            {
                var panel=Child(area,"Play strip",MemoryGame.StripAt,MemoryGame.StripFacing,ref added);
                Undo.AddComponent<MemoryButtonPanelGizmo>(panel.gameObject);game.ButtonPanel=panel;
            }
            foreach(var t in new[]{game.Table,game.ReadingStand,game.BookModel})if(t)Quiet(t);
            // The stand-in player
            var dev=Root(scene,"Editor only",ref added);dev.tag="EditorOnly";
            if(!Object.FindObjectsByType<MemorySeatedPlayer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(p=>p.gameObject.scene==scene))
            {
                var seated=SeatedPlayer(dev.transform,area);if(seated)added++;
            }
            EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(scene);
            return added;
        }

        // The scene's things only show: no shadows of their own (as the art add-on had them), no probes.
        static void Quiet(Transform root)
        {
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if(r.shadowCastingMode==ShadowCastingMode.Off&&r.lightProbeUsage==LightProbeUsage.Off&&r.reflectionProbeUsage==ReflectionProbeUsage.Off)continue;
                Undo.RecordObject(r,"Quiet");r.shadowCastingMode=ShadowCastingMode.Off;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
        }
        static GameObject Root(Scene scene,string name,ref int added)
        {
            var found=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==name);if(found)return found;
            var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);Undo.RegisterCreatedObjectUndo(go,name);added++;return go;
        }
        static Transform Child(Transform parent,string name,Vector3 position,Quaternion rotation,ref int added)
        {
            var found=parent.Find(name);if(found)return found;
            var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;t.localRotation=rotation;Undo.RegisterCreatedObjectUndo(t.gameObject,name);added++;return t;
        }
        static Transform Model(string path,Transform parent)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!prefab){Debug.LogError("SHAPE_OF_MEMORY_SCENE missing "+path);return null;}
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);Undo.RegisterCreatedObjectUndo(go,prefab.name);return go.transform;
        }
        // A 1.70 m adult on a chair, plain materials, on a floor disc of its own.
        static MemorySeatedPlayer SeatedPlayer(Transform parent,Transform area)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(SeatedPlayerModel);
            if(!model){Debug.LogError("SHAPE_OF_MEMORY_SCENE missing "+SeatedPlayerModel+" (Tools/ShadowArt/build_seated_player.py)");return null;}
            if(AssetImporter.GetAtPath(SeatedPlayerModel) is ModelImporter importer&&importer.materialImportMode!=ModelImporterMaterialImportMode.None)
            {importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importCameras=false;importer.importLights=false;importer.importAnimation=false;importer.SaveAndReimport();model=AssetDatabase.LoadAssetAtPath<GameObject>(SeatedPlayerModel);}
            var root=new GameObject("Seated player");root.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(root,"Seated player");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(model,root.transform);go.name="Model";
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))
            {
                bool chair=r.name.Contains("Chair");r.sharedMaterial=DevMaterial(chair?"Chair":"Seated player",chair?new Color(.42f,.31f,.23f):new Color(.80f,.82f,.88f));
                r.shadowCastingMode=ShadowCastingMode.Off;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cylinder);floor.name="Floor (this player's)";Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.SetParent(root.transform,false);floor.transform.localPosition=new Vector3(0,-.003f,.1f);floor.transform.localScale=new Vector3(1.8f,.003f,1.8f);
            var fr=floor.GetComponent<Renderer>();fr.sharedMaterial=DevMaterial("Floor",new Color(.20f,.21f,.25f));fr.shadowCastingMode=ShadowCastingMode.Off;
            var seated=root.AddComponent<MemorySeatedPlayer>();seated.PlayArea=area;seated.Place();
            return seated;
        }
        static Material DevMaterial(string name,Color color)
        {
            string path=DevFolder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.25f);
            AssetDatabase.CreateAsset(m,path);return m;
        }
    }

    // The editor's view of the look round the play area: the environment scene opened beside PicoFinal (its cloud sea and
    // ruins, as real objects to look at and edit; the look's hidden groups hidden in the Scene view only), and the look's sky
    // drawn round the Scene and Game views, turned as the game turns it. Nothing is written into either scene; Play takes
    // the open environment scene over as it is (MemoryEnvironment). Both can be switched off under Pico Final > Scene.
    [InitializeOnLoad]
    public static class ScenePreview
    {
        const string EnvironmentKey="ShapeOfMemory.Preview.Environment",SkyKey="ShapeOfMemory.Preview.Sky";
        const string EnvironmentMenu="MetaHackthone/Pico Final/Scene/Show the environment scene in the editor",SkyMenu="MetaHackthone/Pico Final/Scene/Show the sky in the editor";
        public static bool ShowEnvironment{get=>EditorPrefs.GetBool(EnvironmentKey,true);set=>EditorPrefs.SetBool(EnvironmentKey,value);}
        public static bool ShowSky{get=>EditorPrefs.GetBool(SkyKey,true);set=>EditorPrefs.SetBool(SkyKey,value);}
        static Material sky;static MemoryGame game;

        static ScenePreview()
        {
            EditorSceneManager.sceneOpened+=(scene,mode)=>{if(mode==OpenSceneMode.Single)Refresh();};
            RenderPipelineManager.beginCameraRendering+=DrawSky;
            EditorApplication.hierarchyChanged+=()=>game=null;
        }
        [MenuItem(EnvironmentMenu)] static void ToggleEnvironment(){ShowEnvironment=!ShowEnvironment;Refresh();}
        [MenuItem(EnvironmentMenu,true)] static bool EnvironmentState(){Menu.SetChecked(EnvironmentMenu,ShowEnvironment);return true;}
        [MenuItem(SkyMenu)] static void ToggleSky(){ShowSky=!ShowSky;SceneView.RepaintAll();}
        [MenuItem(SkyMenu,true)] static bool SkyState(){Menu.SetChecked(SkyMenu,ShowSky);return true;}

        static MemoryGame Game{get{if(!game)game=Object.FindFirstObjectByType<MemoryGame>();return game;}}

        // Opens (or closes) the look's environment scene beside the game's scene; call after the look changes.
        public static void Refresh()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            game=null;var g=Game;if(!g)return;
            string path=g.Look?g.Look.EnvironmentScene:"";
            for(int i=SceneManager.sceneCount-1;i>=0;i--)
            {
                var open=SceneManager.GetSceneAt(i);
                if(open==g.gameObject.scene||!open.path.StartsWith(ShapeOfMemoryBuilder.Root+"/Scenes/"))continue;
                if(open.path!=path||!ShowEnvironment)
                {
                    if(open.isDirty){Debug.LogWarning("SHAPE_OF_MEMORY_SCENE "+open.name+" has unsaved changes: left open");continue;}
                    EditorSceneManager.CloseScene(open,true);
                }
            }
            if(!ShowEnvironment||string.IsNullOrEmpty(path)||!System.IO.File.Exists(path))return;
            var env=SceneManager.GetSceneByPath(path);
            if(!env.isLoaded)env=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            SceneManager.SetActiveScene(g.gameObject.scene);
            // The look's hidden groups: hidden in the Scene view only (nothing written into the scene).
            var visibility=SceneVisibilityManager.instance;
            var root=env.GetRootGameObjects().FirstOrDefault(r=>r.name==MemoryEnvironment.RootName);
            var art=root?root.transform.Find(MemoryEnvironment.ArtName):null;
            if(art)
            {
                visibility.Show(art.gameObject,true);
                foreach(var name in g.Look.EnvironmentHide??new string[0]){var group=art.Find(name);if(group)visibility.Hide(group.gameObject,true);}
            }
            foreach(var r in env.GetRootGameObjects())if(r.CompareTag("EditorOnly"))visibility.Hide(r,true);
            Debug.Log("SHAPE_OF_MEMORY_SCENE showing "+path+" beside "+g.gameObject.scene.name+" (Pico Final > Scene to turn it off)");
        }

        // The look's sky round the editor's Scene and Game views, as MemorySky turns its dome (the glow GlowFacing degrees
        // right of the play area's forward): a dome object that is never saved (HideAndDontSave), moved onto each camera
        // as it renders. Off in Play mode, where the game's own sky takes over.
        static Renderer domeRenderer;
        static void DrawSky(ScriptableRenderContext context,Camera camera)
        {
            var g=Game;
            bool show=ShowSky&&!EditorApplication.isPlayingOrWillChangePlaymode&&g&&g.Look&&g.Look.Sky;
            if(!show){if(domeRenderer)domeRenderer.enabled=false;return;}
            if(camera.cameraType!=CameraType.SceneView&&camera.cameraType!=CameraType.Game)return;
            if(!domeRenderer)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name="Sky preview (editor)";go.hideFlags=HideFlags.HideAndDontSave;
                Object.DestroyImmediate(go.GetComponent<Collider>());domeRenderer=go.GetComponent<Renderer>();
                domeRenderer.shadowCastingMode=ShadowCastingMode.Off;domeRenderer.receiveShadows=false;domeRenderer.lightProbeUsage=LightProbeUsage.Off;domeRenderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            if(!sky||sky.shader!=g.Look.Sky.shader){if(sky)Object.DestroyImmediate(sky);sky=new Material(g.Look.Sky){name="Sky preview",hideFlags=HideFlags.HideAndDontSave};}
            sky.CopyPropertiesFromMaterial(g.Look.Sky);if(sky.HasProperty("_Alpha"))sky.SetFloat("_Alpha",1);sky.renderQueue=MemorySky.SkyQueue;
            domeRenderer.sharedMaterial=sky;domeRenderer.enabled=true;
            float yaw=(g.PlayArea?g.PlayArea.eulerAngles.y:0)+g.Look.GlowFacing-g.Look.GlowAzimuth;
            float radius=Mathf.Min(MemorySky.Radius,camera.farClipPlane*.8f);
            // The sphere is a unit across: scale by the diameter (as MemorySky.Round does).
            var t=domeRenderer.transform;t.SetPositionAndRotation(camera.transform.position,Quaternion.Euler(0,yaw,0));t.localScale=Vector3.one*radius*2;
        }
    }
}
