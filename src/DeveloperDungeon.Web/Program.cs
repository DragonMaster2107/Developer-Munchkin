using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Web;
using DeveloperDungeon.Web.Logic;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<IGameStorage, BrowserGameStorage>();
builder.Services.AddScoped<SettingsStore>();
builder.Services.AddScoped<TableController>();
builder.Services.AddSingleton(CardCatalog.LoadEmbedded());

builder.Services.AddSingleton(await LoadCardArtAsync(builder.HostEnvironment.BaseAddress));

await builder.Build().RunAsync();

// Grafikzuordnung beim Start laden; bei jedem Fehler nur Platzhalter verwenden.
static async Task<CardArtCatalog> LoadCardArtAsync(string baseAddress)
{
    try
    {
        using var http = new HttpClient { BaseAddress = new Uri(baseAddress) };
        return CardArtCatalog.Parse(await http.GetStringAsync("cards/art.json"));
    }
    catch (Exception)
    {
        return CardArtCatalog.Parse("{}");
    }
}
