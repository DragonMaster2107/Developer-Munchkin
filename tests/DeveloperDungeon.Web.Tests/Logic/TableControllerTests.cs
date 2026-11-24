using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Web.Logic;
using DeveloperDungeon.Web.Tests.Support;

namespace DeveloperDungeon.Web.Tests.Logic;

public class TableControllerTests
{
    [Fact]
    public void NewGame_StartsSetupWithHiddenHandAndHandoffToStartPlayer()
    {
        var c = TestGames.NewController();

        Assert.True(c.HasGame);
        Assert.Equal(GamePhase.Setup, c.View.Phase);
        Assert.Equal(c.View.ActivePlayerId, c.HandoffTargetId);
        Assert.Equal(c.View.ActivePlayerId, c.SetupPlayerId);
        Assert.Null(c.View.MyHand);
        Assert.Null(c.RevealedPlayerId);
    }

    [Fact]
    public void ConfirmHandoff_RevealsOnlyThatPlayersHand()
    {
        var c = TestGames.NewController();
        var target = c.HandoffTargetId!;
        var counts = c.View.Players.Select(p => p.HandCount).ToList();

        c.ConfirmHandoff();

        Assert.Null(c.HandoffTargetId);
        Assert.Equal(target, c.RevealedPlayerId);
        Assert.NotNull(c.View.MyHand);
        Assert.Equal(c.View.Players.First(p => p.Id == target).HandCount, c.View.MyHand!.Count);
        Assert.Equal(counts, c.View.Players.Select(p => p.HandCount));
    }

    [Fact]
    public async Task FinishSetup_ForAllPlayers_StartsGame()
    {
        var c = TestGames.NewController(4);
        var order = new List<string>();
        while (c.SetupPlayerId is not null)
        {
            order.Add(c.SetupPlayerId);
            c.ConfirmHandoff();
            await c.FinishSetupForCurrentAsync();
        }

        Assert.Equal(4, order.Distinct().Count());
        Assert.Equal(order[0], c.View.ActivePlayerId);
        Assert.Equal(GamePhase.OpenTicket, c.View.Phase);
        Assert.Equal(c.View.ActivePlayerId, c.HandoffTargetId);
        Assert.Null(c.SetupPlayerId);
    }

    [Fact]
    public async Task NewGame_IsSavedToStorage()
    {
        var storage = new InMemoryGameStorage();
        TestGames.NewController(3, 42, storage);

        Assert.True(storage.Items.ContainsKey(StorageKeys.Save));
        var c2 = new TableController(TestGames.Catalog, storage);
        Assert.Equal(ResumeResult.Resumed, await c2.TryResumeAsync());
    }

    [Fact]
    public async Task AcceptedCommand_SavesState()
    {
        var storage = new InMemoryGameStorage();
        var c = await TestGames.StartedAsync(3, 42, storage);
        var before = storage.Items[StorageKeys.Save];

        c.ConfirmHandoff();
        var result = await c.SendAsync(new OpenTicket(c.View.ActivePlayerId));

        Assert.True(result.Accepted);
        Assert.NotEqual(before, storage.Items[StorageKeys.Save]);
        Assert.NotEmpty(c.LastEvents);
        Assert.Null(c.LastRejection);
    }

