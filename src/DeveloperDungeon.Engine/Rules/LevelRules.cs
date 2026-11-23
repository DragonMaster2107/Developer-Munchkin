using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

/// <summary>Gemeinsame Stufenlogik: Stufe setzen und Spielende bei Stufe 10.</summary>
internal static class LevelRules
{
    public const int WinLevel = 10;

    public static void Set(GameState s, PlayerState player, int to, List<GameEvent> events)
    {
        if (to == player.Level)
        {
            return;
        }
        events.Add(new LevelChanged(player.Id, player.Level, to));
        GameLog.Add(s, $"{player.Name}: Stufe {player.Level} → {to}.");
        player.Level = to;
    }

    /// <summary>Beendet das Spiel, wenn der Spieler Stufe 10 erreicht hat; liefert, ob das Spiel zu Ende ist.</summary>
    public static bool TryWin(GameState s, PlayerState player, List<GameEvent> events)
    {
        if (player.Level < WinLevel)
        {
            return false;
        }
        s.WinnerId = player.Id;
        s.Phase = GamePhase.GameOver;
        events.Add(new GameWon(player.Id));
        GameLog.Add(s, $"{player.Name} erreicht Stufe {WinLevel} und gewinnt das Spiel.");
        return true;
    }
}
