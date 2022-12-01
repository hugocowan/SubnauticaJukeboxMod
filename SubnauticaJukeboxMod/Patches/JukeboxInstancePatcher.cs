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
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || (!__instance.ConsumePower() && !Vars.justStarted)) return true;
                if (Vars.beyondFiveMins && !Vars.newJukeboxInstance) return false;
                if (Vars.newJukeboxInstance) Vars.newJukeboxInstance = false;
                text = Vars.currentTrackTitle;
            }
            catch (Exception e)
            {
                new Error("Something went wrong with setting the Jukebox label", e);
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.SetLength))]
        static void SetLengthPrefix(ref uint length)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.spotify) return;
                length = Vars.currentTrackLength;
            }
            catch (Exception e)
            {
                new Error("Something went wrong with stopping the track", e);
            }
                                       
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.UpdateUI))]
        static void UpdateUIPrefix(JukeboxInstance __instance)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.spotify) return;
                if (Vars.justStarted && (Vars.playingOnStartup || Vars.manualPlay) && null != __instance)
                {
                    new Log("Starting jukebox as Spotify is already playing. Playing closest Jukebox.");

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
                new Error("Something went wrong starting the JukeboxInstance", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnVolume))]
        public static void OnVolumePostfix(JukeboxInstance __instance)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.spotify || !IsPowered()) return;

                int volumePercentage = (int) (__instance.volume * 100);
                Vars.spotifyVolume = volumePercentage;
                Vars.jukeboxVolume = __instance.volume;
                Vars.volumeThrottler.Throttle(() => Vars.spotify.Player.SetVolume(new PlayerVolumeRequest(volumePercentage)));
                Jukebox.volume = 0;
            }
            catch (Exception e)
            {
                new Error("Something went wrong while changing the volume", e);
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
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.spotify) return true;
                if (!IsPowered()) return false;
                if (__instance.shuffle == Vars.spotifyShuffleState) Vars.spotify.Player.SetShuffle(new PlayerShuffleRequest(!__instance.shuffle));
            }
            catch (Exception e)
            {
                new Error("Something went wrong while setting shuffle", e);
            }

            return true;
        }        
        
        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonRepeat))]
        public static bool OnButtonRepeatPrefix(JukeboxInstance __instance)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.spotify) return true;
                if (!IsPowered()) return false;
                new Log("Repeat button pressed");

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
                Vars.spotify.Player.SetRepeat(new PlayerSetRepeatRequest(state));
            }
            catch (Exception e)
            {
                new Error("Something went wrong when setting the repeat state", e);
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonPrevious))]
        public static bool OnButtonPreviousPrefix(ref JukeboxInstance __instance)
        {
            if (!IsPowered()) return false;
            Vars.jukeboxActionTimer = Time.time;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonNext))]
        public static bool OnButtonNextPrefix(ref JukeboxInstance __instance)
        {
            if (!IsPowered()) return false;
            Vars.jukeboxActionTimer = Time.time;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonStop))]
        public async static void OnButtonStopPostfix(JukeboxInstance __instance)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.spotify || !IsPowered()) return;

                new Log("Stop track");
                Vars.jukeboxIsPaused = false;
                Vars.manualPause = true;
                Vars.manualPlay = false;
                Vars.jukeboxIsRunning = false;
                Vars.startingPosition = 0;
                Vars.jukeboxActionTimer = Time.time;
                await Vars.spotify.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                Vars.volumeThrottler.Throttle(() => Vars.spotify.Player.SetVolume(new PlayerVolumeRequest(100)));
                if (Vars.stopCounter >= 1 || !MainPatcher.Config.stopTwiceForStart)
                {
                    new Log("Setting track to the start");
                    Vars.stopCounter = 0;
                    Vars.timeTrackStarted = Time.time;
                    await Vars.spotify.Player.SeekTo(new PlayerSeekToRequest(0));

                }
                else
                {
                    Vars.stopCounter++;
                }
                
            }
            catch (Exception e)
            {
                new Error("Something went wrong with stopping the track", e);
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
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.spotify || !IsPowered()) return;

                if (Vars.jukeboxIsRunning || Vars.jukeboxIsPaused)
                {
                    int trackPosition = (int) (Vars.currentTrackLength * __instance._position); // _position is a percentage
                    Vars.beyondFiveMins = (trackPosition / 1000) >= 300;
                    new Log("End drag occured");
                    Vars.spotify.Player.SeekTo(new PlayerSeekToRequest(trackPosition));
                    Vars.timeTrackStarted = Time.time - trackPosition / 1000;
                }
            }
            catch (Exception e)
            {
                new Error("Something went wrong with stopping the track", e);
            }
            Vars.positionDrag = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonPlayPause))]
        public static bool OnButtonPlayPausePrefix(JukeboxInstance __instance)
        {
            new Log("Playpause triggered");
            try
            {
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Vars.noTrack || null == Vars.spotify) return true;

                // This is to stop the method getting called very quickly after the first one.
                if (Vars.playPauseTimeout + 0.5 > Time.time)
                {
                    return false;
                }

                if (!IsPowered()) return false;
                Vars.manualPause = false;
                Vars.manualPlay = false;
                Vars.playPauseTimeout = Time.time;
                Vars.stopCounter = 0;
                Vars.volumeTimer = 0;
                Vars.jukeboxVolume = __instance.volume;
                Vars.jukeboxActionTimer = Time.time;

                if (__instance != Vars.currentInstance)
                {
                    if (null != Vars.currentInstance)
                    {
                        new Log("New JukeboxInstance does not match previous JukeboxInstance");
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
                new Error("Something went wrong with stopping the track", e);
            }

            return true;
        }

        private static bool IsPowered()
        {
            if (null != Vars.currentInstance && !Vars.currentInstance.ConsumePower())
            {
                Vars.currentInstance.SetLabel("Unpowered");
                return false;
            }

            return true;
        }
    }
}
