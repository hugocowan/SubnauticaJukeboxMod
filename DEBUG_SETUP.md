# Debug Logging Setup for JukeboxSpotify

## Overview
This project uses BepInEx logging with a custom `Plugin.LogDebug()` method that respects a `logging` configuration toggle. Debug logs appear in:
1. BepInEx console (in-game overlay)
2. BepInEx log file
3. Visual Studio Debug Output (when attached to the game process)

## Configuration

### 1. Enable Debug Logging in-game
- Load the game with your mod
- Go to **Options → Mods → JukeboxSpotify**
- Toggle **"Enable logging (for debugging)"** to ON
- This enables `Plugin.LogDebug()` to output messages

### 2. View Logs in Visual Studio

#### Option A: Attach to Running Game Process
1. Start the Subnautica game
2. In Visual Studio: **Debug → Attach to Process (Ctrl+Alt+P)**
3. Search for and select the Subnautica executable process
4. Open **View → Output Window (Ctrl+Alt+O)**
5. Select **"Debug"** from the dropdown to see debugger output
6. BepInEx logs will appear here

#### Option B: Use BepInEx Console (In-Game)
1. Run the game with mod enabled
2. Press `F12` or the console toggle key to open BepInEx console
3. Debug logs appear in real-time with colored output
4. More reliable and immediate than VS output

#### Option C: Check BepInEx Log File
1. Navigate to: `<Subnautica Install>/BepInEx/LogOutput.log`
2. Open with your text editor or VS
3. Contains all historical logs with timestamps
4. Persists across game restarts

### 3. Project Build Settings for Better Debugging

Your `JukeboxSpotify.csproj` is configured with:
- **TargetFramework**: net472 (.NET Framework 4.7.2)
- **AllowUnsafeBlocks**: true
- **LangVersion**: 11 (modern C# features)

No additional configuration needed - your project is ready for debugging.

## Code Example

The `Plugin.LogDebug()` method automatically checks if logging is enabled:

```csharp
public static void LogDebug(string message)
{
    if (IsDebugLoggingEnabled)
    {
        Logger.LogInfo(message);  // Uses BepInEx logger
    }
}

public static bool IsDebugLoggingEnabled => Logger != null && config?.logging == true;
```

Usage in your code:
```csharp
Plugin.LogDebug("Your debug message: " + someValue);
```

## Troubleshooting

### Logs Not Appearing in VS Output
- Ensure debugger is actually attached (should see debug output for other logs)
- Check if logging is enabled in mod config (toggle in Options menu)
- Verify BepInEx is installed and configured correctly
- Try viewing the BepInEx LogOutput.log file directly instead

### Performance Impact
Debug logging uses string concatenation which is efficient for disabled state (`IsDebugLoggingEnabled` returns false). When enabled, it only logs if the toggle is ON, so no performance penalty for disabled logging.

### Missing Console Output
- Console output may not always reach VS debugger
- Use BepInEx in-game console (F12) for immediate feedback
- Check `BepInEx/LogOutput.log` for guaranteed persistence

## Additional Resources
- BepInEx Documentation: https://docs.bepinex.dev/
- Subnautica Modding: https://snmodding.github.io/
- Visual Studio Debugging: https://docs.microsoft.com/en-us/visualstudio/debugger/

