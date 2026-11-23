using DeveloperDungeon.Engine.State;

namespace DeveloperDungeon.Engine.Rules;

internal static class GameLog
{
    public static void Add(GameState s, string text)
    {
        var seq = s.Log.Count == 0 ? 1 : s.Log[^1].Seq + 1;
        s.Log.Add(new LogEntry { Seq = seq, Text = text });
    }
}
