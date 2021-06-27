using HarmonyLib;
using SpotifyAPI.Web;
using UnityEngine;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox))]
    class JukeboxPatcher
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.GetNext))]
        public async static void GetNextPostfix(bool forward)
        {

            if (Spotify.repeatTrack)
            {
                await Spotify.client.Player.SeekTo(new PlayerSeekToRequest(0));
                Spotify.timeTrackStarted = Time.time;
                Spotify.startingPosition = 0;
                return;
            }

            await Spotify.client.Player.SetVolume(new PlayerVolumeRequest(0));
            
            if (forward)
            {
                //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Skip next track", null, true);
                await Spotify.client.Player.SkipNext(new PlayerSkipNextRequest() { DeviceId = Spotify.device.Id });
                Spotify.timeTrackStarted = Time.time;
                Spotify.startingPosition = 1000;
            }
            else
            {
                //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Skip previous track", null, true);
                await Spotify.client.Player.SkipPrevious(new PlayerSkipPreviousRequest() { DeviceId = Spotify.device.Id });
                Spotify.timeTrackStarted = Time.time;
                Spotify.startingPosition = 1000;
            }

            if (true != Spotify.jukeboxIsPlaying)
            {
                await Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id });
            }

            await Spotify.client.Player.SetVolume(new PlayerVolumeRequest(Spotify.spotifyVolume));
            await Spotify.trackDebouncer.DebounceAsync(() => Spotify.GetTrackInfo());
        }


        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.OnApplicationQuit))]
        public static void OnApplicationQuitPostfix()
        {
            Spotify.client.Player.SetVolume(new PlayerVolumeRequest(100));
            Spotify.trackDebouncer.Debounce(() => { }); // Clear the debouncer
            if (Spotify.playingOnStartup) return;
            Spotify.jukeboxIsPlaying = null;
            var playbackRequest = new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id };
            Spotify.client.Player.PausePlayback(playbackRequest);
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.Play))]
        public async static void PlayPostfix()
        {
            //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Play track", null, true);
            Jukebox.volume = 0;
            Spotify.jukeboxIsPlaying = true;

            try
            {
                await Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify.device.Id });
                Spotify.spotifyIsPlaying = true;
            } catch
            {
                //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "This fails when Spotify is already playing", null, true);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.StopInternal))]
        public async static void StopInternalPostfix()
        {
            //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Stop track", null, true);
            Spotify.jukeboxIsPlaying = null;
            Spotify.jukeboxIsPaused = false;
            await Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id });
            Spotify.spotifyIsPlaying = false;
            await Spotify.client.Player.SeekTo(new PlayerSeekToRequest(0));
            Spotify.timeTrackStarted = Time.time;
            Spotify.startingPosition = 0;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.HandleOpenError))]
        public static void HandleOpenErrorPostfix(Jukebox __instance)
        {
            QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "We have an open error D: this._failed: " + __instance._failed, null, false);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Jukebox.UpdateLowLevel))]
        public static void UpdateLowLevelPrefix(Jukebox __instance)
        {
            __instance._file = "event:/jukebox/jukebox_one"; // This avoids errors and generally makes the jukebox very, Very happy.
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Jukebox.UpdateStudio))]
        public static void UpdateStudioPrefix(Jukebox __instance)
        {
            if (Jukebox.isStartingOrPlaying && Spotify.jukeboxNeedsUpdating)
            {
                if (!__instance._playlist.Contains(Spotify.currentTrackTitle)) __instance._playlist.Add(Spotify.currentTrackTitle);

                //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Update Jukebox. file: " + _fileRef(__instance), null, true);
                Spotify.jukeboxNeedsUpdating = false;

                // This makes sure the timeline length is right.
                __instance._length = Spotify.currentTrackLength;

                Spotify.timeTrackStarted = Time.time - Spotify.startingPosition / 1000;
                Jukebox.position = (uint)(Time.time - Spotify.timeTrackStarted) * 1000;

                // This updates the track label in the JukeboxInstance object. It's the only place it needs changing
                if (null != __instance._instance && __instance._instance.file != Spotify.currentTrackTitle)
                {
                    __instance._instance.file = Spotify.currentTrackTitle;
                }

                if (Spotify.justStarted && Spotify.playingOnStartup && Spotify.startingPosition > 0)
                {
                    Spotify.jukeboxNeedsPlaying = true;
                }
            }

            // Here we get the player position in relation to the nearest jukebox or speaker and adjust volume accordingly.
            Vector3 position2 = ((Player.main != null) ? Player.main.transform : MainCamera.camera.transform).position;
            float sqrMagnitude =  (__instance.soundPosition - position2).sqrMagnitude;
            bool soundPositionNotOrigin = __instance.soundPosition.x != 0 && __instance.soundPosition.y != 0 && __instance.soundPosition.z != 0;
            int volumePercentage = (int)(Spotify.jukeboxVolume * 100);

            if (uGUI_SceneLoadingPatcher.loadingDone && __instance._audible && sqrMagnitude <= 400 && soundPositionNotOrigin)
            {
                volumePercentage = (int) ((Spotify.jukeboxVolume - sqrMagnitude / 400) * 100);

                if (MainPatcher.Config.PauseOnLeaveToggleValue && Spotify.jukeboxIsPaused)
                {
                    //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Resuming track cos of distance", null, true);
                    __instance._paused = false;
                    Spotify.jukeboxIsPaused = false;
                    Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify.device.Id });
                    Spotify.spotifyIsPlaying = true;
                }

                if (Player.main != null && true == Player.main.isUnderwater.value) volumePercentage /= 2;
                if (volumePercentage < 0) volumePercentage = 0;
                Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(volumePercentage)));
                Spotify.spotifyVolume = volumePercentage;
            } 
            else if (uGUI_SceneLoadingPatcher.loadingDone && !__instance._audible && soundPositionNotOrigin)
            {
                if (MainPatcher.Config.PauseOnLeaveToggleValue && !Spotify.jukeboxIsPaused)
                {
                    //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "pausing track cos of distance. __instance._audible: " + __instance._audible + " | sqrMagnitude: " + sqrMagnitude, null, true);
                    __instance._paused = true;
                    Spotify.jukeboxIsPaused = true;
                    Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id });
                    Spotify.spotifyIsPlaying = false;
                }

                if (Spotify.spotifyVolume != 0)
                {
                    Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(0)));
                    Spotify.spotifyVolume = 0;
                }
            }

            // Pause/Resume Spotify as needed.
            if (__instance._paused && false == Spotify.jukeboxIsPaused)
            {
                //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Pause track", null, true);
                Spotify.jukeboxIsPaused = true;
                Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id });
                Spotify.spotifyIsPlaying = false;
            }
            else if (!__instance._paused && true == Spotify.jukeboxIsPaused && true == Spotify.jukeboxIsPlaying)
            {
                //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Resume track", null, true);
                Spotify.jukeboxIsPaused = false;
                Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify.device.Id });
                Spotify.spotifyIsPlaying = true;
            }
        }
    }
}
