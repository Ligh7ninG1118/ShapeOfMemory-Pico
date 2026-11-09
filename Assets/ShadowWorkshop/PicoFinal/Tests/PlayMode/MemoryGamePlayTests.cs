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
    // Drives the deliverable scene the way hands do: pointer events on each button's, tile's, edge's and the cover's ray
    // interactable (MetaMenuButton); a tapped edge turns its page by itself, as a quick pinch does. The book lies on its
    // stand, rises onto the walls for a memory and comes back down for a pause or once the memory is found.
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
        [TearDown] public void Clear(){MemoryGame.StoreOverride=null;}
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
        IEnumerator Tap(MemoryTile tile,string what)=>Tap(tile?tile.Press:null,what);
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
        // The title cut short if it plays, then the closed book opens.
        IEnumerator OpenCover(MemoryGame game)
        {
            Assert.AreEqual(MemoryGame.Phase.Cover,game.Current);Assert.IsTrue(game.Book.Closed,"the book starts closed");
            game.FinishTitle();float titled=Time.realtimeSinceStartup+2;while(game.Book.Sealed&&Time.realtimeSinceStartup<titled)yield return null;
            Assert.IsFalse(game.Book.Sealed,"the title ends");Assert.IsTrue(game.Book.OnStand,"the book on the stand");Assert.AreEqual(0,game.Sky.Dark,"the dark gone");
            yield return Tap(game.Book.CoverPress,"the cover");
            float deadline=Time.realtimeSinceStartup+4;while((game.Current==MemoryGame.Phase.Cover||game.Book.Turning)&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsFalse(game.Book.Closed,"the cover opens");Assert.IsTrue(game.Book.OnStand,"on the stand");
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
        static void Solve(Torch2Workshop level){Assert.IsTrue(level.BeginHold(7));level.Hold(7,level.Position,level.Projection.Solution);level.EndHold(7);}
        static int Plates(Transform face)=>face.GetComponentsInChildren<Transform>().Count(t=>t.name=="Plate");
        // The levels come from the Chapters asset: tests name slots, not levels.
        static string Chapter(int index)=>MemoryProgress.Levels[index];
        // Whether a memory's words are on the right page (the one its lamps leave dark), and both pages' words.
        static bool DarkIsRight(Torch2Level level)=>System.Array.IndexOf(level.lamps,1)<0;
        static string BothPages(MemoryGame game)=>game.PageText(false)+"\n"+game.PageText(true);

        [UnityTest] public IEnumerator TheTitleComesUpInTheDarkThenTheBookComesDownAndOpensOntoTheEndpaper()
        {
            MemoryGame game=null;yield return Open(g=>game=g);
            Assert.AreEqual(MemoryBook.Where.Presented,game.Book.At,"the title hangs in the air first");
            Assert.AreEqual(1,game.Sky.Dark,"in the dark");Assert.IsTrue(game.Book.Sealed,"its cover shut through the title");
            yield return null;Assert.IsFalse(game.Book.CoverPress.Available,"pressing the cover does nothing yet");
            StringAssert.Contains("SHAPE",string.Join(" ",game.Book.CoverFace.GetComponentsInChildren<TMP_Text>().Select(t=>t.text)));
            float deadline=Time.realtimeSinceStartup+MemoryGame.TitleSeconds+1;
            while(game.Book.Sealed&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(game.Book.OnStand,"then comes down onto the stand");Assert.IsTrue(game.Book.Closed);Assert.AreEqual(0,game.Sky.Dark,"out of the dark");
            Assert.AreEqual(1,Object.FindObjectsByType<MetaPlayerRig>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,"one player");
            yield return OpenCover(game);
            Assert.AreEqual(MemoryGame.Phase.Title,game.Current);Assert.AreEqual(0,game.Book.Spread);
            StringAssert.Contains("HOW",game.PageText(false));StringAssert.Contains("Turn the page",game.PageText(true));
            Assert.NotNull(game.Tile("title/lower"));Assert.NotNull(game.Tile("title/raise"));
            Assert.IsTrue(game.Book.Corner(true).isActiveAndEnabled,"the endpaper turns on");Assert.IsFalse(game.Book.Corner(false).isActiveAndEnabled,"and not back");
            Assert.IsNull(game.ShownSet,"no buttons by the hand off the walls");
            float height=game.Stage.position.y;yield return Tap(game.Tile("title/raise"),"RAISE");
            Assert.AreEqual(height+MemoryGame.Lift,game.Stage.position.y,1e-4f);Assert.AreEqual(MemoryGame.Lift,game.Progress.Lift,1e-4f);
        }
        [UnityTest] public IEnumerator TurningOntoTheNextMemoryStartsItOnTheWalls()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return OpenCover(game);
            yield return Tap(game.Book.Corner(true),"the endpaper's edge");Assert.IsTrue(game.Book.Turning);yield return Settle(game);
            Assert.AreEqual(MemoryGame.Phase.Levels,game.Current);Assert.AreEqual(MemoryGame.SpreadOf(0),game.Book.Spread);
            StringAssert.Contains("Hold still",game.PageText(true));Assert.IsNull(game.Tile("chapter/play"),"no PLAY: it starts by itself");
            Assert.IsFalse(game.Book.Corner(true).isActiveAndEnabled,"no turning past a memory not yet found");
            yield return WaitFor(game,MemoryGame.Phase.Rising,MemoryGame.Dwell+1);
            yield return WaitFor(game,MemoryGame.Phase.Intro,MemoryGame.RiseSeconds+1);Assert.IsTrue(game.Book.OnWalls);
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
        [UnityTest] public IEnumerator MenuBringsTheBookDownAndResumeTakesItBackUp()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;
            yield return Tap(game.Button("play","menu"));Assert.AreEqual(MemoryGame.Phase.Lowering,game.Current);Assert.IsNull(game.ShownSet);
            yield return WaitFor(game,MemoryGame.Phase.Paused,MemoryGame.PauseSeconds+1);
            Assert.IsTrue(game.Book.OnStand);Assert.IsFalse(level.input.enabled);Assert.AreEqual(0,level.world.LampLevel,.01f,"the lamps go out");
            Assert.Less(level.world.Pile.localScale.x,.05f,"the orb folds away");StringAssert.Contains("PAUSED",game.PageText(true));
            Assert.IsTrue(game.Book.Corner(false).isActiveAndEnabled,"turning back leaves");Assert.IsFalse(game.Book.Corner(true).isActiveAndEnabled);
            yield return Tap(game.Tile("pause/resume"),"RESUME");yield return WaitFor(game,MemoryGame.Phase.Playing,MemoryGame.PauseSeconds+1);
            Assert.IsTrue(game.Book.OnWalls);Assert.IsTrue(level.input.enabled);Assert.AreEqual("play",game.ShownSet);
            yield return new WaitForSecondsRealtime(MemoryGame.FoldSeconds+.1f);Assert.AreEqual(MemoryGame.PileScale,level.world.Pile.localScale.x,.01f,"the orb opens out again");Assert.AreEqual(1,level.world.LampLevel,.01f);
            // Paused again, then the page turned back: the memory is left.
            yield return Tap(game.Button("play","menu"));yield return WaitFor(game,MemoryGame.Phase.Paused,MemoryGame.PauseSeconds+1);
            yield return Tap(game.Book.Corner(false),"the back edge");yield return Settle(game);
            yield return WaitFor(game,MemoryGame.Phase.Title,2);Assert.IsNull(game.Level);Assert.AreEqual(0,game.Book.Spread);
        }
        [UnityTest] public IEnumerator AFoundMemoryGoesBackToTheTableThenItsPageMovesOn()
        {
            var store=new MemoryProgress.InMemory();MemoryGame game=null;yield return Open(g=>game=g,store);yield return Play(game,0);var level=game.Level;
            Solve(level);yield return WaitFor(game,MemoryGame.Phase.Solved,3);
            Assert.IsNull(game.ShownSet);Assert.IsTrue(game.Book.OnWalls,"the found moment plays on the walls");Assert.IsTrue(game.Progress.Solved(0));
            Assert.IsTrue(game.Found,"the found moment plays over the orb");StringAssert.Contains("MEMORY FOUND",string.Join(" ",game.Found.GetComponentsInChildren<TMP_Text>().Select(t=>t.text)));
            yield return new WaitForSecondsRealtime(MemoryGame.FoundHold-.5f);Assert.AreEqual(MemoryGame.Phase.Solved,game.Current,"it holds");
            yield return WaitFor(game,MemoryGame.Phase.Lowering,8);
            foreach(var piece in level.world.Pieces){Assert.Less(level.transform.InverseTransformPoint(piece.position).y,.12f,piece.name+" back on the table");Assert.Greater(piece.localScale.x,0,piece.name+" stays");}
            yield return WaitFor(game,MemoryGame.Phase.Found,MemoryGame.LowerSeconds+1);
            Assert.IsTrue(game.Book.OnStand);StringAssert.Contains("FOUND",game.PageText(true));Assert.NotNull(game.Tile("found/replay"));Assert.AreEqual(1,Plates(game.Book.LeftFace),"its picture");
            yield return Tap(game.Book.Corner(true),"the found page's edge");yield return Settle(game);
            yield return WaitFor(game,MemoryGame.Phase.Levels,2);Assert.IsNull(game.Level);Assert.AreEqual(MemoryGame.SpreadOf(1),game.Book.Spread);
            yield return WaitFor(game,MemoryGame.Phase.Rising,MemoryGame.Dwell+1);yield return WaitFor(game,MemoryGame.Phase.Intro,MemoryGame.RiseSeconds+1);
            Assert.AreEqual(Chapter(1),game.Level.level.name);Assert.AreEqual(0,game.HintStage);
        }
        [UnityTest] public IEnumerator ReturningPlayersOpenAtTheirBookmark()
        {
            var store=new MemoryProgress.InMemory();var progress=new MemoryProgress(store);progress.Solve(0);progress.SawIntro(0);
            MemoryGame game=null;yield return Open(g=>game=g,store);
            Assert.AreEqual(MemoryBook.Where.Presented,game.Book.At,"the title plays for a returning player too");Assert.IsTrue(game.Book.Sealed);
            yield return OpenCover(game);yield return Settle(game);
            Assert.AreEqual(MemoryGame.Phase.Levels,game.Current);Assert.AreEqual(MemoryGame.SpreadOf(1),game.Book.Spread,"the next memory");
            yield return WaitFor(game,MemoryGame.Phase.Rising,MemoryGame.Dwell+1);Assert.AreEqual(Chapter(1),game.Level.level.name);
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
                Assert.Less(Vector3.Distance(level.Drawn(piece.position),t.localPosition),1e-4f,piece.label+" in its place");Assert.AreEqual(piece.Scale,t.localScale,piece.label+" full size");Assert.Less(Quaternion.Angle(Quaternion.Euler(piece.euler),t.localRotation),.05f,piece.label+" turned right");
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
            Assert.IsFalse(game.Book.Corner(true).isActiveAndEnabled,"the last page");
            yield return Tap(game.Book.Corner(false),"the back edge");yield return Settle(game);
            Assert.AreEqual(MemoryGame.Phase.Levels,game.Current);Assert.NotNull(game.Tile("chapter/play"),"a found memory can be played again");
        }
        // By hand: a pinch at the right page's edge lifts the page by that point and it follows the hand round the spine;
        // let go short of upright it falls back, carried over it turns. The back page's edge turns it back.
        [UnityTest] public IEnumerator PagesFollowTheHandThatHoldsTheirEdge()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return OpenCover(game);var book=game.Book;Assert.AreEqual(0,book.Spread);
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
            book.Drop();yield return Settle(game);Assert.AreEqual(0,book.Spread,"let go short of upright, it falls back");
            Assert.IsTrue(book.Grab(At(1,w,h*.2f)));
            for(int i=1;i<=30;i++){book.Drag(At(140f*i/30,w*.95f,h*.2f));yield return null;}
            yield return new WaitForSecondsRealtime(.3f);book.Drop();yield return Settle(game);
            Assert.AreEqual(1,book.Spread,"carried over, it turns");Assert.AreEqual(MemoryGame.Phase.Levels,game.Current);
            Assert.AreEqual(0,book.EdgeAt(At(178,w,h*.5f)));
            Assert.IsTrue(book.Grab(At(178,w,h*.5f)));book.Drop();yield return Settle(game);Assert.AreEqual(0,book.Spread,"a quick pinch turns the page back");
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
            yield return Tap(game.Button("play","menu"));yield return WaitFor(game,MemoryGame.Phase.Paused,MemoryGame.PauseSeconds+1);
            foreach(var light in lights)if(light.Beam)Assert.IsFalse(light.Beam.enabled,"lamps off: no beam");
        }
        // Playtest 2026-11-08: Home's pile looked scattered. A level with drawGather draws its far pieces nearer the hub, after
        // the intro too, while the shadow keeps their real places.
        [UnityTest] public IEnumerator AGatheredPileIsDrawnTighterWithTheSameShadow()
        {
            MemoryGame game=null;yield return Open(g=>game=g);int index=-1;
            for(int i=0;i<MemoryChapters.Count;i++)if(game.Chapter(i).drawGather<1){index=i;break;}
            if(index<0)Assert.Inconclusive("no chapter draws its pile gathered");
            yield return Play(game,index);var level=game.Level;var data=level.level;float drawn=0,real=0;
            for(int i=0;i<data.pieces.Length;i++)
            {
                var piece=data.pieces[i];var t=level.world.Pieces[i];
                Assert.Less(Vector3.Distance(data.Drawn(piece.position),t.localPosition),1e-4f,piece.label+" drawn in its gathered place");
                drawn=Mathf.Max(drawn,t.localPosition.magnitude);real=Mathf.Max(real,piece.position.magnitude);
            }
            Assert.Less(drawn,real-.05f,"the farthest piece is drawn nearer");
            level.Projection.Render(level.Projection.Solution,Torch2Projection.Home);level.Projection.Evaluate();Assert.AreEqual(1,level.Projection.Score.iou,1e-6,"the shadow is the real pile's");
        }
        // Playtest 2026-11-08: a turn that passed the answer while held never counted. Now held within heldSnapAngle of an
        // answer, or swept through it between two samples, the orb leaves the hand, snaps in and the memory is found.
        [UnityTest] public IEnumerator HeldThroughTheAnswerTheOrbSnapsInAndTheMemoryIsFound()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;var answer=level.Projection.Solution;
            // Taken up on one side of the answer, past the snap's reach, then turned through it in one sample.
            var before=Quaternion.AngleAxis(level.level.heldSnapAngle+15,Vector3.up)*answer;level.SetPose(level.Position,before);
            Assert.IsTrue(level.BeginHold(7));level.Hold(7,level.Position,Quaternion.AngleAxis(3,Vector3.up)*before);
            Assert.IsTrue(level.Busy,"still in the hand away from the answer");Assert.IsFalse(level.Snapping);
            level.Hold(7,level.Position,Quaternion.AngleAxis(-(level.level.heldSnapAngle+15),Vector3.up)*answer);
            Assert.IsFalse(level.Busy,"swept through the answer: out of the hand");Assert.IsTrue(level.Snapping);
            Assert.IsFalse(level.BeginHold(8),"nothing takes a solving snap back");
            yield return WaitFor(game,MemoryGame.Phase.Solved,2);Assert.IsTrue(level.Completed);
            yield return new WaitForSecondsRealtime(.5f);Assert.Less(Quaternion.Angle(level.Rotation,answer),.01f,"exactly the answer");
        }
        // Playtest 2026-11-08: the picture upside down (or mirrored) counts, and the orb then turns it the right way up.
        [UnityTest] public IEnumerator AFlippedPictureCountsAndRightsItself()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;var p=level.Projection;
            Assert.GreaterOrEqual(p.Answers.Length,2,"chapter I accepts flips");
            var upsideDown=p.Answers[1];var near=Quaternion.AngleAxis(level.level.heldSnapAngle*.5f,Vector3.right)*upsideDown;
            p.Render(upsideDown,Torch2Projection.Home);p.Evaluate();Assert.IsTrue(p.Score.Ready);Assert.AreEqual(1,p.Matched);
            // Turned half round the picture's upright, the shadow is only near a mirror image (perspective): it never counts.
            var mirrored=Quaternion.AngleAxis(180,Vector3.ProjectOnPlane(Vector3.up,Torch2Projection.Sight(level.level.lamps[0])).normalized)*p.Solution;
            p.Render(mirrored,Torch2Projection.Home);p.Evaluate();Assert.IsFalse(p.Score.Ready,"the mirrored pose is not an answer");Assert.Greater(p.AngleToAnswer(mirrored),90);
            level.SetPose(level.Position,Quaternion.AngleAxis(30,Vector3.right)*upsideDown);
            Assert.IsTrue(level.BeginHold(7));level.Hold(7,level.Position,near);Assert.IsFalse(level.Busy,"near the flipped answer: out of the hand");
            yield return WaitFor(game,MemoryGame.Phase.Solved,2);
            yield return new WaitForSecondsRealtime(Torch2Workshop.RightSeconds+.3f);
            Assert.IsFalse(level.Righting);Assert.Less(Quaternion.Angle(level.Rotation,p.Solution),.01f,"turned the right way up");
        }
        // Playtest 2026-11-08: the music swelled at lookalikes far from the answer. Past cueAngle it stays cold, while a
        // warm wall still sharpens its slide.
        [UnityTest] public IEnumerator TheMusicStaysColdFarFromEveryAnswer()
        {
            MemoryGame game=null;yield return Open(g=>game=g);yield return Play(game,0);var level=game.Level;var p=level.Projection;var cue=level.world.Cue;
            level.SnapWhileHeld=false;
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
    }
}
