﻿using HarmonyLib;
using SpotifyAPI.Web;
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
            if (!MainPatcher.Config.enableModToggle) return;
            new Log("Setting label to " + Spotify.currentTrackTitle, null);
            text = Spotify.currentTrackTitle;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.SetLength))]
        static void SetLengthPrefix(ref uint length)
        {
            if (!MainPatcher.Config.enableModToggle) return;
            length = Spotify.currentTrackLength;
            //new Log("Setting length"); // runs all the time
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.UpdateUI))]
        static void UpdateUIPrefix(JukeboxInstance __instance)
        {
            if (!MainPatcher.Config.enableModToggle) return;
            if (Spotify.jukeboxNeedsPlaying && null != __instance)
            {
                new Log("jukeboxNeedsPlaying: " + Spotify.jukeboxNeedsPlaying, null);
                Spotify.jukeboxNeedsPlaying = false;
                Jukebox.Play(__instance);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnVolume))]
        public static void OnVolumePostfix(JukeboxInstance __instance)
        {
            if (!MainPatcher.Config.enableModToggle) return;
            int volumePercentage = (int) (__instance.volume * 100);

            Spotify.spotifyVolume = volumePercentage;
            Spotify.jukeboxVolume = __instance.volume;
            Spotify.volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(volumePercentage)));
            Jukebox.volume = 0;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonShuffle))]
        public static void OnButtonShufflePostFix(JukeboxInstance __instance)
        {
            if (!MainPatcher.Config.enableModToggle) return;
            Spotify.client.Player.SetShuffle(new PlayerShuffleRequest(__instance.shuffle));
        }        
        
        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonRepeat))]
        public static void OnButtonRepeatPostFix(JukeboxInstance __instance)
        {
            if (!MainPatcher.Config.enableModToggle) return;
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

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnPositionEndDrag))]
        public static void OnPositionEndDragPostFix(JukeboxInstance __instance)
        {
            if (!MainPatcher.Config.enableModToggle) return;
            if (true == Spotify.jukeboxIsPlaying || true == Spotify.jukeboxIsPaused)
            {
                long trackPosition = (long) (Spotify.currentTrackLength * __instance._position); // _position is a percentage
                new Log("End drag occured");
                Spotify.client.Player.SeekTo(new PlayerSeekToRequest(trackPosition) { DeviceId = MainPatcher.Config.deviceId });
                Spotify.timeTrackStarted = Time.time - trackPosition / 1000;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonPlayPause))]
        public static bool OnButtonPlayPausePreFix(JukeboxInstance __instance)
        {
            // This is to stop the method getting called very quickly after the first one.
            if (Spotify.playPauseTimeout + 0.5 > Time.time)
            {
                new Log("very fast consecutive call to method");
                return false;
            }

            Spotify.playPauseTimeout = Time.time;

            if (!MainPatcher.Config.enableModToggle) return true;
            if (!Jukebox.main._paused)
            {
                Spotify.manualJukeboxPause = true;
            } 
            else
            {
                Spotify.manualJukeboxPause = false;
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
    }

    
}
