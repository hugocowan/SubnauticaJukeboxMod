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
        static void SetLabelPrefix(ref string text)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle) return;
                new Log("Setting label to " + Spotify.currentTrackTitle, null);
                text = Spotify.currentTrackTitle;
            }
            catch (Exception e)
            {
                new Error("Something went wrong with setting the Jukebox label", e);
            }

        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.SetLength))]
        static void SetLengthPrefix(ref uint length)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return;
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
                if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return;
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
                if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return;
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

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonShuffle))]
        public static void OnButtonShufflePostFix(JukeboxInstance __instance)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return;
                Spotify.client.Player.SetShuffle(new PlayerShuffleRequest(__instance.shuffle));
            }
            catch (Exception e)
            {
                new Error("Something went wrong while setting shuffle", e);
            }
        }        
        
        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonRepeat))]
        public static void OnButtonRepeatPostFix(JukeboxInstance __instance)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return;
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
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonStop))]
        public async static void OnButtonStopPostfix()
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return;
                new Log("Stop track");
                Spotify.jukeboxIsPaused = false;
                Spotify.manualJukeboxPause = true;
                if (Spotify.spotifyIsPlaying) await Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = MainPatcher.Config.deviceId });
                Spotify.spotifyIsPlaying = false;
                Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(100)));
                if (Spotify.stopCounter >= 1 || !MainPatcher.Config.stopTwiceForStart)
                {
                    Spotify.stopCounter = 0;
                    await Spotify.client.Player.SeekTo(new PlayerSeekToRequest(0));
                }
                else
                {
                    Spotify.stopCounter++;
                }
                Spotify.jukeboxIsPlaying = false;
                Spotify.timeTrackStarted = Time.time;
                Spotify.startingPosition = 0;
            }
            catch (Exception e)
            {
                new Error("Something went wrong with stopping the track", e);
            }

        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnPositionEndDrag))]
        public static void OnPositionEndDragPostFix(JukeboxInstance __instance)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return;
                if (Spotify.jukeboxIsPlaying || Spotify.jukeboxIsPaused)
                {
                    long trackPosition = (long) (Spotify.currentTrackLength * __instance._position); // _position is a percentage
                    new Log("End drag occured");
                    Spotify.client.Player.SeekTo(new PlayerSeekToRequest(trackPosition) { DeviceId = MainPatcher.Config.deviceId });
                    Spotify.timeTrackStarted = Time.time - trackPosition / 1000;
                }
            }
            catch (Exception e)
            {
                new Error("Something went wrong with stopping the track", e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonPlayPause))]
        public static bool OnButtonPlayPausePreFix(JukeboxInstance __instance)
        {
            try
            {
                if (!MainPatcher.Config.enableModToggle || Spotify.noTrack || null == Spotify.client) return true;

                // This is to stop the method getting called very quickly after the first one.
                if (Spotify.playPauseTimeout + 0.5 > Time.time)
                {
                    new Log("very fast consecutive call to method");
                    return false;
                }

                Spotify.manualSpotifyPause = false;
                Spotify.playPauseTimeout = Time.time;
                Spotify.stopCounter = 0;

                if (__instance != Spotify.currentInstance)
                {
                    new Log("New JukeboxInstance does not match previous JukeboxInstance");
                    Spotify.currentInstance = __instance;
                    Jukebox.volume = __instance.volume;
                    Spotify.jukeboxVolume = __instance.volume;
                    Spotify.manualJukeboxPause = false;
                }
                else if (!Spotify.jukeboxIsPaused)
                {
                    Spotify.manualJukeboxPause = true;
                }
                else
                {
                    Spotify.manualJukeboxPause = false;
                    Spotify.jukeboxIsPlaying = true;
                }
                // This is needed for the first time we press play.
                if (!Jukebox.HasFile(__instance._file))
                {
                    new Log("Jukebox doesn't have our track D:");
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
    }
}
