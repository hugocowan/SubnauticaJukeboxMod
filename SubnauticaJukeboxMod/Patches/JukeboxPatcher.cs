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
            try
            {
                if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return;

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

                if (!Spotify.jukeboxIsPlaying)
                {
                    await Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                }

                Spotify.manualJukeboxPause = false;
                Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(Spotify.spotifyVolume)));
            }
            catch (Exception e)
            {
                new Error("Something went wrong with stopping the track", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.OnApplicationQuit))]
        public static void OnApplicationQuitPostfix()
        {
            try
            {
                new Log("Application Quit");
                uGUI_SceneLoadingPatcher.loadingDone = false;
                if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return;
                Spotify.volumeThrottler.Throttle(() => { }); // Clear the throttler
                if (!Spotify.playingOnStartup)
                {
                    var playbackRequest = new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId };
                    if (Spotify.spotifyIsPlaying) Spotify.client.Player.PausePlayback(playbackRequest);
                    Spotify.jukeboxIsPlaying = false;
                }
                
                Spotify.client.Player.SetVolume(new PlayerVolumeRequest(100));
            }
            catch (Exception e)
            {
                new Error("Something went wrong while quitting the application", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.Play))]
        public async static void PlayPostfix()
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || null == Spotify.client) return;
                new Log("Play track");
                Jukebox.volume = 0;
                Spotify.jukeboxIsPlaying = true;
                Spotify.manualSpotifyPause = false;

                await Spotify.client.Player.SetVolume(new PlayerVolumeRequest(Spotify.spotifyVolume));

                try
                {
                    if (!Spotify.spotifyIsPlaying) await Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                    Spotify.spotifyIsPlaying = true;
                }
                catch (Exception e)
                {
                    new Error("Resume failed, likely because Spotify is already playing", e);
                }
            }
            catch (Exception e)
            {
                new Error("Something went wrong with playing the track", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.HandleOpenError))]
        public static void HandleOpenErrorPostfix(Jukebox __instance)
        {
            if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return;
            new Log("We have an open error D: this._failed: " + __instance._failed);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Jukebox.UpdateLowLevel))]
        public static void UpdateLowLevelPrefix(Jukebox __instance)
        {
            if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return;
            __instance._file = "event:/jukebox/jukebox_takethedive"; // This avoids errors and generally makes the jukebox very, Very happy.
        }

        //[HarmonyPrefix]
        //[HarmonyPatch(nameof(Jukebox.UpdateStudio))]
        //public static bool UpdateStudioPrefix(Jukebox __instance)
        //{
        //    if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return true;
        //    if (!__instance._eventInstanceChannelGroup.hasHandle())
        //    {
        //        __instance._eventInstance.getChannelGroup(out __instance._eventInstanceChannelGroup);
        //        new Log("no handle D: " + __instance._eventInstanceChannelGroup.hasHandle());
        //        if (__instance._eventInstanceChannelGroup.hasHandle())
        //        {
        //            return false;
        //        }
        //    }

        //    return true;
        //}

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.UpdateStudio))]
        public static void UpdateStudioPostfix(Jukebox __instance)
        {
            try
            {
                if (MainPatcher.Config.enableModToggle && null != Spotify.client)
                {
                    // Keep checking for track updates
                    if (Time.time > (Spotify.getTrackTimer + 1))
                    {
                        Spotify.getTrackTimer = Time.time;
                        _ = Spotify.GetTrackInfo();
                    }

                    // Keep the Spotify access token up to date
                    if (Spotify.refreshSessionTimer != 0 && Time.time > (Spotify.refreshSessionTimer + Spotify.refreshSessionExpiryTime - 2)) _ = Spotify.RefreshSession();
                }

                // If the mod has been disabled, make sure the jukebox is reset.
                if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client || !uGUI_SceneLoadingPatcher.loadingDone)
                {
                    if (Spotify.resetJukebox)
                    {
                        Spotify.resetJukebox = false;

                        if (Spotify.jukeboxIsPlaying && !Spotify.spotifyIsPlaying)
                        {
                            Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                        }

                        if (__instance._instance)
                        {
                            __instance._instance.file = "event:/jukebox/jukebox_takethedive";
                            Jukebox.position = 0;
                            Jukebox.GetNext(__instance._instance, true);
                            Jukebox.Stop();
                        }
                    }

                    return;
                }

                if (0 != Jukebox.volume) Jukebox.volume = 0; // If we have toggled the mod off/on, this will not be 0 anymore.

                if (Spotify.jukeboxNeedsUpdating) UpdateJukebox(__instance);

                if (Spotify.currentPosition >= 300)
                {
                    Spotify.beyondFiveMins = true;
                    __instance._position = (uint)Spotify.currentPosition * 1000;
                }

                //new Log("JukeboxInstance is null: " + (null == __instance._instance) + " | manual play: " + Spotify.manualSpotifyPlay + " | jukeboxIsPlaying: " + Spotify.jukeboxIsPlaying);

                // If we don't have a jukebox instance, there is nothing to be done.
                if (null == __instance._instance) return;


                // Here we get the player position in relation to the nearest jukebox or speaker and adjust volume accordingly.
                Vector3 position2 = ((Player.main != null) ? Player.main.transform : MainCamera.camera.transform).position;
                float sqrMagnitude = (__instance.soundPosition - position2).sqrMagnitude;
                bool soundPositionNotOrigin = __instance.soundPosition.x != 0 && __instance.soundPosition.y != 0 && __instance.soundPosition.z != 0;
                bool isPowered = __instance._instance.ConsumePower();

                // This if/else block handles all distance-related volume and play/pause changes.
                if (isPowered && !Spotify.manualJukeboxPause && !Spotify.manualSpotifyPause && !Spotify.menuPause && sqrMagnitude <= 400 && soundPositionNotOrigin)
                {
                    int volumePercentage = (int)((Spotify.jukeboxVolume - sqrMagnitude / 400) * 100) + 1;

                    // If PauseOnLeaveToggleValue is true, make sure we resume playback
                    if (MainPatcher.Config.pauseOnLeave && Spotify.jukeboxIsPaused)
                    {
                        new Log("Resuming track - distance");
                        Resume(__instance);
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

                    int volumeDiff = Math.Abs(Spotify.spotifyVolume - volumePercentage);
                    // If the volume has been the same for 2 seconds, let's give poor Spotify a break from volume requests.
                    if (volumeDiff <= 1 && Time.time > (Spotify.volumeTimer + 2)) return;
                    if (volumeDiff > 1) Spotify.volumeTimer = Time.time;

                    volumePercentage += Spotify.volumeModifier; // This ensures Spotify has sound when it's paused/has 0 volume.
                    Spotify.volumeModifier = (Spotify.volumeModifier < 0) ? 0 : -1;

                    if (volumePercentage < 0) volumePercentage = 0;
                    if (volumePercentage > 100) volumePercentage = 100;

                    Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(volumePercentage)));
                    Spotify.spotifyVolume = volumePercentage;

                } // If music is inaudible, set Spotify volume to 0.
                else if (!Spotify.manualJukeboxPause && !Spotify.manualSpotifyPause && !Spotify.menuPause && !__instance._paused && !__instance._audible && soundPositionNotOrigin) 
                {
                    // If PauseOnLeaveToggleValue is true, make sure we pause playback
                    if (MainPatcher.Config.pauseOnLeave && !Spotify.jukeboxIsPaused)
                    {
                        new Log("pausing track - distance.");
                        Pause(__instance);
                    }

                    if (Spotify.spotifyVolume != 0)
                    {
                        Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(0)));
                        Spotify.spotifyVolume = 0;
                    }
                }
                else if ((!isPowered && !__instance._paused) || (Spotify.manualSpotifyPause || __instance._paused || Spotify.menuPause) && !Spotify.jukeboxIsPaused)
                {
                    new Log("Pause track");
                    Pause(__instance);
                }
                else if (isPowered && (Spotify.manualSpotifyPlay || (!__instance._paused && !Spotify.menuPause)) && Spotify.jukeboxIsPaused && Spotify.jukeboxIsPlaying)
                {
                    new Log("Resume track");
                    Resume(__instance);
                }
            } catch(Exception e)
            {
                new Error("Something went wrong with updating the Jukebox", e);
            }
        }

        private static void UpdateJukebox(Jukebox __instance)
        {
            Spotify.jukeboxNeedsUpdating = false;
            if (!__instance._playlist.Contains(Spotify.currentTrackTitle)) __instance._playlist.Add(Spotify.currentTrackTitle);

            // This makes sure the timeline length is right.
            if (__instance._length != Spotify.currentTrackLength) __instance._length = Spotify.currentTrackLength;

            Spotify.timeTrackStarted = Time.time - Spotify.startingPosition / 1000;
            Spotify.currentPosition = (Time.time - Spotify.timeTrackStarted);

            // Make sure the timeline is off by more than a second and that we have't just started.
            if (Math.Abs((Jukebox.position / 1000) - Spotify.currentPosition) > 1 && !Spotify.justStarted)
            {
                Jukebox.position = (uint)Spotify.currentPosition * 1000;
            }

            // This updates the track label in the JukeboxInstance object. It's the only place it needs changing
            if (null != __instance._instance)
            {
                Spotify.currentInstance = __instance._instance;
                if (__instance._instance.file != Spotify.currentTrackTitle) __instance._instance.file = Spotify.currentTrackTitle;
            }
            else
            { // If there's no jukebox playing, we update every jukebox instance's label and length.
                for (int i = 0; i < JukeboxInstance.all.Count; i++)
                {
                    JukeboxInstance jukeboxInstance = JukeboxInstance.all[i];
                    jukeboxInstance.SetLabel(Spotify.currentTrackTitle);
                    jukeboxInstance.SetLength(Spotify.currentTrackLength);
                }
            }

            if ((Spotify.justStarted && Spotify.playingOnStartup) || (Spotify.manualSpotifyPlay && !Spotify.jukeboxIsPlaying))
            {
                Spotify.manualJukeboxPause = false;
                Spotify.jukeboxNeedsPlaying = true;
            }

            Spotify.justStarted = false;
        }

        private static void Pause(Jukebox __instance)
        {
            if (Spotify.manualSpotifyPause)
            {
                __instance._instance.OnButtonPlayPause();
            }
            else
            {
                __instance._paused = true;
            }
            Spotify.jukeboxIsPaused = true;
            Spotify.manualSpotifyPlay = false;
            Spotify.manualSpotifyPause = false;
            if (Spotify.spotifyIsPlaying) Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
            Spotify.spotifyIsPlaying = false;
        }

        private static void Resume(Jukebox __instance)
        {
            if (Spotify.manualSpotifyPlay)
            {
                __instance._instance.OnButtonPlayPause();
            }
            else
            {
                __instance._paused = false;
            }
            Spotify.jukeboxIsPaused = false;
            Spotify.jukeboxIsPlaying = true;
            Spotify.manualSpotifyPlay = false;
            Spotify.manualSpotifyPause = false;
            if (!Spotify.spotifyIsPlaying) Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
            Spotify.spotifyIsPlaying = true;
        }
    }
}
