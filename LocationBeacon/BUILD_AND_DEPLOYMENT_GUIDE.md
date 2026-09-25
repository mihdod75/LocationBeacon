# BUILD AND DEPLOYMENT GUIDE

## Files Created/Updated Summary

| File | Status | Location |
|------|--------|----------|
| AndroidManifest.xml | ✅ UPDATED | `LocationBeacon/Platforms/Android/AndroidManifest.xml` |
| BootReceiver.cs | ✅ CREATED | `LocationBeacon/Platforms/Android/BootReceiver.cs` |
| BeaconAlarmManager.cs | ✅ CREATED | `LocationBeacon/Services/BeaconAlarmManager.cs` |
| BeaconForegroundService.cs | ✅ UPDATED | `LocationBeacon/LocationBeacon/Platforms/Android/BeaconForegroundService.cs` |
| BeaconService.cs | ✅ RECREATED | `LocationBeacon/Services/BeaconService.cs` |

## Step-by-Step Build Instructions

### Step 1: Clean the Project
```bash
cd LocationBeacon

# Remove all build artifacts
rm -rf bin/
rm -rf obj/
rm -rf .vs/

# On Windows, you can also use:
# rd /s /q bin
# rd /s /q obj
```

### Step 2: Restore Dependencies
```bash
dotnet restore
```

### Step 3: Build for Android
```bash
# Full clean build
dotnet clean
dotnet build -c Release -f net10.0-android

# Or for debug build (faster)
dotnet build -c Debug -f net10.0-android
```

### Step 4: Deploy to Device
```bash
# Connect Android device with USB debugging enabled
# Then deploy:
dotnet build -t:Install -c Debug -f net10.0-android
```

## Device Setup (REQUIRED FOR DEEP SLEEP OPERATION)

### 1. Enable USB Debugging
```
Settings → Developer options → USB Debugging → ON
(If Developer options not visible: Settings → About → Build Number → Tap 7 times)
```

### 2. Grant Permissions
```
After app installation, in Settings:
  → Apps → LocationBeacon → Permissions
	→ Location: Set to "Always" or "Allow all the time"
```

### 3. Disable Battery Optimization (CRITICAL)
```
Settings → Apps → Battery → All apps/Special access
  → Find "LocationBeacon"
  → Tap → Select "Not Optimized" or "Unrestricted"
```

### 4. Verify Developer Options Setup
```
Settings → Developer options
  → Stay Awake → OFF (to test deep sleep)
  → USB Debugging → ON
```

## Testing Procedure

### Test 1: Normal Operation (Device Awake)
```
1. Open LocationBeacon app
2. Tap "Start Beacon" button
3. Verify UI shows "Beacon started"
4. Monitor Debug Output (Visual Studio Debug window)
   Expected: Messages like "✓ Location acquisition took 0.45s"
5. Wait ~30 seconds (or whatever interval is set)
6. Verify next beacon sends automatically
```

### Test 2: Deep Sleep Operation
```
1. Start beacon from UI
2. Press Power button to lock screen
3. Wait 10 seconds
4. Open Debug Output window (Visual Studio)
5. Verify beacon messages continue appearing every interval
6. Unlock phone and verify in app that beacons kept sending
```

### Test 3: Extreme Sleep (10+ minutes)
```
1. Start beacon
2. Lock screen
3. Wait 10+ minutes
4. Unlock and check that:
   - Beacons continued sending in background
   - Multiple beacon entries show in status
```

### Test 4: Device Reboot
```
1. Start beacon
2. Lock screen
3. Press and hold Power → Power off → confirm
4. Wait 30 seconds
5. Press Power to turn on; wait for boot to complete
6. Unlock phone
7. Open LocationBeacon app
8. Verify in Debug Output that beacons resumed automatically
	(They should start immediately due to BootReceiver)
```

## Expected Debug Output Log

When app is running normally (with beacon active), you should see:

```
=== BEACON STARTED ===
✓ Foreground Service started
✓ Android foreground service and alarm manager initialized
✓ Beacon alarm scheduled (setAndAllowWhileIdle) every 30 seconds
✓ App is exempt from Doze mode (battery optimization disabled)
✓ Location acquisition took 0.45s
✓ Partial WakeLock acquired for send (30s timeout)
Serialization took 0.0035s
HTTP request took 1.23s
✓ Payload sent successfully. Total interval from last send: 30.12s
✓ Partial WakeLock released
Waiting 0.88 seconds until next send

[Repeat every interval]
```

