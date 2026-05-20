# Quick Start: Debug Logging in Visual Studio

## Your Current Setup ✓

Your project is already configured for logging:
- ✓ Using BepInEx logging (professional, well-integrated)
- ✓ Custom `Plugin.LogDebug()` method in your code
- ✓ Logging toggle in mod config menu
- ✓ Post-build scripts that auto-deploy and launch game
- ✓ .NET Framework 4.7.2 properly configured

## How to View Debug Logs

### Method 1: Visual Studio Debugger (Recommended)

This captures logs in real-time in Visual Studio:

1. **Build & Launch**: 
   - Press `Ctrl+Shift+B` to build
   - Post-build script automatically deploys and starts game

2. **Attach to Process**:
   - Game starts after build
   - Go to: **Debug → Attach to Process** (`Ctrl+Alt+P`)
   - Type "subnautica" to find the process
   - Double-click to attach
   - You should see "Debugger Attached" in VS Output

3. **View Output**:
   - Open: **View → Output Window** (`Ctrl+Alt+O`)
   - In the Output dropdown at top, select **"Debug"**
   - BepInEx logs now appear here as they happen
   - Look for "[Info]" messages containing your debug output

4. **Enable Logging in Game**:
   - In-game: **Options → Mods → JukeboxSpotify**
   - Toggle **"Enable logging (for debugging)"** ON
   - Now your `Plugin.LogDebug()` calls will output

### Method 2: BepInEx Console (Fastest during Development)

For quick iteration without VS:

1. Run game with mod enabled
2. Press `F12` to toggle BepInEx console overlay
3. Logs appear in real-time with color-coding:
   - [Info] = Debug logs (yours)
   - [Warning] = Warnings (yellow)
   - [Error] = Errors (red)
4. Immediate feedback, no VS required

### Method 3: BepInEx Log File (Persistent Record)

For permanent logs that persist across sessions:

1. Open: `<Subnautica Install>\BepInEx\LogOutput.log`
2. Or in VS: **File → Open File** and navigate there
3. Contains complete history with timestamps
4. Never gets cleared by game restart
5. Best for post-session analysis

## Code: Adding Debug Logs

Anywhere in your code, use:

```csharp
// Simple message
Plugin.LogDebug("Initialization complete");

// With values
Plugin.LogDebug("Track: " + trackTitle + ", Duration: " + duration + "ms");

// Error logging (optional exception parameter)
Plugin.LogDebugError("Failed to initialize", exception);
```

The logging is automatically:
- ✓ Ignored if logging disabled (zero performance cost)
- ✓ Sent to BepInEx logger when enabled
- ✓ Appears in all three locations simultaneously

## Visual Studio Output Window Reference

```
Output pane dropdown menu:
- "Debug" = Live logs from debugger (shows Plugin.LogDebug when logging enabled)
- "Package Manager" = NuGet restore logs
- "Build" = Compilation output
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| No logs in VS Output | Ensure logging is enabled in mod config (toggle in Options) |
| "Debugger not attached" message | The game may have already launched before you attached |
| Can't find subnautica.exe | Game may be running as a different process name or not launched |
| Logs appear then disappear | Console was closed; don't close VS during game session |
| F12 console not showing | BepInEx may not be installed; verify BepInEx folder exists |

## Performance

When logging is **disabled** (toggle OFF):
- `IsDebugLoggingEnabled` returns false immediately
- String concatenation never happens
- Zero performance impact

When logging is **enabled** (toggle ON):
- String concatenation for each log message
- Minimal impact for typical debug logging volume
- Turn off when you don't need logs to save performance

---

**Pro Tip**: Use Method 1 + Method 2 together:
- Run game with post-build (Method 1 ready)
- Attach debugger when ready
- Toggle F12 for immediate console view (Method 2)
- Monitor VS Output for persistent record

This gives you real-time feedback with full IDE integration!

