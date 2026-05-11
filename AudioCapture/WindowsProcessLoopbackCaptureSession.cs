using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace JukeboxSpotify
{
    internal sealed class WindowsProcessLoopbackCaptureSession : IAudioCaptureSession
    {
        private const string VirtualAudioDeviceProcessLoopback = "VAD\\Process_Loopback";
        private const ushort WaveFormatPcm = 0x0001;
        private const ushort VariantBlob = 0x0041;
        private readonly int _processId;
        private readonly PcmRingBuffer _ringBuffer;
        private readonly object _sync = new();
        private readonly float[] _silenceBuffer;
        private readonly float[] _conversionBuffer;
        private readonly AutoResetEvent _sampleReadyEvent = new(false);
        private readonly AudioInterfaceActivationHandler _activationHandler = new();
        private WaveFormatEx _captureWaveFormat;
        private IAudioClient _audioClient;
        private IAudioCaptureClient _audioCaptureClient;
        private IActivateAudioInterfaceAsyncOperation _activationOperation;
        private Thread _captureThread;
        private volatile bool _disposed;
        private volatile bool _isCapturing;
        private long _lastStatsLogTick;
        private long _lastOverflowLogTick;
        private int _packetsSinceLastLog;
        private int _silentPacketsSinceLastLog;
        private int _capturedSamplesSinceLastLog;
        private int _droppedSamplesSinceLastLog;

        public WindowsProcessLoopbackCaptureSession(string sourceId, int processId, AudioStreamFormat format, int bufferMilliseconds = 2500)
        {
            if (processId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(processId));
            }

            if (bufferMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferMilliseconds));
            }

            SourceId = sourceId ?? string.Empty;
            _processId = processId;
            Format = format;
            _ringBuffer = new PcmRingBuffer(Math.Max(2048, (format.SamplesPerSecond * bufferMilliseconds) / 1000));
            _silenceBuffer = new float[Math.Max(2048, format.SamplesPerSecond / 10)];
            _conversionBuffer = new float[Math.Max(2048, format.SamplesPerSecond / 10)];
            _captureWaveFormat = new WaveFormatEx
            {
                wFormatTag = WaveFormatPcm,
                nChannels = checked((ushort)format.ChannelCount),
                nSamplesPerSec = checked((uint)format.SampleRate),
                wBitsPerSample = 16,
                nBlockAlign = checked((ushort)(format.ChannelCount * 2)),
                nAvgBytesPerSec = checked((uint)(format.SampleRate * format.ChannelCount * 2)),
                cbSize = 0,
            };

            LogInfo("Created process-loopback capture session. sourceId='" + SourceId + "', processId=" + _processId +
                ", sampleRate=" + format.SampleRate + ", channelCount=" + format.ChannelCount +
                ", ringBufferCapacity=" + _ringBuffer.Capacity + ".");
        }

        public string SourceId { get; }

        public AudioStreamFormat Format { get; }

        public bool IsCapturing => _isCapturing;

        public int AvailableSamples => _ringBuffer.Count;

        public void Start()
        {
            ThrowIfDisposed();
            if (_isCapturing)
            {
                LogInfo("Ignoring capture start request because process-loopback capture is already running for source '" + SourceId + "'.");
                return;
            }

            lock (_sync)
            {
                if (_isCapturing)
                {
                    LogInfo("Ignoring capture start request inside lock because process-loopback capture is already running for source '" + SourceId + "'.");
                    return;
                }

                LogInfo("Starting process-loopback capture. sourceId='" + SourceId + "', processId=" + _processId + ".");
                InitializeAudioClient();
                Marshal.ThrowExceptionForHR(_audioClient.SetEventHandle(_sampleReadyEvent.SafeWaitHandle.DangerousGetHandle()));
                Marshal.ThrowExceptionForHR(_audioClient.Start());

                _isCapturing = true;
                _captureThread = new Thread(CaptureLoop)
                {
                    IsBackground = true,
                    Name = "Jukebox Process Loopback Capture",
                };
                _captureThread.Start();
                LogInfo("Started process-loopback capture thread for source '" + SourceId + "'.");
            }
        }

        public void Stop()
        {
            if (!_isCapturing)
            {
                LogInfo("Ignoring capture stop request because process-loopback capture is not running for source '" + SourceId + "'.");
                return;
            }

            lock (_sync)
            {
                if (!_isCapturing)
                {
                    LogInfo("Ignoring capture stop request inside lock because process-loopback capture is not running for source '" + SourceId + "'.");
                    return;
                }

                _isCapturing = false;
                _sampleReadyEvent.Set();
            }

            _captureThread?.Join(1000);
            _captureThread = null;

            if (_audioClient != null)
            {
                try
                {
                    Marshal.ThrowExceptionForHR(_audioClient.Stop());
                }
                catch (Exception e)
                {
                    if (Plugin.config?.logging == true)
                    {
                        Plugin.Logger.LogError("Stopping process loopback capture failed: " + e);
                    }
                }
            }

            MaybeLogCaptureStats(force: true);
            ReleaseAudioObjects();
            LogInfo("Stopped process-loopback capture for source '" + SourceId + "'. bufferedSamples=" + _ringBuffer.Count + ".");
        }

        public int Read(float[] destination, int offset, int sampleCount)
        {
            return _ringBuffer.Read(destination, offset, sampleCount);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            LogInfo("Disposing process-loopback capture session for source '" + SourceId + "'.");
            _disposed = true;
            Stop();
            _sampleReadyEvent.Dispose();
            _activationHandler.Dispose();
        }

        private void CaptureLoop()
        {
            LogInfo("Entering process-loopback capture thread for source '" + SourceId + "'.");
            while (_isCapturing)
            {
                _sampleReadyEvent.WaitOne(200);
                if (!_isCapturing)
                {
                    break;
                }

                try
                {
                    DrainAudioPackets();
                }
                catch (Exception e)
                {
                    _isCapturing = false;
                    LogError("Process loopback capture failed", e);
                }
            }

            LogInfo("Exiting process-loopback capture thread for source '" + SourceId + "'.");
        }

        private void DrainAudioPackets()
        {
            while (true)
            {
                uint framesAvailable;
                Marshal.ThrowExceptionForHR(_audioCaptureClient.GetNextPacketSize(out framesAvailable));
                if (framesAvailable == 0)
                {
                    break;
                }

                IntPtr dataPointer;
                AudioCaptureClientBufferFlags bufferFlags;
                ulong devicePosition;
                ulong qpcPosition;
                Marshal.ThrowExceptionForHR(_audioCaptureClient.GetBuffer(out dataPointer, out framesAvailable, out bufferFlags, out devicePosition, out qpcPosition));

                try
                {
                    int sampleCount = checked((int)(framesAvailable * (uint)Format.ChannelCount));
                    bool silentPacket = (bufferFlags & AudioCaptureClientBufferFlags.Silent) != 0 || dataPointer == IntPtr.Zero;
                    int writtenSamples;
                    if (silentPacket)
                    {
                        writtenSamples = WriteSilence(sampleCount);
                    }
                    else
                    {
                        writtenSamples = ConvertAndWritePcm16(dataPointer, sampleCount);
                    }

                    int droppedSamples = sampleCount - writtenSamples;
                    _packetsSinceLastLog++;
                    _capturedSamplesSinceLastLog += writtenSamples;
                    if (silentPacket)
                    {
                        _silentPacketsSinceLastLog++;
                    }

                    if (droppedSamples > 0)
                    {
                        _droppedSamplesSinceLastLog += droppedSamples;
                        if (ShouldLog(ref _lastOverflowLogTick))
                        {
                            LogInfo("Process-loopback capture ring buffer overflow. sourceId='" + SourceId + "', requestedSamples=" + sampleCount +
                                ", writtenSamples=" + writtenSamples + ", droppedSamples=" + droppedSamples +
                                ", bufferedSamples=" + _ringBuffer.Count + ", capacity=" + _ringBuffer.Capacity + ".");
                        }
                    }

                    MaybeLogCaptureStats();
                }
                finally
                {
                    Marshal.ThrowExceptionForHR(_audioCaptureClient.ReleaseBuffer(framesAvailable));
                }
            }
        }

        private int WriteSilence(int sampleCount)
        {
            int remaining = sampleCount;
            int writtenTotal = 0;
            while (remaining > 0)
            {
                int chunk = Math.Min(remaining, _silenceBuffer.Length);
                int written = _ringBuffer.Write(_silenceBuffer, 0, chunk);
                writtenTotal += written;
                remaining -= chunk;

                if (written < chunk)
                {
                    break;
                }
            }

            return writtenTotal;
        }

        private int ConvertAndWritePcm16(IntPtr dataPointer, int sampleCount)
        {
            int processed = 0;
            int writtenTotal = 0;

            while (processed < sampleCount)
            {
                int chunk = Math.Min(_conversionBuffer.Length, sampleCount - processed);
                for (int index = 0; index < chunk; index++)
                {
                    short sample = Marshal.ReadInt16(dataPointer, (processed + index) * sizeof(short));
                    _conversionBuffer[index] = sample / 32768f;
                }

                int written = _ringBuffer.Write(_conversionBuffer, 0, chunk);
                writtenTotal += written;
                processed += chunk;

                if (written < chunk)
                {
                    break;
                }
            }

            return writtenTotal;
        }

        private void InitializeAudioClient()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                throw new PlatformNotSupportedException("Process loopback capture is only available on Windows.");
            }

            AudioClientActivationParams activationParams = new()
            {
                ActivationType = AudioClientActivationType.ProcessLoopback,
                ProcessLoopbackParams = new AudioClientProcessLoopbackParams
                {
                    TargetProcessId = (uint)_processId,
                    ProcessLoopbackMode = ProcessLoopbackMode.IncludeTargetProcessTree,
                },
            };

            int activationParamsSize = Marshal.SizeOf<AudioClientActivationParams>();
            IntPtr blobDataMemory = IntPtr.Zero;

            try
            {
                LogInfo("Initializing WASAPI loopback capture client for source '" + SourceId + "' using process id " + _processId + ".");
                blobDataMemory = Marshal.AllocHGlobal(activationParamsSize);
                Marshal.StructureToPtr(activationParams, blobDataMemory, false);

                PropVariant activateParams = new()
                {
                    vt = VariantBlob,
                    blob = new Blob
                    {
                        cbSize = activationParamsSize,
                        pBlobData = blobDataMemory,
                    },
                };

                Guid audioClientInterfaceId = typeof(IAudioClient).GUID;
                int hr = ActivateAudioInterfaceAsync(
                    VirtualAudioDeviceProcessLoopback,
                    ref audioClientInterfaceId,
                    ref activateParams,
                    _activationHandler,
                    out _activationOperation);
                Marshal.ThrowExceptionForHR(hr);

                LogInfo("Waiting for process-loopback audio client activation to complete for source '" + SourceId + "'.");
                _audioClient = _activationHandler.WaitForAudioClient();
                Guid emptyGuid = Guid.Empty;
                AudioClientStreamFlags streamFlags = AudioClientStreamFlags.Loopback |
                    AudioClientStreamFlags.EventCallback |
                    AudioClientStreamFlags.AutoConvertPcm;
                Marshal.ThrowExceptionForHR(_audioClient.Initialize(
                    AudioClientShareMode.Shared,
                    streamFlags,
                    0,
                    0,
                    ref _captureWaveFormat,
                    ref emptyGuid));
                LogInfo("Initialized WASAPI audio client for source '" + SourceId + "'. sampleRate=" + _captureWaveFormat.nSamplesPerSec +
                    ", channels=" + _captureWaveFormat.nChannels + ", bitsPerSample=" + _captureWaveFormat.wBitsPerSample + ".");

                Guid captureClientInterfaceId = typeof(IAudioCaptureClient).GUID;
                object captureClientObject;
                Marshal.ThrowExceptionForHR(_audioClient.GetService(ref captureClientInterfaceId, out captureClientObject));
                _audioCaptureClient = (IAudioCaptureClient)captureClientObject;
                LogInfo("Acquired WASAPI capture client service for source '" + SourceId + "'.");
            }
            finally
            {
                if (blobDataMemory != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(blobDataMemory);
                }
            }
        }

        private void ReleaseAudioObjects()
        {
            if (_activationOperation != null)
            {
                Marshal.FinalReleaseComObject(_activationOperation);
                _activationOperation = null;
            }

            if (_audioCaptureClient != null)
            {
                Marshal.FinalReleaseComObject(_audioCaptureClient);
                _audioCaptureClient = null;
            }

            if (_audioClient != null)
            {
                Marshal.FinalReleaseComObject(_audioClient);
                _audioClient = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private void MaybeLogCaptureStats(bool force = false)
        {
            if (!force && !ShouldLog(ref _lastStatsLogTick))
            {
                return;
            }

            if (_packetsSinceLastLog == 0 && _capturedSamplesSinceLastLog == 0 && _droppedSamplesSinceLastLog == 0 && !force)
            {
                return;
            }

            LogInfo("Process-loopback capture stats. sourceId='" + SourceId + "', processId=" + _processId +
                ", packets=" + _packetsSinceLastLog + ", silentPackets=" + _silentPacketsSinceLastLog +
                ", capturedSamples=" + _capturedSamplesSinceLastLog + ", droppedSamples=" + _droppedSamplesSinceLastLog +
                ", bufferedSamples=" + _ringBuffer.Count + ", capacity=" + _ringBuffer.Capacity + ".");
            _packetsSinceLastLog = 0;
            _silentPacketsSinceLastLog = 0;
            _capturedSamplesSinceLastLog = 0;
            _droppedSamplesSinceLastLog = 0;
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

        private static void LogInfo(string message)
        {
            Plugin.LogDebug(message);
        }

        private static void LogError(string message, Exception exception)
        {
            Plugin.LogDebugError(message, exception);
        }

        [DllImport("Mmdevapi.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int ActivateAudioInterfaceAsync(
            string deviceInterfacePath,
            ref Guid interfaceId,
            ref PropVariant activationParams,
            IActivateAudioInterfaceCompletionHandler completionHandler,
            out IActivateAudioInterfaceAsyncOperation activationOperation);

        [ComImport]
        [Guid("41D949AB-9862-444A-80F6-C261334DA5EB")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IActivateAudioInterfaceAsyncOperation
        {
            [PreserveSig]
            int GetActivateResult(out int activateResult, out IntPtr activatedInterface);
        }

        [ComImport]
        [Guid("41D949AB-9862-444A-80F6-C261334DA5EC")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IActivateAudioInterfaceCompletionHandler
        {
            [PreserveSig]
            int ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation);
        }

        [ComImport]
        [Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAgileObject
        {
        }

        [ComImport]
        [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            [PreserveSig] int Initialize(AudioClientShareMode shareMode, AudioClientStreamFlags streamFlags, long bufferDuration, long periodicity, ref WaveFormatEx waveFormat, ref Guid audioSessionGuid);
            [PreserveSig] int GetBufferSize(out uint bufferSize);
            [PreserveSig] int GetStreamLatency(out long latency);
            [PreserveSig] int GetCurrentPadding(out uint currentPadding);
            [PreserveSig] int IsFormatSupported(AudioClientShareMode shareMode, IntPtr format, IntPtr closestMatch);
            [PreserveSig] int GetMixFormat(out IntPtr deviceFormatPointer);
            [PreserveSig] int GetDevicePeriod(out long defaultDevicePeriod, out long minimumDevicePeriod);
            [PreserveSig] int Start();
            [PreserveSig] int Stop();
            [PreserveSig] int Reset();
            [PreserveSig] int SetEventHandle(IntPtr eventHandle);
            [PreserveSig] int GetService(ref Guid interfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object service);
        }

        [ComImport]
        [Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioCaptureClient
        {
            [PreserveSig] int GetBuffer(out IntPtr data, out uint numFramesToRead, out AudioCaptureClientBufferFlags flags, out ulong devicePosition, out ulong qpcPosition);
            [PreserveSig] int ReleaseBuffer(uint numFramesRead);
            [PreserveSig] int GetNextPacketSize(out uint numFramesInNextPacket);
        }

        private enum AudioClientShareMode
        {
            Shared = 0,
            Exclusive = 1,
        }

        [Flags]
        private enum AudioClientStreamFlags : uint
        {
            Loopback = 0x00020000,
            EventCallback = 0x00040000,
            AutoConvertPcm = 0x80000000,
        }

        [Flags]
        private enum AudioCaptureClientBufferFlags : uint
        {
            None = 0,
            Silent = 0x2,
        }

        private enum AudioClientActivationType
        {
            Default = 0,
            ProcessLoopback = 1,
        }

        private enum ProcessLoopbackMode
        {
            IncludeTargetProcessTree = 0,
            ExcludeTargetProcessTree = 1,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioClientProcessLoopbackParams
        {
            public uint TargetProcessId;
            public ProcessLoopbackMode ProcessLoopbackMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AudioClientActivationParams
        {
            public AudioClientActivationType ActivationType;
            public AudioClientProcessLoopbackParams ProcessLoopbackParams;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Blob
        {
            public int cbSize;
            public IntPtr pBlobData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropVariant
        {
            public ushort vt;
            public ushort wReserved1;
            public ushort wReserved2;
            public ushort wReserved3;
            public Blob blob;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormatEx
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        [ComVisible(true)]
        private sealed class AudioInterfaceActivationHandler : IActivateAudioInterfaceCompletionHandler, IAgileObject, IDisposable
        {
            private readonly TaskCompletionSource<IAudioClient> _audioClientSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly ManualResetEventSlim _completed = new(false);
            private bool _disposed;

            public int ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation)
            {
                try
                {
                    int activateResult;
                    IntPtr activatedInterface;
                    Marshal.ThrowExceptionForHR(activateOperation.GetActivateResult(out activateResult, out activatedInterface));
                    Marshal.ThrowExceptionForHR(activateResult);

                    try
                    {
                        _audioClientSource.TrySetResult((IAudioClient)Marshal.GetObjectForIUnknown(activatedInterface));
                        WindowsProcessLoopbackCaptureSession.LogInfo("Process-loopback audio activation completed successfully.");
                    }
                    finally
                    {
                        if (activatedInterface != IntPtr.Zero)
                        {
                            Marshal.Release(activatedInterface);
                        }
                    }

                    return 0;
                }
                catch (Exception e)
                {
                    WindowsProcessLoopbackCaptureSession.LogError("Process-loopback audio activation callback failed", e);
                    _audioClientSource.TrySetException(e);
                    return Marshal.GetHRForException(e);
                }
                finally
                {
                    _completed.Set();
                }
            }

            public IAudioClient WaitForAudioClient()
            {
                WindowsProcessLoopbackCaptureSession.LogInfo("Waiting for process-loopback audio activation to signal completion.");
                if (!_completed.Wait(TimeSpan.FromSeconds(5)))
                {
                    TimeoutException timeoutException = new("Timed out waiting for process loopback audio activation.");
                    WindowsProcessLoopbackCaptureSession.LogError("Timed out waiting for process-loopback audio activation", timeoutException);
                    throw timeoutException;
                }

                IAudioClient audioClient = _audioClientSource.Task.GetAwaiter().GetResult();
                WindowsProcessLoopbackCaptureSession.LogInfo("Process-loopback activation returned an audio client instance.");
                return audioClient;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _completed.Dispose();
            }
        }
    }
}