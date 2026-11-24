using DeveloperDungeon.Web.Logic;

namespace DeveloperDungeon.Web.Tests.Logic;

public class SetupFormTests
{
    [Fact]
    public void Starts_WithThreeRows_DistinctColors()
    {
        var form = new SetupForm();

        Assert.Equal(3, form.Rows.Count);
        Assert.Equal(3, form.Rows.Select(r => r.Color).Distinct().Count());
        Assert.All(form.Rows, r => Assert.Contains(PlayerColors.All, c => c.Hex == r.Color));
    }

    [Fact]
    public void AddPlayer_StopsAtSix()
    {
        var form = new SetupForm();
        for (var i = 0; i < 10; i++)
        {
            form.AddPlayer();
        }

        Assert.Equal(6, form.Rows.Count);
        Assert.Equal(6, form.Rows.Select(r => r.Color).Distinct().Count());
    }

    [Fact]
    public void RemovePlayer_StopsAtThree()
    {
        var form = new SetupForm();
        form.AddPlayer();
        form.AddPlayer();
        for (var i = 0; i < 10; i++)
        {
            form.RemovePlayer(0);
        }

        Assert.Equal(3, form.Rows.Count);
    }

    [Fact]
    public void Validate_EmptyName()
    {
        var form = Filled("Anna", "  ", "Cem");

        Assert.Contains("Jeder Spieler braucht einen Namen.", form.Validate());
    }

    [Fact]
    public void Validate_DuplicateNameIgnoringCase()
    {
        var form = Filled("Anna", "anna ", "Cem");

        Assert.Contains("Jeder Name darf nur einmal vorkommen.", form.Validate());
    }

    [Fact]
    public void Validate_DuplicateColor()
    {
        var form = Filled("Anna", "Ben", "Cem");
        form.Rows[1].Color = form.Rows[0].Color;

        Assert.Contains("Jede Farbe darf nur einmal vorkommen.", form.Validate());
    }

    [Fact]
    public void Validate_ValidForm_HasNoErrors()
    {
        Assert.Empty(Filled("Anna", "Ben", "Cem").Validate());
    }

    [Fact]
    public void ToPlayers_TrimsNames()
    {
        var players = Filled("  Anna ", "Ben", "Cem").ToPlayers();

        Assert.Equal("Anna", players[0].Name);
        Assert.Equal(3, players.Count);
    }

    private static SetupForm Filled(params string[] names)
    {
        var form = new SetupForm();
        for (var i = 0; i < names.Length; i++)
        {
            form.Rows[i].Name = names[i];
        }
        return form;
    }
}
