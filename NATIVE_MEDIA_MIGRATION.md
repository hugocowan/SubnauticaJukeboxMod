# Native Media Migration

## Current Refactor Status

- [x] Gameplay hooks depend on `IMediaController` instead of the raw Spotify client.
- [x] Track-state polling is centralized in `MediaPlaybackCoordinator`.
- [x] The existing Spotify Web API path is wrapped in `SpotifyWebApiMediaController`.
- [x] FMOD streaming scaffolding exists for a PCM bridge and custom streaming sound.
- [x] Windows GSMTC session discovery and polling are implemented.
- [x] WASAPI per-process capture is implemented behind `IAudioCaptureSession`.
- [x] Captured PCM is routed into a live jukebox FMOD stream.
- [x] Jukebox volume changes stay inside the game instead of mutating the media client volume.

## Phase 1: Native Media Control

- [x] Replace `MediaControllerFactory.CreateDefault()` with a native-first selection flow.
- [x] Implement `WindowsMediaSessionController.InitializeAsync()` using GSMTC session-manager discovery.
- [x] Map GSMTC metadata into `MediaPlaybackSnapshot`.
- [x] Map GSMTC transport controls into `PlayAsync()`, `PauseAsync()`, `NextAsync()`, `PreviousAsync()`, and `SeekAsync()`.
- [ ] Add recovery for session switches, app exits, and device changes.

## Phase 2: Audio Capture Pipeline

- [x] Add an `IAudioCaptureSession` implementation for per-process loopback capture.
- [x] Correlate the selected GSMTC session with a process-loopback capture target by process identity heuristics.
- [ ] Handle unsupported cases such as browser process sharing and DRM-blocked sessions.
- [x] Feed captured interleaved float PCM into `PcmRingBuffer` without allocations on the hot path.

## Phase 3: In-Game Audio Integration

- [x] Bind `JukeboxFmodStreamingBridge` to an active jukebox FMOD channel.
- [x] Replace the current source-volume attenuation path with in-engine spatialization.
- [ ] Reuse existing jukebox pause, power, underwater, and occlusion behavior by driving the original emitter.
- [ ] Detect underruns and overflows and surface them in debug logging.

## Phase 4: Cleanup

- [ ] Remove Spotify OAuth config and the legacy web-API backend after native control is stable.
- [ ] Rename Spotify-specific state in `Vars` to source-agnostic names.
- [ ] Add an in-game source picker once multiple media-session backends are stable.