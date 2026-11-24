using DeveloperDungeon.Engine.Cards;

namespace DeveloperDungeon.Web.Logic;

/// <summary>Deutsche Kopf- und Fußtexte einer Karte, wie in der Stilreferenz.</summary>
public static class CardTexts
{
    public static string KindLabel(CardKind kind) => kind switch
    {
        CardKind.Monster => "Monster",
        CardKind.MonsterEnhancer => "Verstärker",
        CardKind.Curse => "Fluch",
        CardKind.Department => "Abteilung",
        CardKind.Language => "Sprache",
        CardKind.Special => "Sonderkarte",
        CardKind.Equipment => "Ausrüstung",
        CardKind.OneShot => "Loot · Einmalig",
        CardKind.LevelUp => "Stufe hoch",
        _ => kind.ToString(),
    };

    public static string HeaderBadge(CardDefinition c) => c.Kind switch
    {
        CardKind.Monster => c.Level is { } level ? $"Stufe {level}" : "",
        CardKind.Curse => "Sofort",
        CardKind.Equipment => $"{SlotName(c)} · +{c.Bonus}",
        _ => "",
    };

    public static string FooterLeft(CardDefinition c) => c.Kind switch
    {
        CardKind.Monster => $"{c.RewardLevels} {(c.RewardLevels == 1 ? "Stufe" : "Stufen")}",
        CardKind.Curse => "Ticket-Stapel",
        CardKind.Equipment => "Ausrüstung",
        CardKind.OneShot => "Einwegkarte",
        _ => c.Deck == Deck.Loot ? "Loot-Stapel" : "Ticket-Stapel",
    };

    public static string FooterRight(CardDefinition c) => c.Kind switch
    {
        CardKind.Monster => $"{c.RewardLoot} Loot",
        CardKind.Equipment or CardKind.OneShot when c.Value > 0 => $"{c.Value} Credits",
        _ => "",
    };

    private static string SlotName(CardDefinition c) => c.Slot switch
    {
        Slot.Head => "Kopf",
        Slot.Body => "Körper",
        Slot.Feet => "Füße",
        Slot.Hands => c.Hands >= 2 ? "2 Hände" : "1 Hand",
        Slot.Big => "Groß",
        _ => "Klein",
    };
}
