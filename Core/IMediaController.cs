using System.Threading.Tasks;

namespace JukeboxSpotify
{
    internal interface IMediaController
    {
        string ControllerName { get; }

        bool IsReady { get; }

        bool SupportsSourceVolume { get; }

        Task InitializeAsync();

        Task<MediaPlaybackSnapshot> GetPlaybackSnapshotAsync();

        Task<IAudioCaptureSession> CreateAudioCaptureSessionAsync();

        Task PlayAsync();

        Task PauseAsync();

        Task NextAsync();

        Task PreviousAsync();

        Task SeekAsync(long positionMs);

        Task SetSourceVolumeAsync(int volumePercent);

        Task SetShuffleAsync(bool enabled);

        Task SetRepeatModeAsync(MediaRepeatMode repeatMode);

        Task ShutdownAsync();
    }
}