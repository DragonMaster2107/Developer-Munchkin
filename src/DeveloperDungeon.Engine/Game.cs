using System.Text.Json;
using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.Serialization;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine;

public record PlayerSetup(string Name, string Color);

public sealed class Game
{
    private const int MinPlayers = 3;
    private const int MaxPlayers = 6;
    private const int StartCardsPerDeck = 4;

    private readonly GameState _state;
    private readonly GameRandom _rng;

    private Game(GameState state, CardCatalog catalog, GameRandom rng)
    {
        _state = state;
        Catalog = catalog;
        _rng = rng;
    }

    public CardCatalog Catalog { get; }

    /// <summary>Der Zustand; <c>RngState</c> ist dabei stets aktuell.</summary>
    public GameState State
    {
        get
        {
            _state.RngState = _rng.State;
            return _state;
        }
    }

    internal GameRandom Rng => _rng;

    public static Game New(IReadOnlyList<PlayerSetup> players, CardCatalog catalog, ulong seed)
    {
        if (players.Count is < MinPlayers or > MaxPlayers)
        {
            throw new ArgumentException($"Ein Spiel braucht {MinPlayers} bis {MaxPlayers} Spieler.", nameof(players));
        }

        var state = new GameState();
        var rng = new GameRandom(seed);
        var game = new Game(state, catalog, rng);

        var nextId = 1;
        foreach (var card in catalog.All)
        {
            var target = card.Deck == Deck.Ticket ? state.TicketDeck : state.LootDeck;
            for (var i = 0; i < card.Count; i++)
            {
                target.Add(new CardInstance { InstanceId = nextId++, CardId = card.Id });
            }
        }
        rng.Shuffle(state.TicketDeck);
        rng.Shuffle(state.LootDeck);

        for (var i = 0; i < players.Count; i++)
        {
            state.Players.Add(new PlayerState
            {
                Id = $"p{i + 1}",
                Name = players[i].Name,
                Color = players[i].Color,
            });
        }

        foreach (var player in state.Players)
        {
            for (var i = 0; i < StartCardsPerDeck; i++)
            {
                AddIfNotNull(player.Hand, Decks.Draw(state, rng, Deck.Ticket));
                AddIfNotNull(player.Hand, Decks.Draw(state, rng, Deck.Loot));
            }
        }

        state.ActivePlayerIndex = rng.Next(players.Count);
        state.Phase = GamePhase.Setup;
        state.TurnNumber = 0;
        state.Log.Add(new LogEntry { Seq = 1, Text = $"Neues Spiel mit {players.Count} Spielern" });

        return game;
    }

    public static Game Load(string json, CardCatalog catalog)
    {
        var state = JsonSerializer.Deserialize(json, EngineJsonContext.Default.GameState)
            ?? throw new JsonException("Leerer Spielstand.");
        return new Game(state, catalog, new GameRandom(state.RngState));
    }

    public string Save() => JsonSerializer.Serialize(State, EngineJsonContext.Default.GameState);

    public CommandResult Execute(GameCommand command)
    {
        var rejection = CommandGuard.Check(_state, command);
        var result = rejection is not null
            ? CommandResult.Rejected(rejection)
            : command switch
            {
                StartGame cmd => StartRules.Handle(_state, Catalog, _rng, cmd),
                EquipCard cmd => EquipRules.Handle(_state, Catalog, _rng, cmd),
                UnequipCard cmd => EquipRules.Handle(_state, Catalog, _rng, cmd),
                DiscardOwnCard cmd => EquipRules.Handle(_state, Catalog, _rng, cmd),
                _ => CommandResult.Rejected("Unbekannter Befehl."),
            };

        if (result.Accepted)
        {
            _state.RngState = _rng.State;
        }
        return result;
    }

    private static void AddIfNotNull(List<CardInstance> list, CardInstance? card)
    {
        if (card is not null)
        {
            list.Add(card);
        }
    }
}
