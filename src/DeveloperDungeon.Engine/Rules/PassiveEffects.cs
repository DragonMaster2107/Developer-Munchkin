using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class PassiveEffects
{
    /// <summary>Effekte aller getragenen Karten, Abteilungen und Sprachen (nicht Hand/Rucksack). <paramref name="excludeInstanceId"/> blendet eine Karte aus.</summary>
    public static IEnumerable<Effect> Of(GameState s, CardCatalog c, string playerId, int? excludeInstanceId = null)
    {
        var player = s.Players.First(p => p.Id == playerId);
        return player.Equipped.Concat(player.Departments).Concat(player.Languages)
            .Where(card => card.InstanceId != excludeInstanceId)
            .SelectMany(card => c.Get(card.CardId).Effects);
    }

    public static int MaxDepartments(GameState s, CardCatalog c, string playerId, int? excludeInstanceId = null) =>
        Math.Max(1, Of(s, c, playerId, excludeInstanceId).OfType<MaxDepartments>().Select(e => e.Max).DefaultIfEmpty(1).Max());

    public static int MaxLanguages(GameState s, CardCatalog c, string playerId, int? excludeInstanceId = null) =>
        Math.Max(1, Of(s, c, playerId, excludeInstanceId).OfType<MaxLanguages>().Select(e => e.Max).DefaultIfEmpty(1).Max());
}
