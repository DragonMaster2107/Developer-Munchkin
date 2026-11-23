namespace DeveloperDungeon.Engine.Cards;

// Properties mit Standardwert nutzen set statt init: der JSON-Source-Generator
// würde bei init-Properties den Initialwert (z. B. Count = 1) verwerfen.
public sealed record CardDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Deck Deck { get; init; }
    public CardKind Kind { get; init; }
    public string Flavor { get; set; } = "";
    public string RulesText { get; set; } = "";
    public int Count { get; set; } = 1;
    public IReadOnlyList<Effect> Effects { get; set; } = [];
    public IReadOnlyList<string> Tags { get; set; } = [];

    // Monster
    public int? Level { get; init; }
    public int RewardLevels { get; init; }
    public int RewardLoot { get; init; }
    public IReadOnlyList<Effect> BadStuff { get; set; } = [];
    public int? NoPursuitBelowLevel { get; init; }

    // Ausrüstung
    public Slot Slot { get; init; }
    public int Hands { get; init; }
    public int Bonus { get; init; }
    public int Value { get; init; }
    public string? RequiresDepartment { get; init; }
    public string? RequiresLanguage { get; init; }

    // Abteilung / Sprache
    public string? Key { get; init; }
}
