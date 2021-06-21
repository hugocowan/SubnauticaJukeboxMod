using HarmonyLib;
using QModManager.Utility;
using SpotifyAPI.Web;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox))]
    class JukeboxPatcher
    {
        static AccessTools.FieldRef<Jukebox, List<string>> _playlistRef = AccessTools.FieldRefAccess<Jukebox, List<string>>("_playlist");
        static AccessTools.FieldRef<Jukebox, string> _fileRef = AccessTools.FieldRefAccess<Jukebox, string>("_file");

        [HarmonyPostfix]
        [HarmonyPatch("GetNext")]
        public async static void GetNextPostfix(JukeboxInstance jukebox, bool forward)
        {
            if (forward)
            {
                //Logger.Log(Logger.Level.Info, "Skip next track", null, true);
                await Spotify._spotify.Player.SkipNext(new PlayerSkipNextRequest() { DeviceId = Spotify._device.Id });
            }
            else
            {
                //Logger.Log(Logger.Level.Info, "Skip previous track", null, true);
                await Spotify._spotify.Player.SkipPrevious(new PlayerSkipPreviousRequest() { DeviceId = Spotify._device.Id });
            }

            if (null == MainPatcher._isPlaying || false == MainPatcher._isPlaying)
            {
                await Spotify._spotify.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id });
            }

            await Task.Delay(1000);
            await Spotify.GetTrackInfo();
        }


        [HarmonyPostfix]
        [HarmonyPatch("OnApplicationQuit")]
        public async static void OnApplicationQuitPostfix()
        {
            MainPatcher._isPlaying = null;
            var playbackRequest = new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id };
            await Spotify._spotify.Player.PausePlayback(playbackRequest);
        }

        [HarmonyPostfix]
        [HarmonyPatch("Play")]
        public async static void PlayPostfix()
        {
            //Logger.Log(Logger.Level.Info, "Hey, we are skipping to the next song", null, true);
            Jukebox.volume = 0;

            MainPatcher._isPlaying = true;
            await Spotify._spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify._device.Id });
        }

        [HarmonyPostfix]
        [HarmonyPatch("StopInternal")]
        public async static void StopInternalPostfix()
        {
            //Logger.Log(Logger.Level.Info, "Stopping track", null, true);
            MainPatcher._isPlaying = null;
            MainPatcher._isPaused = false;
            await Spotify._spotify.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id });
            await Spotify._spotify.Player.SeekTo(new PlayerSeekToRequest(0));
        }

        [HarmonyPrefix]
        [HarmonyPatch("UpdateStudio")]
        public static void UpdateStudioPrefix(Jukebox __instance, ref bool ____paused, ref string ____file)
        {
            if (____paused && MainPatcher._isPaused == false)
            {
                //Logger.Log(Logger.Level.Info, "Pausing track", null, true);
                MainPatcher._isPaused = true;
                Spotify._spotify.Player.PausePlayback(new PlayerPausePlaybackRequest() { DeviceId = Spotify._device.Id });
            }
            else if (!____paused && true == MainPatcher._isPaused && true == MainPatcher._isPlaying)
            {
                //Logger.Log(Logger.Level.Info, "Resuming track", null, true);
                MainPatcher._isPaused = false;
                Spotify._spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify._device.Id });
            }

            if (Spotify._jukeboxNeedsUpdating)
            {
                Spotify._jukeboxNeedsUpdating = false;
                _playlistRef(__instance).Add(Spotify._currentTrackTitle);
                _fileRef(__instance) = Spotify._currentTrackTitle;
                Spotify._jukeboxInstanceNeedsUpdating = true;
                JukeboxInstance.NotifyInfo(Spotify._currentTrackTitle, new Jukebox.TrackInfo() { label = Spotify._currentTrackTitle, length = Spotify._currentTrackLength });
            }
        }

        //var newSongRequest = new PlayerAddToQueueRequest("spotify:track:" + track.Id) { DeviceId = availableDevice.Id };
        //await Spotify._spotify.Player.AddToQueue(newSongRequest);

        //private async static Task PlayTrack()
        //{
        //var track = await Spotify._spotify.Tracks.Get("4iV5W9uYEdYUVa79Axb7Rh");

        //// Add a song to the device's queue.

        //    // Skip tracks until we are at the first one.
        //    // This effectively clears the old queue, which is not currently a feature.
        //    while (track.Id != currentTrack.Id)
        //    {
        //        // Skip the current song if it's not the one we want.
        //        await Spotify._spotify.Player.SkipNext(new PlayerSkipNextRequest() { DeviceId = Spotify._device.Id });

        //        // We need this delay or else the song we want can get skipped.
        //        await Task.Delay(250);

        //        // Update what's currently playing.
        //        currentlyPlaying = await Spotify._spotify.Player.GetCurrentPlayback();
        //        currentTrack = (FullTrack)currentlyPlaying.Item;
        //    }
    }
}
