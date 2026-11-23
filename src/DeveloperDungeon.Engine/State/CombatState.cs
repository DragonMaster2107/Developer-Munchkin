using System.Text.Json.Serialization;
using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Serialization;

namespace DeveloperDungeon.Engine.State;

[JsonConverter(typeof(CamelCaseEnumConverter<CombatStage>))]
public enum CombatStage { Fighting, Fleeing }

public sealed class OneShotPlay
{
    public CardInstance Card { get; set; } = new();
    public Side Side { get; set; }
    public string PlayedBy { get; set; } = "";
}

public sealed class HelpOffer
{
    public string PlayerId { get; set; } = "";
    public int LootShare { get; set; }
}

public sealed class FleeingPlayer
{
    public string PlayerId { get; set; } = "";
    public List<int> UnresolvedMonsterIds { get; set; } = [];
}

public sealed class CombatState
{
    public CombatStage Stage { get; set; }
    public List<CardInstance> Monsters { get; set; } = [];
    /// <summary>Schlüssel = InstanceId des Monsters.</summary>
    public Dictionary<int, List<CardInstance>> Enhancers { get; set; } = [];
    public List<OneShotPlay> OneShots { get; set; } = [];
    /// <summary>Sonstige gespielte Karten, z. B. Wandernder Bug.</summary>
    public List<CardInstance> OtherPlayed { get; set; } = [];
    public string? HelperId { get; set; }
    public int HelperLootShare { get; set; }
    public List<HelpOffer> Offers { get; set; } = [];
    public int ManualPlayerAdjust { get; set; }
    public int ManualMonsterAdjust { get; set; }
    public List<FleeingPlayer> Fleeing { get; set; } = [];
}
