using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

public sealed record CombatSummary(int PlayerStrength, int MonsterStrength, bool PlayerWinsTies, bool PlayerWouldWin, int TotalRewardLevels, int TotalRewardLoot);

public static class StrengthCalculator
{
    /// <summary>Wie <see cref="Summarize"/>, liefert aber null, wenn kein Kampf läuft.</summary>
    public static CombatSummary? TrySummarize(GameState s, CardCatalog c) =>
        s.Combat is null ? null : Summarize(s, c);

    /// <summary>Stärken und Belohnungen des laufenden Kampfes; setzt einen Kampf voraus.</summary>
    public static CombatSummary Summarize(GameState s, CardCatalog c)
    {
        var combat = s.Combat ?? throw new InvalidOperationException("Es läuft kein Kampf.");
        var fighters = new List<string> { s.Players[s.ActivePlayerIndex].Id };
        if (combat.HelperId is not null)
        {
            fighters.Add(combat.HelperId);
        }

        var monsterDefs = combat.Monsters.Select(m => c.Get(m.CardId)).ToList();
        var monsterTags = monsterDefs.SelectMany(d => d.Tags).ToHashSet();
        var modifiers = combat.Enhancers.Values.SelectMany(l => l)
            .SelectMany(e => c.Get(e.CardId).Effects).OfType<MonsterModifier>().ToList();
        var passive = fighters.ToDictionary(id => id, id => PassiveEffects.Of(s, c, id).ToList());

        var player = fighters.Sum(id => BasePower(s, c, id))
            + passive.Values.SelectMany(e => e).OfType<ConditionalCombatBonus>()
                .Where(e => e.MonsterTag is not null && monsterTags.Contains(e.MonsterTag)).Sum(e => e.Amount)
            + fighters.Sum(id => TemporaryEffects.CombatModifierFor(s, id))
            + combat.OneShots.Where(o => o.Side == Side.Player).Sum(o => CombatBonusOf(c, o.Card))
            + combat.ManualPlayerAdjust;

        var fighterStates = s.Players.Where(p => fighters.Contains(p.Id)).ToList();
        var departments = fighterStates.SelectMany(p => p.Departments).Select(d => c.Get(d.CardId).Key).ToHashSet();
        var languages = fighterStates.SelectMany(p => p.Languages).Select(l => c.Get(l.CardId).Key).ToHashSet();
        var monster = monsterDefs.Sum(d => d.Level ?? 0)
            + modifiers.Sum(e => e.Levels)
            + combat.OneShots.Where(o => o.Side == Side.Monster).Sum(o => CombatBonusOf(c, o.Card))
            + monsterDefs.SelectMany(d => d.Effects).OfType<ConditionalCombatBonus>()
                .Where(e => (e.PlayerDepartment is not null && departments.Contains(e.PlayerDepartment))
                    || (e.PlayerLanguage is not null && languages.Contains(e.PlayerLanguage)))
                .Sum(e => e.Amount)
            + combat.ManualMonsterAdjust;

        var tiesWin = passive.Values.SelectMany(e => e).Any(e => e is TiesWin);
        return new CombatSummary(
            player,
            monster,
            tiesWin,
            player > monster || (player == monster && tiesWin),
            monsterDefs.Sum(d => d.RewardLevels),
            monsterDefs.Sum(d => d.RewardLoot) + modifiers.Sum(e => e.ExtraLoot));
    }

    /// <summary>Stufe + Bonus der getragenen Ausrüstung + passive Kampfboni.</summary>
    public static int BasePower(GameState s, CardCatalog c, string playerId)
    {
        var player = s.Players.First(p => p.Id == playerId);
        var gear = player.Equipped
            .Where(card => PassiveEffects.IsActive(c, player, card))
            .Sum(card => c.Get(card.CardId) is { Kind: CardKind.Equipment } d ? d.Bonus : 0);
        return player.Level + gear + PassiveEffects.Of(s, c, playerId).OfType<CombatBonus>().Sum(e => e.Amount);
    }

    private static int CombatBonusOf(CardCatalog c, CardInstance card) =>
        c.Get(card.CardId).Effects.OfType<CombatBonus>().Sum(e => e.Amount);
}
