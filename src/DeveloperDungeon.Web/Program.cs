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

await builder.Build().RunAsync();
