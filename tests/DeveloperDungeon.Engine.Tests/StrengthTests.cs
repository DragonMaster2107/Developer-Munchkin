using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Tests.Support;

namespace DeveloperDungeon.Engine.Tests;

public class StrengthTests
{
    private static Game Fight(string monster, Func<GameBuilder, GameBuilder>? setup = null)
    {
        var builder = GameBuilder.Create().ClearHands().Active(1).Phase(GamePhase.OpenTicket).TicketTop(monster);
        var game = (setup?.Invoke(builder) ?? builder).Build();
        Assert.True(game.Execute(new OpenTicket("p1")).Accepted);
        return game;
    }

    private static CombatSummary Summary(Game game) => StrengthCalculator.Summarize(game.State, game.Catalog);

    [Fact]
    public void PlayerStrength_IsLevelPlusEquippedBonuses()
    {
        var game = Fight("monster-l1", b => b.Level(1, 2).Equip(1, "equip-head", "equip-ide-1h").Backpack(1, "equip-big"));

        Assert.Equal(5, Summary(game).PlayerStrength);
    }

    [Fact]
    public void ConditionalBonus_AppliesOnlyAgainstMatchingTag()
    {
        var legacy = Fight("monster-l3-legacy", b => b.Language(1, "lang-csharp"));
        var plain = Fight("monster-l1", b => b.Language(1, "lang-csharp"));

        Assert.Equal(2, Summary(legacy).PlayerStrength);
        Assert.Equal(1, Summary(plain).PlayerStrength);
    }

    [Fact]
    public void MonsterBonus_AgainstQa()
    {
        var qa = Fight("monster-l8-hates-qa", b => b.Department(1, "dept-qa"));
        var other = Fight("monster-l8-hates-qa");

        Assert.Equal(10, Summary(qa).MonsterStrength);
        Assert.Equal(8, Summary(other).MonsterStrength);
    }

    [Fact]
    public void TemporaryPenalty_ReducesStrength()
    {
        var game = Fight("monster-l1", b => b.Level(1, 4));
        game.State.TemporaryEffects.Add(new TemporaryEffect { PlayerId = "p1", CombatModifier = -2, StartsAfterTurnNumber = -1, SourceCardId = "curse-penalty" });

        Assert.Equal(2, Summary(game).PlayerStrength);
    }

    [Fact]
    public void HelperStrength_IsAdded()
    {
        var game = Fight("monster-l1", b => b.Level(1, 2).Level(2, 3).Equip(2, "equip-head"));
        game.State.Combat!.HelperId = "p2";

        Assert.Equal(2 + 3 + 1, Summary(game).PlayerStrength);
    }

    [Fact]
    public void TiesWin_FromQa()
    {
        var withQa = Fight("monster-l1", b => b.Department(1, "dept-qa"));
        var without = Fight("monster-l1");

        Assert.True(Summary(withQa).PlayerWinsTies);
        Assert.False(Summary(without).PlayerWinsTies);
    }

    [Fact]
    public void Rewards_IncludeEnhancerLevelsAndLoot()
    {
        var game = Fight("monster-l3-legacy");
        game.State.Combat!.Enhancers[game.State.Combat.Monsters[0].InstanceId] = [new CardInstance { InstanceId = 9999, CardId = "enhancer-plus5" }];

        var summary = Summary(game);

        Assert.Equal(8, summary.MonsterStrength);
        Assert.Equal(1, summary.TotalRewardLevels);
        Assert.Equal(2, summary.TotalRewardLoot);
    }

    [Fact]
    public void RestrictedItem_WithoutRequirement_GivesNoBonus()
    {
        var game = GameBuilder.Create().ClearHands().Equip(1, "equip-qa-only").Department(1, "dept-qa").Build();
        var withDepartment = StrengthCalculator.BasePower(game.State, game.Catalog, "p1");

        game.State.Players[0].Departments.Clear();

        Assert.Equal(withDepartment - 4, StrengthCalculator.BasePower(game.State, game.Catalog, "p1"));
    }
}
