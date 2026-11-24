using System.Text.RegularExpressions;
using Bunit;
using DeveloperDungeon.Web.Pages;

namespace DeveloperDungeon.Web.Tests.Pages;

public class RulesTests : BunitContext
{
    [Theory]
    [InlineData("Haben mehrere Personen gleich die niedrigste Stufe, werden die überzähligen Karten unter ihnen aufgeteilt.")]
    [InlineData("Die helfende Person steigt keine Stufe auf, außer eine Fähigkeit erlaubt es.")]
    [InlineData("Geht der Kampf verloren, flieht sie selbst")]
    [InlineData("standardmäßig 5 Sekunden, einstellbar in den Einstellungen")]
    public void Rules_ExplainDetails(string text)
    {
        var cut = Render<Rules>();

        Assert.Contains(text, Regex.Replace(string.Concat(cut.Nodes.Select(n => n.TextContent)), @"\s+", " "));
    }
}
