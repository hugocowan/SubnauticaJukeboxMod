using HarmonyLib;
using QModManager.Utility;
using SpotifyAPI.Web;
using System.Collections.Generic;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox))]
    class JukeboxPatcher
    {
        static AccessTools.FieldRef<Jukebox, List<string>> playlistRef = AccessTools.FieldRefAccess<Jukebox, List<string>>("_playlist");

        [HarmonyPostfix]
        [HarmonyPatch("GetNext")]
        public async static void GetNextPostfix(JukeboxInstance jukebox, bool forward)
        {
            if (Spotify.repeatTrack)
            {
                await Spotify.client.Player.SeekTo(new PlayerSeekToRequest(0));
                return;
            }

            await Spotify.client.Player.SetVolume(new PlayerVolumeRequest(0));
            
            if (forward)
            {
                //Logger.Log(Logger.Level.Info, "Skip next track", null, true);
                await Spotify.client.Player.SkipNext(new PlayerSkipNextRequest() { DeviceId = Spotify.device.Id });
            }
            else
            {
                //Logger.Log(Logger.Level.Info, "Skip previous track", null, true);
                await Spotify.client.Player.SkipPrevious(new PlayerSkipPreviousRequest() { DeviceId = Spotify.device.Id });
            }

            if (true != Spotify.isPlaying)
            {
                await Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id });
            }

            await Spotify.client.Player.SetVolume(new PlayerVolumeRequest(Spotify.volume));
            await Spotify.GetTrackInfo();
        }


        [HarmonyPostfix]
        [HarmonyPatch("OnApplicationQuit")]
        public async static void OnApplicationQuitPostfix()
        {
            if (Spotify.playingOnStartup) return;
            Spotify.isPlaying = null;
            var playbackRequest = new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id };
            await Spotify.client.Player.PausePlayback(playbackRequest);
        }

        [HarmonyPostfix]
        [HarmonyPatch("Play")]
        public async static void PlayPostfix()
        {
            //Logger.Log(Logger.Level.Info, "Hey, we are skipping to the next song", null, true);
            Jukebox.volume = 0;

            Spotify.isPlaying = true;

            try
            {
                await Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify.device.Id });
            } catch
            {
                //Logger.Log(Logger.Level.Info, "This fails when Spotify is already playing", null, true);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("StopInternal")]
        public async static void StopInternalPostfix()
        {
            //Logger.Log(Logger.Level.Info, "Stopping track", null, true);
            Spotify.isPlaying = null;
            Spotify.isPaused = false;
            await Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id });
            await Spotify.client.Player.SeekTo(new PlayerSeekToRequest(0));
        }

        [HarmonyPrefix]
        [HarmonyPatch("UpdateStudio")]
        public static void UpdateStudioPrefix(Jukebox __instance, ref bool ____paused, ref string ____file, ref uint ____length)
        {
            ____length = Spotify.currentTrackLength;

            if (____paused && Spotify.isPaused == false)
            {
                //Logger.Log(Logger.Level.Info, "Pausing track", null, true);
                Spotify.isPaused = true;
                Spotify.client.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify.device.Id });
            }
            else if (!____paused && true == Spotify.isPaused && true == Spotify.isPlaying)
            {
                //Logger.Log(Logger.Level.Info, "Resuming track", null, true);
                Spotify.isPaused = false;
                Spotify.client.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify.device.Id });
            }

            if (Spotify.jukeboxNeedsUpdating)
            {
                Spotify.jukeboxNeedsUpdating = false;
                playlistRef(__instance).Add(Spotify.currentTrackTitle);
                ____file = Spotify.currentTrackTitle;
                
                Spotify.jukeboxInstanceNeedsUpdating = true;
                JukeboxInstance.NotifyInfo(Spotify.currentTrackTitle, new Jukebox.TrackInfo() { label = Spotify.currentTrackTitle, length = Spotify.currentTrackLength });
            }

        }
    }
}
