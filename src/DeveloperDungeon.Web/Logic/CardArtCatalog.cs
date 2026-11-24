using System.Text.Json;
using DeveloperDungeon.Engine.Cards;

namespace DeveloperDungeon.Web.Logic;

/// <summary>Ordnet Karten ihre Grafik zu; fehlende Grafiken werden durch Platzhalter nach Kartenart ersetzt.</summary>
public sealed class CardArtCatalog
{
    private static readonly string[] Extensions = ["svg", "webp", "png"];
    private readonly Dictionary<string, string> _extensionById;

    private CardArtCatalog(Dictionary<string, string> extensionById) => _extensionById = extensionById;

    public static CardArtCatalog Parse(string json)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    var ext = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                    if (ext is not null && Extensions.Contains(ext))
                    {
                        map[property.Name] = ext;
                    }
                }
            }
        }
        catch (JsonException)
        {
            map.Clear();
        }

        return new CardArtCatalog(map);
    }

    public string UrlFor(CardDefinition card)
    {
        if (_extensionById.TryGetValue(card.Id, out var ext))
        {
            return $"cards/{card.Id}.{ext}";
        }

        var placeholder = card.Kind switch
        {
            CardKind.Monster => "monster",
            CardKind.Curse => "curse",
            _ => card.Deck == Deck.Loot ? "loot" : "ticket",
        };
        return $"cards/_placeholder-{placeholder}.svg";
    }
}
