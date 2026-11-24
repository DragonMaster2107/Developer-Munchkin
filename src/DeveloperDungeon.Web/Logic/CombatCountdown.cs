namespace DeveloperDungeon.Web.Logic;

/// <summary>Sekundentakt-Countdown für die letzte Chance vor der Kampfauswertung.</summary>
public sealed class CombatCountdown(TimeProvider time) : IDisposable
{
    private readonly object _gate = new();
    private ITimer? _timer;

    public bool IsRunning { get; private set; }
    public int RemainingSeconds { get; private set; }

    public event Action? Tick;
    public event Action? Elapsed;

    /// <summary>Startet den Countdown neu; bei 0 Sekunden läuft <see cref="Elapsed"/> sofort.</summary>
    public void Start(int seconds)
    {
        Cancel();
        if (seconds <= 0)
        {
            Elapsed?.Invoke();
            return;
        }

        lock (_gate)
        {
            RemainingSeconds = seconds;
            IsRunning = true;
            _timer = time.CreateTimer(_ => OnSecond(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            StopTimer();
            RemainingSeconds = 0;
        }
    }

    public void Dispose() => Cancel();

    private void OnSecond()
    {
        bool done;
        lock (_gate)
        {
            if (!IsRunning)
            {
                return;
            }

            RemainingSeconds--;
            done = RemainingSeconds <= 0;
            if (done)
            {
                StopTimer();
            }
        }

        Tick?.Invoke();
        if (done)
        {
            Elapsed?.Invoke();
        }
    }

    private void StopTimer()
    {
        IsRunning = false;
        _timer?.Dispose();
        _timer = null;
    }
}
