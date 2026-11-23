using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Views;

namespace DeveloperDungeon.Engine.Sessions;

public sealed class LocalGameSession(Game game, Action<string>? onSaved = null) : IGameSession
{
    public CardCatalog Catalog => game.Catalog;

    public event Action<IReadOnlyList<GameEvent>>? StateChanged;

    public GameView GetView(string? viewerId) => game.GetView(viewerId);

    public Task<CommandResult> SendAsync(GameCommand command)
    {
        var result = game.Execute(command);
        if (result.Accepted)
        {
            onSaved?.Invoke(game.Save());
            StateChanged?.Invoke(result.Events);
        }
        return Task.FromResult(result);
    }

    public Task<CommandResult> CheckAsync(GameCommand command) => Task.FromResult(game.Preview(command));
}
