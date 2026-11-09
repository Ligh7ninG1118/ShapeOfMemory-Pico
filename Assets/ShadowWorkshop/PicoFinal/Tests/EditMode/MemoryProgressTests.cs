using NUnit.Framework;

namespace ShapeOfMemory.Tests
{
    public class MemoryProgressTests
    {
        [Test] public void LevelsOpenInOrderAndAllStayOpenAfterOneRun()
        {
            var p=new MemoryProgress(new MemoryProgress.InMemory(),new[]{"A","B","C","D"});
            Assert.IsTrue(p.Unlocked(0));Assert.IsFalse(p.Unlocked(1));Assert.AreEqual(0,p.Continue);
            p.Solve(0);Assert.IsTrue(p.Unlocked(1));Assert.IsFalse(p.Unlocked(2));Assert.AreEqual(1,p.Continue);
            for(int i=1;i<p.Count;i++)p.Solve(i);
            Assert.AreEqual(-1,p.Continue,"every memory found");Assert.AreEqual(4,p.SolvedCount);
            for(int i=0;i<p.Count;i++)Assert.IsTrue(p.Unlocked(i));
        }
        // Without a list of its own, progress follows the Chapters asset.
        [Test] public void TheGamesOrderIsTheChapterList()
        {
            var p=new MemoryProgress(new MemoryProgress.InMemory());
            Assert.AreEqual(MemoryChapters.Load().Ids,MemoryProgress.Levels);Assert.AreEqual(MemoryChapters.Count,p.Count);
            for(int i=0;i<p.Count;i++)Assert.AreEqual(MemoryProgress.Levels[i],p.Id(i));
        }
        // A launch at a level outside the game plays it as a book of its own; its progress never touches the game's.
        [Test] public void AnOwnLevelListKeepsItsProgressApart()
        {
            var store=new MemoryProgress.InMemory();var own=new MemoryProgress(store,new[]{"Teapot"});
            Assert.AreEqual(1,own.Count);Assert.AreEqual("Teapot",own.Id(0));Assert.AreEqual(0,own.IndexOf("Teapot"));Assert.AreEqual(-1,own.IndexOf(MemoryProgress.Levels[0]));
            own.Solve(0);own.SawIntro(0);Assert.AreEqual(-1,own.Continue);Assert.IsTrue(own.SeenIntro(0));
            var game=new MemoryProgress(store);Assert.AreEqual(0,game.SolvedCount,"the game's chapters are untouched");Assert.AreEqual(1,game.IndexOf(MemoryProgress.Levels[1]));
        }
        [Test] public void HintsKeepTheMostUsedAndLiftIsClamped()
        {
            var store=new MemoryProgress.InMemory();var p=new MemoryProgress(store);
            p.UsedHints(2,1);p.UsedHints(2,2);p.UsedHints(2,1);p.UsedHints(2,9);Assert.AreEqual(2,p.Hints(2));Assert.AreEqual(0,p.Hints(1));
            p.Lift=.05f;Assert.AreEqual(.05f,p.Lift,1e-4f);p.Lift=-1;Assert.AreEqual(-MemoryProgress.MaxLift,p.Lift,1e-4f);
            Assert.AreEqual(-MemoryProgress.MaxLift,new MemoryProgress(store).Lift,1e-4f,"read back from the store");
        }
    }
}
