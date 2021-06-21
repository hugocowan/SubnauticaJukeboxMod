using DebounceThrottle;
using HarmonyLib;
using QModManager.Utility;
using SpotifyAPI.Web;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(JukeboxInstance))]
    class JukeboxInstancePatcher
    {
        static AccessTools.FieldRef<JukeboxInstance, string> fileRef = AccessTools.FieldRefAccess<JukeboxInstance, string>("_file");
        private static AccessTools.FieldRef<JukeboxInstance, float> _positionRef = AccessTools.FieldRefAccess<JukeboxInstance, float>("_position");
        private static AccessTools.FieldRef<JukeboxInstance, UnityEngine.Material> _materialPositionRef = AccessTools.FieldRefAccess<JukeboxInstance, UnityEngine.Material>("_materialPosition");
        private static ThrottleDispatcher volumeThrottler = new ThrottleDispatcher(100);

        [HarmonyPrefix]
        [HarmonyPatch("SetLabel")]
        static void SetLabelPrefix(ref string text)
        {
            text = Spotify.currentTrackTitle;
        }

        [HarmonyPrefix]
        [HarmonyPatch("SetLength")]
        static void SetLengthPrefix(ref uint length)
        {
            length = Spotify.currentTrackLength;

            //var secs = (length / 1000) % 60;
            //var mins = Math.Floor((decimal) ((length / 1000) / 60));

            //Logger.Log(Logger.Level.Info, "Setting length to " + Spotify.currentTrackLength + "ms | " + mins + "m" + secs + "s");
        }

        [HarmonyPrefix]
        [HarmonyPatch("UpdateUI")]
        static void UpdateUIPrefix(JukeboxInstance __instance, ref float ____position, ref float ____cachedPosition)
        {
            Logger.Log(Logger.Level.Info, "_position: " + ____position + " | _cachedPosition " + ____cachedPosition, null, true);
            if (Spotify.jukeboxInstanceNeedsUpdating)
            {
                Spotify.jukeboxInstanceNeedsUpdating = false;

                fileRef(__instance) = Spotify.currentTrackTitle;
                JukeboxInstance.NotifyInfo(Spotify.currentTrackTitle, new Jukebox.TrackInfo() { label = Spotify.currentTrackTitle, length = Spotify.currentTrackLength });

                if (false && Spotify.playingOnStartup && Spotify.init)
                {
                    //Logger.Log(Logger.Level.Info, "currentTrackLength: " + Spotify.currentTrackLength + " | startingPosition: " + Spotify.startingPosition + " | " + Math.Floor((decimal)((Spotify.startingPosition / 1000) / 60)) + "m" + (Spotify.startingPosition / 1000) % 60 + "s", null, true);
                    // I need a float percentage of how far into the track we are starting at.
                    ____position = (float) Spotify.startingPosition / (float) Spotify.currentTrackLength;
                    ____cachedPosition = Spotify.startingPosition;
                    Jukebox.position = Spotify.startingPosition;
                    Logger.Log(Logger.Level.Info, "Modded _position: " + ____position + " | Jukebox.position: " + Jukebox.position, null, true);
                    Jukebox.Play(__instance);
                    Spotify.init = false;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnVolume")]
        public static void OnVolumePostfix(float __volume)
        {
            int volumePercentage = (int) (__volume * 100);

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
            Logger.Log(Logger.Level.Info, "Repeat button pressed. ___repeat: " + ___repeat.ToString(), null, true);

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
                Logger.Log(Logger.Level.Info, "End drag occured. _position: " + _positionRef(__instance) + " | trackPosition: " + trackPosition + " | trackLength: " + Spotify.currentTrackLength, null, true);
                Spotify.client.Player.SeekTo(new PlayerSeekToRequest(trackPosition) { DeviceId = Spotify.device.Id });
            }
        }


        
    }
}
