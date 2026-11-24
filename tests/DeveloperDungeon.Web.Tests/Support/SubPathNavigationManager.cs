using Microsoft.AspNetCore.Components;

namespace DeveloperDungeon.Web.Tests.Support;

/// <summary>Navigation wie unter GitHub Pages: die App liegt unter einem Unterpfad, "/" verlässt sie.</summary>
public sealed class SubPathNavigationManager : NavigationManager
{
    public const string Base = "http://localhost/Developer-Munchkin/";

    public SubPathNavigationManager(string relativeUri = "spiel") => Initialize(Base, Base + relativeUri);

    protected override void NavigateToCore(string uri, NavigationOptions options) => Uri = ToAbsoluteUri(uri).ToString();
}
