using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Tests.Support;

internal sealed class GameBuilder
{
    private readonly int _players;
    private readonly ulong _seed;
    private readonly List<Action<Game>> _steps = [];
    private GamePhase _phase = GamePhase.Setup;

    private GameBuilder(int players, ulong seed)
    {
        _players = players;
        _seed = seed;
    }

    public static GameBuilder Create(int players = 3, ulong seed = 42) => new(players, seed);

    public GameBuilder Hand(int p, params string[] cardIds) => Add(g => Move(g, cardIds, g.State.Players[p - 1].Hand));
    public GameBuilder Equip(int p, params string[] cardIds) => Add(g => Move(g, cardIds, g.State.Players[p - 1].Equipped));
    public GameBuilder Backpack(int p, params string[] ids) => Add(g => Move(g, ids, g.State.Players[p - 1].Backpack));
    public GameBuilder Department(int p, string id) => Add(g => Move(g, [id], g.State.Players[p - 1].Departments));
    public GameBuilder Language(int p, string id) => Add(g => Move(g, [id], g.State.Players[p - 1].Languages));
    public GameBuilder Level(int p, int level) => Add(g => g.State.Players[p - 1].Level = level);
    public GameBuilder Active(int p) => Add(g => g.State.ActivePlayerIndex = p - 1);

    public GameBuilder Phase(GamePhase phase)
    {
        _phase = phase;
        return this;
    }

    /// <summary>Die erste Id liegt oben (Index 0).</summary>
    public GameBuilder TicketTop(params string[] ids) => Add(g => MoveToTop(g, ids, g.State.TicketDeck));
    public GameBuilder LootTop(params string[] ids) => Add(g => MoveToTop(g, ids, g.State.LootDeck));

    /// <summary>Legt alle Handkarten unter die jeweiligen Stapel zurueck.</summary>
    public GameBuilder ClearHands() => Add(g =>
    {
        var s = g.State;
        foreach (var p in s.Players)
        {
            foreach (var card in p.Hand)
            {
                (g.Catalog.Get(card.CardId).Deck == Deck.Ticket ? s.TicketDeck : s.LootDeck).Add(card);
            }
            p.Hand.Clear();
        }
    });

    public Game Build()
    {
        var setups = Enumerable.Range(1, _players).Select(i => new PlayerSetup($"Spieler {i}", $"farbe{i}")).ToList();
        var game = Game.New(setups, TestData.Catalog, _seed);
        foreach (var step in _steps)
        {
            step(game);
        }
        game.State.Phase = _phase;
        return game;
    }

    private GameBuilder Add(Action<Game> step)
    {
        _steps.Add(step);
        return this;
    }

    private static CardInstance TakeFree(GameState s, string cardId)
    {
        foreach (var pile in new[] { s.TicketDeck, s.TicketDiscard, s.LootDeck, s.LootDiscard })
        {
            var card = pile.FirstOrDefault(c => c.CardId == cardId);
            if (card is not null)
            {
                pile.Remove(card);
                return card;
            }
        }
        throw new InvalidOperationException($"Keine freie Instanz von '{cardId}' mehr in Stapeln oder Ablagen.");
    }

    private static void Move(Game g, string[] ids, List<CardInstance> target)
    {
        foreach (var id in ids)
        {
            target.Add(TakeFree(g.State, id));
        }
    }

    private static void MoveToTop(Game g, string[] ids, List<CardInstance> deck)
    {
        var cards = ids.Select(id => TakeFree(g.State, id)).ToList();
        deck.InsertRange(0, cards);
    }
}
