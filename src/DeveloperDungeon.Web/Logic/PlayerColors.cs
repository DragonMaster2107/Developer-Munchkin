namespace DeveloperDungeon.Web.Logic;

public static class PlayerColors
{
    public static IReadOnlyList<(string Hex, string Name)> All { get; } =
    [
        ("#e07a5f", "Koralle"),
        ("#3d85c6", "Blau"),
        ("#81b29a", "Salbei"),
        ("#f2b134", "Senf"),
        ("#9b5de5", "Lila"),
        ("#f15bb5", "Pink"),
    ];
}
