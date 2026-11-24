using System.Text.Json;
using DeveloperDungeon.Engine;
using DeveloperDungeon.Engine.Cards;
using DeveloperDungeon.Engine.Commands;
using DeveloperDungeon.Engine.Events;
using DeveloperDungeon.Engine.Sessions;
using DeveloperDungeon.Engine.State;
using DeveloperDungeon.Engine.Views;

namespace DeveloperDungeon.Web.Logic;

public enum ResumeResult { NoSave, Resumed, Incompatible }

/// <summary>Steuert eine Partie am geteilten Gerät: Gerät übergeben, Aufbau-Runde, Eingreifen und Speichern.</summary>
public sealed class TableController(CardCatalog catalog, IGameStorage storage, TimeProvider time) : IDisposable
{
    public const string SaveFailedWarning = "Spielstand konnte nicht gespeichert werden.";

    private LocalGameSession? _session;
    private GameView? _view;
    private string? _pendingJson;
    private int _setupDone;
    private int _playerCount;

    public CardCatalog Catalog => catalog;
    public bool HasGame => _session is not null;

    /// <summary>Sicht für <see cref="RevealedPlayerId"/>; ohne aufgedeckte Hand die öffentliche Sicht.</summary>
    public GameView View => _view ?? throw new InvalidOperationException("Es läuft kein Spiel.");

    public string ActorId => InterjectorId ?? (View.Phase == GamePhase.Setup && SetupPlayerId is not null ? SetupPlayerId : View.ActivePlayerId);
    public string? RevealedPlayerId { get; private set; }
    public string? HandoffTargetId { get; private set; }
    public string? InterjectorId { get; private set; }
    public string? SetupPlayerId { get; private set; }

    public IReadOnlyList<GameEvent> LastEvents { get; private set; } = [];
    /// <summary>Zählt jeden angenommenen Befehl hoch; wer Ereignisse nur einmal abspielen will, vergleicht damit.</summary>
    public int EventsVersion { get; private set; }
    public string? LastRejection { get; private set; }
    public string? LastWarning { get; private set; }

    /// <summary>Zählt jede neue Rückmeldung (abgelehnter Befehl, Speicherwarnung) hoch, auch bei gleichem Text.</summary>
    public int FeedbackVersion { get; private set; }
    public string? Feedback { get; private set; }

    public event Action? Changed;

    private CombatCountdown? _countdown;
    private Task _resolution = Task.CompletedTask;

    public bool CountdownRunning => _countdown?.IsRunning ?? false;
    public int CountdownRemaining => _countdown?.RemainingSeconds ?? 0;

    private CombatCountdown Countdown
    {
        get
        {
            if (_countdown is null)
            {
                _countdown = new CombatCountdown(time);
                _countdown.Tick += () => Changed?.Invoke();
                _countdown.Elapsed += () => _resolution = ResolveAfterCountdownAsync();
            }
            return _countdown;
        }
    }

    public void Dispose() => _countdown?.Dispose();

    /// <summary>Startet den Countdown zur Kampfauswertung; nur vom Gerät des aktiven Spielers (oder eines Eingreifenden) im laufenden Kampf.</summary>
    public async Task RequestCombatResolutionAsync(int countdownSeconds)
    {
        var holdsDevice = RevealedPlayerId == View.ActivePlayerId || InterjectorId is not null;
        if (_session is null || HandoffTargetId is not null || !holdsDevice || View.Combat is not { State.Stage: CombatStage.Fighting })
        {
            return;
        }

        Countdown.Start(countdownSeconds);
        Changed?.Invoke();
        await _resolution;
    }

    private async Task ResolveAfterCountdownAsync()
    {
        if (_session is null || View.Combat is not { State.Stage: CombatStage.Fighting })
        {
            return;
        }

        InterjectorId = null;
        await SendAsync(new ResolveCombat(View.ActivePlayerId));
        if (HandoffTargetId is null && RevealedPlayerId != View.ActivePlayerId)
        {
            RequestDevice(View.ActivePlayerId);
            Changed?.Invoke();
        }
    }

    public async Task StartNewGameAsync(IReadOnlyList<PlayerSetup> players, ulong seed)
    {
        Attach(Game.New(players, catalog, seed));
        try
        {
            await storage.RemoveAsync(StorageKeys.Save);
        }
        catch (Exception)
        {
            // Das Überschreiben beim Speichern ersetzt den alten Stand ohnehin.
        }
        await PersistAsync(_pendingJson!);
        BeginSetupRound();
        Changed?.Invoke();
    }

