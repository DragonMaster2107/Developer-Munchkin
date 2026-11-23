using System.Text.Json;
using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Random;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.Serialization;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Views;

namespace DeveloperDungeon.Engine;

public record PlayerSetup(string Name, string Color);

public sealed class Game
{
    private const int MinPlayers = 3;
    private const int MaxPlayers = 6;
    private const int StartCardsPerDeck = 4;

    private readonly GameState _state;
    private readonly GameRandom _rng;
    private readonly Queue<int> _forcedRolls = new();

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

    /// <summary>Nur für Tests: W6-Ergebnisse, die vor dem Zufallsgenerator verbraucht werden; nicht Teil des Spielstands.</summary>
    internal void ForceNextRolls(params int[] rolls)
    {
        foreach (var roll in rolls)
        {
            _forcedRolls.Enqueue(roll);
        }
    }

    private int RollD6() => _forcedRolls.Count > 0 ? _forcedRolls.Dequeue() : _rng.RollD6();

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

    /// <summary>Spielersicht; die Hand ist nur für <paramref name="viewerId"/> enthalten.</summary>
    public GameView GetView(string? viewerId)
    {
        var s = State;
        var players = s.Players
            .Select(p => new PublicPlayerView(
                p.Id, p.Name, p.Color, p.Level, LevelTitles.For(p.Level), p.Hand.Count,
                [.. p.Equipped], [.. p.Backpack], [.. p.Departments], [.. p.Languages],
                StrengthCalculator.BasePower(s, Catalog, p.Id)))
            .ToList();
        var myHand = viewerId is null ? null : s.Players.FirstOrDefault(p => p.Id == viewerId)?.Hand;
        var combat = s.Combat is null ? null : new CombatView(s.Combat, StrengthCalculator.Summarize(s, Catalog));

        return new GameView(
            s.Phase,
            s.Players[s.ActivePlayerIndex].Id,
            s.TurnNumber,
            players,
            myHand is null ? null : [.. myHand],
            combat,
            s.TicketDeck.Count,
            s.LootDeck.Count,
            s.TicketDiscard.LastOrDefault(),
            s.LootDiscard.LastOrDefault(),
            s.PendingChoices.FirstOrDefault(),
            s.WinnerId,
            [.. s.Log.TakeLast(100)]);
    }

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
                PlayCard cmd => CardPlayRules.Handle(_state, Catalog, _rng, cmd),
                ResolveCombat cmd => CombatRules.Handle(_state, Catalog, _rng, cmd),
                OfferHelp cmd => CombatRules.Handle(_state, cmd),
                AcceptHelp cmd => CombatRules.Handle(_state, cmd),
                AttemptEscape cmd => EscapeRules.Handle(_state, Catalog, _rng, RollD6, cmd),
                ResolveChoice cmd => ChoiceRules.Handle(_state, Catalog, cmd),
                SellItems cmd => EconomyRules.Sell(_state, Catalog, cmd),
                ManualAdjustLevel or ManualMoveCard or ManualDiscard or ManualTakeFromDiscard or ManualCombatAdjust or RollDie
                    => ToolboxRules.Handle(_state, Catalog, RollD6, command),
                OpenTicket or LookForTrouble or WorkBacklog or EndTurn => TurnRules.Handle(_state, Catalog, _rng, command),
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
