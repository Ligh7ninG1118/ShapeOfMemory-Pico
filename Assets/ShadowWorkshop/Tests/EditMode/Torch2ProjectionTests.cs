using NUnit.Framework;
using UnityEngine;
using CloudGarden.ShadowPuzzle;

namespace CloudGarden.Tests
{
    public sealed class Torch2ProjectionTests
    {
        static readonly string[] Ids={"Birthday","BonVoyage","GoodMorning","Home"};
        static Torch2Projection Load(string id){var level=Torch2Level.Load(id);Assert.NotNull(level,"Missing torch level 2 "+id+"; run Import torch levels 2");return new Torch2Projection(level);}
        // The answer turned about a workbench axis through the pile's pivot, then moved.
        static Torch2Score At(Torch2Projection p,Quaternion turn,Vector3 move=default)
        {p.Render(turn*p.Solution,Torch2Projection.Home+move);p.Evaluate();return p.Score;}
        static Torch2Score At(Torch2Projection p,float dyaw,float dpitch,Vector3 move=default)
        {p.Render(Torch2Projection.Pose(dyaw,dpitch)*p.Solution,Torch2Projection.Home+move);p.Evaluate();return p.Score;}
        // Turning about this axis only turns that wall's picture in place.
        static Vector3 LampAxis(int wall)=>(Torch2Projection.Home-Torch2Projection.Lamps[wall]).normalized;

