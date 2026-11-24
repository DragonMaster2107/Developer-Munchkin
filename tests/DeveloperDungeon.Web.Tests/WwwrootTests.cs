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
}
