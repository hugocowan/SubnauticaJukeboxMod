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
        static void UpdateUIPrefix(JukeboxInstance __instance)
        {
            if (
                (Spotify.jukeboxNeedsPlaying) //|| (Spotify.isCurrentlyPlaying && !Jukebox.isStartingOrPlaying)
                && null != __instance
                )
            {
                Spotify.jukeboxNeedsPlaying = false;
                Jukebox.Play(__instance);
            }
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
    }

    
}
