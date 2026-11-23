using System.Text.Json.Serialization;
using DeveloperDungeon.Engine.Serialization;

namespace DeveloperDungeon.Engine.State;

[JsonConverter(typeof(CamelCaseEnumConverter<ChoiceKind>))]
public enum ChoiceKind { DiscardSmallItem, GiveEquippedItem }

public sealed class PendingChoice
{
    public string PlayerId { get; set; } = "";
    public ChoiceKind Kind { get; set; }
    public string? RecipientPlayerId { get; set; }
    public string SourceCardId { get; set; } = "";
}
