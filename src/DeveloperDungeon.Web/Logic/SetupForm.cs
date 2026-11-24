using DeveloperDungeon.Engine;

namespace DeveloperDungeon.Web.Logic;

public sealed class SetupRow
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
}

public sealed class SetupForm
{
    public const int MinPlayers = 3;
    public const int MaxPlayers = 6;

    public List<SetupRow> Rows { get; } = [];

    public SetupForm()
    {
        for (var i = 0; i < MinPlayers; i++)
        {
            AddPlayer();
        }
    }

    public void AddPlayer()
    {
        if (Rows.Count >= MaxPlayers)
        {
            return;
        }
        var free = PlayerColors.All.First(c => Rows.All(r => r.Color != c.Hex));
        Rows.Add(new SetupRow { Color = free.Hex });
    }

    public void RemovePlayer(int index)
    {
        if (Rows.Count > MinPlayers && index >= 0 && index < Rows.Count)
        {
            Rows.RemoveAt(index);
        }
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var names = Rows.Select(r => r.Name.Trim()).ToList();
        if (names.Any(n => n.Length == 0))
        {
            errors.Add("Jeder Spieler braucht einen Namen.");
        }
        var filled = names.Where(n => n.Length > 0).ToList();
        if (filled.Count != filled.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            errors.Add("Jeder Name darf nur einmal vorkommen.");
        }
        if (Rows.Select(r => r.Color).Distinct().Count() != Rows.Count)
        {
            errors.Add("Jede Farbe darf nur einmal vorkommen.");
        }
        return errors;
    }

    public IReadOnlyList<PlayerSetup> ToPlayers() =>
        [.. Rows.Select(r => new PlayerSetup(r.Name.Trim(), r.Color))];
}
