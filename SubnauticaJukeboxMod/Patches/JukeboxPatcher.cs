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
                Spotify.trackDebouncer.Debounce(() => { }); // Clear the debouncer
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

                try
                {
                    await Spotify.client.Player.SetVolume(new PlayerVolumeRequest(Spotify.spotifyVolume));
                }
                catch (Exception e)
                {
                    new Error("Setting Spotify volume failed", e);
                }

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
            __instance._file = "event:/jukebox/jukebox_one"; // This avoids errors and generally makes the jukebox very, Very happy.
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Jukebox.UpdateStudio))]
        public static void UpdateStudioPrefix(Jukebox __instance)
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
                    if (Spotify.refreshSessionTimer != 0 && Time.time > (Spotify.refreshSessionTimer + Spotify.refreshSessionExpiryTime - 2))
                    {
                        _ = Spotify.RefreshSession();
                    }
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
                            __instance._instance.file = "event:/jukebox/jukebox_one";
                            Jukebox.position = 0;
                            Jukebox.GetNext(__instance._instance, true);
                            Jukebox.Stop();
                        }
                    }

                    return;
                }

                if (0 != Jukebox.volume) Jukebox.volume = 0; // If we have toggled the mod off/on, this will not be 0 anymore.

                if (Spotify.jukeboxNeedsUpdating)
                {
                    Spotify.jukeboxNeedsUpdating = false;
                    if (!__instance._playlist.Contains(Spotify.currentTrackTitle)) __instance._playlist.Add(Spotify.currentTrackTitle);

                    // This makes sure the timeline length is right.
                    if (__instance._length != Spotify.currentTrackLength) __instance._length = Spotify.currentTrackLength;

                    Spotify.timeTrackStarted = Time.time - Spotify.startingPosition / 1000;
                    float currentPosition = (Time.time - Spotify.timeTrackStarted);

                    if (Math.Abs((Jukebox.position / 1000) - currentPosition) > 1) Jukebox.position = (uint)currentPosition * 1000;

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

                    if (Spotify.justStarted && Spotify.playingOnStartup)
                    {
                        Spotify.jukeboxNeedsPlaying = true;
                    }

                    Spotify.justStarted = false;
                }

                // Here we get the player position in relation to the nearest jukebox or speaker and adjust volume accordingly.
                Vector3 position2 = ((Player.main != null) ? Player.main.transform : MainCamera.camera.transform).position;
                float sqrMagnitude = (__instance.soundPosition - position2).sqrMagnitude;
                bool soundPositionNotOrigin = __instance.soundPosition.x != 0 && __instance.soundPosition.y != 0 && __instance.soundPosition.z != 0;
                int volumePercentage = (int)(Spotify.jukeboxVolume * 100);

                // This if/else block handles all distance-related volume and play/pause changes.
                if (!Spotify.manualJukeboxPause && !Spotify.manualSpotifyPause && !Spotify.menuPause && __instance._audible && sqrMagnitude <= 400 && soundPositionNotOrigin)
                {
                    volumePercentage = (int)((Spotify.jukeboxVolume - sqrMagnitude / 400) * 100) + 1;

                    // If PauseOnLeaveToggleValue is true, make sure we resume playback
                    if (MainPatcher.Config.pauseOnLeave && Spotify.jukeboxIsPaused)
                    {
                        new Log("Resuming track - distance");
                        __instance._paused = false;
                        Spotify.jukeboxIsPaused = false;
                        Spotify.jukeboxIsPlaying = true;
                        if (!Spotify.spotifyIsPlaying) Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                        Spotify.spotifyIsPlaying = true;
                    }

                    // Check whether the jukebox is in a SeaTruck.
                    bool seaTruckJukeboxPlaying = null != __instance._instance && null != __instance._instance.GetComponentInParent<SeaTruckSegment>();

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

                    if (Math.Abs(Spotify.spotifyVolume - volumePercentage) <= 1 && Time.time > (Spotify.volumeTimer + 2))
                    {
                        // The volume has been the same for 2 seconds, let's give poor Spotify a break from volume requests.
                        return;
                    }

                    if (Math.Abs(Spotify.spotifyVolume - volumePercentage) > 1) Spotify.volumeTimer = Time.time;

                    volumePercentage += Spotify.volumeModifier; // This ensures Spotify has sound when it's paused/has 0 volume.
                    Spotify.volumeModifier = (Spotify.volumeModifier < 0) ? 0 : -1;

                    if (volumePercentage < 0) volumePercentage = 0;
                    if (volumePercentage > 100) volumePercentage = 100;

                    Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(volumePercentage)));
                    Spotify.spotifyVolume = volumePercentage;
                    
                }
                else if (!Spotify.manualJukeboxPause && !Spotify.manualSpotifyPause && !Spotify.menuPause && !__instance._paused && !__instance._audible && soundPositionNotOrigin) // If music is inaudible, set Spotify volume to 0.
                {
                    // If PauseOnLeaveToggleValue is true, make sure we pause playback
                    if (MainPatcher.Config.pauseOnLeave && !Spotify.jukeboxIsPaused)
                    {
                        new Log("pausing track - distance.");
                        __instance._paused = true;
                        Spotify.jukeboxIsPaused = true;
                        if (Spotify.spotifyIsPlaying) Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                        Spotify.spotifyIsPlaying = false;
                    }

                    if (Spotify.spotifyVolume != 0)
                    {
                        Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(0)));
                        Spotify.spotifyVolume = 0;
                    }
                }
                else if ((Spotify.manualSpotifyPause || __instance._paused || Spotify.menuPause) && !Spotify.jukeboxIsPaused)
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
                    Spotify.manualSpotifyPause = false;
                    if (Spotify.spotifyIsPlaying) Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                    Spotify.spotifyIsPlaying = false;
                }
                else if (Spotify.manualSpotifyPlay || (!__instance._paused && !Spotify.menuPause) && Spotify.jukeboxIsPaused && Spotify.jukeboxIsPlaying)
                {
                    new Log("Resume track");
                    try
                    {
                        if (null == __instance._instance)
                        {
                            new Log("No instance exists, creating instance");
                            __instance._instance = new JukeboxInstance();
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
                    catch (Exception e)
                    {
                        new Error("Something went wrong while editing the instance", e);
                    }

                    Spotify.jukeboxIsPaused = false;
                    Spotify.jukeboxIsPlaying = true;
                    Spotify.manualSpotifyPlay = false;
                    if (!Spotify.spotifyIsPlaying) Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                    Spotify.spotifyIsPlaying = true;
                }
            } catch(Exception e)
            {
                new Error("Something went wrong with updating the Jukebox", e);
            }
        }
    }
}
