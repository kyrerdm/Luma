using System;
using System.Windows.Threading;
using Luma.Models;

namespace Luma.Views;

public partial class FlyoutWindow
{
    internal static readonly TimeSpan MediaTransitionLimit = TimeSpan.FromSeconds(8);
    private readonly DispatcherTimer mediaTransitionTimer = new() { Interval = MediaTransitionLimit };
    private MediaState? transitionOrigin;
    private MediaState? transitionLatest;
    internal bool MediaTransitionActive => transitionOrigin is not null;

    internal bool BeginMediaTransition()
    {
        if (preview || !settings.ShowMedia || !media.Available) return false;
        transitionOrigin = media;
        transitionLatest = media;
        mediaTransitionTimer.Stop();
        mediaTransitionTimer.Start();
        hideTimer.Stop();
        return true;
    }

    internal void CancelMediaTransition()
    {
        var wasActive = MediaTransitionActive;
        mediaTransitionTimer.Stop();
        transitionOrigin = null;
        transitionLatest = null;
        if (wasActive) RestartTimer();
    }

    private bool HoldMediaTransition(MediaState next)
    {
        if (transitionOrigin is not { } previous) return false;
        transitionLatest = next;
        // Empty sessions and generic placeholder titles are normal while a player switches tracks.
        if (!next.Available || !next.HasTrackMetadata || string.IsNullOrWhiteSpace(next.Title)) return true;
        if (next.SessionId == previous.SessionId && next.Title == previous.Title && next.Artist == previous.Artist) return true;
        CancelMediaTransition();
        return false;
    }

    private void ExpireMediaTransition()
    {
        var latest = transitionLatest;
        CancelMediaTransition();
        if (latest is not null) UpdateMedia(latest);
        RestartTimer();
    }
}
