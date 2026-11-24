using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Rules;
using DeveloperDungeon.Engine.Views;

namespace DeveloperDungeon.Web.Logic;

public enum CueKind { MonsterSlam, ScreenShake, LevelUp, LevelDown, CardFly, EscapeSuccess, EscapeFail, Victory }

public sealed record AnimationCue(CueKind Kind, string Text, int DurationMs);

/// <summary>Übersetzt Spielereignisse in kurze Animations-Cues (Text plus Dauer).</summary>
public static class AnimationCues
{
    public static IReadOnlyList<AnimationCue> From(IReadOnlyList<GameEvent> events, GameView view, CardCatalog catalog)
    {
        string NameOf(string playerId) => view.Players.FirstOrDefault(p => p.Id == playerId)?.Name ?? playerId;

        var cues = new List<AnimationCue>();
        foreach (var e in events)
        {
            AnimationCue? cue = e switch
            {
                CombatStarted s => new(CueKind.MonsterSlam, $"{catalog.Get(s.MonsterCardId).Name}!", 900),
                CursePlayed c => new(CueKind.ScreenShake, $"Fluch: {catalog.Get(c.CardId).Name}", 700),
                LevelChanged l when l.To > l.From => new(CueKind.LevelUp, $"{NameOf(l.PlayerId)}: {LevelTitles.For(l.To)}", 1500),
                LevelChanged l when l.To < l.From => new(CueKind.LevelDown, $"{NameOf(l.PlayerId)}: Stufe {l.To}", 900),
                CardPlayedInCombat p => new(CueKind.CardFly, catalog.Get(p.CardId).Name, 600),
                // Wurf 0: ohne Würfeln entkommen (z. B. durch eine Fluchtkarte).
                EscapeRolled { Roll: 0 } => new(CueKind.EscapeSuccess, "Entkommen", 800),
                EscapeRolled r => new(r.Escaped ? CueKind.EscapeSuccess : CueKind.EscapeFail, $"Wurf {r.Roll}", 800),
                GameWon w => new(CueKind.Victory, $"{NameOf(w.PlayerId)} ist CTO!", 1500),
                _ => null,
            };
            if (cue is not null)
            {
                cues.Add(cue);
            }
        }
        return cues;
    }

    private static readonly CueKind[] Priority =
        [CueKind.Victory, CueKind.LevelUp, CueKind.MonsterSlam, CueKind.ScreenShake, CueKind.EscapeFail, CueKind.EscapeSuccess, CueKind.LevelDown, CueKind.CardFly];

    public const int MaxPerBatch = 2;

    /// <summary>Wählt höchstens zwei Cues nach Wichtigkeit aus und behält deren ursprüngliche Reihenfolge.</summary>
    public static IReadOnlyList<AnimationCue> SelectForPlayback(IReadOnlyList<AnimationCue> cues) =>
        cues.Count <= MaxPerBatch
            ? cues
            : [.. cues
                .Select((cue, index) => (cue, index))
                .OrderBy(x => Array.IndexOf(Priority, x.cue.Kind)).ThenBy(x => x.index)
                .Take(MaxPerBatch)
                .OrderBy(x => x.index)
                .Select(x => x.cue)];
}
