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

    // Nicht readonly: nach einer Ausnahme in Execute wird der Zustand aus dem Schnappschuss wiederhergestellt.
    private GameState _state;
    private GameRandom _rng;
    private readonly Queue<int> _forcedRolls = new();

    private Game(GameState state, CardCatalog catalog, GameRandom rng)
    {
        _state = state;
        Catalog = catalog;
        _rng = rng;
    }

    public CardCatalog Catalog { get; }

    /// <summary>Der lebende Zustand (nur engine-intern und für Tests); <c>RngState</c> ist dabei stets aktuell. Öffentlich: <see cref="GetView"/> und <see cref="Save"/>.</summary>
    internal GameState State
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

    /// <summary>Lädt einen Spielstand. Ist er beschädigt oder passt er nicht zum Katalog, wird eine <see cref="InvalidDataException"/> geworfen.</summary>
    public static Game Load(string json, CardCatalog catalog)
    {
        GameState state;
        try
        {
            state = Deserialize(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Der Spielstand ist beschädigt: {ex.Message}", ex);
        }

        var problem = FindLoadProblem(state, catalog);
        if (problem is not null)
        {
            throw new InvalidDataException($"Der Spielstand kann nicht geladen werden: {problem}");
        }
        return new Game(state, catalog, new GameRandom(state.RngState));
    }

    private static string? FindLoadProblem(GameState s, CardCatalog catalog)
    {
        if (s.SchemaVersion != GameState.CurrentSchemaVersion)
        {
            return $"unbekannte Version {s.SchemaVersion} (erwartet {GameState.CurrentSchemaVersion}).";
        }
        if (s.Players is null || s.Players.Count is < MinPlayers or > MaxPlayers)
        {
            return $"Ein Spiel braucht {MinPlayers} bis {MaxPlayers} Spieler.";
        }
        if (s.ActivePlayerIndex < 0 || s.ActivePlayerIndex >= s.Players.Count)
        {
            return "Der aktive Spieler existiert nicht.";
        }

        var known = catalog.All.Select(d => d.Id).ToHashSet();
        var seen = new HashSet<int>();
        foreach (var card in AllCards(s))
        {
            if (card is null || !known.Contains(card.CardId))
            {
                return $"Unbekannte Karte '{card?.CardId}'.";
            }
            if (!seen.Add(card.InstanceId))
            {
                return $"Die Karten-Instanz {card.InstanceId} liegt mehrfach im Spiel.";
            }
        }
        return null;
    }

    /// <summary>Alle Karteninstanzen in Stapeln, Ablagen, Spielerzonen und im Kampf.</summary>
    private static IEnumerable<CardInstance?> AllCards(GameState s)
    {
        var zones = new List<List<CardInstance>?> { s.TicketDeck, s.TicketDiscard, s.LootDeck, s.LootDiscard };
        foreach (var p in s.Players)
        {
            zones.AddRange([p.Hand, p.Equipped, p.Backpack, p.Departments, p.Languages]);
        }
        if (s.Combat is { } combat)
        {
            zones.Add(combat.Monsters);
            zones.Add(combat.OtherPlayed);
            zones.AddRange(combat.Enhancers?.Values ?? Enumerable.Empty<List<CardInstance>>());
            zones.Add(combat.OneShots?.Select(o => o.Card).ToList());
        }
        return zones.SelectMany(z => z ?? []);
    }

    public string Save() => JsonSerializer.Serialize(State, EngineJsonContext.Default.GameState);

    /// <summary>Spielersicht als Schnappschuss (teilt keine veränderlichen Objekte mit dem Zustand); die Hand ist nur für <paramref name="viewerId"/> enthalten.</summary>
    public GameView GetView(string? viewerId)
    {
        var s = State;
        var players = s.Players
            .Select(p => new PublicPlayerView(
                p.Id, p.Name, p.Color, p.Level, LevelTitles.For(p.Level), p.Hand.Count,
                Copy(p.Equipped), Copy(p.Backpack), Copy(p.Departments), Copy(p.Languages),
                StrengthCalculator.BasePower(s, Catalog, p.Id)))
            .ToList();
        var myHand = viewerId is null ? null : s.Players.FirstOrDefault(p => p.Id == viewerId)?.Hand;
        var summary = StrengthCalculator.TrySummarize(s, Catalog);
        var combat = s.Combat is null || summary is null ? null : new CombatView(CopyCombat(s.Combat), summary);
        var choice = s.PendingChoices.FirstOrDefault();

        return new GameView(
            s.Phase,
            s.Players[s.ActivePlayerIndex].Id,
            s.TurnNumber,
            players,
            myHand is null ? null : Copy(myHand),
            combat,
            s.TicketDeck.Count,
            s.LootDeck.Count,
            Copy(s.TicketDiscard.LastOrDefault()),
            Copy(s.LootDiscard.LastOrDefault()),
            choice is null ? null : new PendingChoice { PlayerId = choice.PlayerId, Kind = choice.Kind, RecipientPlayerId = choice.RecipientPlayerId, SourceCardId = choice.SourceCardId },
            s.WinnerId,
            [.. s.Log.TakeLast(100).Select(l => new LogEntry { Seq = l.Seq, Text = l.Text })]);
    }

    private static List<CardInstance> Copy(List<CardInstance> cards) => [.. cards.Select(c => Copy(c)!)];

    private static CardInstance? Copy(CardInstance? card) =>
        card is null ? null : new CardInstance { InstanceId = card.InstanceId, CardId = card.CardId };

    private static CombatState CopyCombat(CombatState combat) =>
        JsonSerializer.Deserialize(JsonSerializer.Serialize(combat, EngineJsonContext.Default.CombatState), EngineJsonContext.Default.CombatState)!;

    /// <summary>Prüft und wendet einen Befehl an. Wirft nie: eine interne Ausnahme stellt den Zustand vor dem Befehl wieder her und liefert eine Ablehnung.</summary>
    public CommandResult Execute(GameCommand command)
    {
        var snapshot = Save();
        try
        {
            return Dispatch(command);
        }
        catch (Exception ex)
        {
            _state = Deserialize(snapshot);
            _rng = new GameRandom(_state.RngState);
            return CommandResult.Rejected($"Interner Fehler: {ex.Message}");
        }
    }

    private static GameState Deserialize(string json) =>
        JsonSerializer.Deserialize(json, EngineJsonContext.Default.GameState)
            ?? throw new JsonException("Leerer Spielstand.");

    private CommandResult Dispatch(GameCommand command)
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
