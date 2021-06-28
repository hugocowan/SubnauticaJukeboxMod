using HarmonyLib;
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
            text = Spotify.currentTrackTitle;
            //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Setting label to " + Spotify.currentTrackTitle, null, true);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.SetLength))]
        static void SetLengthPrefix(ref uint length)
        {
            length = Spotify.currentTrackLength;
            //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Setting length to " + Spotify.currentTrackLength + "ms | " + mins + "m" + secs + "s");
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.UpdateUI))]
        static void UpdateUIPrefix(JukeboxInstance __instance)
        {
            if (Spotify.jukeboxNeedsPlaying && Spotify.justStarted && null != __instance)
            {
                //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "jukeboxNeedsPlaying: " + Spotify.jukeboxNeedsPlaying, null, true);
                Spotify.jukeboxNeedsPlaying = false;
                Spotify.justStarted = false;
                Jukebox.Play(__instance);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnVolume))]
        public static void OnVolumePostfix(JukeboxInstance __instance)
        {
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
            Spotify.client.Player.SetShuffle(new PlayerShuffleRequest(__instance.shuffle));
        }        
        
        [HarmonyPostfix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonRepeat))]
        public static void OnButtonRepeatPostFix(JukeboxInstance __instance)
        {
            //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Repeat button pressed. repeat: " + __instance.repeat.ToString(), null, true);

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
            if (true == Spotify.jukeboxIsPlaying || true == Spotify.jukeboxIsPaused)
            {
                long trackPosition = (long) (Spotify.currentTrackLength * __instance._position); // _position is a percentage
                //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "End drag occured. _position: " + __instance._position + " | trackPosition: " + trackPosition + " | trackLength: " + Spotify.currentTrackLength, null, true);
                Spotify.client.Player.SeekTo(new PlayerSeekToRequest(trackPosition) { DeviceId = MainPatcher.Config.deviceId });
                Spotify.timeTrackStarted = Time.time - trackPosition / 1000;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(JukeboxInstance.OnButtonPlayPause))]
        public static bool OnButtonPlayPausePreFix(JukeboxInstance __instance)
        {
            if (!Jukebox.main._paused)
            {
                Spotify.manualPause = true;
            } 
            else
            {
                Spotify.manualPause = false;
            }
            // This is needed for the first time we press play.
            if (!Jukebox.HasFile(__instance._file))
            {
                QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Jukebox doesn't have our track D:", null, false);
                Jukebox.Play(__instance);
                return false;
            }

            return true;
        }
    }

    
}