        [TestCaseSource(nameof(Ids))] public void SolutionMatchesEveryWall(string id)
        {
            var p=Load(id);var s=At(p,Quaternion.identity);Assert.IsTrue(s.Ready);Assert.AreEqual(1,s.iou,1e-6);
            foreach(var wall in p.scores){Assert.IsTrue(wall.Ready);Assert.AreEqual(0,wall.tilt);}
        }
        [TestCaseSource(nameof(Ids))] public void StartPoseDoesNotMatch(string id)
        {
            var p=Load(id);p.Render(p.Start,Torch2Projection.Home);p.Evaluate();Assert.IsFalse(p.Score.Ready);Assert.Less(p.Score.warmth,.5f);
        }
        [TestCaseSource(nameof(Ids))] public void HandHeldErrorsAreForgiven(string id)
        {
            var p=Load(id);var axes=new System.Collections.Generic.List<Vector3>{Vector3.up,Vector3.right,Vector3.forward};
            foreach(int w in p.walls)axes.Add(LampAxis(w));
            foreach(var axis in axes)foreach(float a in new[]{-4f,4f})Assert.IsTrue(At(p,Quaternion.AngleAxis(a,axis)).Ready,$"{a:+0;-0} degrees about {axis}");
            Assert.IsTrue(At(p,Quaternion.identity,new Vector3(.04f,-.03f,.03f)).Ready,"carried 5 cm off the rest position");
        }
        [TestCaseSource(nameof(Ids))] public void TiltedPictureStillMatches(string id)
        {
            // The orb turns freely, so a picture may sit a little tilted on its wall; well past that it fails.
            var p=Load(id);
            for(int j=0;j<p.walls.Length;j++)
            {
                foreach(float a in new[]{-10f,10f}){At(p,Quaternion.AngleAxis(a,LampAxis(p.walls[j])));Assert.IsTrue(p.scores[j].Ready,$"wall {j} tilted {a}");Assert.GreaterOrEqual(Mathf.Abs(p.scores[j].tilt),5,"matched against a tilted answer");}
                foreach(float a in new[]{-40f,40f}){At(p,Quaternion.AngleAxis(a,LampAxis(p.walls[j])));Assert.IsFalse(p.scores[j].Ready,$"wall {j} tilted {a}");}
            }
        }
        [TestCaseSource(nameof(Ids))] public void OtherPosesNeverMatch(string id)
        {
            var p=Load(id);
            for(int yaw=-150;yaw<=180;yaw+=30)for(int pitch=-60;pitch<=60;pitch+=30)
            {
                if(Mathf.Abs(yaw)<30&&Mathf.Abs(pitch)<30)continue;
                Assert.IsFalse(At(p,yaw,pitch).Ready,$"yaw {yaw:+0;-0} pitch {pitch:+0;-0}");
            }
        }
        [TestCaseSource(nameof(Ids))] public void OtherOrientationsNeverMatch(string id)
        {
            // Uniformly random orientations at least 30 degrees from every answer (build_torch2.py checks 1000 per level).
            var p=Load(id);var random=new System.Random(7);int tried=0;
            while(tried<150)
            {
                var q=new Vector4((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1);
                if(q.sqrMagnitude>1||q.sqrMagnitude<.01f)continue;q=q.normalized;var rotation=new Quaternion(q.x,q.y,q.z,q.w);
                if(p.AngleToAnswer(rotation)<30)continue;tried++;
                p.Render(rotation,Torch2Projection.Home);p.Evaluate();Assert.IsFalse(p.Score.Ready,$"{rotation.eulerAngles} matches");
            }
        }
        // Each accepted flip is an answer of its own, matched exactly at its half turn; a level without flips has only its
        // solution.
        [TestCaseSource(nameof(Ids))] public void FlippedPicturesAreAnswersToo(string id)
        {
            // These script levels ship without flips (build_torch2.py knows none); a copy takes both.
            Assert.AreEqual(1,Load(id).Answers.Length);
            var level=Object.Instantiate(Torch2Level.Load(id));level.flips=Torch2Flips.UpsideDown|Torch2Flips.Mirrored;
            try
            {
                var p=new Torch2Projection(level);
                Assert.AreEqual(level.solution,p.Answers[0]);Assert.AreEqual(1+Torch2Projection.Turns(level.lamps,level.flips).Count,p.Answers.Length);Assert.Greater(p.Answers.Length,1);
                for(int a=0;a<p.Answers.Length;a++)
                {
                    p.Render(p.Answers[a],Torch2Projection.Home);p.Evaluate();Assert.IsTrue(p.Score.Ready,$"answer {a}");Assert.AreEqual(a,p.Matched);Assert.AreEqual(1,p.Score.iou,1e-6);
                    if(a>0)Assert.AreEqual(180,Quaternion.Angle(p.Answers[a],p.Solution),.01f,"a half turn");
                }
            }
            finally{Object.DestroyImmediate(level);}
        }
        [Test] public void FlipsGiveHalfTurnsPerWallLayout()
        {
            var both=Torch2Flips.UpsideDown|Torch2Flips.Mirrored;
            Assert.AreEqual(3,Torch2Projection.Turns(new[]{0},both).Count);Assert.AreEqual(1,Torch2Projection.Turns(new[]{1},Torch2Flips.UpsideDown).Count);
            Assert.AreEqual(1,Torch2Projection.Turns(new[]{0},Torch2Flips.Mirrored).Count);Assert.AreEqual(0,Torch2Projection.Turns(new[]{0},Torch2Flips.None).Count);
            Assert.AreEqual(3,Torch2Projection.Turns(new[]{0,1},both).Count,"mirror both walls, or one wall's own upside-down turn");
            Assert.AreEqual(0,Torch2Projection.Turns(new[]{0,1},Torch2Flips.UpsideDown).Count,"no one turn shows both walls upside down");
            // Upside down is a half turn about the lamp's line of sight: the picture turns in place on its wall.
            var upside=Torch2Projection.Turns(new[]{0},Torch2Flips.UpsideDown)[0];var sight=Torch2Projection.Sight(0);
            Assert.Less(Vector3.Angle(upside*sight,sight),.01f);
        }
        [Test] public void GatheringPullsOnlyFarPiecesAndKeepsTheirDirection()
        {
            var near=new Vector3(.05f,.03f,0);Assert.AreEqual(near,Torch2Level.Gathered(near,.4f,.12f),"within reach of the hub: as placed");
            var far=new Vector3(.2f,-.1f,.2f);var drawn=Torch2Level.Gathered(far,.4f,.12f);
            Assert.AreEqual(.12f+(far.magnitude-.12f)*.4f,drawn.magnitude,1e-5f);Assert.Less((far.normalized-drawn.normalized).magnitude,1e-5f,"same direction");
            Assert.AreEqual(far,Torch2Level.Gathered(far,1,.12f),"1 draws everything as placed");
        }
        [Test] public void HomeNeedsBothWallsAtOnce()
        {
            var p=Load("Home");Assert.AreEqual(2,p.walls.Length);
            var random=new System.Random(4);
            for(int i=0;i<40;i++)
            {
                At(p,(float)random.NextDouble()*360,(float)random.NextDouble()*120-60);
                Assert.AreEqual(p.scores[0].Ready&&p.scores[1].Ready,p.Score.Ready);Assert.AreEqual(Mathf.Min(p.scores[0].iou,p.scores[1].iou),p.Score.iou,1e-6);
            }
        }
        [TestCaseSource(nameof(Ids))] public void WarmthGrowsTowardTheAnswer(string id)
        {
            var p=Load(id);Assert.Less(At(p,120,40).warmth,At(p,15,0).warmth);
        }
        [TestCaseSource(nameof(Ids))] public void PileFitsInsideTheOrb(string id)
        {
            foreach(var piece in Torch2Level.Load(id).pieces)Assert.LessOrEqual(piece.position.magnitude,.26f,piece.label);
            Assert.Greater(Torch2Input.OrbRadius,.26f);Assert.Greater(Torch2Input.GrabReach,Torch2Input.OrbRadius);
            Assert.GreaterOrEqual(Torch2Workshop.MinPosition.y,Torch2Input.OrbRadius,"the orb never sinks into the table");
        }
        [Test] public void LampsLightTheirOwnWall()
        {
            // Each lamp sits at the front corner opposite its wall and aims at the rest position.
            for(int k=0;k<2;k++)
            {
                Assert.Greater(Vector3.Dot(Torch2Projection.Lamps[k]-Torch2Projection.Home,Torch2Projection.WallNormal[k]),.7f);
                Assert.AreEqual(k==0?1:-1,Mathf.Sign(Torch2Projection.Lamps[k].x));
            }
        }
        [Test] public void UnchangedPoseSkipsTheRedraw()
        {
            var p=Load("Birthday");var h=Torch2Projection.Home;
            Assert.IsTrue(p.Render(Torch2Projection.Pose(10,5),h));Assert.IsFalse(p.Render(Torch2Projection.Pose(10,5),h));
            Assert.IsTrue(p.Render(Torch2Projection.Pose(10,6),h));Assert.IsTrue(p.Render(Torch2Projection.Pose(10,6),h+Vector3.right*.01f));
        }
        [TestCaseSource(nameof(Ids))] public void DisplayedOutlineIsTheScoredShadow(string id)
        {
            // The sharp shadow mesh and the scoring raster come from the same triangles: filling the outline at the
            // raster's pixel centres gives the raster back, apart from pixels whose centre sits on an edge.
            var p=Load(id);var outline=new System.Collections.Generic.List<Vector2>();
            var poses=new[]{(Quaternion.identity,Vector3.zero),(Quaternion.Euler(-15,25,10),new Vector3(.08f,.05f,-.06f)),(Quaternion.Euler(40,-70,-30),new Vector3(-.25f,.3f,.3f))};
            for(int n=0;n<poses.Length;n++)
            {
                var rotation=poses[n].Item1*p.Solution;var position=Torch2Projection.Home+poses[n].Item2;p.Render(rotation,position);
                for(int j=0;j<p.walls.Length;j++)
                {
                    p.Outline(j,rotation,position,outline);Assert.AreEqual(0,outline.Count%3);
                    var filled=new bool[Torch2Projection.Width*Torch2Projection.Height];
                    for(int t=0;t<outline.Count;t+=3)Fill(filled,outline[t]/Torch2Projection.Pixel,outline[t+1]/Torch2Projection.Pixel,outline[t+2]/Torch2Projection.Pixel);
                    int differ=0,area=0;
                    for(int i=0;i<filled.Length;i++){if(filled[i]!=p.shadow[j][i])differ++;if(p.shadow[j][i])area++;}
                    foreach(var q in outline)Assert.IsTrue(q.x>=-1e-5f&&q.y>=-1e-5f&&q.x<=Torch2Projection.Width*Torch2Projection.Pixel+1e-5f&&q.y<=Torch2Projection.Height*Torch2Projection.Pixel+1e-5f,"outline leaves the wall");
                    // Far poses may push the shadow mostly off the wall; that is what the clipping is for.
                    if(n==0)Assert.Greater(area,50,$"{id} wall {j} has a shadow");
                    Assert.LessOrEqual(differ,Mathf.Max(3,area/200),$"{id} wall {j}: {differ} of {area} pixels differ");
                }
            }
        }
        static void Fill(bool[] mask,Vector2 a,Vector2 b,Vector2 c)
        {
            float area=(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);if(area<0){var t=b;b=c;c=t;}
            for(int y=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))));y<=Mathf.Min(Torch2Projection.Height-1,Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))));y++)
            for(int x=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))));x<=Mathf.Min(Torch2Projection.Width-1,Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))));x++)
            {
                var p=new Vector2(x+.5f,y+.5f);
                if(Edge(a,b,p)>=0&&Edge(b,c,p)>=0&&Edge(c,a,p)>=0)mask[y*Torch2Projection.Width+x]=true;
            }
        }
        static float Edge(Vector2 a,Vector2 b,Vector2 p)=>(b.x-a.x)*(p.y-a.y)-(b.y-a.y)*(p.x-a.x);
    }
}
