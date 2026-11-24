using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Tests.Support;

namespace DeveloperDungeon.Web.Tests.Logic;

public class AnimationCuesTests
{
    private static readonly CardCatalog Catalog = TestGames.Catalog;

    private static (TableController C, string PlayerId, string Name) Setup()
    {
        var c = TestGames.NewController();
        var p = c.View.Players[0];
        return (c, p.Id, p.Name);
    }

    private static string FirstCardOf(CardKind kind) => Catalog.All.First(x => x.Kind == kind).Id;

    [Fact]
    public void CombatStarted_GivesMonsterSlamWithName()
    {
        var (c, id, _) = Setup();
        var monster = Catalog.Get(FirstCardOf(CardKind.Monster));

        var cue = Assert.Single(AnimationCues.From([new CombatStarted(id, monster.Id)], c.View, Catalog));

        Assert.Equal(new AnimationCue(CueKind.MonsterSlam, $"{monster.Name}!", 900), cue);
    }

    [Fact]
    public void LevelUp_UsesNewTitle()
    {
        var (c, id, name) = Setup();

        var cue = Assert.Single(AnimationCues.From([new LevelChanged(id, 4, 5)], c.View, Catalog));

        Assert.Equal(CueKind.LevelUp, cue.Kind);
        Assert.Equal($"{name}: Senior Developer", cue.Text);
        Assert.Equal(1500, cue.DurationMs);
    }

    [Fact]
    public void LevelDown_GivesLevelDown()
    {
        var (c, id, name) = Setup();

        var cue = Assert.Single(AnimationCues.From([new LevelChanged(id, 5, 4)], c.View, Catalog));

        Assert.Equal(new AnimationCue(CueKind.LevelDown, $"{name}: Stufe 4", 900), cue);
    }

    [Fact]
    public void Curse_GivesShake()
    {
        var (c, id, _) = Setup();
        var curse = Catalog.Get(FirstCardOf(CardKind.Curse));

        var cue = Assert.Single(AnimationCues.From([new CursePlayed(id, id, curse.Id)], c.View, Catalog));

        Assert.Equal(new AnimationCue(CueKind.ScreenShake, $"Fluch: {curse.Name}", 700), cue);
    }

    [Fact]
    public void UnrelatedEvent_GivesNoCue()
    {
        var (c, id, _) = Setup();

        Assert.Empty(AnimationCues.From([new TurnEnded(id, id)], c.View, Catalog));
    }

    [Fact]
    public void EscapeAndWin_AreMapped()
    {
        var (c, id, name) = Setup();
        var monster = FirstCardOf(CardKind.Monster);

        var cues = AnimationCues.From(
            [new EscapeRolled(id, monster, 5, 0, true), new EscapeRolled(id, monster, 2, 0, false), new GameWon(id)], c.View, Catalog);

        Assert.Equal([CueKind.EscapeSuccess, CueKind.EscapeFail, CueKind.Victory], cues.Select(x => x.Kind));
        Assert.Equal("Wurf 5", cues[0].Text);
        Assert.Equal($"{name} ist CTO!", cues[2].Text);
    }

    [Fact]
    public void AutoEscape_WithoutRoll_ShowsEscaped()
    {
        var (c, id, _) = Setup();

        var cues = AnimationCues.From([new EscapeRolled(id, FirstCardOf(CardKind.Monster), 0, 0, true)], c.View, Catalog);

        var cue = Assert.Single(cues);
        Assert.Equal(CueKind.EscapeSuccess, cue.Kind);
        Assert.Equal("Entkommen", cue.Text);
    }

    [Fact]
    public void AllDurations_AtMost1500()
    {
        var (c, id, _) = Setup();
        var monster = FirstCardOf(CardKind.Monster);
        var curse = FirstCardOf(CardKind.Curse);

        var cues = AnimationCues.From(
        [
            new CombatStarted(id, monster), new CursePlayed(id, id, curse), new LevelChanged(id, 1, 2), new LevelChanged(id, 2, 1),
            new CardPlayedInCombat(id, monster, null), new EscapeRolled(id, monster, 1, 0, false), new EscapeRolled(id, monster, 6, 0, true), new GameWon(id),
        ], c.View, Catalog);

        Assert.Equal(8, cues.Count);
        Assert.All(cues, x => Assert.InRange(x.DurationMs, 1, 1500));
    }
}

public class AnimationCuesSelectionTests
{
    private static AnimationCue Cue(CueKind k) => new(k, k.ToString(), 100);

    [Fact]
    public void SelectForPlayback_KeepsAtMostTwo_ByPriority_InOriginalOrder()
    {
        var all = new[] { CueKind.CardFly, CueKind.LevelDown, CueKind.MonsterSlam, CueKind.EscapeSuccess, CueKind.LevelUp }.Select(Cue).ToList();

        var picked = AnimationCues.SelectForPlayback(all);

        Assert.Equal([CueKind.MonsterSlam, CueKind.LevelUp], picked.Select(c => c.Kind));
    }

    [Fact]
    public void SelectForPlayback_Victory_BeatsEverything()
    {
        var all = new[] { CueKind.LevelUp, CueKind.MonsterSlam, CueKind.Victory }.Select(Cue).ToList();

        Assert.Equal([CueKind.LevelUp, CueKind.Victory], AnimationCues.SelectForPlayback(all).Select(c => c.Kind));
    }

    [Fact]
    public void SelectForPlayback_ShortLists_AreUnchanged()
    {
        var one = new[] { Cue(CueKind.CardFly) };

        Assert.Equal(one, AnimationCues.SelectForPlayback(one));
        Assert.Empty(AnimationCues.SelectForPlayback([]));
    }
}
