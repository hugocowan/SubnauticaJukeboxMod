using FMOD;
using FMODUnity;
using System;
using System.Runtime.InteropServices;

namespace JukeboxSpotify
{
    internal sealed class JukeboxFmodStreamingBridge : IDisposable
    {
        private static readonly SOUND_PCMREAD_CALLBACK PcmReadCallback = OnPcmRead;
        private readonly AudioStreamFormat _format;
        private readonly PcmRingBuffer _pcmBuffer;
        private readonly uint _soundLengthBytes;
        private readonly GCHandle _selfHandle;
        private long _lastOverflowLogTick;
        private long _lastUnderrunLogTick;
        private bool _disposed;
        private Sound _sound;
        private float[] _callbackBuffer;

        public JukeboxFmodStreamingBridge(AudioStreamFormat format, int bufferMilliseconds = 2500)
        {
            if (bufferMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferMilliseconds));
            }

            _format = format;
            _pcmBuffer = new PcmRingBuffer(Math.Max(2048, (format.SamplesPerSecond * bufferMilliseconds) / 1000));
            _soundLengthBytes = checked((uint)(Math.Max(format.SamplesPerSecond, 2048) * sizeof(float)));
            _selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);
            Plugin.LogDebug("Created jukebox streaming audio bridge. sampleRate=" + format.SampleRate + ", channelCount=" + format.ChannelCount +
                ", bufferMilliseconds=" + bufferMilliseconds + ", ringBufferCapacity=" + _pcmBuffer.Capacity + ", soundLengthBytes=" + _soundLengthBytes + ".");
        }

        public int BufferedSamples => _pcmBuffer.Count;

        public bool IsCreated => _sound.handle != IntPtr.Zero;

        public Sound Sound => _sound;

        public bool EnsureCreated(string soundName)
        {
            if (_sound.handle != IntPtr.Zero)
            {
                return true;
            }

            CREATESOUNDEXINFO exInfo = new()
            {
                cbsize = Marshal.SizeOf<CREATESOUNDEXINFO>(),
                length = _soundLengthBytes,
                numchannels = _format.ChannelCount,
                defaultfrequency = _format.SampleRate,
                format = SOUND_FORMAT.PCMFLOAT,
                decodebuffersize = (uint)Math.Max(_format.SampleRate / 10, 1024),
                pcmreadcallback = PcmReadCallback,
                userdata = GCHandle.ToIntPtr(_selfHandle),
            };

            MODE mode = MODE.OPENUSER | MODE.LOOP_NORMAL | MODE._3D | MODE._3D_WORLDRELATIVE | MODE.CREATESTREAM;
            RESULT result = RuntimeManager.CoreSystem.createSound(IntPtr.Zero, mode, ref exInfo, out _sound);
            if (result != RESULT.OK)
            {
                Plugin.LogDebugError("Failed to create FMOD streaming sound '" + soundName + "'. result=" + result);
                _sound.clearHandle();
                return false;
            }

            result = _sound.setUserData(GCHandle.ToIntPtr(_selfHandle));
            if (result != RESULT.OK)
            {
                Plugin.LogDebugError("Failed to attach userdata to FMOD streaming sound '" + soundName + "'. result=" + result);
                _sound.release();
                _sound.clearHandle();
                return false;
            }

            Plugin.LogDebug("Created FMOD streaming sound '" + soundName + "'. sampleRate=" + _format.SampleRate + ", channelCount=" + _format.ChannelCount + ".");
            return true;
        }

        public int Write(float[] samples, int sampleCount)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            int requestedSamples = Math.Min(sampleCount, samples.Length);
            int writtenSamples = _pcmBuffer.Write(samples, 0, requestedSamples);
            if (writtenSamples < requestedSamples && ShouldLog(ref _lastOverflowLogTick))
            {
                Plugin.LogDebug("Streaming bridge buffer overflow. requestedSamples=" + requestedSamples + ", writtenSamples=" + writtenSamples +
                    ", bufferedSamples=" + _pcmBuffer.Count + ", capacity=" + _pcmBuffer.Capacity + ".");
            }

            return writtenSamples;
        }

        public void Reset()
        {
            _pcmBuffer.Clear();
            Plugin.LogDebug("Reset the jukebox streaming bridge PCM buffer.");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Reset();

            if (_sound.handle != IntPtr.Zero)
            {
                RESULT result = _sound.release();
                if (result != RESULT.OK)
                {
                    Plugin.LogDebugError("Failed to release the FMOD streaming sound. result=" + result);
                }

                _sound.clearHandle();
            }

            if (_selfHandle.IsAllocated)
            {
                _selfHandle.Free();
            }
        }

        private static RESULT OnPcmRead(IntPtr soundHandle, IntPtr data, uint dataLength)
        {
            int sampleCount = checked((int)(dataLength / sizeof(float)));
            try
            {
                Sound sound = new() { handle = soundHandle };
                RESULT userDataResult = sound.getUserData(out IntPtr userData);
                if (userDataResult != RESULT.OK || userData == IntPtr.Zero)
                {
                    ZeroBuffer(data, sampleCount);
                    return RESULT.OK;
                }

                if (GCHandle.FromIntPtr(userData).Target is not JukeboxFmodStreamingBridge bridge || bridge._disposed)
                {
                    ZeroBuffer(data, sampleCount);
                    return RESULT.OK;
                }

                bridge.FillPcmBuffer(data, sampleCount);
                return RESULT.OK;
            }
            catch (Exception e)
            {
                ZeroBuffer(data, sampleCount);
                Plugin.LogDebugError("FMOD PCM read callback failed", e);
                return RESULT.OK;
            }
        }

        private void FillPcmBuffer(IntPtr data, int sampleCount)
        {
            EnsureCallbackBuffer(sampleCount);
            int samplesRead = _pcmBuffer.Read(_callbackBuffer, 0, sampleCount);
            if (samplesRead < sampleCount)
            {
                Array.Clear(_callbackBuffer, samplesRead, sampleCount - samplesRead);

                if (ShouldLog(ref _lastUnderrunLogTick))
                {
                    Plugin.LogDebug("Streaming bridge underrun. requestedSamples=" + sampleCount + ", samplesRead=" + samplesRead +
                        ", bufferedSamples=" + _pcmBuffer.Count + ", capacity=" + _pcmBuffer.Capacity + ".");
                }
            }

            Marshal.Copy(_callbackBuffer, 0, data, sampleCount);
        }

        private void EnsureCallbackBuffer(int sampleCount)
        {
            if (_callbackBuffer == null || _callbackBuffer.Length < sampleCount)
            {
                _callbackBuffer = new float[Math.Max(sampleCount, 2048)];
            }
        }

        private static void ZeroBuffer(IntPtr data, int sampleCount)
        {
            float[] silentBuffer = new float[sampleCount];
            Marshal.Copy(silentBuffer, 0, data, sampleCount);
        }

        private static bool ShouldLog(ref long lastLogTick, int minimumIntervalMs = 1000)
        {
            long currentTick = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
            if (currentTick - lastLogTick < minimumIntervalMs)
            {
                return false;
            }

            lastLogTick = currentTick;
            return true;
        }
    }
}