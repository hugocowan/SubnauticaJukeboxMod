using HarmonyLib;
using SpotifyAPI.Web;
using System;
using UnityEngine;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(JukeboxInstance))]
    class JukeboxInstancePatcher
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.SetLabel))]
        static bool SetLabelPrefix(ref JukeboxInstance __instance, ref string text)
        {
            try
            {
                if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || (!__instance.ConsumePower() && !Vars.justStarted)) return true;
                if (Vars.beyondFiveMins && !Vars.newJukeboxInstance) return false;
                if (Vars.newJukeboxInstance) Vars.newJukeboxInstance = false;
                text = Vars.currentTrackTitle;
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong with setting the Jukebox label : " + e);
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.SetLength))]
        static void SetLengthPrefix(ref uint length)
        {
            try
            {
                if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.client) return;
                length = Vars.currentTrackLength;
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong with setting the track length : " + e);
            }
                                       
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.UpdateUI))]
        static void UpdateUIPrefix(JukeboxInstance __instance)
        {
            try
            {
                if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.client) return;
                if (Vars.justStarted && (Vars.playingOnStartup || Vars.manualPlay) && null != __instance)
                {
                    if (Plugin.config.logging) Plugin.Logger.LogInfo("Starting jukebox as Spotify is already playing. Playing closest Jukebox.");

                    JukeboxInstance closestJukeboxInstance = __instance;
                    float closestJukeboxSquareMagnitude = 9999999999999;

                    // Find the closest JukeboxInstance to the player and pass that one to Jukebox.Play
                    for (int i = 0; i < JukeboxInstance.all.Count; i++)
                    {
                        JukeboxInstance jukeboxInstance = JukeboxInstance.all[i];
                        jukeboxInstance.GetSoundPosition(out Vector3 jukeboxInstancePosition, out float min, out float power);
                        Vector3 playerPosition = ((Player.main != null) ? Player.main.transform : MainCamera.camera.transform).position;
                        float sqrMagnitude = (jukeboxInstancePosition - playerPosition).sqrMagnitude;

                        if (sqrMagnitude < closestJukeboxSquareMagnitude)
                        {
                            closestJukeboxInstance = jukeboxInstance;
                            closestJukeboxSquareMagnitude = sqrMagnitude;
                        }
                    }

                    Jukebox.Play(closestJukeboxInstance);
                    Vars.justStarted = false;
                }
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong starting the JukeboxInstance : " + e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnVolume))]
        public static void OnVolumePostfix(JukeboxInstance __instance)
        {
            try
            {
                if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.client || !IsPowered(__instance)) return;

                int volumePercentage = (int) (__instance.volume * 100);
                Vars.spotifyVolume = volumePercentage;
                Vars.jukeboxVolume = __instance.volume;
                Vars.volumeThrottler.Throttle(() => Vars.client.Player.SetVolume(new PlayerVolumeRequest(volumePercentage)));
                Jukebox.volume = 0;
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong while changing the volume : " + e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.UpdatePositionSlider))]
        public static void UpdatePositionSliderPrefix(JukeboxInstance __instance)
        {
            float trackPosition = (Vars.currentPosition * 1000) / (float)Vars.currentTrackLength; // _position is a percentage

            if (Vars.beyondFiveMins && !Vars.positionDrag)
            {
                __instance._position = trackPosition;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonShuffle))]
        public static bool OnButtonShufflePrefix(JukeboxInstance __instance)
        {
            try
            {
                if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.client) return true;
                if (!IsPowered(__instance)) return false;
                if (__instance.shuffle == Vars.spotifyShuffleState) Vars.client.Player.SetShuffle(new PlayerShuffleRequest(!__instance.shuffle));
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong while setting shuffle : " + e);
            }

            return true;
        }        
        
        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonRepeat))]
        public static bool OnButtonRepeatPrefix(JukeboxInstance __instance)
        {
            try
            {
                if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.client) return true;
                if (!IsPowered(__instance)) return false;
                if (Plugin.config.logging) Plugin.Logger.LogInfo("Repeat button pressed");

                PlayerSetRepeatRequest.State state;

                switch (__instance.repeat.ToString())
                {
                    case "Track":
                        Vars.repeatTrack = true;
                        state = PlayerSetRepeatRequest.State.Track;
                        break;
                    case "All":
                        Vars.repeatTrack = false;
                        state = PlayerSetRepeatRequest.State.Off;
                        break;
                    default:
                        Vars.repeatTrack = false;
                        state = PlayerSetRepeatRequest.State.Context;
                        break;
                }
                Vars.client.Player.SetRepeat(new PlayerSetRepeatRequest(state));
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong when setting the repeat state : " + e);
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonPrevious))]
        public static bool OnButtonPreviousPrefix(ref JukeboxInstance __instance)
        {
            if (!IsPowered(__instance)) return false;
            Vars.jukeboxActionTimestamp = Time.time;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonNext))]
        public static bool OnButtonNextPrefix(ref JukeboxInstance __instance)
        {
            if (!IsPowered(__instance)) return false;
            Vars.jukeboxActionTimestamp = Time.time;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonStop))]
        public static void OnButtonStopPostfix(ref JukeboxInstance __instance)
        {
            try
            {
                if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.client || !IsPowered(__instance)) return;

                if (Plugin.config.logging) Plugin.Logger.LogInfo("Stop track");
                Vars.jukeboxIsPaused = false;
                Vars.manualPause = true;
                Vars.manualPlay = false;
                Vars.jukeboxIsRunning = false;
                Vars.startingPosition = 0;
                Vars.jukeboxActionTimestamp = Time.time;
                Vars.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Plugin.config.deviceId });
                Vars.volumeThrottler.Throttle(() => Vars.client.Player.SetVolume(new PlayerVolumeRequest(100)));
                if (Vars.stopCounter >= 1 || !Plugin.config.stopTwiceForStart)
                {
                    if (Plugin.config.logging) Plugin.Logger.LogInfo("Setting track to the start");
                    Vars.stopCounter = 0;
                    Vars.timeTrackStarted = Time.time;
                    Vars.client.Player.SeekTo(new PlayerSeekToRequest(0));
                }
                else
                {
                    Vars.stopCounter++;
                }
                
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong with stopping the track : " + e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnPositionBeginDrag))]
        public static void OnPositionBeginDragPrefix()
        {
            Vars.positionDrag = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnPositionEndDrag))]
        public static void OnPositionEndDragPostfix(JukeboxInstance __instance)
        {
            try
            {
                if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.client || !IsPowered(__instance)) return;

                if (Vars.jukeboxIsRunning || Vars.jukeboxIsPaused)
                {
                    long trackPosition = (long) (Vars.currentTrackLength * __instance._position); // _position is a percentage
                    Vars.beyondFiveMins = (trackPosition / 1000) >= 300;
                    if (Plugin.config.logging) Plugin.Logger.LogInfo("End drag occured");
                    Vars.client.Player.SeekTo(new PlayerSeekToRequest(trackPosition) { DeviceId = Plugin.config.deviceId });
                    Vars.timeTrackStarted = Time.time - trackPosition / 1000;
                }
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong with moving the track timeline : " + e);
            }
            Vars.positionDrag = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonPlayPause))]
        public static bool OnButtonPlayPausePrefix(JukeboxInstance __instance)
        {
            if (Plugin.config.logging) Plugin.Logger.LogInfo("Playpause triggered: " + Vars.playPauseTimestamp + " | time: " + Time.time);
            try
            {
                if (!Plugin.config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.client) return true;

                // This is to stop the method getting called very quickly after the first one.
                if (Vars.playPauseTimestamp + 0.5 > Time.time)
                {
                    return false;
                }

                if (!IsPowered(__instance)) return false;
                Vars.manualPause = false;
                Vars.manualPlay = false;
                Vars.playPauseTimestamp = Time.time;
                Vars.stopCounter = 0;
                Vars.volumeTimer = 0;
                Vars.jukeboxVolume = __instance.volume;
                Vars.jukeboxActionTimestamp = Time.time;

                if (__instance != Vars.currentInstance)
                {
                    if (null != Vars.currentInstance)
                    {
                        if (Plugin.config.logging) Plugin.Logger.LogInfo("New JukeboxInstance does not match previous JukeboxInstance");
                        Vars.newJukeboxInstance = true;
                    }
                    __instance._file = Vars.defaultTrack;
                    Vars.currentInstance = __instance;
                    Vars.manualPlay = true;
                }
                else if (!Vars.jukeboxIsPaused)
                {
                    Vars.manualPause = true;
                }
                else
                {
                    Vars.manualPlay = true;
                }

                // This is needed for the first time we press play on an inactive jukebox.
                if (!Jukebox.HasFile(__instance._file))
                {
                    Jukebox.Play(__instance);
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                if (Plugin.config.logging) Plugin.Logger.LogError("Something went wrong when playing the Play/Pause button : " + e);
            }

            return true;
        }

        private static bool IsPowered(JukeboxInstance __instance)
        {
            if (!__instance.ConsumePower())
            {
                __instance.SetLabel("Unpowered");
                return false;
            }

            return true;
        }
    }
}
