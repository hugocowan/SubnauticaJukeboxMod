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
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Spotify.noTrack || null == Spotify.client) return;

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

                if (!Spotify.isPlaying)
                {
                    await Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                }

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
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Spotify.noTrack || null == Spotify.client) return;
                Spotify.volumeThrottler.Throttle(() => { }); // Clear the throttler
                if (!Spotify.playingOnStartup)
                {
                    var playbackRequest = new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId };
                    Spotify.client.Player.PausePlayback(playbackRequest);
                    Spotify.isPlaying = false;
                }
                
                Spotify.client.Player.SetVolume(new PlayerVolumeRequest(100));
            }
            catch (Exception e)
            {
                new Error("Something went wrong while quitting the application", e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Jukebox.Play))]
        public static void PlayPrefix(Jukebox __instance)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || null == Spotify.client) return;

                new Log("Play track");
                Jukebox.volume = 0;
                Spotify.isPlaying = true;
                Spotify.stopCounter = 0;
                Spotify.volumeTimer = 0;

                try
                {
                    Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
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

            return;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.HandleOpenError))]
        public static void HandleOpenErrorPostfix(Jukebox __instance)
        {
            if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Spotify.noTrack || null == Spotify.client) return;
            new Log("We have an open error D: this._failed: " + __instance._failed);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Jukebox.UpdateLowLevel))]
        public static void UpdateLowLevelPrefix(Jukebox __instance)
        {
            if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Spotify.noTrack || null == Spotify.client) return;
            __instance._file = "event:/jukebox/jukebox_takethedive"; // This avoids errors and generally makes the jukebox very, Very happy.
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.UpdateStudio))]
        public static void UpdateStudioPostfix(Jukebox __instance)
        {
            try
            {
                if (MainPatcher.Config.enableModToggle && null != Spotify.client)
                {
                    KeepAlive();
                }

                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Spotify.noTrack || null == Spotify.client || !uGUI_SceneLoadingPatcher.loadingDone)
                {
                    // If the mod has been disabled, make sure the jukebox is reset.
                    if (Spotify.resetJukebox) ResetJukebox(__instance);
                    return;
                }

                if (0 != Jukebox.volume) Jukebox.volume = 0; // If we have toggled the mod off/on, this will not be 0 anymore.

                if (Spotify.jukeboxNeedsUpdating) UpdateJukebox(__instance);

                if (Spotify.currentPosition >= 300)
                {
                    Spotify.beyondFiveMins = true;
                    __instance._position = (uint)Spotify.currentPosition * 1000;
                }

                // If we don't have a jukebox instance, there is nothing more to be done.
                if (null == __instance._instance) return;

                // Here we get the player position in relation to the nearest jukebox or speaker and adjust volume accordingly.
                Vector3 position2 = ((Player.main != null) ? Player.main.transform : MainCamera.camera.transform).position;
                float sqrMagnitude = (__instance.soundPosition - position2).sqrMagnitude;
                bool soundPositionNotOrigin = __instance.soundPosition.x != 0 && __instance.soundPosition.y != 0 && __instance.soundPosition.z != 0;
                bool isPowered = __instance._instance.ConsumePower();

                // This if/else block handles all distance-related volume and play/pause changes.
                if (isPowered && !Spotify.menuPause && (Spotify.isPlaying || Spotify.distancePause) && sqrMagnitude <= 400 && soundPositionNotOrigin)
                {
                    int volumePercentage = (int)((Spotify.jukeboxVolume - sqrMagnitude / 400) * 100) + 1;

                    // If PauseOnLeaveToggleValue is true and we paused due to distance, make sure we resume playback
                    if (MainPatcher.Config.pauseOnLeave && !Spotify.isPlaying && Spotify.distancePause)
                    {
                        new Log("Resume track - distance");
                        Spotify.distancePause = false;
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
                else if (Spotify.isPlaying && !Spotify.menuPause && !__instance._paused && !__instance._audible && soundPositionNotOrigin) 
                {
                    // If PauseOnLeaveToggleValue is true, make sure we pause playback
                    if (MainPatcher.Config.pauseOnLeave)
                    {
                        new Log("Pause track - distance.");
                        Spotify.distancePause = true;
                        Pause(__instance, isPowered);
                    }

                    if (Spotify.spotifyVolume != 0)
                    {
                        Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(0)));
                        Spotify.spotifyVolume = 0;
                    }
                }
                else if ((!isPowered && !__instance._paused) || ((__instance._paused || Spotify.menuPause) && Spotify.isPlaying) || (Spotify.playStateChange && !Spotify.isPlaying))
                {
                    new Log("Pause track");
                    Pause(__instance, isPowered);
                }
                else if (isPowered && !__instance._paused && !Spotify.menuPause && !Spotify.isPlaying || (Spotify.playStateChange && Spotify.isPlaying))
                {
                    new Log("Resume track");
                    Resume(__instance);
                }
                else if (Spotify.playStateChange)
                {
                    Spotify.playStateChange = false;
                    Spotify.isPlaying ? 
                        (new Log("Resume track - Spotify client manual play"), Resume(__instance, isPowered)) : 
                        (new Log("Pause track - Spotify client manual pause"), Pause(__instance, isPowered));
                }

                if (!isPowered && Spotify.isPlaying)
                {
                    new Log("Pause track - no power");
                    Pause(__instance, isPowered);
                }

            } catch(Exception e)
            {
                new Error("Something went wrong while updating the Jukebox", e);
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

            // Only update the timeline if it's off by more than a second and we haven't just started.
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

            if (Spotify.justStarted && Spotify.playingOnStartup && !Spotify.isPlaying)
            {
                Spotify.jukeboxNeedsPlaying = true;
            }

            Spotify.justStarted = false;
        }

        private static void Pause(Jukebox __instance, bool isPowered)
        {
            if (!isPowered)
            {
                if (Spotify.spotifyVolume != 0) Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(0)));
                Spotify.spotifyVolume = 0;
                Spotify.lastJukeboxActionTime = Time.time;
            }

            if (__instance._instance.canvas.enabled)
            {
                __instance._instance.OnButtonPlayPause();
            }
            else
            {
                __instance._paused = true;
            }
            Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
            Spotify.isPlaying = false;
        }

        private static void Resume(Jukebox __instance)
        {
            if (__instance._instance.canvas.enabled)
            {
                __instance._instance.OnButtonPlayPause();
            }
            else
            {
                __instance._paused = false;
            }
            Spotify.isPlaying = true;
            Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
        }

        private static void KeepAlive()
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

        private static void ResetJukebox(Jukebox __instance)
        {
            Spotify.resetJukebox = false;

            if (Spotify.isPlaying)
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
    }
}
