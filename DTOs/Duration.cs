namespace Groovo.DTOs;
public readonly struct Duration : IEquatable<Duration>, IComparable<Duration>
{
    private readonly int _totalSeconds;

    // Constructors
    public Duration(int totalSeconds)
    {
        if (totalSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(totalSeconds), "Duration cannot be negative");

        _totalSeconds = totalSeconds;
    }

    public Duration(int minutes, int seconds) : this(minutes * 60 + seconds) { }
    public Duration(int hours, int minutes, int seconds) : this(hours * 3600 + minutes * 60 + seconds) { }

    /// <summary>Total duration in seconds</summary>
    public int TotalSeconds => _totalSeconds;

    /// <summary>Hours component of the duration</summary>
    public int Hours => _totalSeconds / 3600;

    /// <summary>Minutes component (0-59)</summary>
    public int Minutes => (_totalSeconds % 3600) / 60;

    /// <summary>Seconds component (0-59)</summary>
    public int Seconds => _totalSeconds % 60;

    /// <summary>Total duration in minutes (can be > 59)</summary>
    public double TotalMinutes => _totalSeconds / 60.0;

    /// <summary>Format as MM:SS or H:MM:SS if over an hour</summary>
    public override string ToString()
    {
        return Hours > 0 
            ? $"{Hours}:{Minutes:D2}:{Seconds:D2}"
            : $"{Minutes:D2}:{Seconds:D2}";
    }

    // Implicit conversions
    public static implicit operator int(Duration duration) => duration._totalSeconds;
    public static implicit operator Duration(int seconds) => new(seconds);

    // Arithmetic operators
    public static Duration operator +(Duration left, Duration right) => new(left._totalSeconds + right._totalSeconds);
    public static Duration operator -(Duration left, Duration right) => new(Math.Max(0, left._totalSeconds - right._totalSeconds));
    public static Duration operator *(Duration duration, double multiplier) => new((int)(duration._totalSeconds * multiplier));
    public static Duration operator /(Duration duration, double divisor) => new((int)(duration._totalSeconds / divisor));

    // Comparison operators
    public static bool operator ==(Duration left, Duration right) => left._totalSeconds == right._totalSeconds;
    public static bool operator !=(Duration left, Duration right) => left._totalSeconds != right._totalSeconds;
    public static bool operator <(Duration left, Duration right) => left._totalSeconds < right._totalSeconds;
    public static bool operator >(Duration left, Duration right) => left._totalSeconds > right._totalSeconds;
    public static bool operator <=(Duration left, Duration right) => left._totalSeconds <= right._totalSeconds;
    public static bool operator >=(Duration left, Duration right) => left._totalSeconds >= right._totalSeconds;

    // Interface implementations
    public bool Equals(Duration other) => _totalSeconds == other._totalSeconds;
    public override bool Equals(object? obj) => obj is Duration other && Equals(other);
    public override int GetHashCode() => _totalSeconds.GetHashCode();
    public int CompareTo(Duration other) => _totalSeconds.CompareTo(other._totalSeconds);
}