using HarmonyLib;
using SpotifyAPI.Web;
using System;
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
            if (!MainPatcher.Config.enableModToggle) return;

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
                new Log("Skip next track");
                await Spotify.client.Player.SkipNext(new PlayerSkipNextRequest() { DeviceId = MainPatcher.Config.deviceId });
                Spotify.timeTrackStarted = Time.time;
                Spotify.startingPosition = 1000;
            }
            else
            {
                new Log("Skip previous track");
                await Spotify.client.Player.SkipPrevious(new PlayerSkipPreviousRequest() { DeviceId = MainPatcher.Config.deviceId });
                Spotify.timeTrackStarted = Time.time;
                Spotify.startingPosition = 1000;
            }

            if (true != Spotify.jukeboxIsPlaying)
            {
                await Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
            }

            Spotify.manualJukeboxPause = false;
            Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(Spotify.spotifyVolume)));
            await Spotify.trackDebouncer.DebounceAsync(() => Spotify.GetTrackInfo());
        }


        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.OnApplicationQuit))]
        public static void OnApplicationQuitPostfix()
        {
            new Log("Application Quit");
            if (!MainPatcher.Config.enableModToggle) return;
            Spotify.trackDebouncer.Debounce(() => { }); // Clear the debouncer
            Spotify.volumeThrottler.Throttle(() => { }); // Clear the throttler
            if (!Spotify.playingOnStartup)
            {
                var playbackRequest = new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId };
                Spotify.client.Player.PausePlayback(playbackRequest);
                Spotify.jukeboxIsPlaying = null;
            }
            Spotify.client.Player.SetVolume(new PlayerVolumeRequest(100));
            
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.Play))]
        public async static void PlayPostfix()
        {
            if (!MainPatcher.Config.enableModToggle) return;
            new Log("Play track");
            Jukebox.volume = 0;
            Spotify.jukeboxIsPlaying = true;
            Spotify.manualSpotifyPause = false;

            try
            {
                await Spotify.client.Player.SetVolume(new PlayerVolumeRequest(Spotify.spotifyVolume));
                await Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                Spotify.spotifyIsPlaying = true;
            } catch
            {
                //new Error("Resume failed, likely because Spotify is already playing", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.StopInternal))]
        public async static void StopInternalPostfix()
        {
            new Log("Stop track");
            if (!MainPatcher.Config.enableModToggle) return;
            Spotify.jukeboxIsPlaying = null;
            Spotify.jukeboxIsPaused = false;
            await Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
            Spotify.spotifyIsPlaying = false;
            Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(100)));
            await Spotify.client.Player.SeekTo(new PlayerSeekToRequest(0));
            Spotify.timeTrackStarted = Time.time;
            Spotify.startingPosition = 0;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.HandleOpenError))]
        public static void HandleOpenErrorPostfix(Jukebox __instance)
        {
            if (!MainPatcher.Config.enableModToggle) return;
            new Log("We have an open error D: this._failed: " + __instance._failed);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Jukebox.UpdateLowLevel))]
        public static void UpdateLowLevelPrefix(Jukebox __instance)
        {
            if (!MainPatcher.Config.enableModToggle) return;
            __instance._file = "event:/jukebox/jukebox_one"; // This avoids errors and generally makes the jukebox very, Very happy.
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Jukebox.UpdateStudio))]
        public static void UpdateStudioPrefix(Jukebox __instance)
        {
            // If the mod has been disabled, make sure the jukebox is reset.
            if (!MainPatcher.Config.enableModToggle)
            {
                if (Spotify.resetJukebox)
                {
                    Spotify.resetJukebox = false;

                    if (true == Spotify.jukeboxIsPlaying)
                    {
                        Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                    }

                    if (__instance._instance)
                    {
                        __instance._instance.file = "event:/jukebox/jukebox_one";
                        Jukebox.position = 0;
                        Jukebox.GetNext(__instance._instance, true);
                        Jukebox.Stop();
                        Jukebox.volume = Spotify.jukeboxVolume;
                    }
                }

                return;
            }

            if (0 != Jukebox.volume) Jukebox.volume = 0; // If we have toggled the mod off/on, this will not be 0 anymore.

            //new Log("Yo. null == __instance: " + (null == __instance));
            if (Jukebox.isStartingOrPlaying && Spotify.jukeboxNeedsUpdating)
            {
                if (!__instance._playlist.Contains(Spotify.currentTrackTitle)) __instance._playlist.Add(Spotify.currentTrackTitle);

                //new Log("Update Jukebox.");
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

                if (Spotify.justStarted && Spotify.playingOnStartup)
                {
                    Spotify.jukeboxNeedsPlaying = true;
                    Spotify.justStarted = false;
                }
            }

            // Here we get the player position in relation to the nearest jukebox or speaker and adjust volume accordingly.
            Vector3 position2 = ((Player.main != null) ? Player.main.transform : MainCamera.camera.transform).position;
            float sqrMagnitude =  (__instance.soundPosition - position2).sqrMagnitude;
            bool soundPositionNotOrigin = __instance.soundPosition.x != 0 && __instance.soundPosition.y != 0 && __instance.soundPosition.z != 0;
            int volumePercentage = (int)(Spotify.jukeboxVolume * 100);

            // If music is audible, set the volume.
            if (!Spotify.manualJukeboxPause && !Spotify.menuPause && uGUI_SceneLoadingPatcher.loadingDone && __instance._audible && sqrMagnitude <= 400 && soundPositionNotOrigin)
            {
                volumePercentage = (int) ((Spotify.jukeboxVolume - sqrMagnitude / 400) * 100) + 1;

                // If PauseOnLeaveToggleValue is true, make sure we resume playback
                if (MainPatcher.Config.pauseOnLeave && Spotify.jukeboxIsPaused)
                {
                    new Log("Resuming track cos of distance");
                    __instance._paused = false;
                    Spotify.jukeboxIsPaused = false;
                    Spotify.jukeboxIsPlaying = true;
                    Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                    Spotify.spotifyIsPlaying = true;
                }

                // Check whether the jukebox is in a SeaTruck.
                bool seaTruckJukeboxPlaying = null != __instance._instance.GetComponentInParent<SeaTruckSegment>();

                // If the player is underwater, or if the seatruck jukebox is playing but the player is not in the seatruck, or if a base's jukebox is playing but they aren't in the base,
                // halve the music volume as there is a body of water between the jukebox and the player.
                if (
                    null != Player.main && (true == Player.main.isUnderwater.value) ||
                    (seaTruckJukeboxPlaying && (null == Player.main.currentInterior || Player.main.currentInterior.GetType().ToString() != "SeaTruckSegment")) ||
                    (!seaTruckJukeboxPlaying && (null == Player.main.currentInterior || Player.main.currentInterior.GetType().ToString() != "BaseRoot"))
                    )
                {
                    volumePercentage /= 2;
                }
                volumePercentage += new System.Random().Next(-1, 1); // This ensures Spotify has sound when it's paused/has 0 volume.

                if (volumePercentage < 0) volumePercentage = 0;
                if (volumePercentage > 100) volumePercentage = 100;

                Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(volumePercentage)));
                Spotify.spotifyVolume = volumePercentage;
            } 
            else if (!Spotify.manualJukeboxPause && !Spotify.menuPause && !__instance._paused && uGUI_SceneLoadingPatcher.loadingDone && !__instance._audible && soundPositionNotOrigin) // If music is inaudible, set Spotify volume to 0.
            {
                // If PauseOnLeaveToggleValue is true, make sure we pause playback
                if (MainPatcher.Config.pauseOnLeave && !Spotify.jukeboxIsPaused)
                {
                    new Log("pausing track cos of distance.");
                    __instance._paused = true;
                    Spotify.jukeboxIsPaused = true;
                    Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                    Spotify.spotifyIsPlaying = false;
                }

                if (Spotify.spotifyVolume != 0)
                {
                    Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(0)));
                    Spotify.spotifyVolume = 0;
                }
            }


            // Pause/Resume Spotify as needed.
            if (uGUI_SceneLoadingPatcher.loadingDone && ((Spotify.manualSpotifyPause && !Spotify.manualJukeboxPlay) || __instance._paused || Spotify.menuPause) && false == Spotify.jukeboxIsPaused)
            {
                if (Spotify.manualSpotifyPause && null != __instance._instance)
                {
                    __instance._instance.OnButtonPlayPause();
                }
                else
                {
                    __instance._paused = true;
                }
                new Log("Pause track");
                Spotify.jukeboxIsPaused = true;
                Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                Spotify.spotifyIsPlaying = false;
                Spotify.manualSpotifyPause = false;
            }
            else if (uGUI_SceneLoadingPatcher.loadingDone && Spotify.manualSpotifyPlay || (!__instance._paused && !Spotify.menuPause) && true == Spotify.jukeboxIsPaused && true == Spotify.jukeboxIsPlaying)
            {
                new Log("Resume track");
                try
                {
                    if (null == __instance._instance)
                    {
                        __instance._instance = new JukeboxInstance();
                        //__instance._instance.file = Spotify.currentTrackTitle;
                        Spotify.jukeboxNeedsPlaying = true;
                        __instance._instance.UpdateUI();
                    }
                    else if (Spotify.manualSpotifyPlay)
                    {
                        __instance._instance.OnButtonPlayPause();
                    }
                    else
                    {
                        __instance._paused = false;
                    }
                }
                catch(Exception e)
                {
                    new Error("Something went wrong while editing the instance", e);
                }

                Spotify.jukeboxIsPaused = false;
                Spotify.jukeboxIsPlaying = true;
                Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                Spotify.spotifyIsPlaying = true;
                Spotify.manualSpotifyPlay = false;
            }
        }
    }
}
