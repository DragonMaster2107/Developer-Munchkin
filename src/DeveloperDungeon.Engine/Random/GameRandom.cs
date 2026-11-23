namespace DeveloperDungeon.Engine.Random;

/// <summary>Deterministic random source (SplitMix64); the full state is a single ulong.</summary>
public sealed class GameRandom
{
    public GameRandom(ulong state) => State = state;

    public ulong State { get; private set; }

    private ulong NextUInt64()
    {
        State += 0x9E3779B97F4A7C15UL;
        var z = State;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Returns a uniform value in [0, maxExclusive) using rejection sampling (no modulo bias).</summary>
    public int Next(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);
        var max = (ulong)maxExclusive;
        var limit = ulong.MaxValue - ulong.MaxValue % max;
        ulong value;
        do
        {
            value = NextUInt64();
        } while (value >= limit);
        return (int)(value % max);
    }

    public int RollD6() => Next(6) + 1;

    /// <summary>Fisher-Yates shuffle.</summary>
    public void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