    [Fact]
    public async Task RejectedCommand_DoesNotSave_AndSetsLastRejection()
    {
        var storage = new InMemoryGameStorage();
        var c = await TestGames.StartedAsync(3, 42, storage);
        var before = storage.Items[StorageKeys.Save];
        var changed = 0;
        c.Changed += () => changed++;

        var result = await c.SendAsync(new WorkBacklog(c.View.ActivePlayerId));

        Assert.False(result.Accepted);
        Assert.Equal(result.Rejection, c.LastRejection);
        Assert.NotNull(c.LastRejection);
        Assert.Equal(before, storage.Items[StorageKeys.Save]);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task SaveFailure_SetsWarning_AndPlayContinues()
    {
        var storage = new ThrowingStorage();
        var c = await TestGames.StartedAsync(3, 42, storage);
        Assert.Equal("Spielstand konnte nicht gespeichert werden.", c.LastWarning);

        c.ConfirmHandoff();
        var result = await c.SendAsync(new OpenTicket(c.View.ActivePlayerId));
        Assert.True(result.Accepted);
        Assert.Equal("Spielstand konnte nicht gespeichert werden.", c.LastWarning);

        storage.Fail = false;
        var active = c.View.ActivePlayerId;
        await c.SendAsync(new ManualAdjustLevel(active, active, 1));
        Assert.Null(c.LastWarning);
    }

    [Fact]
    public async Task TurnEnded_HidesHandAndRequestsNextPlayer()
    {
        var (c, first) = await TestGames.AfterFirstTurnAsync();

        Assert.NotEqual(first, c.View.ActivePlayerId);
        Assert.Null(c.RevealedPlayerId);
        Assert.Null(c.View.MyHand);
        Assert.Equal(c.View.ActivePlayerId, c.HandoffTargetId);
    }

    [Fact]
    public async Task Interjection_RevealsInterjectorAndActsAsInterjector()
    {
        var c = await TestGames.StartedAsync();
        c.ConfirmHandoff();
        var other = c.View.Players.First(p => p.Id != c.View.ActivePlayerId).Id;

        c.BeginInterjection(other);

        Assert.Equal(other, c.InterjectorId);
        Assert.Equal(other, c.ActorId);
        Assert.Equal(other, c.HandoffTargetId);
        Assert.Null(c.RevealedPlayerId);
        c.ConfirmHandoff();
        Assert.Equal(other, c.RevealedPlayerId);
        Assert.NotNull(c.View.MyHand);
    }

    [Fact]
    public async Task EndInterjection_ReturnsDeviceToActive()
    {
        var c = await TestGames.StartedAsync();
        c.ConfirmHandoff();
        var other = c.View.Players.First(p => p.Id != c.View.ActivePlayerId).Id;
        c.BeginInterjection(other);
        c.ConfirmHandoff();

        c.EndInterjection();

        Assert.Null(c.InterjectorId);
        Assert.Equal(c.View.ActivePlayerId, c.HandoffTargetId);
        Assert.Null(c.RevealedPlayerId);
        Assert.Equal(c.View.ActivePlayerId, c.ActorId);
    }

    [Fact]
    public async Task BeginInterjection_ByActivePlayer_IsIgnored()
    {
        var c = await TestGames.StartedAsync();
        c.ConfirmHandoff();

        c.BeginInterjection(c.View.ActivePlayerId);

        Assert.Null(c.InterjectorId);
        Assert.Null(c.HandoffTargetId);
        Assert.Equal(c.View.ActivePlayerId, c.RevealedPlayerId);
    }

    [Fact]
    public async Task BeginInterjection_WhileHandoffPending_IsIgnored()
    {
        var c = await TestGames.StartedAsync();
        var other = c.View.Players.First(p => p.Id != c.View.ActivePlayerId).Id;

        c.BeginInterjection(other);

        Assert.Null(c.InterjectorId);
        Assert.Equal(c.View.ActivePlayerId, c.HandoffTargetId);
    }

    [Fact]
    public async Task Resume_NoSave_ReturnsNoSave()
    {
        var c = new TableController(TestGames.Catalog, new InMemoryGameStorage());
        Assert.Equal(ResumeResult.NoSave, await c.TryResumeAsync());
        Assert.False(c.HasGame);
    }

    [Fact]
    public async Task Resume_CorruptSave_ReturnsIncompatible_AndKeepsSave()
    {
        var storage = new InMemoryGameStorage();
        storage.Items[StorageKeys.Save] = "{ das ist kein Spielstand";
        var c = new TableController(TestGames.Catalog, storage);

        Assert.Equal(ResumeResult.Incompatible, await c.TryResumeAsync());
        Assert.False(c.HasGame);
        Assert.Equal("{ das ist kein Spielstand", storage.Items[StorageKeys.Save]);
    }

    [Fact]
    public async Task Resume_InCombat_HidesHandAndHasNoCountdown()
    {
        var (c, storage) = await TestGames.InCombatAsync();
        var active = c.View.ActivePlayerId;

        var resumed = new TableController(TestGames.Catalog, storage);
        Assert.Equal(ResumeResult.Resumed, await resumed.TryResumeAsync());

        Assert.Equal(GamePhase.Combat, resumed.View.Phase);
        Assert.Null(resumed.RevealedPlayerId);
        Assert.Null(resumed.View.MyHand);
        Assert.Equal(active, resumed.HandoffTargetId);
        Assert.Null(resumed.InterjectorId);
    }

    [Fact]
    public async Task Resume_InSetup_ContinuesWithFirstUnfinishedPlayer()
    {
        var storage = new InMemoryGameStorage();
        var c = TestGames.NewController(3, 42, storage);
        c.ConfirmHandoff();
        await c.FinishSetupForCurrentAsync();
        Assert.NotEqual(c.View.ActivePlayerId, c.SetupPlayerId);

        var resumed = new TableController(TestGames.Catalog, storage);
        Assert.Equal(ResumeResult.Resumed, await resumed.TryResumeAsync());

        Assert.Equal(GamePhase.Setup, resumed.View.Phase);
        Assert.Equal(resumed.View.ActivePlayerId, resumed.SetupPlayerId);
        Assert.Equal(resumed.View.ActivePlayerId, resumed.HandoffTargetId);
    }

    [Fact]
    public async Task StartNewGame_ReplacesIncompatibleSave()
    {
        var storage = new InMemoryGameStorage();
        storage.Items[StorageKeys.Save] = "kaputt";
        var c = new TableController(TestGames.Catalog, storage);
        await c.StartNewGameAsync(TestGames.Setups(3), 7);

        Assert.NotEqual("kaputt", storage.Items[StorageKeys.Save]);
    }

    [Fact]
    public async Task TurnChange_HidesHandWhileSaveIsStillPending()
    {
        var storage = new BlockingStorage();
        var c = await TestGames.AtEndTurnAsync(storage);
        var before = c.View.ActivePlayerId;
        var giveAway = c.View.MyHand!.Skip(5).Select(x => x.InstanceId).ToList();
        storage.Block = new TaskCompletionSource();

        var pending = c.SendAsync(new EndTurn(before, giveAway));

        Assert.False(pending.IsCompleted);
        Assert.NotEqual(before, c.View.ActivePlayerId);
        Assert.Null(c.RevealedPlayerId);
        Assert.Null(c.View.MyHand);
        Assert.Equal(c.View.ActivePlayerId, c.HandoffTargetId);

        storage.Block.SetResult();
        Assert.True((await pending).Accepted);
        Assert.Null(c.RevealedPlayerId);
    }

    private sealed class BlockingStorage : IGameStorage
    {
        public TaskCompletionSource? Block { get; set; }
        public Task<string?> LoadAsync(string key) => Task.FromResult<string?>(null);
        public Task SaveAsync(string key, string value) => Block?.Task ?? Task.CompletedTask;
        public Task RemoveAsync(string key) => Task.CompletedTask;
    }

    private sealed class ThrowingStorage : IGameStorage
    {
        public bool Fail { get; set; } = true;
        public Task<string?> LoadAsync(string key) => Task.FromResult<string?>(null);
        public Task SaveAsync(string key, string value) => Fail ? throw new InvalidOperationException("Quota") : Task.CompletedTask;
        public Task RemoveAsync(string key) => Task.CompletedTask;
    }
}
