using System.Threading.Tasks;

namespace JukeboxSpotify
{
    internal sealed class NullMediaController : IMediaController
    {
        public string ControllerName => "None";

        public bool IsReady => false;

        public bool SupportsSourceVolume => false;

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public Task<MediaPlaybackSnapshot> GetPlaybackSnapshotAsync()
        {
            return Task.FromResult(MediaPlaybackSnapshot.Empty);
        }

        public Task<IAudioCaptureSession> CreateAudioCaptureSessionAsync()
        {
            return Task.FromResult<IAudioCaptureSession>(null);
        }

        public Task PlayAsync()
        {
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            return Task.CompletedTask;
        }

        public Task NextAsync()
        {
            return Task.CompletedTask;
        }

        public Task PreviousAsync()
        {
            return Task.CompletedTask;
        }

        public Task SeekAsync(long positionMs)
        {
            return Task.CompletedTask;
        }

        public Task SetSourceVolumeAsync(int volumePercent)
        {
            return Task.CompletedTask;
        }

        public Task SetShuffleAsync(bool enabled)
        {
            return Task.CompletedTask;
        }

        public Task SetRepeatModeAsync(MediaRepeatMode repeatMode)
        {
            return Task.CompletedTask;
        }

        public Task ShutdownAsync()
        {
            return Task.CompletedTask;
        }
    }
}