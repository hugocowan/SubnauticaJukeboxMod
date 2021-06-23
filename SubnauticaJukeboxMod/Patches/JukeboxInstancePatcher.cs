using DebounceThrottle;
using HarmonyLib;
using SpotifyAPI.Web;
using UnityEngine;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(JukeboxInstance))]
    class JukeboxInstancePatcher
    {
        private static AccessTools.FieldRef<JukeboxInstance, float> _positionRef = AccessTools.FieldRefAccess<JukeboxInstance, float>("_position");
        private static ThrottleDispatcher volumeThrottler = new ThrottleDispatcher(100);

        [HarmonyPrefix]
        [HarmonyPatch("SetLabel")]
        static void SetLabelPrefix(ref string text)
        {
            text = Spotify.currentTrackTitle;
            //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Setting label to " + Spotify.currentTrackTitle, null, true);
        }

        [HarmonyPrefix]
        [HarmonyPatch("SetLength")]
        static void SetLengthPrefix(ref uint length)
        {
            length = Spotify.currentTrackLength;
            //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Setting length to " + Spotify.currentTrackLength + "ms | " + mins + "m" + secs + "s");
        }

        [HarmonyPrefix]
        [HarmonyPatch("UpdateUI")]
        static void UpdateUIPrefix(JukeboxInstance __instance, ref float ____position)
        {
            if (Spotify.jukeboxNeedsPlaying && Spotify.justStarted && null != __instance)
            {
                Spotify.jukeboxNeedsPlaying = false;
                Spotify.justStarted = false;
                Jukebox.Play(__instance);
            }

            //_positionRef(__instance) = (float) Spotify.timelinePosition / (float) Spotify.currentTrackLength;
            //QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "_positionRef after: " + _positionRef(__instance), null, true);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnVolume")]
        public static void OnVolumePostfix(float ___volume)
        {
            int volumePercentage = (int) (___volume * 100);

            Spotify.volume = volumePercentage;

            volumeThrottler.Throttle(() => Spotify.client.Player.SetVolume(new PlayerVolumeRequest(volumePercentage)));
            
            Jukebox.volume = 0;
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnButtonShuffle")]
        public static void OnButtonShufflePostFix(ref bool ___shuffle)
        {
            Spotify.client.Player.SetShuffle(new PlayerShuffleRequest(___shuffle));
        }        
        
        [HarmonyPostfix]
        [HarmonyPatch("OnButtonRepeat")]
        public static void OnButtonRepeatPostFix(ref Jukebox.Repeat ___repeat)
        {
            QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Repeat button pressed. ___repeat: " + ___repeat.ToString(), null, true);

            PlayerSetRepeatRequest.State state;

            switch (___repeat.ToString())
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
        [HarmonyPatch("OnPositionEndDrag")]
        public static void OnPositionEndDragPostFix(JukeboxInstance __instance)
        {
            if (true == Spotify.isPlaying || true == Spotify.isPaused)
            {
                long trackPosition = (long) (Spotify.currentTrackLength * _positionRef(__instance)); // _position is a percentage
                QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "End drag occured. _position: " + _positionRef(__instance) + " | trackPosition: " + trackPosition + " | trackLength: " + Spotify.currentTrackLength, null, true);
                Spotify.client.Player.SeekTo(new PlayerSeekToRequest(trackPosition) { DeviceId = Spotify.device.Id });
                Spotify.timeTrackStarted = Time.time - trackPosition / 1000;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnButtonPlayPause")]
        public static bool OnButtonPlayPausePreFix(JukeboxInstance __instance, ref string ____file)
        {
            if (____file != Spotify.currentTrackTitle) ____file = Spotify.currentTrackTitle;
            if (!Jukebox.HasFile(____file))
            {
                QModManager.Utility.Logger.Log(QModManager.Utility.Logger.Level.Info, "Jukebox doesn't have our track D:", null, true);
                Jukebox.Play(__instance);
                return false;
            }

            return true;
        }
    }

    
}
