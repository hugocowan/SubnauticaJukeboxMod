using HarmonyLib;
using SpotifyAPI.Web;

namespace JukeboxSpotify
{
    [HarmonyPatch(typeof(Jukebox), "Play")]
    class JukeboxPlayPatcher
    {
        [HarmonyPostfix]
        public async static void Postfix()
        {
            Jukebox.volume = 0;

            MainPatcher._isPlaying = true;
            await Spotify._spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest() { DeviceId = Spotify._device.Id });
        }

        //private async static Task PlayTrack()
        //{
            //var track = await Spotify._spotify.Tracks.Get("4iV5W9uYEdYUVa79Axb7Rh");

            // Add a song to the device's queue.
            //var newSongRequest = new PlayerAddToQueueRequest("spotify:track:" + track.Id) { DeviceId = availableDevice.Id };

            //await Spotify._spotify.Player.AddToQueue(newSongRequest);

            //try
            //{
            //    CurrentlyPlayingContext currentlyPlaying = await Spotify._spotify.Player.GetCurrentPlayback();
            //    FullTrack currentTrack = (FullTrack)currentlyPlaying.Item;

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

            //}
            //catch (Exception e)
            //{
            //    new ErrorHandler(e, "Skipping song went wrong");
            //}

            

            // Play the song.
            
        //}
    }
}
