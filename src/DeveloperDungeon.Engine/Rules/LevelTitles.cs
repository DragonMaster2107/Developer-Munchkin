namespace DeveloperDungeon.Engine.Rules;

public static class LevelTitles
{
    private static readonly string[] Titles =
    [
        "Unbezahlter Praktikant",
        "Werkstudent",
        "Junior Developer",
        "Developer",
        "Senior Developer",
        "Senior Developer mit Meinung",
        "Software Architect",
        "Principal Engineer",
        "Tech Lead",
        "CTO",
    ];

    public static string For(int level) => Titles[Math.Clamp(level, 1, Titles.Length) - 1];
}
