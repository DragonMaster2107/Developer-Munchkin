using DeveloperDungeon.Web.Tests.Logic;

namespace DeveloperDungeon.Web.Tests;

public class WwwrootTests
{
    private static readonly string Wwwroot = CardArtCatalogTests.FindWwwroot();

    [Theory]
    [InlineData(@"/\.svg$/")]
    [InlineData(@"/\.webp$/")]
    public void PublishedServiceWorker_CachesCardImagesOffline(string pattern)
    {
        var script = File.ReadAllText(Path.Combine(Wwwroot, "service-worker.published.js"));
        var include = script.Split('\n').Single(l => l.Contains("const offlineAssetsInclude"));

        Assert.Contains(pattern, include);
    }

    /// <summary>Konvention für Kartengrafiken: Jede SVG schaltet ihre Animationen bei reduzierter Bewegung ab.</summary>
    [Fact]
    public void EveryCardSvg_RespectsReducedMotion()
    {
        var files = Directory.GetFiles(Path.Combine(Wwwroot, "cards"), "*.svg");

        Assert.NotEmpty(files);
        Assert.All(files, file => Assert.Contains("prefers-reduced-motion", File.ReadAllText(file)));
    }
}
