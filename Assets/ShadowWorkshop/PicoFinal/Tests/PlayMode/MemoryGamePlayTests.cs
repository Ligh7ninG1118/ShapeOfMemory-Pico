using System.Collections;
using System.Linq;
using CloudGarden.ShadowPuzzle;
using NUnit.Framework;
using Oculus.Interaction;
using PhysicalAssembly.MetaXR;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShapeOfMemory.Tests
{
    // Drives the deliverable scene the way hands do: pointer events on each button's, tile's, ribbon's and edge's ray
    // interactable (MetaMenuButton); a tapped edge turns its page by itself, as a quick pinch does. The cover only opens
    // when a hand carries it over (MemoryBook.CoverHand, driven directly). The book lies on its stand, rises onto the walls
    // for a memory (PLAY on its page; the stand's drawer runs out as it starts) and comes back down for a pause or once the
    // memory is found.
    public class MemoryGamePlayTests
    {
        const string Scene="Assets/ShadowWorkshop/PicoFinal/Scenes/PicoFinal.unity";
        static readonly string[] DebugWords={"WARMTH","RESET x2","MAIN MENU","PREV","NEXT LEVEL","THIS WALL","LOWER","RAISE"};
        int pointer=4000;

        IEnumerator Open(System.Action<MemoryGame> found,MemoryProgress.IStore store=null)
        {
            MemoryGame.StoreOverride=store??new MemoryProgress.InMemory();
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Scene,new LoadSceneParameters(LoadSceneMode.Single));
#endif
            MemoryGame game=null;float deadline=Time.realtimeSinceStartup+10;
            while((!game||!game.Ready)&&Time.realtimeSinceStartup<deadline){game=Object.FindFirstObjectByType<MemoryGame>();yield return null;}
            Assert.IsTrue(game&&game.Ready,"the game starts");game.Book.Calm=true;found(game);
            yield return null;
        }
        // Every test reads English words; the language test switches and comes back.
        [SetUp] public void English()=>MemoryText.Use("en");
        [TearDown] public void Clear(){MemoryGame.StoreOverride=null;MemoryText.Use("en");}
        // A hand's ray pinch: select, a frame, release. Actions run on the next Update.
        IEnumerator Tap(Pressable press,string what)
        {
            Assert.NotNull(press,what);Assert.IsTrue(press.Available,what+" is available");
            var ray=press.Native.Ray;var pose=new Pose(press.transform.position,Quaternion.identity);int id=pointer++;
            ray.PublishPointerEvent(new PointerEvent(id,PointerEventType.Hover,pose));ray.PublishPointerEvent(new PointerEvent(id,PointerEventType.Select,pose));
            yield return null;
            ray.PublishPointerEvent(new PointerEvent(id,PointerEventType.Unselect,pose));ray.PublishPointerEvent(new PointerEvent(id,PointerEventType.Unhover,pose));
            yield return null;yield return null;
        }
        IEnumerator Tap(MemoryButton button)=>Tap(button?button.Press:null,button?button.name:"button");
        // A hand's ray pinch held for seconds (a hold button or tile).
        IEnumerator Hold(Pressable press,float seconds)
        {
            Assert.NotNull(press);Assert.IsTrue(press.Available,press.Label+" is available");
            var ray=press.Native.Ray;var pose=new Pose(press.transform.position,Quaternion.identity);int id=pointer++;
            ray.PublishPointerEvent(new PointerEvent(id,PointerEventType.Hover,pose));ray.PublishPointerEvent(new PointerEvent(id,PointerEventType.Select,pose));
            yield return new WaitForSecondsRealtime(seconds);
            // What it did may have rebuilt the page under it.
            if(ray){ray.PublishPointerEvent(new PointerEvent(id,PointerEventType.Unselect,pose));ray.PublishPointerEvent(new PointerEvent(id,PointerEventType.Unhover,pose));}
            yield return null;yield return null;
        }
        // The open chapter's PLAY: the drawer runs out, the memory starts and the book rises.
        IEnumerator PressPlay(MemoryGame game)
        {
            yield return null;yield return Tap(game.Tile("chapter/play"),"the open chapter's PLAY");
            yield return WaitFor(game,MemoryGame.Phase.Rising,MemoryDrawer.RunOut+1);
        }
        IEnumerator Tap(MemoryTile tile,string what)=>Tap(tile?tile.Press:null,what);
        // A hand pinches a ribbon's hanging silk and lifts it round the rail, toward the reader and up, to degrees from
        // hanging over a few frames, then lets go (MemoryBookmarks.Hand, driven directly, as the cover's carry is).
        static IEnumerator Lift(MemoryBookmarks marks,string id,float degrees,int hand=1)
        {
            var m=marks.Find(id);Assert.NotNull(m,id+"'s ribbon");
            float grown=Time.realtimeSinceStartup+2;while(m.Grow<1&&Time.realtimeSinceStartup<grown)yield return null;Assert.AreEqual(1,m.Grow,id+"'s ribbon has grown out");
            var pivot=m.Hang.position;
            Vector3 At(float a){a*=Mathf.Deg2Rad;return pivot+marks.transform.TransformDirection(new Vector3(0,-Mathf.Cos(a),-Mathf.Sin(a)))*.05f;}
            marks.Hand(hand,At(0),true,true);Assert.AreSame(m,marks.Held(hand),"a pinch on its silk takes "+id+"'s ribbon");
            for(int i=1;i<=8&&marks.Held(hand)==m;i++){marks.Hand(hand,At(degrees*i/8),true,false);yield return null;}
            marks.Hand(hand,At(degrees),false,false);Assert.IsNull(marks.Held(hand),"let go");
            yield return null;
        }
        // A hand pinches a ribbon's hanging silk (a centimetre in front of its middle) and lets go the next frame. With
        // PinchPicks it lifts itself and turns the book (lifts); without, it just hangs again.
        static IEnumerator Pick(MemoryBookmarks marks,string id,int hand=1,bool lifts=true)
        {
            var m=marks.Find(id);Assert.NotNull(m,id+"'s ribbon");
            float grown=Time.realtimeSinceStartup+2;while(m.Grow<1&&Time.realtimeSinceStartup<grown)yield return null;Assert.AreEqual(1,m.Grow,id+"'s ribbon has grown out");
            var at=m.Hang.TransformPoint(new Vector3(0,-MemoryBookmarks.Drop*.5f,-.01f));bool lifted=false;
            void Heard(string line,string trace,LogType type){if(line.StartsWith($"SHAPE_OF_MEMORY_RIBBON {id} lifted"))lifted=true;}
            Application.logMessageReceived+=Heard;
            try
            {
                marks.Hand(hand,at,true,true);Assert.AreSame(m,marks.Held(hand),"a pinch on its silk takes "+id+"'s ribbon");
                yield return null;
                marks.Hand(hand,at,false,false);Assert.IsNull(marks.Held(hand),"let go");
                float deadline=Time.realtimeSinceStartup+MemoryBookmarks.ClickSeconds+.5f;while(m.Holder>=0&&Time.realtimeSinceStartup<deadline)yield return null;
            }
            finally{Application.logMessageReceived-=Heard;}
            Assert.Less(m.Holder,0,"done with it");Assert.AreEqual(lifts,lifted,lifts?"it lifted itself":"it never lifted");
        }
        // Until the book stops turning.
        static IEnumerator Settle(MemoryGame game,float seconds=6)
        {
            float deadline=Time.realtimeSinceStartup+seconds;yield return null;
            while(game.Book.Turning&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;Assert.IsFalse(game.Book.Turning,"the page lands");
        }
        static IEnumerator WaitFor(MemoryGame game,MemoryGame.Phase phase,float seconds=6)
        {
            float deadline=Time.realtimeSinceStartup+seconds;while(game.Current!=phase&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(phase,game.Current);
        }
        // The title cut short if it plays.
        static IEnumerator EndTitle(MemoryGame game)
        {
            Assert.AreEqual(MemoryGame.Phase.Cover,game.Current);Assert.IsTrue(game.Book.Closed,"the book starts closed");
            game.FinishTitle();float titled=Time.realtimeSinceStartup+2;while(game.Book.Sealed&&Time.realtimeSinceStartup<titled)yield return null;
            Assert.IsFalse(game.Book.Sealed,"the title ends");Assert.IsTrue(game.Book.OnStand,"the book on the stand");Assert.AreEqual(0,game.Sky.Dark,"the dark gone");
        }
        // Then a hand carries the closed cover open (pinch at its outer edge, round the spine past upright, let go), or the
        // desktop clicks it.
        IEnumerator OpenCover(MemoryGame game,bool click=false)
        {
            yield return EndTitle(game);
            if(click)game.Book.CoverPress.Control.Press();
            else yield return CarryCover(game.Book,0,140);
            float deadline=Time.realtimeSinceStartup+4;while(game.Book.Closed&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;
            Assert.IsFalse(game.Book.Closed,"the cover opens");Assert.IsTrue(game.Book.OnStand,"on the stand");
        }
        // Long enough for a cover let go to swing all the way, open or shut (at most .7 s).
        static IEnumerator CoverSwings(){yield return new WaitForSecondsRealtime(.9f);}
        // A fingertip at the closed cover's outer edge, the cover lifted degrees round the spine (0: lying shut).
        static Vector3 CoverEdge(MemoryBook book,float degrees)
        {
            float a=degrees*Mathf.Deg2Rad;
            return book.transform.TransformPoint(new Vector3(MemoryBook.Width*Mathf.Cos(a),MemoryBook.Width*Mathf.Sin(a),MemoryBook.Height*.5f));
        }
        // A hand pinches the cover's edge, carries it from one angle to another over a few frames and lets go.
        static IEnumerator CarryCover(MemoryBook book,float from,float to,int hand=1)
        {
            book.CoverHand(hand,CoverEdge(book,from),true,true);Assert.IsTrue(book.CoverHeld,"the pinch takes the cover");
            for(int i=1;i<=6;i++){book.CoverHand(hand,CoverEdge(book,Mathf.Lerp(from,to,i/6f)),true,false);yield return null;}
            Assert.AreEqual(to,book.Opening,1,"the cover follows the hand");
            book.CoverHand(hand,CoverEdge(book,to),false,false);Assert.IsFalse(book.CoverHeld);
        }
        // Straight into a memory: the book rises, the intro is skipped.
        static IEnumerator Play(MemoryGame game,int index)
        {
            game.Load(index,true);Assert.AreEqual(MemoryGame.Phase.Rising,game.Current,"the book rises first");
            yield return WaitFor(game,MemoryGame.Phase.Intro,MemoryGame.RiseSeconds+1);Assert.IsTrue(game.Book.OnWalls,"the book is the walls");
            yield return SkipIntro(game);Assert.AreEqual(MemoryGame.Phase.Playing,game.Current);
        }
        static IEnumerator SkipIntro(MemoryGame game){game.FinishIntro();yield return null;yield return null;}
        static void AssertNoDebugText(GameObject root)
        {
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))foreach(var word in DebugWords)
                Assert.IsFalse(label.text.Contains(word),$"debug text '{label.text}' on {label.name}");
        }
        // How deep any point of the book is inside the reading stand's board, lip, post or foot (metres; 0: clear of it).
        // The game's own geometry only: the environment artist's dressing (PicoFinalArtDressing: "Art: ..." objects, FBX
        // meshes that aren't readable) is fitted and flight-checked by its own script.
        static float StandOverlap(MemoryGame game)
        {
            var stand=game.Stage.Find("Reading stand");Assert.NotNull(stand,"the reading stand");
            if(game.BookRest)return BoardOverlap(game);
            float worst=0;
            foreach(var filter in game.Book.GetComponentsInChildren<MeshFilter>())
            {
                var shown=filter.GetComponent<Renderer>();if(!shown||!shown.enabled||!filter.sharedMesh||!filter.sharedMesh.isReadable)continue;
                var toWorld=filter.transform.localToWorldMatrix;
                foreach(var vertex in filter.sharedMesh.vertices)
                {
                    var point=toWorld.MultiplyPoint3x4(vertex);
                    foreach(Transform part in stand)
                    {
                        if(part.name.StartsWith("Art"))continue;
                        var p=part.InverseTransformPoint(point);var s=part.lossyScale;bool round=part.name.Contains("post")||part.name.Contains("foot");
                        float depth=round?Mathf.Min((.5f-new Vector2(p.x,p.z).magnitude)*s.x,(1-Mathf.Abs(p.y))*s.y):
                            Mathf.Min((.5f-Mathf.Abs(p.x))*s.x,Mathf.Min((.5f-Mathf.Abs(p.y))*s.y,(.5f-Mathf.Abs(p.z))*s.z));
                        worst=Mathf.Max(worst,depth);
                    }
                }
            }
            return worst;
        }
        // The scene's own stand (the environment artist's model; its book's mesh isn't readable at runtime): how far the
        // book's model, as a box (thickness ArtHalf each side of the hinge, from just behind the hinge out to its cover's
        // edge, its length along the spine), goes under the board, the Book Rest's plane, within the board (BoardWidth across,
        // from the lip up to the crest's foot BoardLength up the board). Measured offline against the stand's mesh
        // (2026-11-13): the title's flight at MemoryGame.TitleArc clears the crest and the lip; only the spine's bulge, which
        // a book opened flat hangs below its covers, sits a few millimetres into the board, under the book.
        // Our own book (2026-11-15 evening on): the corners of each of its shown parts' boxes (covers, page blocks, the
        // spine's cylinder as its box), the same way.
        const float BoardWidth=.71f,BoardLength=.34f;
        static float BoardOverlap(MemoryGame game)
        {
            var rest=game.BookRest;var model=game.Book.Model;float worst=0;
            void Check(Vector3 world){var p=rest.InverseTransformPoint(world);if(Mathf.Abs(p.x)<BoardWidth*.5f&&p.z>0&&p.z<BoardLength)worst=Mathf.Max(worst,-p.y);}
            if(model)
            {
                foreach(float x in new[]{-MemoryBook.ArtHalf,MemoryBook.ArtHalf})foreach(float y in new[]{0f,1.126f})foreach(float z in new[]{-.55f,.55f})Check(model.TransformPoint(new Vector3(x,y,z)));
                return worst;
            }
            foreach(var r in game.Book.GetComponentsInChildren<MeshRenderer>())
            {
                if(!r.enabled||!(r.name=="Cover"||r.name=="Page block"||r.name=="Spine"))continue;
                var b=r.GetComponent<MeshFilter>().sharedMesh.bounds;
                for(int i=0;i<8;i++)Check(r.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            }
            return worst;
        }
        static void Solve(Torch2Workshop level){Assert.IsTrue(level.BeginHold(7));level.Hold(7,level.Position,level.Projection.Solution);level.EndHold(7);}
        static int Plates(Transform face)=>face.GetComponentsInChildren<Transform>().Count(t=>t.name=="Plate");
        // The levels come from the Chapters asset: tests name slots, not levels.
        static string Chapter(int index)=>MemoryProgress.Levels[index];
        // Whether a memory's words are on the right page (the one its lamps leave dark), and both pages' words.
        static bool DarkIsRight(Torch2Level level)=>System.Array.IndexOf(level.lamps,1)<0;
        static string BothPages(MemoryGame game)=>game.PageText(false)+"\n"+game.PageText(true);

        // The language picked at start prints the book (Chinese here, upright and in its own font: MemoryText), and a change
        // of language mid-game prints the cover, the open pages and the buttons again.
        [UnityTest] public IEnumerator TheBookPrintsInItsLanguageAndAgainWhenTheLanguageChanges()
        {
            MemoryText.Use("zh-Hans");
            MemoryGame game=null;yield return Open(g=>game=g);
            string Cover()=>string.Join(" ",game.Book.CoverFace.GetComponentsInChildren<TMP_Text>().Select(t=>t.text));
            StringAssert.Contains("浮影集",Cover());
            yield return OpenCover(game,click:true);
            StringAssert.Contains("读者须知",game.PageText(false));StringAssert.Contains("翻开下一页",game.PageText(true));
            var labels=game.Book.RightFace.GetComponentsInChildren<TMP_Text>();
            var fonts=MemoryFonts.Load();
            Assert.IsTrue(labels.All(l=>l.font==fonts.cjkText||l.font==fonts.cjkBold),"in the Chinese faces");
            Assert.IsFalse(labels.Any(l=>(l.fontStyle&FontStyles.Italic)!=0),"upright: no fake italics in Chinese");
            Assert.AreEqual("提示",game.Button("play","hint").Caption);
            MemoryText.Use("en");yield return null;yield return null;yield return null;
            StringAssert.Contains("Turn the page to begin",game.PageText(true));StringAssert.Contains("READER",game.PageText(false));
            StringAssert.Contains("SHAPE",Cover());StringAssert.DoesNotContain("浮影集",Cover());
            Assert.IsTrue(game.Book.RightFace.GetComponentsInChildren<TMP_Text>().All(l=>l.font==fonts.text||l.font==fonts.caps||l.font==fonts.title),"back in the Latin faces");
            Assert.AreEqual("HINT",game.Button("play","hint").Caption);
        }

        [UnityTest] public IEnumerator TheTitleComesUpInTheDarkThenTheBookComesDownAndOpensOntoTheEndpaper()
        {
            MemoryGame game=null;yield return Open(g=>game=g);
            Assert.AreEqual(MemoryBook.Where.Presented,game.Book.At,"the title hangs in the air first");
            Assert.AreEqual(1,game.Sky.Dark,"in the dark");Assert.IsTrue(game.Book.Sealed,"its cover shut through the title");
            Assert.IsFalse(game.ToolbarUp,"no toolbar in the title's dark");
            yield return null;Assert.IsFalse(game.Book.CoverPress.Available,"pressing the cover does nothing yet");
            StringAssert.Contains("SHAPE",string.Join(" ",game.Book.CoverFace.GetComponentsInChildren<TMP_Text>().Select(t=>t.text)));
            float deadline=Time.realtimeSinceStartup+MemoryGame.TitleSeconds+1,deepest=0;int flying=0;
            while(game.Book.Sealed&&Time.realtimeSinceStartup<deadline){if(game.Book.Flying){flying++;deepest=Mathf.Max(deepest,StandOverlap(game));}yield return null;}
            Assert.Greater(flying,10,"the flight down was watched");
            Assert.Less(deepest,.0005f,"the book comes down onto the stand, not through it (metres inside)");
            Assert.IsTrue(game.Book.OnStand,"then comes down onto the stand");Assert.IsTrue(game.Book.Closed);Assert.AreEqual(0,game.Sky.Dark,"out of the dark");
            // Shut, the spine wraps the hinge and meets both boards' outsides (set beside it, the page blocks showed through).
            var spine=game.Book.transform.Find("Spine");Assert.NotNull(spine);
            Assert.Less(new Vector2(spine.localPosition.x,spine.localPosition.y).magnitude,.002f,"the spine on the hinge");Assert.AreEqual(MemoryBook.Thickness*2,spine.localScale.x,.003f,"as thick as the book");
            Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,"one player");
            yield return OpenCover(game,click:true);
            Assert.AreEqual(MemoryGame.Phase.Title,game.Current);Assert.AreEqual(MemoryGame.BeginSpread,game.Book.Spread);
            // Our own book (the user's pick, 2026-11-15 evening): its boards, page blocks, spine and tooled cover; the scene's
            // model hidden.
            Assert.IsFalse(game.ArtBook);Assert.IsNull(game.Book.Model,"our own book, not the scene's model");
            Assert.IsFalse(game.BookModel&&game.BookModel.gameObject.activeInHierarchy,"the scene's model hidden");
            var shown=game.Book.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).Select(r=>r.name).ToArray();
            foreach(var part in new[]{"Cover","Page block","Spine","Cover art"})CollectionAssert.Contains(shown,part);
            StringAssert.Contains("READER",game.PageText(false));StringAssert.Contains("Turn the page",game.PageText(true));
            Assert.NotNull(game.Tile("title/lower"));Assert.NotNull(game.Tile("title/raise"));
            Assert.IsTrue(game.Book.Corner(true).isActiveAndEnabled,"the endpaper turns on");Assert.IsTrue(game.Book.Corner(false).isActiveAndEnabled,"and back, to Settings");
            Assert.IsNull(game.ShownSet,"no play buttons off the walls");Assert.NotNull(game.Bookmarks.Find("quit"),"QUIT's ribbon");Assert.IsNull(game.Button("bar","quit"),"no QUIT plaque");
            float height=game.Stage.position.y;yield return Tap(game.Tile("title/raise"),"RAISE");
            Assert.AreEqual(height+MemoryGame.Lift,game.Stage.position.y,1e-4f);Assert.AreEqual(MemoryGame.Lift,game.Progress.Lift,1e-4f);
        }
        // The book chooses, PLAY starts: Begin has none; the next chapter's page has PLAY under its riddle and nothing starts
        // by itself; pressed, the stand's drawer runs out, the memory rises onto the walls with gold motes flying from the
        // drawer to the table, and the drawer slides shut.
        [UnityTest] public IEnumerator PlayStartsTheOpenChapterOnTheWalls()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return OpenCover(game);var drawer=game.Drawer;Assert.NotNull(drawer,"the scene's stand has its drawer");
            yield return null;Assert.IsNull(game.Tile("chapter/play"),"no PLAY on Begin");StringAssert.DoesNotContain("DRAWER",game.PageText(false),"Begin says nothing of the drawer");
            yield return Tap(game.Book.Corner(true),"the endpaper's edge");Assert.IsTrue(game.Book.Turning);yield return Settle(game);
            Assert.AreEqual(MemoryGame.Phase.Levels,game.Current);Assert.AreEqual(MemoryGame.SpreadOf(0),game.Book.Spread);
            Assert.IsFalse(game.Book.Corner(true).isActiveAndEnabled,"no turning past a memory not yet found");
            var play=game.Tile("chapter/play");Assert.NotNull(play,"PLAY under the riddle");Assert.AreEqual("PLAY",play.Label.text);
            yield return new WaitForSecondsRealtime(2);Assert.AreEqual(MemoryGame.Phase.Levels,game.Current,"nothing starts by itself");Assert.Less(drawer.Out,.002f,"the drawer stays shut");
            yield return Tap(play,"PLAY");Assert.IsTrue(drawer.Opening,"the drawer runs out");Assert.IsFalse(game.Bookmarks.Shown(),"the ribbons go");
            yield return WaitFor(game,MemoryGame.Phase.Rising,MemoryDrawer.RunOut+1);Assert.Greater(drawer.Out,.05f,"out");Assert.AreEqual(0,game.LevelIndex);
            float deadline=Time.realtimeSinceStartup+.5f;while(drawer.MotesFlying==0&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.Greater(drawer.MotesFlying,0,"motes fly from it to the table");
            yield return WaitFor(game,MemoryGame.Phase.Intro,MemoryGame.RiseSeconds+1);Assert.IsTrue(game.Book.OnWalls);
            Assert.IsFalse(drawer.Opening);Assert.Less(drawer.Out,.002f,"and it slides shut");
            var level=game.Level;Assert.AreEqual(Chapter(0),level.level.name);Assert.IsTrue(level.bare);Assert.IsTrue(level.input.FixedInPlace,"the orb stays where it is");
            Assert.IsFalse(level.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Lamp stand"||t.name=="Lamp body"),"the lamps are only their light");
            Assert.IsTrue(level.GetComponentsInChildren<Light>(true).Any(l=>l.type==LightType.Spot),"their light stays");
            yield return SkipIntro(game);Assert.AreEqual(MemoryGame.Phase.Playing,game.Current);Assert.AreEqual("play",game.ShownSet);
            AssertNoDebugText(level.gameObject);Assert.IsEmpty(level.world.controls,"no debug buttons in the room");
            // The first chapter lights one page; the other carries the goal, the riddle and the hints.
            bool dark=DarkIsRight(level.level);var darkFace=dark?game.Book.RightFace:game.Book.LeftFace;
            StringAssert.Contains("objects in the rings",game.PageText(dark));StringAssert.Contains(level.level.riddle,game.PageText(dark));Assert.AreEqual(2,game.Button("play","hint").PipsLit);
            yield return Tap(game.Button("play","hint"));Assert.AreEqual(1,game.HintStage);StringAssert.Contains(level.level.hint,game.PageText(dark));Assert.AreEqual(0,Plates(darkFace));
            yield return Tap(game.Button("play","hint"));Assert.AreEqual(2,game.HintStage);Assert.AreEqual(1,Plates(darkFace),"the picture is printed on the page");
            Assert.IsFalse(game.Button("play","hint").Press.Available,"two hints only");Assert.AreEqual(0,game.Button("play","hint").PipsLit);
        }
        [UnityTest] public IEnumerator ResetNeedsAFullHold()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);
            var level=game.Level;var turned=Quaternion.AngleAxis(60,Vector3.up)*level.Projection.Start;
            Assert.IsTrue(level.BeginHold(7));level.Hold(7,level.Position,turned);level.EndHold(7);
            var button=game.Button("play","reset");var ray=button.Press.Native.Ray;var pose=new Pose(button.transform.position,Quaternion.identity);
            ray.PublishPointerEvent(new PointerEvent(77,PointerEventType.Hover,pose));ray.PublishPointerEvent(new PointerEvent(77,PointerEventType.Select,pose));
            yield return new WaitForSecondsRealtime(.4f);Assert.Greater(button.Press.Held,.2f);
            ray.PublishPointerEvent(new PointerEvent(77,PointerEventType.Unselect,pose));yield return new WaitForSecondsRealtime(.8f);
            Assert.AreEqual(0,Quaternion.Angle(turned,level.Rotation),.01f,"a short press does nothing");
            ray.PublishPointerEvent(new PointerEvent(78,PointerEventType.Select,pose));yield return new WaitForSecondsRealtime(MemoryGame.HoldToReset+.3f);
            ray.PublishPointerEvent(new PointerEvent(78,PointerEventType.Unselect,pose));
            Assert.AreEqual(0,Quaternion.Angle(level.Projection.Start,level.Rotation),.01f,"held long enough: back to the start");
        }
        [UnityTest] public IEnumerator PauseBringsTheBookDownAndResumeTakesItBackUp()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;
            yield return Tap(game.Button("play","pause"));Assert.AreEqual(MemoryGame.Phase.Lowering,game.Current);Assert.IsNull(game.ShownSet);
            yield return WaitFor(game,MemoryGame.Phase.Paused,MemoryGame.PauseSeconds+1);
            Assert.IsTrue(game.Book.OnStand);Assert.IsFalse(level.input.enabled);Assert.AreEqual(0,level.world.LampLevel,.01f,"the lamps go out");
            Assert.Less(level.world.Pile.localScale.x,.05f,"the orb folds away");StringAssert.Contains("PAUSED",game.PageText(true));
            Assert.IsTrue(game.Book.Corner(false).isActiveAndEnabled,"turning back leaves");Assert.IsFalse(game.Book.Corner(true).isActiveAndEnabled);
            Assert.IsNull(game.Tile("pause/restart"),"no RESTART: HOLD TO RESET does it");Assert.IsNull(game.Tile("pause/quit"),"no QUIT: it's on the toolbar");
            Assert.IsTrue(game.Bookmarks.Shown(),"the ribbons while paused, QUIT's among them");
            yield return Tap(game.Tile("pause/resume"),"RESUME");yield return WaitFor(game,MemoryGame.Phase.Playing,MemoryGame.PauseSeconds+1);
            Assert.IsTrue(game.Book.OnWalls);Assert.IsTrue(level.input.enabled);Assert.AreEqual("play",game.ShownSet);
            yield return new WaitForSecondsRealtime(MemoryGame.FoldSeconds+.1f);Assert.AreEqual(MemoryGame.PileScale,level.world.Pile.localScale.x,.01f,"the orb opens out again");Assert.AreEqual(1,level.world.LampLevel,.01f);
            // Paused again, then the page turned back: the memory is left.
            yield return Tap(game.Button("play","pause"));yield return WaitFor(game,MemoryGame.Phase.Paused,MemoryGame.PauseSeconds+1);
            yield return Tap(game.Book.Corner(false),"the back edge");yield return Settle(game);
            yield return WaitFor(game,MemoryGame.Phase.Title,2);Assert.IsNull(game.Level);Assert.AreEqual(MemoryGame.BeginSpread,game.Book.Spread);
        }
        // The headset's menu over a memory (focus lost) pauses it, as PAUSE does.
        [UnityTest] public IEnumerator LosingFocusPausesTheMemory()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);
            game.PauseForSystem();Assert.AreEqual(MemoryGame.Phase.Lowering,game.Current,"the book comes down");
            yield return WaitFor(game,MemoryGame.Phase.Paused,MemoryGame.PauseSeconds+1);Assert.IsFalse(game.Level.input.enabled);
            game.PauseForSystem();Assert.AreEqual(MemoryGame.Phase.Paused,game.Current,"paused already: nothing more");
        }
        // QUIT (the user, 2026-11-15): off the walls, a ribbon turns to the back endpaper, which asks (KEEP READING turns back,
        // QUIT leaves); while a memory plays, HOLD TO QUIT on the play strip with the others (a short press does nothing);
        // paused, the ribbon leaves the memory for the back endpaper, and KEEP READING goes to its chapter.
        [UnityTest] public IEnumerator QuitIsARibbonOffTheWallsAndAHoldOnTheStrip()
        {
            MemoryGame game=null;yield return Open(g=>game=g);int quits=0;game.QuitHandler=()=>quits++;
            Assert.IsFalse(game.Bookmarks.Shown(),"nothing in the title's dark");
            yield return OpenCover(game);Assert.IsNull(game.Button("bar","quit"),"no QUIT plaque");Assert.IsNull(game.ShownSet);
            var ribbon=game.Bookmarks.Find("quit");Assert.NotNull(ribbon,"QUIT's ribbon");
            Assert.Greater(game.Stage.InverseTransformPoint(ribbon.Hang.position).x,game.Stage.InverseTransformPoint(game.Bookmarks.Find("credits").Hang.position).x,"the last on the right");
            yield return Lift(game.Bookmarks,"quit",80);yield return Settle(game);
            Assert.AreEqual(MemoryGame.Phase.Quit,game.Current);Assert.AreEqual(game.QuitSpread,game.Book.Spread);StringAssert.Contains("CLOSE THE BOOK",game.PageText(false));
            Assert.AreEqual(0,quits,"turning there leaves nothing");Assert.IsFalse(game.Book.Corner(true).isActiveAndEnabled,"the last page");
            yield return Tap(game.Tile("close/stay"),"KEEP READING");yield return Settle(game);
            Assert.AreEqual(MemoryGame.Phase.Title,game.Current,"back where the ribbon was touched");Assert.AreEqual(MemoryGame.BeginSpread,game.Book.Spread);
            yield return Lift(game.Bookmarks,"quit",80);yield return Settle(game);
            yield return Tap(game.Tile("close/quit"),"QUIT");Assert.AreEqual(1,quits,"QUIT on the page leaves");
            // In a memory: on the strip, the last of four.
            yield return Play(game,0);Assert.AreEqual("play",game.ShownSet);Assert.IsFalse(game.Bookmarks.Shown(),"no ribbons on the walls");
            var quit=game.Button("play","quit");Assert.NotNull(quit,"HOLD TO QUIT on the strip");Assert.IsTrue(quit.Press.Available);
            float X(string id)=>game.Stage.InverseTransformPoint(game.Button("play",id).transform.position).x;
            Assert.Less(X("hint"),X("reset"));Assert.Less(X("reset"),X("pause"));Assert.Less(X("pause"),X("quit"));Assert.AreEqual(0,X("reset")+X("pause"),.01f,"the row centred under the orb");
            Assert.Less(game.Stage.InverseTransformPoint(quit.transform.position).z,MemoryGame.StripAt.z+.05f,"under the orb, in front of it");
            yield return Hold(quit.Press,.4f);Assert.AreEqual(1,quits,"a short press does nothing");
            yield return Hold(quit.Press,MemoryGame.HoldToQuit+.3f);Assert.AreEqual(2,quits,"held: it quits");
            // Paused: QUIT's ribbon leaves the memory.
            yield return Tap(game.Button("play","pause"));yield return WaitFor(game,MemoryGame.Phase.Paused,MemoryGame.PauseSeconds+1);
            Assert.IsTrue(game.Bookmarks.Shown(),"the ribbons while paused");
            yield return Lift(game.Bookmarks,"quit",80);yield return Settle(game);
            yield return WaitFor(game,MemoryGame.Phase.Quit,2);Assert.IsNull(game.Level,"the memory is left");Assert.AreEqual(2,quits);
            yield return Tap(game.Tile("close/stay"),"KEEP READING");yield return Settle(game);
            Assert.AreEqual(MemoryGame.Phase.Levels,game.Current);Assert.AreEqual(MemoryGame.SpreadOf(0),game.Book.Spread,"back to its chapter");
        }
        // The soundtrack: silent through the title, on once the cover is open; it fades out and holds while a memory is up on
        // the walls (the closeness cue is the music there) and goes on from where it held when the book comes down.
        [UnityTest] public IEnumerator TheSoundtrackWaitsForTheOpenBookAndMakesWayForTheCue()
        {
            MemoryGame game=null;yield return Open(g=>game=g);var music=game.Music;Assert.NotNull(music.Soundtrack,"the shipping soundtrack");
            yield return new WaitForSecondsRealtime(.5f);Assert.IsFalse(music.Started,"no music in the title");Assert.IsFalse(music.Source.isPlaying);
            yield return OpenCover(game);yield return null;
            Assert.IsTrue(music.Started,"the open book starts it");Assert.AreEqual(0,music.Song);
            yield return new WaitForSecondsRealtime(1);Assert.IsTrue(music.Source.isPlaying);Assert.Greater(music.Fade,.2f,"fading in");
            yield return Play(game,0);yield return new WaitForSecondsRealtime(music.Soundtrack.fadeOut+.2f);
            Assert.IsTrue(music.Held,"held while the memory is out");Assert.IsFalse(music.Source.isPlaying);float heldAt=music.Source.time;
            // MENU: the music comes back as the book starts down (it follows the book), so read its time the frame it does.
            yield return Tap(game.Button("play","pause"));
            float deadline=Time.realtimeSinceStartup+1;while(music.Held&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(music.Held,"back with the book");Assert.IsTrue(game.Book.Flying,"while the book comes down, not after");
            Assert.AreEqual(heldAt,music.Source.time,.25f,"from where it held");
            yield return WaitFor(game,MemoryGame.Phase.Paused,MemoryGame.PauseSeconds+1);Assert.IsTrue(music.Source.isPlaying);
        }
        [UnityTest] public IEnumerator AFoundMemoryGoesBackToTheTableThenItsPageMovesOn()
        {
            var store=new MemoryProgress.InMemory();MemoryGame game=null;yield return Open(g=>game=g,store);yield return Play(game,0);var level=game.Level;
            Solve(level);yield return WaitFor(game,MemoryGame.Phase.Solved,5);
            Assert.IsNull(game.ShownSet);Assert.IsTrue(game.Book.OnWalls,"the found moment plays on the walls");Assert.IsTrue(game.Progress.Solved(0));
            Assert.IsTrue(game.Found,"the found moment plays over the orb");StringAssert.Contains("MEMORY FOUND",string.Join(" ",game.Found.GetComponentsInChildren<TMP_Text>().Select(t=>t.text)));
            yield return new WaitForSecondsRealtime(MemoryGame.FoundHold-.5f);Assert.AreEqual(MemoryGame.Phase.Solved,game.Current,"it holds");
            yield return WaitFor(game,MemoryGame.Phase.Lowering,8);
            // Back to their places on the table and gone (2026-11-16: the table is cleared after a memory).
            foreach(var piece in level.world.Pieces){Assert.Less(level.transform.InverseTransformPoint(piece.position).y,.12f,piece.name+" back on the table");Assert.AreEqual(0,piece.localScale.x,1e-4f,piece.name+" faded away");}
            yield return WaitFor(game,MemoryGame.Phase.Found,MemoryGame.LowerSeconds+1);
            Assert.IsTrue(game.Book.OnStand);StringAssert.Contains("FOUND",game.PageText(true));Assert.AreEqual(1,Plates(game.Book.LeftFace),"its picture");
            var again=game.Tile("found/replay");Assert.NotNull(again,"PLAY AGAIN on the found page");Assert.AreEqual("PLAY AGAIN",again.Label.text);
            yield return Tap(game.Book.Corner(true),"the found page's edge");yield return Settle(game);
            yield return WaitFor(game,MemoryGame.Phase.Levels,2);Assert.IsNull(game.Level);Assert.AreEqual(MemoryGame.SpreadOf(1),game.Book.Spread);
            yield return PressPlay(game);yield return WaitFor(game,MemoryGame.Phase.Intro,MemoryGame.RiseSeconds+1);
            Assert.AreEqual(Chapter(1),game.Level.level.name);Assert.AreEqual(0,game.HintStage);
        }
        // The cover does nothing by itself (the user, 2026-11-11): no press, fingertip or quick pinch opens it; a hand has to
        // carry it past upright. A look and a pinch carries it by the hand's sweep.
        [UnityTest] public IEnumerator OnlyAHandCarryingTheCoverOverOpensIt()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return EndTitle(game);var book=game.Book;
            yield return Tap(book.CoverPress,"the cover");yield return CoverSwings();
            Assert.IsTrue(book.Closed,"a ray or poke press does not open it");Assert.AreEqual(0,book.Opening,.01f);
            Assert.IsFalse(book.CoverPress.Touch(true,"R"),"a fingertip press is refused");book.CoverPress.Touch(false,"R");
            book.CoverHand(1,CoverEdge(book,0),true,true);Assert.IsTrue(book.CoverHeld);yield return null;
            book.CoverHand(1,CoverEdge(book,0),false,false);yield return CoverSwings();
            Assert.IsTrue(book.Closed,"a quick pinch leaves it shut");Assert.AreEqual(0,book.Opening,.01f);
            yield return CarryCover(book,0,60);yield return CoverSwings();
            Assert.IsTrue(book.Closed,"short of upright it falls shut");Assert.AreEqual(0,book.Opening,.01f);
            Assert.AreEqual(MemoryGame.Phase.Cover,game.Current);
            var tip=CoverEdge(book,0);Assert.IsTrue(book.GazeCoverGrab(0,tip),"a look and a pinch takes it");
            for(int i=1;i<=6;i++){book.CoverHand(0,tip-book.transform.right*(MemoryBook.IndirectReach*.8f*i/6),true,false);yield return null;}
            Assert.AreEqual(144,book.Opening,1,"the sweep lifts it");
            book.CoverHand(0,tip,false,false);yield return CoverSwings();
            Assert.IsFalse(book.Closed,"past upright it opens");Assert.AreEqual(MemoryGame.Phase.Title,game.Current);Assert.AreEqual(MemoryGame.BeginSpread,book.Spread);
        }
        [UnityTest] public IEnumerator ReturningPlayersOpenOntoTheEndpaperAndNothingStarts()
        {
            var store=new MemoryProgress.InMemory();var progress=new MemoryProgress(store);progress.Solve(0);progress.SawIntro(0);
            MemoryGame game=null;yield return Open(g=>game=g,store);
            Assert.AreEqual(MemoryBook.Where.Presented,game.Book.At,"the title plays for a returning player too");Assert.IsTrue(game.Book.Sealed);
            yield return OpenCover(game);
            Assert.AreEqual(MemoryGame.Phase.Title,game.Current);Assert.AreEqual(MemoryGame.BeginSpread,game.Book.Spread,"Begin, not the next memory");
            Assert.IsFalse(game.Book.Turning,"no riffle");StringAssert.Contains("1 of",game.PageText(true));
            yield return new WaitForSecondsRealtime(2);
            Assert.AreEqual(MemoryGame.Phase.Title,game.Current,"nothing starts by itself");Assert.IsNull(game.Level);Assert.IsTrue(game.Book.OnStand);
        }
        // Build and Run's launch argument: one of the book's chapters opens straight into it, locked or not.
        [UnityTest] public IEnumerator LaunchAtALevelOpensItsChapter()
        {
            PhysicalAssembly.LaunchArguments.Overrides["level"]=Chapter(2);
            try
            {
                MemoryGame game=null;yield return Open(g=>game=g);
                Assert.AreEqual(2,game.LevelIndex);Assert.AreEqual(Chapter(2),game.Level.level.name);Assert.AreEqual(MemoryGame.SpreadOf(2),game.Book.Spread);
                Assert.AreEqual(MemoryChapters.Count,game.Progress.Count,"the game's own chapters");Assert.IsFalse(game.OwnBook);
                yield return WaitFor(game,MemoryGame.Phase.Intro,MemoryGame.RiseSeconds+1);
                yield return SkipIntro(game);Assert.AreEqual(MemoryGame.Phase.Playing,game.Current);Assert.IsFalse(game.Guide.Teaching,"no ghost pinch outside the first chapter");
            }
            finally{PhysicalAssembly.LaunchArguments.Overrides.Clear();}
        }
        [UnityTest] public IEnumerator IntroBuildsTheOrbThenLightsTheLampsAndTheGuideTeachesOnce()
        {
            MemoryGame game=null;var store=new MemoryProgress.InMemory();yield return Open(g=>game=g,store);
            game.Load(0,true);yield return WaitFor(game,MemoryGame.Phase.Intro,MemoryGame.RiseSeconds+1);
            var intro=game.Intro;intro.Frozen=true;intro.Seek(0);var level=game.Level;var world=level.world;
            Assert.IsNull(game.ShownSet,"no buttons during the intro");
            Assert.IsFalse(intro.Skippable,"the first intro plays through");Assert.IsFalse(level.input.enabled);
            Assert.AreEqual(0,world.LampLevel);Assert.AreEqual(0,world.Cue.Level);Assert.IsTrue(world.OrbParts.All(p=>!p.gameObject.activeSelf),"no orb yet");
            foreach(var piece in world.Pieces){Assert.Less(level.transform.InverseTransformPoint(piece.position).y,.12f,piece.name+" lies on the table");Assert.AreEqual(Vector3.zero,piece.localScale,piece.name+" grows in");}
            intro.Seek(intro.Length*.5f);Assert.IsTrue(world.Pieces.Any(p=>level.transform.InverseTransformPoint(p.position).y>.15f),"halfway, some have risen");
            Assert.AreEqual(0,world.LampLevel,"lamps stay off until the orb is built");
            intro.Frozen=false;float deadline=Time.realtimeSinceStartup+intro.Length+2;
            while(game.Current==MemoryGame.Phase.Intro&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual(MemoryGame.Phase.Playing,game.Current);Assert.AreEqual("play",game.ShownSet);Assert.IsTrue(level.input.enabled);
            Assert.AreEqual(1,world.LampLevel);Assert.AreEqual(1,world.Cue.Level);Assert.IsTrue(world.OrbParts.All(p=>p.gameObject.activeSelf));
            for(int i=0;i<world.Pieces.Length;i++)
            {
                var piece=level.level.pieces[i];var t=world.Pieces[i];
                Assert.Less(Vector3.Distance(level.Drawn(piece),t.localPosition),1e-4f,piece.label+" in its place");Assert.AreEqual(piece.Scale,t.localScale,piece.label+" full size");Assert.Less(Quaternion.Angle(Quaternion.Euler(piece.euler),t.localRotation),.05f,piece.label+" turned right");
            }
            Assert.IsTrue(game.Progress.SeenIntro(0));Assert.AreEqual(0,Quaternion.Angle(level.Projection.Start,level.Rotation),.01f,"the orb starts at the start pose");
            // The first level teaches the roll until the orb has turned 30 degrees.
            Assert.IsTrue(game.Guide.Teaching);yield return null;
            Assert.IsTrue(level.GetComponentsInChildren<Transform>().Any(t=>t.name=="Ghost pinch"&&t.gameObject.activeInHierarchy),"ghost pinch shows");
            Assert.IsTrue(level.BeginHold(7));level.Hold(7,level.Position,Quaternion.AngleAxis(40,Vector3.up)*level.Rotation);level.EndHold(7);
            yield return null;yield return null;Assert.IsFalse(game.Guide.Teaching);Assert.IsTrue(game.Progress.Taught);
            Assert.IsFalse(level.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("SHARPENS")),"no closeness-cue lesson after it");
            Assert.IsFalse(BothPages(game).Contains("objects in the rings"),"the goal line goes once taught");
            // Seen once: the next time it can be skipped, and the guide stays away.
            game.Load(0,true);Assert.IsTrue(game.Intro.Skippable);yield return WaitFor(game,MemoryGame.Phase.Intro,MemoryGame.RiseSeconds+1);yield return SkipIntro(game);Assert.IsFalse(game.Guide.Teaching);
        }
        [UnityTest] public IEnumerator TheLastChapterEndsTheBook()
        {
            MemoryGame game=null;var store=new MemoryProgress.InMemory();yield return Open(g=>game=g,store);
            int last=MemoryChapters.Count-1;yield return Play(game,last);Assert.AreEqual(Chapter(last),game.Level.level.name);
            // Its name and riddle are on the dark page, or in the margins when both pages are lit.
            var words=game.Level.level;StringAssert.Contains(words.button,BothPages(game));StringAssert.Contains(words.riddle,BothPages(game));
            if(words.lamps.Length>1){StringAssert.Contains(words.button,game.PageText(false),"the left margin");StringAssert.Contains(words.riddle,game.PageText(true),"the right margin");}
            Solve(game.Level);yield return WaitFor(game,MemoryGame.Phase.Solved);
            yield return WaitFor(game,MemoryGame.Phase.Found,MemoryGame.FoundHold+8);
            Assert.AreEqual(words.lamps.Length,Plates(game.Book.LeftFace),"one picture plate per wall");
            yield return Tap(game.Book.Corner(true),"the found page's edge");yield return Settle(game);
            yield return WaitFor(game,MemoryGame.Phase.Ending,2);Assert.IsNull(game.Level);Assert.AreEqual(game.EndingSpread,game.Book.Spread);
            StringAssert.Contains("THANK YOU",game.PageText(false));int plates=0;for(int i=0;i<MemoryChapters.Count;i++)plates+=game.Chapter(i).lamps.Length;
            Assert.AreEqual(plates,Plates(game.Book.RightFace),"every memory's plates, one per wall");
            // On to the credits, printed from their asset; on from them, the back endpaper. Back to the ending without its
            // arpeggio again.
            yield return Tap(game.Book.Corner(true),"the ending's edge");yield return Settle(game);
            yield return WaitFor(game,MemoryGame.Phase.Credits,2);Assert.AreEqual(game.CreditsSpread,game.Book.Spread);
            var credits=MemoryCredits.Load();Assert.NotNull(credits,"Resources/"+MemoryCredits.ResourcePath);string left=game.PageText(false),right=game.PageText(true);
            StringAssert.Contains(credits.madeBy,left);foreach(var name in credits.Names)StringAssert.Contains(name,left);
            StringAssert.Contains(credits.thanksTo,right);foreach(var section in credits.Sections)StringAssert.Contains(section.heading.ToUpperInvariant(),right);
            Assert.IsTrue(game.Book.Corner(true).isActiveAndEnabled,"on to the back endpaper");
            int arpeggios=0;void Count(string line,string trace,LogType type){if(line.StartsWith("SHAPE_OF_MEMORY_ENDING"))arpeggios++;}
            Application.logMessageReceived+=Count;
            try{yield return Tap(game.Book.Corner(false),"the credits' back edge");yield return Settle(game);yield return WaitFor(game,MemoryGame.Phase.Ending,2);}
            finally{Application.logMessageReceived-=Count;}
            Assert.AreEqual(0,arpeggios,"back from the credits, the ending doesn't play again");
            yield return Tap(game.Book.Corner(false),"the back edge");yield return Settle(game);
            Assert.AreEqual(MemoryGame.Phase.Levels,game.Current);yield return null;Assert.AreEqual("PLAY AGAIN",game.Tile("chapter/play").Label.text,"a found memory can be played again");
        }
        // By hand: a pinch at the right page's edge lifts the page by that point and it follows the hand round the spine;
        // let go short of upright it falls back, carried over it turns. The back page's edge turns it back.
        // Birthday's shadow still passes about 20 degrees off its pose, so its lock only takes the orb within lockAngle of
        // the answer (the user, 2026-11-15: the snap angle was too large there and for the chef, GoodMorning).
        [UnityTest] public IEnumerator BirthdayLocksOnlyNearItsAnswer()
        {
            MemoryGame game=null;yield return Open(g=>game=g);
            int Index(string name)=>Enumerable.Range(0,MemoryChapters.Count).First(i=>game.Chapter(i).name.StartsWith(name));
            int birthday=Index("Birthday");Assert.AreEqual(10,game.Chapter(birthday).lockAngle,.01f);Assert.AreEqual(6,game.Chapter(Index("GoodMorning")).lockAngle,.01f);
            yield return Play(game,birthday);var level=game.Level;
            // A pose 16 degrees off whose shadow still passes.
            var p=new Torch2Projection(level.level);var off=Quaternion.identity;var random=new System.Random(3);bool found=false;
            for(int i=0;i<300&&!found;i++)
            {
                var axis=new Vector3((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1).normalized;
                off=Quaternion.AngleAxis(16,axis)*level.level.solution;p.Render(off,level.Position);p.Evaluate();
                found=p.Score.Ready&&p.AngleToAnswer(off)>12;
            }
            Assert.IsTrue(found,"a pose 16 degrees off that passes");
            Assert.IsTrue(level.BeginHold(7));level.Hold(7,level.Position,off);yield return new WaitForSecondsRealtime(.3f);
            Assert.IsTrue(level.Projection.Score.Ready,"it passes there");Assert.IsFalse(level.InReach,"but nothing locks so far off");
            level.Hold(7,level.Position,level.level.solution);yield return new WaitForSecondsRealtime(.3f);Assert.IsTrue(level.InReach,"at the answer it would");
            level.EndHold(7);
        }
        // A memory's things lie in front of both pages (the walls) and off the stand, which comes up under the book as it
        // comes down after a find while the things stay (2026-11-15: they came up through both).
        [UnityTest] public IEnumerator TheThingsOnTheTableKeepClearOfTheBookAndTheStand()
        {
            MemoryGame game=null;yield return Open(g=>game=g);
            Assert.AreEqual(1,Torch2Transit.KeepOut.Length,"the stand kept clear");var stand=Torch2Transit.KeepOut[0];
            for(int c=0;c<MemoryChapters.Count;c++)
            {
                var level=game.Chapter(c);var poses=Torch2Transit.TablePoses(level,out _);
                for(int i=0;i<poses.Length;i++)
                {
                    var piece=level.pieces[i];var b=Torch2Transit.Bounds(piece);var min=new Vector2(float.MaxValue,float.MaxValue);var max=-min;
                    for(int k=0;k<8;k++)
                    {
                        var corner=b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));
                        var at=poses[i].position+poses[i].rotation*Vector3.Scale(corner,piece.Scale);
                        for(int w=0;w<2;w++)Assert.GreaterOrEqual(Vector3.Dot(at-Torch2Projection.WallOrigin[w],Torch2Projection.WallNormal[w]),.015f,$"{level.name} {piece.label} in front of page {w}");
                        min=Vector2.Min(min,new Vector2(at.x,at.z));max=Vector2.Max(max,new Vector2(at.x,at.z));
                    }
                    Assert.IsFalse(stand.Overlaps(Rect.MinMaxRect(min.x,min.y,max.x,max.y)),$"{level.name} {piece.label} off the stand");
                }
            }
        }
        [UnityTest] public IEnumerator PagesFollowTheHandThatHoldsTheirEdge()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return OpenCover(game);var book=game.Book;int begin=MemoryGame.BeginSpread;Assert.AreEqual(begin,book.Spread);
            float w=MemoryBook.Width,h=MemoryBook.Height;
            // Book space: the spine's near foot at the origin, the right page along +x, up the page +z, the pages facing +y.
            Vector3 At(float angleDegrees,float radius,float up){float a=angleDegrees*Mathf.Deg2Rad;return book.transform.TransformPoint(new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,up));}
            Assert.AreEqual(-1,book.EdgeAt(At(0,w*.5f,h*.5f)),"the middle of the page is not its edge");
            Assert.AreEqual(1,book.EdgeAt(At(2,w,h*.3f)),"the right page's edge, anywhere along it");
            Assert.AreEqual(1,book.EdgeAt(At(2,w,h*.9f)));
            Assert.IsTrue(book.Grab(At(2,w*.98f,h*.5f)),"a pinch at the edge takes the page");
            Assert.IsTrue(book.Turning&&book.Dragging,"pinched at the edge, the page is held");
            for(int i=1;i<=20;i++){book.Drag(At(60f*i/20,w*.95f,h*.5f));yield return null;}
            yield return new WaitForSecondsRealtime(.4f);
            book.Drop();yield return Settle(game);Assert.AreEqual(begin,book.Spread,"let go short of upright, it falls back");
            Assert.IsTrue(book.Grab(At(1,w,h*.2f)));
            for(int i=1;i<=30;i++){book.Drag(At(140f*i/30,w*.95f,h*.2f));yield return null;}
            yield return new WaitForSecondsRealtime(.3f);book.Drop();yield return Settle(game);
            Assert.AreEqual(begin+1,book.Spread,"carried over, it turns");Assert.AreEqual(MemoryGame.Phase.Levels,game.Current);
            Assert.AreEqual(0,book.EdgeAt(At(178,w,h*.5f)));
            Assert.IsTrue(book.Grab(At(178,w,h*.5f)));book.Drop();yield return Settle(game);Assert.AreEqual(begin,book.Spread,"a quick pinch turns the page back");
            // Sitting back: the hand takes the edge and draws across the book without lifting over it; the page follows it
            // (upright with the hand over the spine) and, drawn past the spine, it turns.
            Assert.IsTrue(book.Grab(At(-3,w*.98f,h*.4f)),"a low pinch at the edge takes it");
            for(int i=1;i<=24;i++)
            {
                book.Drag(book.transform.TransformPoint(new Vector3(w*.98f*(1-1.3f*i/24),-.03f,h*.4f)));yield return null;
                // Halfway across, held there a moment (the page eases after the hand): well up, though the hand never rose.
                if(i==12){float until=Time.realtimeSinceStartup+.3f;while(Time.realtimeSinceStartup<until){book.Drag(book.transform.TransformPoint(new Vector3(w*.98f*(1-1.3f*.5f),-.03f,h*.4f)));yield return null;}Assert.Greater(book.Turn,.2f,"halfway across it is well up");}
            }
            yield return new WaitForSecondsRealtime(.2f);book.Drop();yield return Settle(game);
            Assert.AreEqual(begin+1,book.Spread,"drawn across, it turns");
        }
        // A fingertip (MemoryTouch, fed positions here) presses a tile only coming through its face from in front, once
        // per push; arriving from the side through it presses nothing.
        [UnityTest] public IEnumerator AFingertipPressesFromInFrontOnly()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return OpenCover(game);
            var press=game.Tile("title/raise").Press;var face=press.Face;var outward=-face.forward;var touch=MemoryTouch.Add(new GameObject("Touch test"),null);
            try
            {
                float height=game.Stage.position.y;
                touch.Step(1,true,face.position+face.right*.3f);touch.Step(1,true,face.position-outward*.003f);yield return null;yield return null;
                Assert.AreEqual(height,game.Stage.position.y,1e-5f,"arriving from the side, through the face: nothing");
                touch.Step(1,true,face.position+outward*.05f);Assert.IsTrue(press.Hover,"a fingertip in front hovers it");
                touch.Step(1,true,face.position+outward*.02f);touch.Step(1,true,face.position+outward*.002f);yield return null;yield return null;
                Assert.AreEqual(height+MemoryGame.Lift,game.Stage.position.y,1e-4f,"pushed through from in front: one press");
                touch.Step(1,true,face.position-outward*.004f);yield return null;yield return null;
                Assert.AreEqual(height+MemoryGame.Lift,game.Stage.position.y,1e-4f,"held in: no second press");
                touch.Step(1,true,face.position+outward*.04f);touch.Step(1,true,face.position+outward*.02f);touch.Step(1,true,face.position+outward*.002f);yield return null;yield return null;
                Assert.AreEqual(height+2*MemoryGame.Lift,game.Stage.position.y,1e-4f,"backed out and in again: a second press");
                touch.Step(1,false,default);Assert.IsFalse(press.Hover,"a lost hand hovers nothing");
            }
            finally{Object.Destroy(touch.gameObject);}
        }
        // A fingertip sweeping across a tile, dipping through its face on the way, presses nothing; one coming in at 45
        // degrees does, and so does a straight push after a sweep (MemoryTouch.MaxApproach).
        [UnityTest] public IEnumerator AFingertipSweepingAcrossPressesNothing()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return OpenCover(game);
            var press=game.Tile("title/raise").Press;var face=press.Face;var outward=-face.forward;var touch=MemoryTouch.Add(new GameObject("Touch test"),null);
            Vector3 At(float across,float above)=>face.position+face.right*across+outward*above;
            try
            {
                float height=game.Stage.position.y;
                // Sideways along the face, 12 mm over it down to 4 mm: about 73 degrees off its normal at the crossing.
                foreach(var (x,h) in new[]{(-.04f,.012f),(-.03f,.011f),(-.02f,.010f),(-.01f,.008f),(0f,.004f)})touch.Step(1,true,At(x,h));
                yield return null;yield return null;
                Assert.IsTrue(press.Hover,"the sweep was over the tile");
                Assert.AreEqual(height,game.Stage.position.y,1e-5f,"a sweep across it: nothing");
                // Backed out and pushed straight in: a press.
                touch.Step(1,true,At(0,.03f));touch.Step(1,true,At(0,.015f));touch.Step(1,true,At(0,.002f));yield return null;yield return null;
                Assert.AreEqual(height+MemoryGame.Lift,game.Stage.position.y,1e-4f,"then a straight push presses");
                // Out again, then in at 45 degrees: still a press.
                touch.Step(1,true,At(-.022f,.026f));
                foreach(var (x,h) in new[]{(-.0165f,.0205f),(-.011f,.015f),(-.0055f,.0095f),(0f,.004f)})touch.Step(1,true,At(x,h));
                yield return null;yield return null;
                Assert.AreEqual(height+2*MemoryGame.Lift,game.Stage.position.y,1e-4f,"a press 45 degrees off square still takes");
            }
            finally{Object.Destroy(touch.gameObject);}
        }
        [UnityTest] public IEnumerator TheOrbRollsButNeverMoves()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;var input=level.input;
            var centre=level.transform.TransformPoint(level.Position);
            Assert.AreEqual(Torch2Input.Grip.None,input.Zone(centre),"the hub takes nothing");
            Assert.AreEqual(Torch2Input.Grip.Roll,input.Zone(centre+level.transform.TransformVector(Vector3.back*input.Radius)),"the rings roll it");
            Assert.AreEqual(Torch2Input.Grip.None,input.Zone(centre+level.transform.TransformVector(Vector3.back*(input.Reach+.02f))),"past the smaller rings' reach, nothing");
            Assert.AreEqual(Torch2Projection.Home,level.Position);
        }
        // Playtest 2026-11-08: no wires, no hub, a smaller orb casting the same shadow, and the light shown as a flashlight.
        [UnityTest] public IEnumerator TheObjectsFloatFreeInASmallerOrbLitByAFlashlight()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;var world=level.world;
            Assert.IsTrue(world.Wires.All(w=>w==null),"no wires");Assert.IsFalse(world.Hub.enabled,"no hub");
            Assert.AreEqual(MemoryGame.PileScale,world.Pile.localScale.x,1e-4f);Assert.AreEqual(MemoryGame.PileScale,level.input.OrbScale);
            var lights=level.GetComponentsInChildren<MemoryFlashlight>();Assert.AreEqual(level.level.lamps.Length,lights.Length,"a flashlight per lit lamp");
            foreach(var light in lights)
            {
                var spot=light.transform.parent.GetComponentInChildren<Light>();Assert.NotNull(spot,"it holds the lamp's light");
                Assert.Less(Vector3.Distance(light.LensRenderer.transform.position,spot.transform.position),.01f,"its lens is at the light");
                Assert.Less(Vector3.Angle(light.transform.forward,level.transform.TransformPoint(Torch2Projection.Home)-light.transform.position),1,"aimed at the objects");
                if(light.Beam)Assert.IsTrue(light.Beam.enabled,"the beam shows while the lamp is on");
            }
            // The shadow is the pile's own: the projection never sees the drawing's scale.
            level.Projection.Render(level.Projection.Solution,Torch2Projection.Home);level.Projection.Evaluate();Assert.AreEqual(1,level.Projection.Score.iou,1e-6);
            yield return Tap(game.Button("play","pause"));yield return WaitFor(game,MemoryGame.Phase.Paused,MemoryGame.PauseSeconds+1);
            foreach(var light in lights)if(light.Beam)Assert.IsFalse(light.Beam.enabled,"lamps off: no beam");
        }
        // Playtest 2026-11-08: Home's pile looked scattered. A level with drawGather draws its far pieces nearer the hub, after
        // the intro too, while the shadow keeps their real places.
        [UnityTest] public IEnumerator AGatheredPileIsDrawnTighterWithTheSameShadow()
        {
            MemoryGame game=null;yield return Open(g=>game=g);int index=-1;
            for(int i=0;i<MemoryChapters.Count;i++)if(game.Chapter(i).drawGather<1){index=i;break;}
            if(index<0)Assert.Inconclusive("no chapter draws its pile gathered");
            yield return Play(game,index);var level=game.Level;var data=level.level;float nearer=0;
            for(int i=0;i<data.pieces.Length;i++)
            {
                var piece=data.pieces[i];var t=level.world.Pieces[i];
                Assert.Less(Vector3.Distance(data.Drawn(piece),t.localPosition),1e-4f,piece.label+" drawn in its gathered place");
                nearer=Mathf.Max(nearer,piece.position.magnitude-t.localPosition.magnitude);
            }
            // 2026-11-15: pieces that the level's gather drew into each other are gathered less (Torch2Piece.drawGather), so
            // the farthest may stay out; some far piece still comes in.
            Assert.Greater(nearer,.05f,"a far piece is drawn nearer");
            level.Projection.Render(level.Projection.Solution,Torch2Projection.Home);level.Projection.Evaluate();Assert.AreEqual(1,level.Projection.Score.iou,1e-6,"the shadow is the real pile's");
        }
        // 2026-11-15 (the user: "nerf the snap"): nothing snaps the orb out of a moving hand, and nothing turns it on a
        // release. Left alone within reach of the answer for LockSeconds, the light on the pages comes up, the orb glides
        // slowly into the answer, and only as it lands is the memory found. A touch while it waits starts the wait again;
        // once it has locked, nothing takes it back. 2026-11-16 (the user): a hand holding it still counts as leaving it
        // alone; the lock then takes it out of the hand. A hand still turning it never locks.
        [UnityTest] public IEnumerator LeftAloneAtTheAnswerTheLightComesUpThenTheOrbGlidesIn()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;var answer=level.Projection.Solution;
            Assert.IsTrue(level.HoldToSolve);var near=Quaternion.AngleAxis(4,Vector3.up)*answer;
            // Held and turning to and fro about the answer (3.5 degrees either way, .6 s a swing: more than StillAngle
            // between the 10 Hz checks), never leaving its reach: no lock.
            Assert.IsTrue(level.BeginHold(7));
            float from=Time.realtimeSinceStartup,until=from+level.LockSeconds+.5f;
            while(Time.realtimeSinceStartup<until){level.Hold(7,level.Position,Quaternion.AngleAxis(3.5f*Mathf.Sin((Time.realtimeSinceStartup-from)*Mathf.PI*2/.6f),Vector3.up)*near);yield return null;}
            Assert.IsTrue(level.InReach,"precondition: about 4 degrees off is in reach");
            Assert.IsTrue(level.Busy,"held and turning, it stays in the hand");Assert.IsFalse(level.Locking);Assert.IsFalse(level.Completed);
            level.Hold(7,level.Position,near);
            level.EndHold(7);Assert.IsFalse(level.Snapping,"no snap on letting go");
            yield return new WaitForSecondsRealtime(level.LockSeconds*.6f);
            Assert.IsFalse(level.Locking,"not yet");Assert.AreEqual(4,Quaternion.Angle(level.Rotation,answer),.2f,"where the hand left it");
            Assert.IsTrue(level.BeginHold(7));level.EndHold(7);
            yield return new WaitForSecondsRealtime(level.LockSeconds*.6f);Assert.IsFalse(level.Locking,"a touch starts the wait again");
            yield return WaitForLock(level);
            Assert.IsFalse(level.BeginHold(8),"nothing takes a lock back");
            yield return new WaitForSecondsRealtime(level.BrightenSeconds+.05f);
            Assert.AreEqual(1,level.world.Brighten,1e-3f,"the light is up");Assert.IsFalse(level.Completed,"before the orb lands");
            Assert.AreEqual(1+Torch2Room.BrightGain,level.world.Room.wallQuads[level.level.lamps[0]].sharedMaterial.GetFloat("_Bright"),1e-3f,"on the lit page");
            Assert.Greater(Quaternion.Angle(level.Rotation,answer),3,"the glide sets off slowly");
            yield return WaitFor(game,MemoryGame.Phase.Solved,level.LockRemaining+1);
            Assert.IsTrue(level.Completed);Assert.IsFalse(level.Righting);Assert.Less(Quaternion.Angle(level.Rotation,answer),.01f,"exactly the answer as it is found");
        }
        // 2026-11-16 (the user): held still in reach (within StillAngle, hand tremor), the memory locks out of the hand: the
        // hand loses the orb and can't take it back, and it glides in as it does untouched.
        [UnityTest] public IEnumerator HeldStillAtTheAnswerItLocksOutOfTheHand()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;var answer=level.Projection.Solution;
            var near=Quaternion.AngleAxis(4,Vector3.up)*answer;
            Assert.IsTrue(level.BeginHold(7));
            float until=Time.realtimeSinceStartup+level.LockSeconds+.6f;int step=0;
            // A trembling hand: half a degree either way, every frame.
            while(!level.Locking&&Time.realtimeSinceStartup<until){level.Hold(7,level.Position,Quaternion.AngleAxis((step++%2==0?.5f:-.5f),Vector3.right)*near);yield return null;}
            Assert.IsTrue(level.Locking,"held still in reach, it locks");Assert.IsFalse(level.Busy,"out of the hand");Assert.IsFalse(level.BeginHold(7),"the hand can't take it back");
            var before=level.Rotation;level.Hold(7,level.Position,near);Assert.AreEqual(0,Quaternion.Angle(before,level.Rotation),1e-3f,"the old hold moves nothing");
            yield return WaitFor(game,MemoryGame.Phase.Solved,level.LockRemaining+1);
            Assert.Less(Quaternion.Angle(level.Rotation,answer),.01f,"exactly the answer as it is found");
        }
        // The play strip wakes only while the head looks down at it (headsets; the desktop's never sleeps).
        [Test] public void TheStripWakesOnlyLookedDownAt()
        {
            Vector3 Look(float down,float aside=0)=>Quaternion.Euler(down,aside,0)*Vector3.forward;
            Assert.IsFalse(MemoryGame.LooksAtStrip(Look(5),false),"at the walls");
            Assert.IsFalse(MemoryGame.LooksAtStrip(Look(27),false),"at the orb");
            Assert.IsFalse(MemoryGame.LooksAtStrip(Look(32.7f),false),"at the orb's lowest ring");
            Assert.IsTrue(MemoryGame.LooksAtStrip(Look(45),false),"down at the strip");
            Assert.IsTrue(MemoryGame.LooksAtStrip(Look(34),true),"awake, it stays awake a little above where it woke");
            Assert.IsFalse(MemoryGame.LooksAtStrip(Look(31),true),"and sleeps again looking up at the orb");
            Assert.IsFalse(MemoryGame.LooksAtStrip(Look(50,60),false),"down, but off to the side");
        }
        // MENU while a lock comes in undoes it, so the found moment never plays with the book down (and the paused orb never
        // locks); back on the walls and left alone in reach, it locks again.
        [UnityTest] public IEnumerator MenuUndoesALockThatComesAgainAfterResume()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;
            level.SetPose(level.Position,level.Projection.Solution);yield return WaitForLock(level);
            yield return Tap(game.Button("play","pause"));
            Assert.IsFalse(level.Locking,"undone");Assert.AreEqual(0,level.world.Brighten,"the light back down");
            yield return WaitFor(game,MemoryGame.Phase.Paused,MemoryGame.PauseSeconds+1);
            yield return new WaitForSecondsRealtime(level.LockSeconds+.3f);Assert.IsFalse(level.Locking||level.Completed,"never while paused");
            yield return Tap(game.Tile("pause/resume"),"RESUME");yield return WaitFor(game,MemoryGame.Phase.Playing,MemoryGame.PauseSeconds+1);
            yield return WaitForLock(level);yield return WaitFor(game,MemoryGame.Phase.Solved,level.LockRemaining+1);
        }
        static IEnumerator WaitForLock(Torch2Workshop level)
        {
            float deadline=Time.realtimeSinceStartup+level.LockSeconds+.6f;while(!level.Locking&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(level.Locking,"left alone in reach, it locks");
        }
        // Playtest 2026-11-08: the picture upside down counts, and the orb then turns it the right way up. 2026-11-15 (the
        // user's defaults): on a one-lamp chapter it counts turned any way on the wall. Since the lock, the glide in rights it,
        // so the memory is found the right way up.
        [UnityTest] public IEnumerator ATurnedPictureCountsAndRightsItself()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;var p=level.Projection;
            Assert.IsTrue(p.AnyTurn,"chapter I counts its picture turned any way");
            var upsideDown=Quaternion.AngleAxis(180,p.TurnAxis)*p.Solution;var turned=Quaternion.AngleAxis(-110,p.TurnAxis)*p.Solution;
            var near=Quaternion.AngleAxis(4,Vector3.right)*turned;
            p.Render(upsideDown,Torch2Projection.Home);p.Evaluate();Assert.IsTrue(p.Score.Ready,"upside down");
            p.Render(turned,Torch2Projection.Home);p.Evaluate();Assert.IsTrue(p.Score.Ready,"turned");
            // Turned half round the picture's upright, the shadow is only near a mirror image (perspective): it never counts.
            var mirrored=Quaternion.AngleAxis(180,Vector3.ProjectOnPlane(Vector3.up,Torch2Projection.Sight(level.level.lamps[0])).normalized)*p.Solution;
            p.Render(mirrored,Torch2Projection.Home);p.Evaluate();Assert.IsFalse(p.Score.Ready,"the mirrored pose is not an answer");Assert.Greater(p.AngleToAnswer(mirrored),90);
            level.SetPose(level.Position,Quaternion.AngleAxis(30,Vector3.right)*turned);
            Assert.IsTrue(level.BeginHold(7));level.Hold(7,level.Position,near);level.EndHold(7);Assert.IsFalse(level.Busy);
            yield return WaitFor(game,MemoryGame.Phase.Solved,level.LockSeconds+level.BrightenSeconds+level.GlideSeconds.y+1.5f);
            Assert.IsFalse(level.Righting,"righted during the glide");Assert.Less(Quaternion.Angle(level.Rotation,p.Solution),.01f,"the right way up as it is found");
        }
        // Playtest 2026-11-08: the music swelled at lookalikes far from the answer. Past cueAngle it stays cold, while a
        // warm wall still sharpens its slide.
        [UnityTest] public IEnumerator TheMusicStaysColdFarFromEveryAnswer()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;var p=level.Projection;var cue=level.world.Cue;
            // The warmest of many poses past cueAngle from every answer.
            var random=new System.Random(4);var far=Quaternion.identity;float warmest=-1;
            for(int n=0;n<400;n++)
            {
                var q=Quaternion.Euler((float)random.NextDouble()*360,(float)random.NextDouble()*360,(float)random.NextDouble()*360);
                if(p.AngleToAnswer(q)<level.level.cueAngle+10)continue;p.Render(q,Torch2Projection.Home);p.Evaluate();if(!p.Score.Ready&&p.Score.warmth>warmest){warmest=p.Score.warmth;far=q;}
            }
            Assert.GreaterOrEqual(warmest,level.level.tiers.x,"precondition: a far pose that scores warm");
            Assert.IsTrue(level.BeginHold(7));level.Hold(7,level.Position,far);yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(0,cue.Overall.Tier,$"far ({p.AngleToAnswer(far):0} degrees) and warm ({warmest:0.00}): the music stays cold");
            Assert.GreaterOrEqual(cue.Walls[0].Tier,1,"the slide still stirs");
            level.Hold(7,level.Position,Quaternion.AngleAxis(4,Vector3.up)*p.Solution);yield return new WaitForSecondsRealtime(1.2f);
            Assert.GreaterOrEqual(cue.Overall.Tier,3,"near the answer it warms");level.EndHold(7);
        }
        // A tracked hand at tip (world), its palm below it, pinched or open.
        static void Sample(Torch2Input input,int h,Vector3 tip,bool closed)=>input.SampleHand(h,true,new Pose(tip+Vector3.down*.08f,Quaternion.identity),tip,closed?.005f:.08f,.025f);
        static void Arm(Torch2Input input,int h,Vector3 tip){for(int i=0;i<5;i++)Sample(input,h,tip,false);}
        // Playtest 2026-11-11: a second hand on the rings used to be ignored. Now it joins: both turn the orb (as a wheel
        // when they hold opposite sides) and either lets go while the other carries on. The pinched ring shows Grab while
        // the others rest, the things keep their colours, and a dot on the shell shows each grip.
        [UnityTest] public IEnumerator TwoHandsTurnTheOrbTogether()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;var input=level.input;
            var grip=level.GetComponent<MemoryGrip>();Assert.NotNull(grip,"the grip dots");
            var centre=level.Position;float r=input.Radius;var piece=level.world.Pieces[0].GetComponent<Renderer>();var block=new MaterialPropertyBlock();
            Vector3 W(Vector3 local)=>level.transform.TransformPoint(local);
            var left=centre+Vector3.left*r;var right=centre+Vector3.right*r;var lift=Vector3.up*.03f;
            yield return new WaitForSecondsRealtime(.15f);piece.GetPropertyBlock(block,0);Assert.IsTrue(block.HasColor("_BaseColor"),"the things are tinted per material");var pieceColor=block.GetColor("_BaseColor");
            Arm(input,0,W(left));Arm(input,1,W(right));
            Sample(input,0,W(left),true);Sample(input,1,W(right),false);input.Turn();
            Assert.AreEqual(Torch2Input.Grip.Roll,input.Grabbed);Assert.AreEqual(0,level.Owner,"the left hand takes it");
            Assert.IsTrue(input.Hovering(1,out _),"the right could join");
            Sample(input,0,W(left),true);Sample(input,1,W(right),true);input.Turn();
            Assert.IsTrue(input.Holding(1,out _,out _),"the right hand joins");Assert.AreEqual(0,level.Owner);
            var before=level.Rotation;
            Sample(input,0,W(left-lift),true);Sample(input,1,W(right+lift),true);input.Turn();
            (level.Rotation*Quaternion.Inverse(before)).ToAngleAxis(out float angle,out var axis);
            Assert.AreEqual(Mathf.Atan2(.03f,r)*Mathf.Rad2Deg,angle,.2f,"left down, right up: turned as far as the hands went round");
            Assert.Greater(Mathf.Abs(Vector3.Dot(axis,Vector3.forward)),.999f,"like a wheel, about the line through its middle");
            yield return new WaitForSecondsRealtime(.15f);
            Assert.IsTrue(input.Holding(0,out _,out _)&&input.Holding(1,out _,out _),"still held by both after frames");
            for(int h=0;h<2;h++)
            {
                Assert.AreEqual(2,grip.State(h),"a full dot");Assert.IsTrue(grip.DotShown(h));Assert.IsFalse(grip.TetherShown(h),"pinched at the shell: no tether");
                Assert.AreEqual(r,Vector3.Distance(level.transform.InverseTransformPoint(grip.DotPosition(h)),centre),1e-4f,"the dot is on the shell");
            }
            int lit=level.world.HeldRings(),count=0;for(int i=0;i<3;i++)if(((lit>>i)&1)!=0)count++;
            Assert.That(count,Is.InRange(1,2),"the rings nearest the two grips");
            for(int i=0;i<3;i++)
            {
                level.world.OrbParts[i].GetComponent<Renderer>().GetPropertyBlock(block);
                Assert.AreEqual(((lit>>i)&1)!=0?MemoryPalette.StrokeHeld:MemoryPalette.StrokeRest,block.GetColor("_BaseColor").a,1e-3f,$"ring {i}");
            }
            piece.GetPropertyBlock(block,0);Assert.IsTrue(pieceColor==block.GetColor("_BaseColor"),"held, the things keep their colour");
            // The left lets go: the right keeps it and turns it alone.
            Sample(input,0,W(left-lift),false);Sample(input,1,W(right+lift),true);input.Turn();
            Assert.AreEqual(1,level.Owner,"passed to the right");Assert.IsTrue(level.Busy);
            before=level.Rotation;Sample(input,0,W(left),false);Sample(input,1,W(right+lift*2),true);input.Turn();
            Assert.Greater(Quaternion.Angle(before,level.Rotation),1,"the right turns it alone");
            Sample(input,1,W(right+lift*2),false);input.Turn();Assert.IsFalse(level.Busy,"both let go");
            Sample(input,0,W(left),false);Sample(input,1,W(right+lift*2),false);input.Turn();yield return null;Assert.AreEqual(1,grip.State(0),"an open hand at the rings: a faint dot where it would take hold");Assert.AreEqual(1,grip.State(1));
            // Pinched well inside the shell: a tether from the fingers to the dot.
            var inside=centre+Vector3.back*(r*.5f);Arm(input,1,W(inside));Sample(input,1,W(inside),true);input.Turn();Assert.IsTrue(input.Holding(1,out _,out _));
            yield return null;Assert.IsTrue(grip.TetherShown(1),"a tether");
            Sample(input,1,W(inside),false);input.Turn();Assert.IsFalse(level.Busy);
        }
        // Settings, a page back from Begin: the music, which hand the toolbar is by, the language and starting over.
        [UnityTest] public IEnumerator SettingsAreAPageBackFromBegin()
        {
            var store=new MemoryProgress.InMemory();var progress=new MemoryProgress(store);progress.Solve(0);
            MemoryGame game=null;yield return Open(g=>game=g,store);yield return OpenCover(game);
            StringAssert.Contains("Settings",game.PageText(true),"Begin says where Settings are");
            yield return Tap(game.Book.Corner(false),"Begin's back edge");yield return Settle(game);
            Assert.AreEqual(MemoryGame.Phase.Settings,game.Current);Assert.AreEqual(MemoryGame.SettingsSpread,game.Book.Spread);
            StringAssert.Contains("SETTINGS",game.PageText(false));Assert.IsFalse(game.Book.Corner(false).isActiveAndEnabled,"the first page");
            Assert.NotNull(game.Tile("settings/lower"));Assert.NotNull(game.Tile("settings/raise"));
            // Music off and on again.
            yield return Tap(game.Tile("settings/music"),"MUSIC");Assert.IsFalse(game.Progress.Music);Assert.IsTrue(game.Music.Muted);StringAssert.Contains("MUSIC OFF",game.PageText(true));
            yield return Tap(game.Tile("settings/music"),"MUSIC");Assert.IsTrue(game.Progress.Music);Assert.IsFalse(game.Music.Muted);
            Assert.IsNull(game.Tile("settings/hand/left"),"no hand setting: the QUIT plaque it moved is gone");
            // Each language by its own name; picking one prints the book in it.
            StringAssert.Contains("English",game.PageText(false));StringAssert.Contains("简体中文",game.PageText(false));
            yield return Tap(game.Tile("settings/language/zh-Hans"),"简体中文");Assert.AreEqual("zh-Hans",game.Progress.Language);
            for(int i=0;i<5;i++)yield return null;StringAssert.Contains("设置",game.PageText(false));
            yield return Tap(game.Tile("settings/language/en"),"English");for(int i=0;i<5;i++)yield return null;StringAssert.Contains("SETTINGS",game.PageText(false));
            // Start over: a short press does nothing, the hold forgets and turns to Begin.
            yield return Hold(game.Tile("settings/forget").Press,.5f);Assert.IsTrue(game.Progress.Solved(0),"a short press forgets nothing");
            yield return Hold(game.Tile("settings/forget").Press,MemoryGame.HoldToForget+.3f);Assert.AreEqual(0,game.Progress.SolvedCount,"held: every memory waits again");
            yield return Settle(game);yield return WaitFor(game,MemoryGame.Phase.Title,2);Assert.AreEqual(MemoryGame.BeginSpread,game.Book.Spread);
        }
        // Ribbons: Settings, Begin, the credits and QUIT's (the back endpaper). A ribbon is lifted, not pressed: lifted past
        // LiftOn the book turns straight to its spread; short of it, it falls back and nothing happens; a fingertip or a
        // ray's press does nothing; the desktop's click lifts it by itself.
        [UnityTest] public IEnumerator RibbonsTurnStraightToTheirSpreads()
        {
            var store=new MemoryProgress.InMemory();var progress=new MemoryProgress(store);progress.Solve(0);
            MemoryGame game=null;yield return Open(g=>game=g,store);
            Assert.IsFalse(game.Bookmarks.Shown(),"none while the book is closed");
            yield return OpenCover(game);var marks=game.Bookmarks;Assert.IsTrue(marks.Shown());
            // They grow out of the book one after another.
            Assert.Less(marks.Marks[0].Grow,1,"growing out");Assert.LessOrEqual(marks.Marks[3].Grow,marks.Marks[0].Grow,"one after another");
            yield return new WaitForSecondsRealtime(MemoryBookmarks.GrowSeconds+4*MemoryBookmarks.GrowStagger+.2f);
            Assert.IsTrue(marks.Marks.All(m=>m.Grow==1),"all out");
            CollectionAssert.AreEqual(new[]{"settings","begin","credits","quit"},marks.Marks.Select(m=>m.Id).ToArray(),"no ribbon per chapter");
            // Up over the stand's front rail, not through it: each ribbon's hang point is past the rail and above it.
            foreach(var m in marks.Marks)
            {
                var p=game.BookRest.InverseTransformPoint(m.Hang.position);
                Assert.Less(p.z,-.05f,m.Id+" hangs past the rail's front");Assert.Greater(p.y,.042f,m.Id+" hangs from above the rail");
            }
            Assert.Less(game.Stage.InverseTransformPoint(marks.Find("begin").Hang.position).x,0,"Begin left of the knob");Assert.Greater(game.Stage.InverseTransformPoint(marks.Find("credits").Hang.position).x,0,"the credits right of it");
            var credits=marks.Find("credits");
            Assert.IsFalse(credits.Press.Touch(true,"R"),"a fingertip press does nothing");credits.Press.Touch(false,"R");
            // A tracked hand near, open: the ribbon stirs (lifts a little and flutters) and lights only once a pinch would take it.
            var tracking=marks.Tracking;var reader=marks.ReadHand;var near=credits.Hang.TransformPoint(new Vector3(0,-MemoryBookmarks.Drop*.5f,-.06f));
            marks.Tracking=()=>true;marks.ReadHand=(int hand,out Vector3 tip,out float pinch)=>{tip=near;pinch=.1f;return hand==1;};
            try
            {
                yield return new WaitForSecondsRealtime(.4f);
                Assert.Greater(credits.Tempt,.9f,"stirred by a hand 6 cm away");Assert.IsNull(marks.Held(1),"not taken");Assert.IsFalse(credits.Outline.enabled,"no border yet: too far to take");
                Assert.Less(marks.Find("begin").Tempt,.01f,"the others hang still");
                near=credits.Hang.TransformPoint(new Vector3(0,-MemoryBookmarks.Drop*.5f,-.02f));yield return null;yield return null;
                Assert.IsTrue(credits.Outline.enabled,"the border lights where a pinch would take it");
            }
            finally{marks.Tracking=tracking;marks.ReadHand=reader;}
            yield return new WaitForSecondsRealtime(.4f);Assert.Less(credits.Tempt,.01f,"the hand gone, it settles");
            // Stirred, the silk stays one piece: its hanging part leaves the rail's lip where the span over the rail ends.
            Assert.Less(Vector3.Distance(credits.Line[0],credits.AcrossTo),1e-4f,"hanging from the lip");
            // A pinch is the choice (PinchPicks): pinched and let go at once, it lifts itself and one page turns, straight
            // to the credits (not a riffle through every spread between).
            Assert.IsTrue(MemoryBookmarks.PinchPicks);
            int landings=0;void Landed(string line,string trace,LogType type){if(line.StartsWith("SHAPE_OF_MEMORY_BOOK landed spread="))landings++;}
            Application.logMessageReceived+=Landed;
            try{yield return Pick(marks,"credits");yield return Settle(game);}
            finally{Application.logMessageReceived-=Landed;}
            Assert.AreEqual(1,landings,"one flip");
            Assert.AreEqual(game.CreditsSpread,game.Book.Spread);Assert.AreEqual(MemoryGame.Phase.Credits,game.Current,"the credits before the end");
            yield return Tap(game.Book.Corner(false),"the credits' back edge");yield return Settle(game);Assert.AreEqual(MemoryGame.Phase.Ending,game.Current);
            Assert.IsFalse(game.Book.Corner(false).isActiveAndEnabled,"not back onto a chapter that isn't open");
            yield return Pick(marks,"begin",hand:0);yield return Settle(game);Assert.AreEqual(MemoryGame.Phase.Title,game.Current);Assert.AreEqual(MemoryGame.BeginSpread,game.Book.Spread);
            // The 2026-11-15 lift (PinchPicks off): lifted short it falls back and nothing turns; lifted past LiftOn it turns.
            MemoryBookmarks.PinchPicks=false;
            try
            {
                yield return Lift(marks,"credits",35);Assert.IsFalse(game.Book.Turning,"lifted short: nothing turns");Assert.AreEqual(MemoryGame.BeginSpread,game.Book.Spread);
                Assert.Greater(credits.Raised,1f,"it falls back from where it was let go");
                yield return new WaitForSecondsRealtime(1.2f);Assert.AreEqual(0,credits.Raised,.5f,"and hangs again");
                yield return Lift(marks,"credits",80);yield return Settle(game);Assert.AreEqual(game.CreditsSpread,game.Book.Spread,"lifted past LiftOn");
                yield return Pick(marks,"begin",hand:0,lifts:false);yield return Settle(game);Assert.AreEqual(game.CreditsSpread,game.Book.Spread,"a pinch alone does nothing");
            }
            finally{MemoryBookmarks.PinchPicks=true;}
            // The desktop's click: it lifts itself, and the book turns.
            marks.Find("settings").Press.Control.Press();yield return new WaitForSecondsRealtime(MemoryBookmarks.ClickSeconds+.1f);
            yield return Settle(game);Assert.AreEqual(MemoryGame.Phase.Settings,game.Current);
            // Hidden while a memory is up on the walls.
            yield return Play(game,0);Assert.IsFalse(marks.Shown());Assert.IsFalse(marks.Marks[0].Root.gameObject.activeSelf);
        }
        // The wall tone: the room's walls and the book's paper take it, and on a pale one the page's words print in ink.
        [UnityTest] public IEnumerator TheWallToneColoursThePagesAndTheirWords()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;bool right=DarkIsRight(level.level);
            var face=right?game.Book.RightFace:game.Book.LeftFace;
            TMP_Text Riddle()=>face.GetComponentsInChildren<TMP_Text>().First(t=>t.text.Contains(level.level.riddle.Substring(0,12)));
            try
            {
                var pale=new Color(.62f,.58f,.52f);game.UseWallTone(pale);yield return null;
                Assert.AreEqual(pale,MemoryBook.Dark);Assert.IsTrue(MemoryGame.PaleWalls);
                Assert.AreEqual(MemoryPages.Ink,Riddle().color,"ink words on a pale wall");
                game.UseWallTone(MemoryPalette.Ink700);yield return null;Assert.IsFalse(MemoryGame.PaleWalls);
                Assert.AreEqual(MemoryPalette.Moonlight,Riddle().color,"moonlight words on a dark one");
            }
            finally{game.UseWallTone(game.WallTone);}
        }
    }
}
