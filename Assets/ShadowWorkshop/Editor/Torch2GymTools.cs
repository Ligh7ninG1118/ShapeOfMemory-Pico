using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CloudGarden.Integration;
using CloudGarden.ShadowPuzzle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShadowWorkshopIntegration.Editor
{
    // The designers' gym for torch-2 levels (Torch2Gym): its scene, piece tags, lamp views, checks, export, open-in-gym
    // and play. Docs/ShadowWorkshop.md, "Shadow Gym", is the designer guide.
    public static class Torch2GymTools
    {
        public const string Scene="Assets/ShadowWorkshop/Scenes/ShadowGym.unity";
        public const string LevelFolder="Assets/ShadowWorkshop/Resources/ShadowArt/Torch2",ShadowFolder="Assets/ShadowWorkshop/Art/Torch2Gym";
        internal const string PlayKey="Torch2Gym.Play",ReturnKey="Torch2Gym.Return";
        // Set by a game that can start at a gym level on a headset (Shape of Memory, through Build and Run). The gym's
        // inspector shows Run on headset while it is set.
        public static Action<Torch2Gym> RunOnHeadset;

        [MenuItem("MetaHackthone/Shadow Workshop/Gym/Open gym")]
        public static void Open()
        {
            if(EditorApplication.isPlaying||!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(File.Exists(Scene))EditorSceneManager.OpenScene(Scene);else Create();
            var gym=Object.FindFirstObjectByType<Torch2Gym>();if(gym)Selection.activeObject=gym;
        }
        // A new gym scene: the game's room, a camera where the desktop player stands and an example pile to replace.
        public static Torch2Gym Create(string path=Scene)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var light=new GameObject("Gym lighting").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.color=new Color(1,.91f,.78f);light.shadows=LightShadows.None;light.transform.rotation=Quaternion.Euler(48,-28,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.65f,.70f);
            var camera=new GameObject("Gym camera").AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=48;camera.nearClipPlane=.02f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.075f,.09f);
            camera.transform.position=new Vector3(0,1.15f,-2.0f);camera.transform.LookAt(new Vector3(0,.40f,.30f));
            var root=new GameObject("Shadow Gym");root.SetActive(false);var gym=root.AddComponent<Torch2Gym>();
            gym.surfaceMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Graybox surfaces.mat");
            gym.screenMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Projection screen.mat");
            gym.glowMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Tracked hands and lamp.mat");
            var cluster=new GameObject("Cluster (the orb)").transform;cluster.SetParent(root.transform,false);cluster.localPosition=Torch2Projection.Home;gym.cluster=cluster;
            // Example: a mushroom from two primitives. Replace with your own pieces.
            Primitive(PrimitiveType.Cylinder,"Example stem",cluster,new Vector3(0,-.05f,0),new Vector3(.06f,.07f,.06f),new Color(.93f,.89f,.80f));
            Primitive(PrimitiveType.Sphere,"Example cap",cluster,new Vector3(0,.04f,0),new Vector3(.22f,.10f,.22f),new Color(.80f,.25f,.20f));
            root.SetActive(true);EditorSceneManager.SaveScene(scene,path);return gym;
        }
        static void Primitive(PrimitiveType type,string name,Transform parent,Vector3 position,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/ShadowWorkshop/Materials/Graybox surfaces.mat");var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;
            var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",color);renderer.SetPropertyBlock(block);
        }

        [MenuItem("MetaHackthone/Shadow Workshop/Gym/Mark selection as visual only")] static void MarkVisual()=>Mark(ShadowPiece.Role.VisualOnly);
        [MenuItem("MetaHackthone/Shadow Workshop/Gym/Mark selection as picture")] static void MarkPicture()=>Mark(ShadowPiece.Role.Picture);
        [MenuItem("GameObject/Shadow Gym/Visual only (casts no shadow)",false,40)] static void ContextVisual()=>Mark(ShadowPiece.Role.VisualOnly);
        [MenuItem("GameObject/Shadow Gym/Picture (casts the shadow)",false,41)] static void ContextPicture()=>Mark(ShadowPiece.Role.Picture);
        public static void Mark(ShadowPiece.Role role)
        {
            foreach(var go in Selection.gameObjects)
            {
                var tag=go.GetComponent<ShadowPiece>();if(!tag)tag=Undo.AddComponent<ShadowPiece>(go);
                Undo.RecordObject(tag,"Mark shadow piece");tag.role=role;
            }
        }
        [MenuItem("Assets/Open in Shadow Gym")] static void OpenAsset()
        {
            var level=Selection.activeObject as Torch2Level;if(!level)return;
            Open();var gym=Object.FindFirstObjectByType<Torch2Gym>();if(gym)Load(gym,level);
        }
        [MenuItem("Assets/Open in Shadow Gym",true)] static bool CanOpenAsset()=>Selection.activeObject is Torch2Level;

        // The scene view looks out of the lamp at the pile: the silhouette you see is the shadow.
        public static void LookFromLamp(Torch2Gym gym,int k)
        {
            var view=SceneView.lastActiveSceneView;if(!view)return;
            var lamp=gym.transform.TransformPoint(Torch2Projection.Lamps[k]);var pile=gym.transform.TransformPoint(Torch2Projection.Home);var rotation=Quaternion.LookRotation(pile-lamp,Vector3.up);
            view.orthographic=false;view.cameraSettings.fieldOfView=Torch2Projection.HalfAngle*2+8;
            // The scene view orbits a pivot at a distance derived from its size: measure that, then put the camera on the lamp.
            view.LookAtDirect(pile,rotation,1);view.LookAtDirect(pile,rotation,Vector3.Distance(pile,lamp)/view.cameraDistance);view.Repaint();
        }
        // Moves the pieces so the picture pieces' bounds are centred on the hub.
        public static void Recenter(Torch2Gym gym)
        {
            gym.Refresh(true);var bounds=gym.Pieces.Where(p=>!p.visualOnly&&p.source).Select(p=>p.source.GetComponent<Renderer>().bounds).ToList();if(bounds.Count==0)return;
            var all=bounds[0];foreach(var b in bounds)all.Encapsulate(b);var offset=gym.cluster.InverseTransformPoint(all.center);
            foreach(Transform child in gym.cluster){Undo.RecordObject(child,"Recenter pieces");child.localPosition-=offset;}
        }
        // Shrinks the pile about the hub until every piece is within MaxPieceRadius.
        public static void FitInsideOrb(Torch2Gym gym)
        {
            gym.Refresh(true);float radius=Radius(gym);if(radius<=Torch2Gym.MaxPieceRadius)return;float f=Torch2Gym.MaxPieceRadius/radius;
            foreach(Transform child in gym.cluster){Undo.RecordObject(child,"Fit pieces inside the orb");child.localPosition*=f;child.localScale*=f;}
        }

        public static int ShadowTriangles(Torch2Gym gym)
        {
            long n=0;foreach(var p in gym.Pieces)if(!p.visualOnly&&p.shadow)for(int s=0;s<p.shadow.subMeshCount;s++)if(p.shadow.GetTopology(s)==MeshTopology.Triangles)n+=p.shadow.GetIndexCount(s)/3;
            return (int)n;
        }
        // Furthest shadow-casting vertex from the hub.
        public static float Radius(Torch2Gym gym)
        {
            float r=0;if(!gym.Preview)return r;
            foreach(var piece in gym.Preview.pieces)
            {
                if(piece.visualOnly)continue;var m=Matrix4x4.TRS(piece.position,Quaternion.Euler(piece.euler),piece.Scale);
                foreach(var v in piece.ShadowMesh.vertices)r=Mathf.Max(r,m.MultiplyPoint3x4(v).magnitude);
            }
            return r;
        }
        public static List<string> MaterialWarnings(Torch2Gym gym)
        {
            var notes=new List<string>();var seen=new HashSet<Material>();
            foreach(var piece in gym.Pieces)foreach(var m in piece.materials)
            {
                if(!m){notes.Add($"{piece.label}: missing material.");continue;}
                if(seen.Add(m)&&m.GetTag("RenderPipeline",false,"")!="UniversalPipeline")notes.Add($"{m.name} ({m.shader.name}) is not a URP shader and may render pink on Quest; switch it to Universal Render Pipeline/Lit.");
            }
            return notes;
        }

        public sealed class Result
        {
            public readonly List<string> errors=new List<string>(),warnings=new List<string>(),facts=new List<string>();
            public bool Ok=>errors.Count==0;
            public override string ToString()=>string.Join("\n",errors.Select(e=>"ERROR  "+e).Concat(warnings.Select(w=>"WARNING  "+w)).Concat(facts));
        }
        // What export checks, mirroring Tools/ShadowArt/build_torch2.py: the answer shows on every wall, random
        // orientations do not pass (the closest sets the threshold when auto), how far the answer can be off and still
        // pass, and a start that is far and cold (picked when auto).
        public static Result Check(Torch2Gym gym,int samples=1000)
        {
            var result=new Result();gym.Refresh(true);var picture=gym.Pieces.Count(p=>!p.visualOnly);
            if(!Regex.IsMatch(gym.id??"","^[A-Za-z][A-Za-z0-9_]*$"))result.errors.Add("Level id: a letter, then letters, digits or _ (it names the asset).");
            else if(ScriptLevels().Contains(gym.id))result.errors.Add($"'{gym.id}' is made by Tools/ShadowArt/build_torch2.py, and Import torch levels 2 would overwrite it. Pick another id.");
            if(picture==0)result.errors.Add("Nothing under the cluster casts a shadow.");
            foreach(var line in gym.Notes.Split('\n'))if(line.Trim()!="")result.warnings.Add(line.Trim());
            int triangles=ShadowTriangles(gym);result.facts.Add($"{picture} picture pieces, {gym.Pieces.Count-picture} visual only, {triangles} shadow triangles (budget {Torch2Gym.ShadowTriangleBudget}).");
            if(triangles>Torch2Gym.ShadowTriangleBudget)result.warnings.Add($"{triangles} shadow triangles is over the {Torch2Gym.ShadowTriangleBudget} budget; the shadow is redrawn every frame the orb moves on Quest. Give heavy pieces a simpler ShadowPiece proxy.");
            float radius=Radius(gym);if(radius>Torch2Gym.MaxPieceRadius)result.warnings.Add($"Pieces reach {radius*100:0} cm from the hub, past the orb's {Torch2Gym.MaxPieceRadius*100:0} cm grab reach (Fit inside orb).");
            result.warnings.AddRange(MaterialWarnings(gym));
            if(!result.Ok||gym.Projection==null)return Finish(gym,result);
            var level=Object.Instantiate(gym.Preview);level.hideFlags=HideFlags.DontSave;
            try
            {
                level.iouMin=gym.autoThreshold?.62f:gym.iouMin;var p=new Torch2Projection(level);
                p.Render(Quaternion.identity,Torch2Projection.Home);p.Evaluate();
                for(int j=0;j<p.walls.Length;j++)if(!p.scores[j].visible)result.errors.Add($"At the answer the {WallName(p.walls[j])} wall shows no shadow inside its light; move pieces into the beam.");
                if(!result.Ok)return Finish(gym,result);
                foreach(var line in Cropped(p))result.warnings.Add(line);
                var far=Sample(p,level,samples,out _);float top=far.Count>0?far.Max(f=>f.iou):0;
                var lookalike=Lookalike(p,level,far);float lookalikeAngle=p.AngleToAnswer(lookalike.pose);
                if(gym.autoThreshold){Undo.RecordObject(gym,"Check level");gym.iouMin=Threshold(top,lookalike.iou);}
                level.iouMin=gym.iouMin;p=new Torch2Projection(level);Sample(p,level,samples,out int passes);
                result.facts.Add($"Match threshold IoU {gym.iouMin:0.00}{(gym.autoThreshold?" (auto)":"")}; the closest of {far.Count} random orientations reached {top:0.00}"+
                    (lookalike.iou>0?$", the closest lookalike {lookalike.iou:0.00} ({lookalikeAngle:0} degrees from the answer).":".")+
                    (p.Answers.Length>1?$" Also counted: the picture {Flips(level.flips)} ({p.Answers.Length-1} more answer{(p.Answers.Length>2?"s":"")}).":""));
                if(passes>0)result.warnings.Add($"{passes} of {far.Count} random orientations also pass: the picture has a second answer (often its mirror image{(p.Answers.Length>1?", here one the accepted flips do not cover":"")}). Fine if it reads the same, otherwise break the symmetry.");
                else if(lookalike.iou>=gym.iouMin)result.warnings.Add($"The pile turned {lookalikeAngle:0} degrees from the answer matches at IoU {lookalike.iou:0.00}, so the level solves there too"+
                    (gym.autoThreshold?" (the threshold is at its 0.95 limit). Fine if it reads the same, otherwise break the symmetry.":$". Tick Auto Threshold, or raise it to {Threshold(top,lookalike.iou):0.00}."));
                var axes=new List<(string,Vector3)>{("up",Vector3.up),("right",Vector3.right),("forward",Vector3.forward)};
                foreach(int k in p.walls)axes.Add(($"{WallName(k)} lamp's line of sight (tilts the picture)",(Torch2Projection.Home-Torch2Projection.Lamps[k]).normalized));
                var spans=new List<string>();int tight=99;
                foreach(var (name,axis) in axes){Window(p,axis,out int lo,out int hi);spans.Add($"{name} {lo:+0;-0;0}..{hi:+0;-0;0}");tight=Mathf.Min(tight,Mathf.Min(-lo,hi));}
                result.facts.Add("Turns about each axis that still pass (degrees, 3-degree steps): "+string.Join("; ",spans)+".");
                if(tight<6)result.warnings.Add($"One axis passes only within {tight} degrees, hard to hold by hand (aim for 6 or more). Thin parts and fine detail make it tight.");
                int moves=0;foreach(var d in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back}){p.Render(Quaternion.identity,Torch2Projection.Home+d*.06f);p.Evaluate();if(p.Score.Ready)moves++;}
                result.facts.Add($"Carried 6 cm off the rest spot, it still passes in {moves} of 6 directions.");
                Cue(gym,p,result);
                if(gym.autoStart)
                {
                    Undo.RecordObject(gym,"Check level");gym.start=PickStart(p,out float warmth);
                    result.facts.Add($"Start picked: {Quaternion.Angle(gym.start,Quaternion.identity):0} degrees from the answer, warmth {warmth*100:0}%.");
                }
                else
                {
                    p.Render(gym.start,Torch2Projection.Home);p.Evaluate();float angle=p.AngleToAnswer(gym.start);
                    if(p.Score.Ready)result.errors.Add("The start already solves the level.");
                    else if(p.Score.warmth>.5f||angle<45)result.warnings.Add($"The start is close to the answer ({angle:0} degrees, warmth {p.Score.warmth*100:0}%).");
                    else result.facts.Add($"Start: {angle:0} degrees from the answer, warmth {p.Score.warmth*100:0}%.");
                }
                level.start=gym.start;Transit(gym,level,result);
            }
            catch(OperationCanceledException){result.errors.Add("Check cancelled.");}
            finally{EditorUtility.ClearProgressBar();Object.DestroyImmediate(level);}
            return Finish(gym,result);
        }
        static Result Finish(Torch2Gym gym,Result result){Undo.RecordObject(gym,"Check level");gym.report=result.ToString();EditorUtility.SetDirty(gym);return result;}
        // Weakest wall's IoU among walls that meet cover and spill (0 if one does not) for random orientations at least
        // 30 degrees from the answer, with each orientation; passes counts the ones that pass outright.
        static List<(float iou,Quaternion pose)> Sample(Torch2Projection p,Torch2Level level,int samples,out int passes)
        {
            var far=new List<(float,Quaternion)>();var random=new System.Random(1);passes=0;
            for(int n=0;n<samples;n++)
            {
                if(n%50==0&&EditorUtility.DisplayCancelableProgressBar("Check level","Trying random orientations",n/(float)samples))throw new OperationCanceledException();
                var q=RandomRotation(random);if(p.AngleToAnswer(q)<30)continue;
                p.Render(q,Torch2Projection.Home);p.Evaluate();if(p.Score.Ready)passes++;far.Add((Weakest(p,level),q));
            }
            return far;
        }
        static float Weakest(Torch2Projection p,Torch2Level level)
        {
            float weakest=1;foreach(var s in p.scores)weakest=Mathf.Min(weakest,s.visible&&s.cover>=level.coverMin&&s.spill<=level.spillMax?s.iou:0);return weakest;
        }
        // The auto threshold: over the closest random orientation by 0.05 and over the closest lookalike by 0.03, up to the
        // next hundredth (less a hair, so float error cannot push 0.85 to 0.86).
        public static float Threshold(float randomTop,float lookalike)=>Mathf.Clamp(Mathf.Ceil(Mathf.Max(.62f,randomTop+.05f,lookalike+.03f)*100-.001f)/100,.62f,.95f);
        public const int LookalikeSeeds=10;
        // Random orientations rarely land on a lookalike's best pose (often the pile turned round, its mirror image), and a
        // player can turn the orb onto it. So the best far ones climb to their local best match, staying past
        // lookalikeAngle. Returns the best, weakest wall's IoU (0 if none).
        static (float iou,Quaternion pose) Lookalike(Torch2Projection p,Torch2Level level,List<(float iou,Quaternion pose)> far)
        {
            var seeds=far.Where(f=>p.AngleToAnswer(f.pose)>=level.lookalikeAngle).OrderByDescending(f=>f.iou).Take(LookalikeSeeds).ToList();
            (float iou,Quaternion pose) best=(0,Quaternion.identity);
            for(int k=0;k<seeds.Count;k++)
            {
                if(EditorUtility.DisplayCancelableProgressBar("Check level","Looking for lookalike poses",k/(float)seeds.Count))throw new OperationCanceledException();
                var found=Climb(p,level,seeds[k].pose);if(found.iou>best.iou)best=found;
            }
            return best;
        }
        // From seed, turns the orb about the room's axes in shrinking steps while the match improves, never nearer an answer
        // (the solution or an accepted flip) than lookalikeAngle. A climb that ends against that limit was heading for the answer itself: iou 0.
        public static (float iou,Quaternion pose) Climb(Torch2Projection p,Torch2Level level,Quaternion seed)
        {
            float limit=level.lookalikeAngle;var axes=new[]{Vector3.right,Vector3.up,Vector3.forward};
            // Failing cover or spill still climbs, a step below any pass.
            float Match(Quaternion q){p.Render(q,Torch2Projection.Home);p.Evaluate();float w=1;foreach(var s in p.scores)w=Mathf.Min(w,s.visible&&s.cover>=level.coverMin&&s.spill<=level.spillMax?s.iou:s.iou-.3f);return w;}
            var pose=seed;float value=Match(pose);
            foreach(float step in new[]{8f,4f,2f,1f,.5f})
                for(int round=0;round<40;round++)
                {
                    bool better=false;
                    foreach(var axis in axes)for(int sign=-1;sign<=1;sign+=2)
                    {
                        var next=Quaternion.AngleAxis(sign*step,axis)*pose;if(p.AngleToAnswer(next)<limit)continue;
                        float m=Match(next);if(m>value+1e-4f){value=m;pose=next;better=true;}
                    }
                    if(!better)break;
                }
            if(p.AngleToAnswer(pose)<limit+2)return (0,pose);
            p.Render(pose,Torch2Projection.Home);p.Evaluate();return (Weakest(p,level),pose);
        }
        // At the answer (rendered on p): walls whose shadow is partly outside the lamp's light or runs off the wall.
        static IEnumerable<string> Cropped(Torch2Projection p)
        {
            const int W=Torch2Projection.Width,H=Torch2Projection.Height;
            for(int j=0;j<p.walls.Length;j++)
            {
                int cast=0,seen=0,edge=0;var shadow=p.shadow[j];
                for(int i=0;i<shadow.Length;i++)
                {
                    if(!shadow[i])continue;cast++;if(p.visible[j][i])seen++;
                    int x=i%W,y=i/W;if(x==0||y==0||x==W-1||y==H-1)edge++;
                }
                float dark=1-seen/(float)Mathf.Max(1,cast);
                if(dark>.03f||edge>0)yield return $"At the answer {dark*100:0}% of the {WallName(p.walls[j])} wall's shadow is outside its light{(edge>0?" and it runs off the wall":"")}, so the picture is cut there. Move the pieces nearer the hub (Recenter) or further from the lamp.";
            }
        }
        // The closeness cue on this level (Torch2Cue): how many random poses reach each tier (lookalikes past lookalikeAngle
        // stop at 1), from how far a typical turn reaches it (median over random axes), how far the release snap reaches,
        // and whether the slides can blur and the layers stay in step.
        static void Cue(Torch2Gym gym,Torch2Projection p,Result result)
        {
            var tiers=gym.tiers;var random=new System.Random(3);const int samples=600;var reach=new int[4];var heard=new int[4];int lookalikes=0;
            if(!(tiers.x<tiers.y&&tiers.y<tiers.z&&tiers.z<1))result.warnings.Add($"Tiers must rise below 1 (now {tiers.x:0.00} / {tiers.y:0.00} / {tiers.z:0.00}).");
            for(int n=0;n<samples;n++)
            {
                var q=RandomRotation(random);p.Render(q,Torch2Projection.Home);p.Evaluate();float angle=p.AngleToAnswer(q);bool far=angle>gym.lookalikeAngle;
                int tier=Torch2Tier.Target(p.Score.warmth,p.Score.Ready,far,tiers,0);for(int k=1;k<=3;k++)if(tier>=k)reach[k]++;
                int music=Torch2Tier.Target(angle>gym.cueAngle?0:p.Score.warmth,p.Score.Ready,far,tiers,0);for(int k=1;k<=3;k++)if(music>=k)heard[k]++;
                if(far&&Torch2Tier.Target(p.Score.warmth,p.Score.Ready,false,tiers,0)>=2)lookalikes++;
            }
            var angles=new[]{5,10,15,20,25,30,40,50,60};var median=new float[angles.Length];var w=new float[24];
            for(int a=0;a<angles.Length;a++)
            {
                for(int i=0;i<w.Length;i++){p.Render(Quaternion.AngleAxis(angles[a],RandomRotation(random)*Vector3.forward),Torch2Projection.Home);p.Evaluate();w[i]=p.Score.warmth;}
                Array.Sort(w);median[a]=w[w.Length/2];
            }
            // Widest angle with every smaller one at or above t (0 if even 5 degrees is not).
            int From(float t,float limit=180){int found=0;for(int a=0;a<angles.Length&&angles[a]<=limit&&median[a]>=t;a++)found=angles[a];return found;}
            string Pct(int k)=>(100f*reach[k]/samples).ToString(reach[k]*100<samples*10?"0.0":"0")+"%";
            string Heard(int k)=>(100f*heard[k]/samples).ToString(heard[k]*100<samples*10?"0.0":"0")+"%";
            int[] from={0,From(tiers.x),From(tiers.y),From(tiers.z)};int snap=From(gym.snapWarmth,gym.snapAngle);
            result.facts.Add($"Cue tiers at warmth {tiers.x:0.00} / {tiers.y:0.00} / {tiers.z:0.00}: random poses reach them {Pct(1)} / {Pct(2)} / {Pct(3)} ({lookalikes} lookalikes over {gym.lookalikeAngle:0} degrees held at tier 1); a typical turn reaches them from about {from[1]} / {from[2]} / {from[3]} degrees. Let go and it snaps from about {snap} degrees (warmth {gym.snapWarmth:0.00} within {gym.snapAngle:0}); Shape of Memory also snaps it out of the hand within {gym.heldSnapAngle:0}. The music warms only within {gym.cueAngle:0} degrees of an answer: random poses make it heard at tiers 1-3 {Heard(1)} / {Heard(2)} / {Heard(3)}.");
            if(reach[1]*100>samples*35)result.warnings.Add($"Tier 1 comes too easily: {Pct(1)} of random poses reach it. Raise the first tier.");
            if(from[3]<10)result.warnings.Add($"Tier 3 (hot) only shows within {from[3]} degrees, about where the level already passes. Lower the third tier.");
            foreach(int k in gym.Lamps)
            {
                var slide=k==0?gym.leftSlide:gym.rightSlide;
                if(!slide)result.facts.Add($"The {WallName(k)} lamp projects a generated placeholder slide.");
                else if(slide.mipmapCount<=1)result.warnings.Add($"{slide.name} has no mipmaps, so it cannot blur: tick Generate Mipmaps in its import settings.");
            }
            var layers=gym.layers??new AudioClip[0];
            if(layers.Length==0)result.facts.Add("The generated placeholder sound layers play.");
            else if(Array.Exists(layers,c=>!c))result.warnings.Add("A sound layer slot is empty, so the placeholder layers play instead.");
            else if(layers.Any(c=>Mathf.Abs(c.length-layers[0].length)>.002f))result.warnings.Add($"Sound layers differ in length ({string.Join(", ",layers.Select(c=>c.length.ToString("0.00")+" s"))}), so they drift out of step. Trim them to one length.");
        }
        // The table to the orb with the real shapes (convex hulls), 30 times a second: a piece that cuts into another
        // deeper than the two already do at rest (on the table, or in the orb) is reported with the moment.
        public static void Transit(Torch2Gym gym,Torch2Level level,Result result)
        {
            if(!gym.tableLayout){result.facts.Add("No table layout: the game scatters the things on the table by itself.");return;}
            if(!Torch2Transit.HasTable(level)){result.errors.Add("The table layout must hold a twin of every piece, named like it or like a parent under the cluster (see the notes above), or be cleared.");return;}
            var transit=new Torch2Transit(level,level.start,Torch2Projection.Home,gym.transitFly,gym.transitOverlap);int n=level.pieces.Length;
            result.facts.Add($"Table to orb: {n} pieces as {transit.movers.Length} things, one after another, {transit.Length:0.0} s ({gym.transitFly:0.00} s each, overlap {gym.transitOverlap:0.00}).");
            foreach(var m in transit.movers)if(m.blocked)result.warnings.Add($"{level.pieces[m.members[0]].label}: no clear line into its place in the orb, so it comes in through its neighbours. Move it, or the things round it.");
            var clashes=Clashes(level,transit);
            for(int i=0;i<clashes.Count&&i<6;i++)result.warnings.Add(clashes[i]+" Lower the overlap, or move one of them.");
            if(clashes.Count>6)result.warnings.Add($"... and {clashes.Count-6} more pairs.");
            if(clashes.Count==0)result.facts.Add("Nothing cuts into anything on the way (convex shapes).");
        }
        // One line per pair of pieces that cut into each other on the way, at the first moment they do.
        public static List<string> Clashes(Torch2Level level,Torch2Transit transit)
        {
            int n=level.pieces.Length;var lines=new List<string>();
            var root=new GameObject("Transit check"){hideFlags=HideFlags.HideAndDontSave};var colliders=new Collider[n];var copies=new List<Mesh>();
            try
            {
                for(int i=0;i<n;i++)
                {
                    var piece=level.pieces[i];if(!piece.mesh)continue;var go=new GameObject(piece.label){hideFlags=HideFlags.HideAndDontSave};go.transform.SetParent(root.transform,false);go.transform.localScale=piece.Scale;
                    var mesh=piece.mesh.isReadable?piece.mesh:Torch2Gym.ReadableCopy(piece.mesh);if(mesh!=piece.mesh)copies.Add(mesh);
                    var collider=go.AddComponent<MeshCollider>();collider.convex=true;collider.sharedMesh=mesh;colliders[i]=collider;
                }
                float Depth(int a,Pose pa,int b,Pose pb)=>colliders[a]&&colliders[b]&&Physics.ComputePenetration(colliders[a],pa.position,pa.rotation,colliders[b],pb.position,pb.rotation,out _,out float d)?d:0;
                var onTable=new float[n,n];var inOrb=new float[n,n];
                for(int a=0;a<n;a++)for(int b=0;b<n;b++)if(a!=b){onTable[a,b]=Depth(a,transit.table[a],b,transit.table[b]);inOrb[a,b]=Depth(a,transit.slot[a],b,transit.slot[b]);}
                var reported=new HashSet<(int,int)>();
                for(float t=0;t<=transit.Length;t+=1/30f)
                    for(int a=0;a<n;a++)if(transit.Flying(a,t))
                    {
                        var pa=transit.Evaluate(a,t);
                        for(int b=0;b<n;b++)
                        {
                            if(b==a||transit.moverOf[a]==transit.moverOf[b]||reported.Contains((Mathf.Min(a,b),Mathf.Max(a,b))))continue;
                            var mb=transit.movers[transit.moverOf[b]];bool waiting=t<=mb.start,landed=t>=mb.end;
                            float allowed=waiting?onTable[a,b]:landed?inOrb[a,b]:Mathf.Max(onTable[a,b],inOrb[a,b]);
                            float depth=Depth(a,pa,b,transit.Evaluate(b,t));if(depth<=allowed+.003f)continue;
                            reported.Add((Mathf.Min(a,b),Mathf.Max(a,b)));
                            lines.Add($"{level.pieces[a].label} cuts {depth*100:0.0} cm into {level.pieces[b].label} ({(waiting?"on the table":landed?"in the orb":"also flying")}) at {t:0.0} s of {transit.Length:0.0}.");
                        }
                    }
            }
            finally{Object.DestroyImmediate(root);foreach(var mesh in copies)Object.DestroyImmediate(mesh);}
            return lines;
        }
        static void Window(Torch2Projection p,Vector3 axis,out int lo,out int hi)
        {
            lo=hi=0;
            for(int d=3;d<=45;d+=3){p.Render(Quaternion.AngleAxis(d,axis),Torch2Projection.Home);p.Evaluate();if(!p.Score.Ready)break;hi=d;}
            for(int d=3;d<=45;d+=3){p.Render(Quaternion.AngleAxis(-d,axis),Torch2Projection.Home);p.Evaluate();if(!p.Score.Ready)break;lo=-d;}
        }
        // The start Check level would pick for the gym as it stands (Auto Start), without checking anything else.
        public static Quaternion PickStart(Torch2Gym gym,out float warmth)
        {
            var level=Object.Instantiate(gym.Preview);level.hideFlags=HideFlags.DontSave;level.iouMin=gym.iouMin;
            try{return PickStart(new Torch2Projection(level),out warmth);}finally{Object.DestroyImmediate(level);}
        }
        // Coldest of up to 40 orientations at least 70 degrees from the answer that light every wall.
        static Quaternion PickStart(Torch2Projection p,out float warmth)
        {
            var random=new System.Random(2);var best=Quaternion.AngleAxis(120,Vector3.up);warmth=2;int found=0;
            for(int n=0;n<600&&found<40;n++)
            {
                var q=RandomRotation(random);if(p.AngleToAnswer(q)<70)continue;
                p.Render(q,Torch2Projection.Home);p.Evaluate();if(p.Score.Ready||Array.Exists(p.scores,s=>!s.visible))continue;
                found++;if(p.Score.warmth<warmth){warmth=p.Score.warmth;best=q;}
            }
            if(found==0){p.Render(best,Torch2Projection.Home);p.Evaluate();warmth=p.Score.warmth;}
            return best;
        }
        static Quaternion RandomRotation(System.Random random)
        {
            while(true)
            {
                var q=new Vector4((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1);
                if(q.sqrMagnitude>1||q.sqrMagnitude<.01f)continue;q=q.normalized;return new Quaternion(q.x,q.y,q.z,q.w);
            }
        }
        static string WallName(int k)=>k==0?"left":"right";
        static string Flips(Torch2Flips flips)=>flips==(Torch2Flips.UpsideDown|Torch2Flips.Mirrored)?"upside down, mirrored, or both":flips==Torch2Flips.UpsideDown?"upside down":"mirrored";
        // Level ids owned by the Python pipeline.
        static HashSet<string> ScriptLevels()
        {
            var json=EverydayImporter.Torch2Source;if(!File.Exists(json))return new HashSet<string>();
            return new HashSet<string>(Regex.Matches(File.ReadAllText(json),"\"id\":\"([^\"]+)\"").Cast<Match>().Select(m=>m.Groups[1].Value));
        }

        // Checks, then writes Resources/ShadowArt/Torch2/<id>.asset (updated in place) and readable shadow copies of
        // meshes whose Read/Write is off into Art/Torch2Gym/<id>. Store assets themselves are never changed.
        public static Torch2Level Export(Torch2Gym gym,int samples=1000,bool interactive=true)
        {
            var result=Check(gym,samples);
            if(!result.Ok)
            {
                var problems="Fix these first:\n\n"+string.Join("\n",result.errors);
                if(interactive){EditorUtility.DisplayDialog("Export level",problems,"OK");return null;}
                throw new InvalidOperationException(problems);
            }
            var folder=ShadowFolder+"/"+gym.id;var copies=new Dictionary<Mesh,Mesh>();
            Mesh ShadowOf(Mesh mesh)
            {
                if(mesh.isReadable)return mesh;
                if(copies.TryGetValue(mesh,out var saved))return saved;
                Directory.CreateDirectory(folder);var path=$"{folder}/{Safe(mesh.name)}_{Key(mesh)}.asset";var copy=Torch2Gym.ReadableCopy(mesh);
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing)
                {
                    existing.Clear();existing.indexFormat=copy.indexFormat;existing.SetVertices(copy.vertices);existing.SetTriangles(copy.triangles,0);existing.RecalculateBounds();
                    Object.DestroyImmediate(copy);EditorUtility.SetDirty(existing);saved=existing;
                }
                else{AssetDatabase.CreateAsset(copy,path);saved=copy;}
                return copies[mesh]=saved;
            }
            var made=gym.MakeLevel(gym.Pieces,ShadowOf);var levelPath=$"{LevelFolder}/{gym.id}.asset";
            var level=AssetDatabase.LoadAssetAtPath<Torch2Level>(levelPath);
            if(level){EditorUtility.CopySerialized(made,level);Object.DestroyImmediate(made);}else{AssetDatabase.CreateAsset(made,levelPath);level=made;}
            level.name=gym.id;EditorUtility.SetDirty(level);AssetDatabase.SaveAssets();
            Debug.Log($"TORCH2_GYM_EXPORTED {levelPath}: {level.pieces.Length} pieces, {copies.Count} readable shadow copies");
            return level;
        }
        static string Key(Mesh mesh)=>AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long local)?guid.Substring(0,8)+"_"+local:mesh.GetInstanceID().ToString();
        static string Safe(string name)=>Regex.Replace(name,"[^A-Za-z0-9_-]","_");

        // Replaces the cluster with a level's pieces, turned so the answer is no rotation, and copies its text, lamps,
        // start and threshold. Works for the Python-made levels too (export them under a new id).
        public static void Load(Torch2Gym gym,Torch2Level level,bool ask=true)
        {
            var cluster=gym.cluster;
            if(ask&&cluster.childCount>0&&!EditorUtility.DisplayDialog("Open level in gym",$"Replace the {cluster.childCount} objects in the cluster with {level.name}?","Replace","Cancel"))return;
            for(int i=cluster.childCount-1;i>=0;i--)Undo.DestroyObjectImmediate(cluster.GetChild(i).gameObject);
            Undo.RecordObject(cluster,"Open level in gym");cluster.localRotation=Quaternion.identity;
            // With a table layout, its twins too. A thing of several pieces gets a parent on both sides ("Thing n"), placed
            // at its first piece, so the pieces pair through it and travel as one.
            bool table=Torch2Transit.HasTable(level);Transform layout=null;
            if(table)
            {
                layout=gym.tableLayout;
                if(!layout){layout=new GameObject("Table layout").transform;Undo.RegisterCreatedObjectUndo(layout.gameObject,"Open level in gym");layout.SetParent(gym.transform,false);}
                else for(int i=layout.childCount-1;i>=0;i--)Undo.DestroyObjectImmediate(layout.GetChild(i).gameObject);
            }
            var sizes=new Dictionary<int,int>();if(table)foreach(var piece in level.pieces)sizes[piece.tableGroup]=sizes.TryGetValue(piece.tableGroup,out int c)?c+1:1;
            var things=new Dictionary<int,(Transform inOrb,Transform onTable)>();
            foreach(var piece in level.pieces)
            {
                var orbPose=new Pose(level.solution*piece.position,level.solution*Quaternion.Euler(piece.euler));var tablePose=new Pose(piece.tablePosition,Quaternion.Euler(piece.tableEuler));
                Transform orbParent=cluster,tableParent=layout;
                if(table&&sizes[piece.tableGroup]>1)
                {
                    if(!things.TryGetValue(piece.tableGroup,out var thing))
                    {
                        thing=(Make("Thing "+piece.tableGroup,cluster,cluster,orbPose).transform,Make("Thing "+piece.tableGroup,layout,gym.transform,tablePose).transform);things[piece.tableGroup]=thing;
                    }
                    orbParent=thing.inOrb;tableParent=thing.onTable;
                }
                var go=Make(piece.label,orbParent,cluster,orbPose,piece);
                if(piece.visualOnly||(piece.shadowMesh&&piece.shadowMesh!=piece.mesh))
                {
                    var tag=go.AddComponent<ShadowPiece>();tag.role=piece.visualOnly?ShadowPiece.Role.VisualOnly:ShadowPiece.Role.Picture;tag.shadowProxy=piece.visualOnly?null:piece.shadowMesh;
                }
                if(table)Make(piece.label,tableParent,gym.transform,tablePose,piece);
            }
            // A level without a table layout scatters its things; an old layout stays in the scene, unhooked.
            Undo.RecordObject(gym,"Open level in gym");gym.tableLayout=table?layout:null;gym.transitFly=level.transitFly;gym.transitOverlap=level.transitOverlap;
            gym.id=ScriptLevels().Contains(level.name)?level.name+"Gym":level.name;
            gym.title=level.title;gym.button=level.button;gym.riddle=level.riddle;gym.hint=level.hint;gym.complete=level.complete;gym.accent=level.accent;
            gym.walls=level.lamps.Length>1?Torch2Gym.Walls.Both:level.lamps.Length==1&&level.lamps[0]==1?Torch2Gym.Walls.Right:Torch2Gym.Walls.Left;
            // The threshold belongs to the pile, so Auto stays on and Check sets it again for whatever the pile becomes (a
            // stale one let new piles solve at wrong angles). The start is a choice, kept; Check warns when it turns warm.
            gym.start=level.start*Quaternion.Inverse(level.solution);gym.autoStart=false;gym.iouMin=level.iouMin;gym.autoThreshold=true;gym.bothFaces=level.bothFaces;
            gym.leftSlide=level.leftSlide;gym.rightSlide=level.rightSlide;gym.layers=level.layers!=null?(AudioClip[])level.layers.Clone():new AudioClip[0];gym.cueVolume=level.cueVolume;
            gym.tiers=level.tiers;gym.lookalikeAngle=level.lookalikeAngle;gym.snapWarmth=level.snapWarmth;gym.snapAngle=level.snapAngle;
            gym.flips=level.flips;gym.cueAngle=level.cueAngle;gym.heldSnapAngle=level.heldSnapAngle;gym.drawGather=level.drawGather;gym.drawGatherFrom=level.drawGatherFrom;
            gym.report=$"Opened {level.name}. Auto Threshold is on: Check level sets it for this pile (the level had {level.iouMin:0.00}).";EditorUtility.SetDirty(gym);gym.Refresh(true);
        }

        // A new object under parent at a pose given in space's frame; with a piece, drawing it.
        static GameObject Make(string name,Transform parent,Transform space,Pose pose,Torch2Piece? piece=null)
        {
            var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Open level in gym");go.transform.SetParent(parent,false);
            go.transform.SetPositionAndRotation(space.TransformPoint(pose.position),space.rotation*pose.rotation);
            if(piece is Torch2Piece p){go.transform.localScale=p.Scale;go.AddComponent<MeshFilter>().sharedMesh=p.mesh;go.AddComponent<MeshRenderer>().sharedMaterials=p.materials;}
            return go;
        }
        // Exports, then plays the level alone in the torch-levels-2 scene; leaving Play comes back to the gym.
        public static void Play(Torch2Gym gym)
        {
            var level=Export(gym);if(!level||!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            // Opening the next scene unloads the asset object, so keep only its id.
            string id=level.name;SessionState.SetString(PlayKey,id);SessionState.SetString(ReturnKey,gym.gameObject.scene.path);
            if(!File.Exists(ShadowDemoBuilder.Torch2))ShadowDemoBuilder.BuildTorch2();
            EditorSceneManager.OpenScene(ShadowDemoBuilder.Torch2);ShadowDemo.PendingLevel=id;EditorApplication.isPlaying=true;
        }
    }

    // Survives the domain reloads around Play: hands the level to ShadowDemo, then returns to the gym afterwards.
    [InitializeOnLoad]
    static class Torch2GymSession
    {
        static Torch2GymSession()
        {
            Torch2Gym.RoomBuilt+=room=>{if(room)SceneVisibilityManager.instance.DisablePicking(room,true);};
            Torch2Gym.StartPicker=gym=>{var start=Torch2GymTools.PickStart(gym,out float warmth);Debug.Log($"TORCH2_GYM_START picked {Quaternion.Angle(start,Quaternion.identity):0} degrees from the answer, warmth {warmth*100:0}%");return start;};
            EditorApplication.playModeStateChanged+=Changed;
            // Edit mode only runs Update on scene changes: draw gym rooms as soon as a scene opens or scripts reload.
            AssemblyReloadEvents.afterAssemblyReload+=RefreshGyms;EditorSceneManager.sceneOpened+=(scene,mode)=>RefreshGyms();
            var id=SessionState.GetString(Torch2GymTools.PlayKey,"");
            if(id!=""&&EditorApplication.isPlayingOrWillChangePlaymode)ShadowDemo.PendingLevel=id;
        }
        static void RefreshGyms(){if(EditorApplication.isPlayingOrWillChangePlaymode)return;foreach(var gym in Object.FindObjectsByType<Torch2Gym>(FindObjectsSortMode.None))if(gym.isActiveAndEnabled)gym.Refresh();}
        static void Changed(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode)return;
            var back=SessionState.GetString(Torch2GymTools.ReturnKey,"");SessionState.EraseString(Torch2GymTools.PlayKey);SessionState.EraseString(Torch2GymTools.ReturnKey);
            if(back!=""&&File.Exists(back))EditorSceneManager.OpenScene(back);
        }
    }
}
