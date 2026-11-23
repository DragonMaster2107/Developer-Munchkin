using DeveloperDungeon.Engine.Random;

namespace DeveloperDungeon.Engine.Tests.Random;

public class GameRandomTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new GameRandom(42);
        var b = new GameRandom(42);
        Assert.Equal(
            Enumerable.Range(0, 20).Select(_ => a.Next(100)),
            Enumerable.Range(0, 20).Select(_ => b.Next(100)));
    }

    [Fact]
    public void RestoredFromState_ContinuesIdentically()
    {
        var a = new GameRandom(7);
        a.Next(10);
        var b = new GameRandom(a.State);
        Assert.Equal(a.Next(1000), b.Next(1000));
    }

    [Fact]
    public void RollD6_StaysInRange_AndHitsAllFaces()
    {
        var r = new GameRandom(1);
        var rolls = Enumerable.Range(0, 1000).Select(_ => r.RollD6()).ToList();
        Assert.All(rolls, x => Assert.InRange(x, 1, 6));
        Assert.Equal(6, rolls.Distinct().Count());
    }

    [Fact]
    public void Shuffle_IsDeterministicPermutation()
    {
        var l1 = Enumerable.Range(0, 30).ToList();
        var l2 = l1.ToList();
        new GameRandom(5).Shuffle(l1);
        new GameRandom(5).Shuffle(l2);
        Assert.Equal(l1, l2);
        Assert.NotEqual(Enumerable.Range(0, 30), l1);
        Assert.Equal(Enumerable.Range(0, 30), l1.Order());
    }
}
