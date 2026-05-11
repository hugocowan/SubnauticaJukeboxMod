using System;

namespace JukeboxSpotify
{
    internal interface IAudioCaptureSession : IDisposable
    {
        string SourceId { get; }

        AudioStreamFormat Format { get; }

        bool IsCapturing { get; }

        int AvailableSamples { get; }

        void Start();

        void Stop();

        int Read(float[] destination, int offset, int sampleCount);
    }
}