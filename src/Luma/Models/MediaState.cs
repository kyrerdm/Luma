using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace Luma.Models;

public sealed record MediaSourceInfo(string Id, string AppName, string Title, string Artist, bool IsPlaying);

public sealed record MediaState
{
    public static MediaState Empty { get; } = new();
    public bool Available { get; init; }
    public string SessionId { get; init; } = "";
    public string AppName { get; init; } = "";
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public bool IsPlaying { get; init; }
    public bool CanToggle { get; init; }
    public bool CanPrevious { get; init; }
    public bool CanNext { get; init; }
    public bool CanSeek { get; init; }
    public TimeSpan Start { get; init; }
    public TimeSpan End { get; init; }
    public TimeSpan Position { get; init; }
    public DateTimeOffset SampledAt { get; init; } = DateTimeOffset.UtcNow;
    public double PlaybackRate { get; init; } = 1;
    public ImageSource? Artwork { get; init; }
    public IReadOnlyList<MediaSourceInfo> Sources { get; init; } = Array.Empty<MediaSourceInfo>();
    public bool AutomaticSource { get; init; } = true;

    public TimeSpan CurrentPosition(DateTimeOffset now)
    {
        var ticks = Position.Ticks;
        if (IsPlaying) ticks += (long)(Math.Max(0, (now - SampledAt).TotalSeconds) * TimeSpan.TicksPerSecond * Math.Clamp(PlaybackRate, 0, 16));
        return TimeSpan.FromTicks(Math.Clamp(ticks, Start.Ticks, Math.Max(Start.Ticks, End.Ticks)));
    }
}