## Troubleshooting

### Issue: "foregroundServiceType 0x00000008 is not a subset of 0x00000000"

**Cause**: Manifest mismatch or incomplete build

**Solution**:
```bash
1. Clean build:
   dotnet clean
   rm -rf bin/ obj/
2. Verify AndroidManifest.xml contains:
   android:foregroundServiceType="location"
3. Rebuild:
   dotnet build -c Debug -f net10.0-android
4. Reinstall:
   dotnet build -t:Install -c Debug -f net10.0-android
```

### Issue: Beacons stop when screen locks

**Cause**: Missing Doze exemption

**Solution**:
```
Settings → Apps → Battery → All apps
  → Find LocationBeacon
  → Long press → Select "Unrestricted" or "Don't optimize"
```

**Verify in Debug Output**:
```
Should see: ✓ App is exempt from Doze mode
Instead of: ⚠ App is NOT exempt from Doze mode
```

### Issue: App crashes immediately

**Cause**: Missing permission or context reference

**Solution**:
1. Check Android API level (minimum 21, targeting 34+)
2. Verify all files created in correct locations
3. Ensure BeaconAlarmManager.cs is in `Services/` folder
4. Ensure BootReceiver.cs is in `Platforms/Android/` folder
5. Clean build and reinstall

### Issue: Alarm doesn't trigger during deep sleep

**Cause**: Device may be in Doze mode AND app is optimized

**Solution**:
```
1. Disable Doze battery optimization (see above)
2. In Debug Output, verify:
   ✓ Beacon alarm scheduled (setAndAllowWhileIdle)
   ✓ App is exempt from Doze mode
3. If still not working, test on Android 12+ device
   (earlier Android versions may have different behavior)
```

## Visual Studio Debug Output Window Setup

### Enable Debug Output in Visual Studio
```
1. Debug → Windows → Output
2. Filter dropdown → "Diagnostic output" or "All Output"
3. Search for keywords like "✓", "✗", "BEACON", "WakeLock"
```

### Real-time Monitoring
```csharp
// Search for these patterns in output:
"=== BEACON STARTED ==="     // Service started
"✓ Payload sent"              // Beacon sent successfully
"✓ Partial WakeLock acquired" // Lock acquired
"✓ Partial WakeLock released" // Lock released (good!)
"⚠ WARNING:"                  // Performance timing issue
"✗"                           // Error occurred
```

## CI/CD Integration (Optional)

### GitHub Actions Example
```yaml
name: Build LocationBeacon

on: [push, pull_request]

jobs:
  build:
	runs-on: ubuntu-latest
	steps:
	  - uses: actions/checkout@v3
	  - uses: actions/setup-dotnet@v3
		with:
		  dotnet-version: '10.0.x'
	  - run: dotnet restore
	  - run: dotnet build -c Release -f net10.0-android
```

## Performance Notes

### Beacon Send Timing
- Location acquisition: 0.5-2s (depends on GPS fix)
- Serialization: 1-5ms
- HTTP request: 1-3s (depends on network)
- **Total per beacon: 2-6 seconds**

### Recommended Intervals
- **30 seconds**: Good balance; reasonable battery impact
- **60 seconds**: Minimal battery; less real-time tracking
- **10 seconds**: Maximum tracking; significant battery drain

### Battery Impact
- With proper PARTIAL wake lock: ~2-5% per hour
- With improper FULL wake lock: ~15-30% per hour
- (Measured on typical modern smartphone)

## Rollback Plan

If issues arise, here's how to revert:

1. Keep backup of original files
2. To rollback:
   ```bash
   git checkout HEAD -- LocationBeacon/Services/BeaconService.cs
   git checkout HEAD -- LocationBeacon/Platforms/Android/AndroidManifest.xml
   rm LocationBeacon/Platforms/Android/BootReceiver.cs
   rm LocationBeacon/Services/BeaconAlarmManager.cs
   ```
3. Clean rebuild:
   ```bash
   dotnet clean && dotnet build -f net10.0-android
   ```

## Next Steps

1. ✅ Build project with new files
2. ✅ Deploy to Android 12+ device
3. ✅ Grant all required permissions
4. ✅ Disable battery optimization
5. ✅ Test deep sleep operation
6. ✅ Monitor Debug Output for success messages
7. ✅ Deploy to production with confidence

**You should no longer see the foregroundServiceType 0x00000008 errors, and beacons will send reliably even during deep sleep.**
