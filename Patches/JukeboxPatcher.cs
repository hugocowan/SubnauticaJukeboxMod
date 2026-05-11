using HarmonyLib;
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
                if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || !Vars.HasActiveMediaController) return;

                if (Vars.repeatTrack)
                {
                    await Plugin.MediaController.SeekAsync(0);
                    Vars.timeTrackStarted = Time.time;
                    Vars.startingPosition = 0;
                    return;
                }

                if (forward)
                {
                    if (Plugin.config.logging) Plugin.Logger.LogInfo("Skip next track");
                    await Plugin.MediaController.NextAsync();
                }
                else
                {
                    if (Plugin.config.logging) Plugin.Logger.LogInfo("Skip previous track");
                    await Plugin.MediaController.PreviousAsync();
                }

                if (!Vars.jukeboxIsRunning)
                {
                    await Plugin.MediaController.PauseAsync();
                }
                Vars.timeTrackStarted = Time.time;
                Vars.startingPosition = 1000;
                Vars.manualPause = false;
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong with getting next/prev track : " + e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Jukebox.OnApplicationQuit))]
        public static void OnApplicationQuitPrefix()
        {
            try
            {
                if (Plugin.config.logging) Plugin.Logger.LogInfo("Application Quit");
                uGUI_SceneLoadingPatcher.loadingDone = false;
                if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || !Vars.HasActiveMediaController) return;
                Vars.volumeThrottler.Throttle(() => { }); // Clear the throttler
                if (!Vars.playingOnStartup)
                {
                    Plugin.MediaController.PauseAsync().Forget("Pausing media on application quit");
                    Vars.jukeboxIsRunning = false;
                }
                JukeboxFmodAudioEmitter.StopAll();
                Plugin.MediaController.ShutdownAsync().Forget("Shutting down media controller");
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong while quitting the application : " + e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Jukebox.Play))]
        public static void PlayPrefix(Jukebox __instance)
        {
            try
            {
                if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || !Vars.HasActiveMediaController) return;

                if (Plugin.config.logging) Plugin.Logger.LogInfo("Play track");
                Jukebox.volume = 0;
                Vars.jukeboxIsRunning = true;
                Vars.manualPause = false;
                Vars.manualPlay = false;
                Vars.stopCounter = 0;
                Vars.volumeTimer = 0;

                try
                {
                    Plugin.MediaController.PlayAsync().Forget("Resuming media playback");
                    if (!Plugin.MediaController.SupportsSourceVolume && __instance != null && __instance._instance != null)
                    {
                        JukeboxFmodAudioEmitter.GetOrCreate(__instance._instance)
                            .EnsurePlayingAsync(Plugin.MediaController, Vars.jukeboxVolume)
                            .Forget("Starting native jukebox audio");
                    }
                }
                catch (Exception e)
                {
                    if (Plugin.config.logging) Plugin.Logger.LogError("Resume failed, likely because the media source is already playing : " + e);
                }
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong with playing the track : " + e);
            }

            return;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.HandleOpenError))]
        public static void HandleOpenErrorPostfix(Jukebox __instance)
        {
            if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || !Vars.HasActiveMediaController) return;
            if (Plugin.config.logging) Plugin.Logger.LogInfo("We have an open error D: this._failed: " + __instance._failed);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Jukebox.UpdateLowLevel))]
        public static void UpdateLowLevelPrefix(Jukebox __instance)
        {
            if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || !Vars.HasActiveMediaController) return;
            __instance._file = Vars.defaultTrack; // This avoids errors and generally makes the jukebox very, Very happy.
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Jukebox.UpdateStudio))]
        public static void UpdateStudioPostfix(Jukebox __instance)
        {
            try
            {
                if (Plugin.config.enableModToggle && Vars.HasActiveMediaController)
                {
                    KeepAlive();
                }

                if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || !Vars.HasActiveMediaController || !uGUI_SceneLoadingPatcher.loadingDone)
                {
                    // If the mod has been disabled, make sure the jukebox is reset.
                    if (Vars.resetJukebox) ResetJukebox(__instance);
                    return;
                }

                // If we don't have a jukebox instance, there is nothing more to be done.
                if (null == __instance || null == __instance._instance) return;

                if (0 != Jukebox.volume) Jukebox.volume = 0; // If we have toggled the mod off/on, this will not be 0 anymore.

                if (Vars.currentPosition >= 300)
                {
                    Vars.beyondFiveMins = true;
                    __instance._position = (uint)Vars.currentPosition * 1000;
                }
                else
                {
                    Vars.beyondFiveMins = false;
                }

                if (Vars.jukeboxNeedsUpdating) UpdateJukebox(__instance);

                if (Vars.justStarted && Vars.jukeboxIsRunning) Vars.justStarted = false;

                bool soundPositionNotOrigin = __instance.soundPosition.x != 0 && __instance.soundPosition.y != 0 && __instance.soundPosition.z != 0;
                bool isPowered = __instance._instance.ConsumePower();

                // Check if we need to pause/resume the jukebox.
                if (
                    !Vars.manualPlay &&
                    (!__instance._paused || !Vars.jukeboxIsPaused) &&
                    (
                        !isPowered ||
                        Vars.manualPause || Vars.menuPause ||
                        (!__instance._audible && soundPositionNotOrigin && Plugin.config.pauseOnLeave)
                    )
                )
                {
                    if (Plugin.config.logging) Plugin.Logger.LogInfo($"Pause track. __instance._paused: " + __instance._paused +
                        " | jukeboxIsPaused: " + Vars.jukeboxIsPaused + " | manualPause: " + Vars.manualPause + " | menuPause: " + Vars.menuPause +
                        " | audible: " + !__instance._audible + " | soundPositionNotOrigin: " + soundPositionNotOrigin + " | config.pauseOnLeave:" + Plugin.config.pauseOnLeave);
                    Pause(__instance, isPowered);
                }
                else if (
                    isPowered &&
                    (__instance._paused || Vars.jukeboxIsPaused) &&
                    !Vars.manualPause && !Vars.menuPause &&
                    (
                        (__instance._audible && soundPositionNotOrigin && Plugin.config.pauseOnLeave && Vars.distancePause) ||
                        Vars.wasPlayingBeforeMenuPause ||
                        (__instance._audible && Vars.manualPlay)
                    )
                )
                {
                    if (Plugin.config.logging) Plugin.Logger.LogInfo($"Resume track. __instance._paused: " + __instance._paused +
                        " | jukeboxIsPaused: " + Vars.jukeboxIsPaused + " | manualPause: " + Vars.manualPause + " | menuPause: " + Vars.menuPause +
                        " | audible: " + !__instance._audible + " | soundPositionNotOrigin: " + soundPositionNotOrigin + "config.pauseOnLeave:" + Plugin.config.pauseOnLeave +
                        " | distancePause: " + Vars.distancePause + " | wasPlayingBeforeMenuPause: " + Vars.wasPlayingBeforeMenuPause + " | manualPlay: " + Vars.manualPlay);
                    Resume(__instance);
                }

                UpdateVolume(__instance, isPowered, soundPositionNotOrigin);

            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong with updating the Jukebox : " + e);
            }
        }

        private static void UpdateJukebox(Jukebox __instance)
        {
            if (!__instance._audible || !__instance._instance.ConsumePower()) return;
            Vars.jukeboxNeedsUpdating = false;
            if (!__instance._playlist.Contains(Vars.currentTrackTitle)) __instance._playlist.Add(Vars.currentTrackTitle);

            // This makes sure the timeline length is right.
            if (__instance._length != Vars.currentTrackLength) __instance._length = Vars.currentTrackLength;
            Vars.timeTrackStarted = Time.time - Vars.startingPosition / 1000;
            Vars.currentPosition = (Time.time - Vars.timeTrackStarted);

            // Only change the track position if the timeline is off by more than a second and we have't just started.
            if (Math.Abs((Jukebox.position / 1000) - Vars.currentPosition) > 1 && !Vars.justStarted)
            {
                if (Plugin.config.logging) Plugin.Logger.LogInfo("Changing Jukebox position");
                Jukebox.position = (uint)Vars.currentPosition * 1000;
            }

            // This updates the track label in the JukeboxInstance object. It's the only place it needs changing
            if (null != __instance._instance)
            {
                Vars.currentInstance = __instance._instance;
                if (__instance._instance.file != Vars.currentTrackTitle) __instance._instance.file = Vars.currentTrackTitle;
            }
            else
            { // If there's no jukebox playing, we update every jukebox instance's label and length.
                for (int i = 0; i < JukeboxInstance.all.Count; i++)
                {
                    JukeboxInstance jukeboxInstance = JukeboxInstance.all[i];
                    jukeboxInstance.SetLabel(Vars.currentTrackTitle);
                    jukeboxInstance.SetLength(Vars.currentTrackLength);
                }
            }
        }

        private static void Pause(Jukebox __instance, bool isPowered)
        {
            if (__instance != null && __instance._instance != null)
            {
                JukeboxFmodAudioEmitter.GetOrCreate(__instance._instance).SetPlaybackActive(false);
            }

            if (!__instance._audible) Vars.distancePause = true;

            if (Vars.manualPause && __instance._instance.canvas.enabled)
            {
                __instance._instance.OnButtonPlayPause();
            }
            else
            {
                __instance._paused = true;
            }
            Vars.jukeboxIsPaused = true;
            Vars.jukeboxActionTimestamp = Time.time;
            Vars.manualPause = false;
            Vars.manualPlay = false;
            Plugin.MediaController.PauseAsync().Forget("Pausing media source");
        }

        private static void Resume(Jukebox __instance)
        {
            if (__instance != null && __instance._instance != null)
            {
                JukeboxFmodAudioEmitter.GetOrCreate(__instance._instance)
                    .EnsurePlayingAsync(Plugin.MediaController, Vars.jukeboxVolume)
                    .Forget("Resuming native jukebox audio");
            }

            if (Vars.manualPlay && __instance._instance.canvas.enabled)
            {
                __instance._instance.OnButtonPlayPause();
            }
            else
            {
                __instance._paused = false;
            }
            Vars.jukeboxIsPaused = false;
            Vars.wasPlayingBeforeMenuPause = false;
            Vars.manualPlay = false;
            Vars.manualPause = false;
            Vars.jukeboxActionTimestamp = Time.time;
            Vars.distancePause = false;
            Plugin.MediaController.PlayAsync().Forget("Resuming media source");
        }

        private static void UpdateVolume(Jukebox __instance, bool isPowered, bool soundPositionNotOrigin)
        {
            if (isPowered && soundPositionNotOrigin && __instance._audible)
            {
                if (__instance._instance != null)
                {
                    JukeboxFmodAudioEmitter.GetOrCreate(__instance._instance).SetBaseVolume(Vars.jukeboxVolume);
                }
            }
        }

        private static void KeepAlive()
        {
            // Keep checking for track updates
            if (Time.time > (Vars.getTrackTimer + 1))
            {
                Vars.getTrackTimer = Time.time;
                MediaPlaybackCoordinator.RefreshAsync().Forget("Refreshing media playback state");
            }
        }

        private static void ResetJukebox(Jukebox __instance)
        {
            Vars.reset();

            if (__instance._instance)
            {
                JukeboxFmodAudioEmitter.StopAll();
                __instance._instance.file = Vars.defaultTrack;
                Jukebox.position = 0;
                Jukebox.GetNext(__instance._instance, true);
                Jukebox.Stop();
            }
        }
    }
}