    public async Task<ResumeResult> TryResumeAsync()
    {
        var json = await storage.LoadAsync(StorageKeys.Save);
        if (json is null)
        {
            return ResumeResult.NoSave;
        }

        Game game;
        try
        {
            game = Game.Load(json, catalog);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException)
        {
            return ResumeResult.Incompatible;
        }

        Attach(game);
        if (View.Phase == GamePhase.Setup)
        {
            BeginSetupRound();
        }
        else
        {
            RequestDevice(View.ActivePlayerId);
        }
        Changed?.Invoke();
        return ResumeResult.Resumed;
    }

    public void RequestDevice(string playerId)
    {
        HandoffTargetId = playerId;
        RevealedPlayerId = null;
        Refresh();
    }

    public void ConfirmHandoff()
    {
        RevealedPlayerId = HandoffTargetId;
        HandoffTargetId = null;
        Refresh();
        Changed?.Invoke();
    }

    public async Task FinishSetupForCurrentAsync()
    {
        if (SetupPlayerId is null)
        {
            return;
        }

        _setupDone++;
        if (_setupDone < _playerCount)
        {
            SetupPlayerId = SetupPlayerAt(_setupDone);
            RequestDevice(SetupPlayerId);
            Changed?.Invoke();
            return;
        }

        var starter = SetupPlayerId;
        SetupPlayerId = null;
        await SendAsync(new StartGame(starter));
        if (View.Phase == GamePhase.Setup)
        {
            // Start abgelehnt: Aufbau-Runde bleibt beim letzten Spieler.
            SetupPlayerId = starter;
            _setupDone = _playerCount - 1;
        }
        else
        {
            RequestDevice(View.ActivePlayerId);
        }
        Changed?.Invoke();
    }

    public void BeginInterjection(string playerId)
    {
        if (playerId == View.ActivePlayerId || HandoffTargetId is not null)
        {
            return;
        }

        _countdown?.Cancel();
        InterjectorId = playerId;
        RequestDevice(playerId);
        Changed?.Invoke();
    }

    public void EndInterjection()
    {
        InterjectorId = null;
        RequestDevice(View.ActivePlayerId);
        Changed?.Invoke();
    }

    public async Task<CommandResult> SendAsync(GameCommand command)
    {
        var session = _session ?? throw new InvalidOperationException("Es läuft kein Spiel.");
        _pendingJson = null;
        var activeBefore = View.ActivePlayerId;
        var result = await session.SendAsync(command);
        if (result.Accepted)
        {
            LastRejection = null;
            LastEvents = result.Events;
            EventsVersion++;
            Refresh();
            if (View.ActivePlayerId != activeBefore)
            {
                InterjectorId = null;
                RequestDevice(View.ActivePlayerId);
            }

            if (_pendingJson is not null)
            {
                await PersistAsync(_pendingJson);
            }
        }
        else
        {
            LastRejection = result.Rejection;
            Notify(result.Rejection);
        }

        Changed?.Invoke();
        return result;
    }

    public Task<CommandResult> CheckAsync(GameCommand command) =>
        (_session ?? throw new InvalidOperationException("Es läuft kein Spiel.")).CheckAsync(command);

    private void Attach(Game game)
    {
        _session = new LocalGameSession(game, json => _pendingJson = json);
        _playerCount = game.GetView(null).Players.Count;
        _pendingJson = game.Save();
        _countdown?.Cancel();
        // Nichts aus der vorigen Partie darf in die erste Sicht der neuen durchsickern (aufgedeckte Hand, Übergabe, Eingreifen, Aufbau-Runde).
        RevealedPlayerId = null;
        HandoffTargetId = null;
        InterjectorId = null;
        SetupPlayerId = null;
        _setupDone = 0;
        LastEvents = [];
        LastRejection = null;
        LastWarning = null;
        Refresh();
    }

    private void BeginSetupRound()
    {
        _setupDone = 0;
        SetupPlayerId = SetupPlayerAt(0);
        RequestDevice(SetupPlayerId);
    }

    private string SetupPlayerAt(int offset)
    {
        var players = View.Players;
        var start = players.ToList().FindIndex(p => p.Id == View.ActivePlayerId);
        return players[(start + offset) % players.Count].Id;
    }

    private void Notify(string? message)
    {
        Feedback = message;
        FeedbackVersion++;
    }

    private void Refresh() => _view = _session?.GetView(RevealedPlayerId);

    private async Task PersistAsync(string json)
    {
        try
        {
            await storage.SaveAsync(StorageKeys.Save, json);
            LastWarning = null;
        }
        catch (Exception)
        {
            LastWarning = SaveFailedWarning;
            Notify(SaveFailedWarning);
        }
    }
}
