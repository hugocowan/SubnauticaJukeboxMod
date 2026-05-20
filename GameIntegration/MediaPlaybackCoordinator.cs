using System;
using System.Threading.Tasks;
using UnityEngine;

namespace JukeboxSpotify
{
    internal static class MediaPlaybackCoordinator
    {
        public static bool HasControllableSession => Plugin.config.enableModToggle && JukeboxInstance.all.Count > 0 && Vars.HasActiveMediaController;

        public static async Task InitializeAsync()
        {
            Plugin.LogDebug("Initializing media playback coordinator. controller=" + Plugin.MediaController.ControllerName +
                ", enableModToggle=" + Plugin.config.enableModToggle + ", preferNativeWindowsMediaSession=" + Plugin.config.preferNativeWindowsMediaSession);
            Vars.mediaControllerInitializationStarted = true;

            if (Plugin.MediaController is NullMediaController)
            {
                Plugin.LogDebug("Current media controller is the null placeholder. Creating the default controller.");
                Plugin.MediaController = MediaControllerFactory.CreateDefault();
            }

            await Plugin.MediaController.InitializeAsync();
            Plugin.LogDebug("Primary media controller initialization finished. controller=" + Plugin.MediaController.ControllerName + ", isReady=" + Plugin.MediaController.IsReady);

            if (!Plugin.MediaController.IsReady)
            {
                if (Plugin.MediaController is WindowsMediaSessionController)
                {
                    Plugin.LogDebug("Primary Windows Media Session controller did not initialize. No further fallback available.");
                }
                else
                {
                    Plugin.LogDebug("Primary media controller did not initialize. Falling back to native Windows media session.");

                    Plugin.MediaController = new WindowsMediaSessionController();
                    await Plugin.MediaController.InitializeAsync();
                    Plugin.LogDebug("Fallback media controller initialization finished. controller=" + Plugin.MediaController.ControllerName + ", isReady=" + Plugin.MediaController.IsReady);
                }
            }

            if (Vars.HasActiveMediaController)
            {
                Plugin.LogDebug("Media controller is active after initialization. Performing an immediate refresh.");
                await RefreshAsync();
                return;
            }

            Plugin.LogDebug("No active media controller is available after initialization.");
        }

        public static async Task RefreshAsync()
        {
            if (!Vars.HasActiveMediaController)
            {
                Plugin.LogDebug("Skipping playback refresh because there is no active media controller.");
                return;
            }

            MediaPlaybackSnapshot snapshot = await Plugin.MediaController.GetPlaybackSnapshotAsync();
            Plugin.LogDebug("Playback refresh snapshot: " + DescribeSnapshot(snapshot));
            ApplySnapshot(snapshot);
        }

        public static void ApplySnapshot(MediaPlaybackSnapshot snapshot)
        {
            snapshot ??= MediaPlaybackSnapshot.Empty;

            if (!snapshot.HasTrack)
            {
                Vars.noTrack = true;
                Vars.currentTrackLength = 0;
                Vars.currentTrackTitle = string.IsNullOrWhiteSpace(snapshot.StatusMessage)
                    ? MediaPlaybackSnapshot.Empty.StatusMessage
                    : snapshot.StatusMessage;
                Plugin.LogDebug("Applying empty playback snapshot. statusMessage='" + Vars.currentTrackTitle + "'.");
                return;
            }

            if (uGUI_SceneLoadingPatcher.loadingDone && null != Jukebox.main._instance)
            {
                Vars.sourceShuffleState = snapshot.ShuffleEnabled;
                if (Jukebox.shuffle != snapshot.ShuffleEnabled)
                {
                    Plugin.LogDebug("Synchronizing jukebox shuffle state to match the media source. shuffleEnabled=" + snapshot.ShuffleEnabled);
                    Jukebox.main._instance.OnButtonShuffle();
                }
            }

            bool isPlaying = snapshot.PlaybackState == MediaPlaybackState.Playing;
            if (Vars.justStarted)
            {
                Vars.playingOnStartup = isPlaying;
            }

            string oldTrackTitle = Vars.currentTrackTitle;
            Vars.startingPosition = snapshot.PositionMs;
            Vars.currentTrackLength = snapshot.DurationMs;
            Vars.noTrack = false;
            Plugin.LogDebug("Applying playback snapshot. currentlyPlaying=" + isPlaying + ", oldTrackTitle='" + oldTrackTitle + "'.");

            if ((Time.time > Vars.jukeboxActionTimestamp + 3) && !Vars.menuPause && isPlaying && (!Vars.jukeboxIsRunning || Vars.jukeboxIsPaused))
            {
                Vars.manualPlay = true;
                Plugin.LogDebug("Snapshot indicates external playback resumed. manualPlay has been set.");
            }
            else if ((Time.time > Vars.jukeboxActionTimestamp + 3) && !Vars.menuPause && !Vars.justStarted && !isPlaying && Vars.jukeboxIsRunning && !Vars.jukeboxIsPaused)
            {
                Vars.manualPause = true;
                Plugin.LogDebug("Snapshot indicates external playback paused. manualPause has been set.");
            }

            Vars.currentTrackTitle = BuildTrackTitle(snapshot);
            Plugin.LogDebug("Resolved track title for UI: '" + Vars.currentTrackTitle + "'.");

            if (
                uGUI_SceneLoadingPatcher.loadingDone &&
                (
                    Vars.playingOnStartup || (!Vars.menuPause && Vars.jukeboxIsRunning) ||
                    oldTrackTitle != Vars.currentTrackTitle ||
                    (Vars.manualPlay && (Vars.jukeboxIsPaused || !Vars.jukeboxIsRunning))
                )
            )
            {
                Vars.jukeboxNeedsUpdating = true;
                Plugin.LogDebug("Marked the jukebox state as needing an update. oldTrackTitle='" + oldTrackTitle + "', newTrackTitle='" + Vars.currentTrackTitle +
                    "', playingOnStartup=" + Vars.playingOnStartup + ", manualPlay=" + Vars.manualPlay + ", jukeboxIsRunning=" + Vars.jukeboxIsRunning + ".");
            }
        }

        private static string BuildTrackTitle(MediaPlaybackSnapshot snapshot)
        {
            if (Plugin.config.includeArtist && !string.IsNullOrWhiteSpace(snapshot.Artist))
            {
                return snapshot.TrackTitle + " - " + snapshot.Artist;
            }

            return snapshot.TrackTitle;
        }

        private static string DescribeSnapshot(MediaPlaybackSnapshot snapshot)
        {
            snapshot ??= MediaPlaybackSnapshot.Empty;

            return "hasTrack=" + snapshot.HasTrack +
                ", playbackState=" + snapshot.PlaybackState +
                ", trackTitle='" + snapshot.TrackTitle + "'" +
                ", artist='" + snapshot.Artist + "'" +
                ", album='" + snapshot.Album + "'" +
                ", positionMs=" + snapshot.PositionMs +
                ", durationMs=" + snapshot.DurationMs +
                ", shuffleEnabled=" + snapshot.ShuffleEnabled +
                ", sourceId='" + snapshot.SourceId + "'" +
                ", statusMessage='" + snapshot.StatusMessage + "'";
        }
    }
}