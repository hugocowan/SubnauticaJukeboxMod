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
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || (!__instance.ConsumePower() && !Spotify.justStarted)) return true;
                if (Spotify.beyondFiveMins) return false;
                text = Spotify.currentTrackTitle;
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
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Spotify.noTrack || null == Spotify.client) return;
                length = Spotify.currentTrackLength;
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
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Spotify.noTrack || null == Spotify.client) return;
                if (Spotify.jukeboxNeedsPlaying && null != __instance)
                {
                    new Log("jukeboxNeedsPlaying: " + Spotify.jukeboxNeedsPlaying, null);
                    Spotify.jukeboxNeedsPlaying = false;
                    Jukebox.Play(__instance);
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
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Spotify.noTrack || null == Spotify.client || !IsPowered(__instance)) return;

                int volumePercentage = (int) (__instance.volume * 100);

                Spotify.spotifyVolume = volumePercentage;
                Spotify.jukeboxVolume = __instance.volume;
                Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(volumePercentage)));
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
            float trackPosition = (Spotify.currentPosition * 1000) / (float)Spotify.currentTrackLength; // _position is a percentage

            if (Spotify.beyondFiveMins && !Spotify.positionDrag)
            {
                __instance._position = trackPosition;
                //Jukebox.main._position = (uint)Spotify.currentPosition * 1000;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonShuffle))]
        public static bool OnButtonShufflePrefix(JukeboxInstance __instance)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Spotify.noTrack || null == Spotify.client) return true;
                if (!IsPowered(__instance)) return false;
                if (__instance.shuffle == Spotify.spotifyShuffleState) Spotify.client.Player.SetShuffle(new PlayerShuffleRequest(!__instance.shuffle));
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
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Spotify.noTrack || null == Spotify.client) return true;
                if (!IsPowered(__instance)) return false;
                new Log("Repeat button pressed");

                PlayerSetRepeatRequest.State state;

                switch (__instance.repeat.ToString())
                {
                    case "Track":
                        Spotify.repeatTrack = true;
                        state = PlayerSetRepeatRequest.State.Track;
                        break;
                    case "All":
                        Spotify.repeatTrack = false;
                        state = PlayerSetRepeatRequest.State.Context;
                        break;
                    default:
                        Spotify.repeatTrack = false;
                        state = PlayerSetRepeatRequest.State.Off;
                        break;
                }

                Spotify.client.Player.SetRepeat(new PlayerSetRepeatRequest(state));
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
            if (!IsPowered(__instance)) return false;
            Spotify.lastJukeboxActionTime = Time.time;
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonNext))]
        public static bool OnButtonNextPrefix(ref JukeboxInstance __instance)
        {
            if (!IsPowered(__instance)) return false;
            Spotify.lastJukeboxActionTime = Time.time;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonStop))]
        public static void OnButtonStopPostfix(ref JukeboxInstance __instance)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Spotify.noTrack || null == Spotify.client || !IsPowered(__instance)) return;

                new Log("Stop track");
                Spotify.isPlaying = false;
                Spotify.startingPosition = 0;
                Spotify.lastJukeboxActionTime = Time.time;

                Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(100)));
                if (Spotify.stopCounter >= 1 || !MainPatcher.Config.stopTwiceForStart)
                {
                    Spotify.stopCounter = 0;
                    Spotify.timeTrackStarted = Time.time;
                    Spotify.client.Player.SeekTo(new PlayerSeekToRequest(0));
                }
                else
                {
                    Spotify.stopCounter++;
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
            Spotify.positionDrag = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnPositionEndDrag))]
        public static void OnPositionEndDragPostfix(JukeboxInstance __instance)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Spotify.noTrack || null == Spotify.client || !IsPowered(__instance)) return;

                if (Spotify.isPlaying)
                {
                    long trackPosition = (long) (Spotify.currentTrackLength * __instance._position); // _position is a percentage
                    Spotify.beyondFiveMins = (trackPosition / 1000) >= 300;
                    new Log("End drag occured");
                    Spotify.client.Player.SeekTo(new PlayerSeekToRequest(trackPosition) { DeviceId = MainPatcher.Config.deviceId });
                    Spotify.timeTrackStarted = Time.time - trackPosition / 1000;
                }
            }
            catch (Exception e)
            {
                new Error("Something went wrong with stopping the track", e);
            }

            Spotify.positionDrag = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonPlayPause))]
        public static bool OnButtonPlayPausePrefix(JukeboxInstance __instance)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || JukeboxInstance.all.Count == 0 || Spotify.noTrack || null == Spotify.client) return true;

                // This is to stop the method getting called very quickly after the first one.
                if (Spotify.playPauseTimeout + 0.5 > Time.time)
                {
                    return false;
                }

                if (!IsPowered(__instance)) return false;

                Spotify.playPauseTimeout = Time.time;
                Spotify.stopCounter = 0;
                Spotify.volumeTimer = 0;
                Spotify.jukeboxVolume = __instance.volume;
                Spotify.lastJukeboxActionTime = Time.time;

                if (__instance != Spotify.currentInstance)
                {
                    if (null != Spotify.currentInstance) new Log("New JukeboxInstance does not match previous JukeboxInstance");
                    Spotify.currentInstance = __instance;
                }
                else
                {
                    Spotify.isPlaying = true;
                }
                // This is needed for the first time we press play.
                if (!Jukebox.HasFile(__instance._file))
                {
                    new Log("Jukebox doesn't have our track");
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
