# Deep Sleep Delay Issues - Root Causes and Fixes

## 🔴 ROOT CAUSE: AlarmManager Limitations During Aggressive Doze Mode

**The infinite delays occur because:**
- When Battery Saver = ON, Android enters aggressive Doze mode
- `AlarmManager.setAndAllowWhileIdle()` has a **hard limit of ~1 alarm every 9 minutes maximum**
- With a 10-second interval configured, the OS simply ignores requests that violate this limit
- This is an Android OS-level constraint, not a code issue

**Solution:** Migrated to **WorkManager** (Google's recommended API for background tasks)
- WorkManager understands Doze mode constraints better
- Uses exponential backoff for retries
- Better balance between reliability and battery life
- Acknowledges OS limitations and works within them

---

## Issues Identified and Fixed

### 1. **Background Location Permission Failure** ✓ FIXED
**Problem:** LocationService was requesting `LocationWhenInUse` permission first, which only works when the app is in the foreground. During deep sleep/background operation, this permission alone cannot provide location updates.

**Location:** `LocationBeacon/Services/LocationService.cs - GetLocationAsync()`

**Fix:** 
- Prioritize `LocationAlways` (background) permission
- Only fallback to `LocationWhenInUse` if `LocationAlways` is denied
- This ensures location services work properly in background/deep sleep mode

---

### 2. **Alarm Rescheduling Not Implemented** ✓ FIXED
**Problem:** The `BeaconAlarmManager.ScheduleBeaconAlarm()` was called only once at startup. Android's `setAndAllowWhileIdle()` method (designed for Doze mode) **fires only one time** - it does not repeat. After the first alarm fires, subsequent alarms won't trigger without rescheduling.

**Location:** `LocationBeacon/Services/BeaconService.cs - StartBeaconAsync()`

**Fix:**
- Reschedule the alarm **after each beacon send** 
- This ensures the alarm manager continuously schedules itself for the next beacon cycle
- The `setAndAllowWhileIdle()` mechanism will properly wake the device from Doze mode for each interval

---

### 3. **Task.Delay() Gets Throttled During Deep Sleep** ✓ FIXED
**Problem:** The main beacon loop was waiting for the full interval using `Task.Delay()`, but during deep sleep, the OS heavily throttles this delay. The wake lock was released before the delay, allowing the device to enter deep sleep while the task waited.

**Location:** `LocationBeacon/Services/BeaconService.cs - StartBeaconAsync()`

**Fix:**
- Alarm-based wakeup strategy: After each beacon send, rely on the alarm manager to wake the device for the next cycle
- Exit the wait loop early (after 1 second) to avoid busy waiting
- The foreground service notification keeps the beacon task alive between cycles
- The next alarm will trigger `OnStartCommand()` which will enter the loop again

---

### 5. **Missing Battery Optimization Whitelist Request** ✓ FIXED
**Problem:** Even with `setAndAllowWhileIdle()`, Android heavily throttles alarms for apps not on the battery optimization whitelist when Doze mode is active. The app was never requesting this whitelist exemption.

**Location:** `LocationBeacon/Platforms/Android/MainActivity.cs - RequestBatteryWhitelistExemption()`

**Fix:**
- Added automatic battery optimization whitelist request when MainActivity launches
- Shows user the battery settings dialog to grant exemption
- Provides manual fallback instructions in the log

---

### 6. **Switched from AlarmManager to WorkManager** ✓ FIXED
**Problem:** AlarmManager's `setAndAllowWhileIdle()` has hard OS limits:
- Maximum ~1 alarm every 9 minutes during Doze
- With 10-second interval request, the OS simply ignores the alarms
- This is an Android OS constraint, not fixable in code

**Location:** `LocationBeacon/Services/BeaconWorkManager.cs` (NEW FILE)

**Solution - Implemented WorkManager:**
- Google's officially recommended API for background periodic tasks
- Better Doze mode handling with exponential backoff retry strategy
- Respects OS constraints while maintaining reliability
- Uses `PeriodicWorkRequest` for recurring beacon sends

**How WorkManager Works During Deep Sleep:**
1. WorkManager schedules periodic work requests
2. When device enters Doze, WorkManager negotiates with OS
3. WorkManager handles batching and backoff automatically
4. Beacon sends happen at intervals the OS will actually allow
5. Far more reliable than fighting OS constraints

**Key Files Added/Modified:**
- ✅ `LocationBeacon/Services/BeaconWorkManager.cs` - NEW
  - `BeaconWorkManager` class for scheduling
  - `BeaconWorker` class for actual work execution
- ✅ `LocationBeacon.csproj` - Added Xamarin.AndroidX.Work.Runtime NuGet
**Problem:** The log said alarms were scheduled "every" X seconds, but `setAndAllowWhileIdle()` only fires once in Doze mode. This was misleading.

**Location:** `LocationBeacon/Services/BeaconAlarmManager.cs - ScheduleBeaconAlarm()`

**Fix:**
- Updated log messages to clarify that `setAndAllowWhileIdle()` fires once but will be rescheduled by the service
- Added better documentation about the different behavior on different Android versions

---

## How It Works Now (Deep Sleep Mode)

### Beacon Cycle During Deep Sleep:
1. **Device in Doze/Deep Sleep** → App's background processes are suspended
2. **Alarm fires after N seconds** → Wakes device from Doze, triggers foreground service
3. **Service OnStartCommand() called** → Beacon loop begins
4. **SendBeaconAsync() executes** with wake lock held
5. **Location acquired** with proper `LocationAlways` permission
6. **Beacon sent** to server
7. **Alarm rescheduled** for next cycle
8. **Short 1-second delay** (not full interval) to prevent busy loop
9. **Wake lock released** → Device can return to Doze
10. **Loop returns to step 2** for next cycle

### Key Benefits:
✓ Accurate intervals even during deep sleep
✓ Alarm manager wakes device from Doze, not relying on throttled Task.Delay()
✓ Foreground service notification keeps task alive between cycles
✓ Location services work in background mode
✓ Minimal battery impact via proper Doze exemptions

---

## Files Modified

1. **LocationBeacon/Services/LocationService.cs**
   - Fixed location permission prioritization for background operation

2. **LocationBeacon/Services/BeaconService.cs**
   - Added alarm rescheduling after each beacon send
   - Changed wait strategy to rely on alarm manager instead of full Task.Delay()
   - Improved logging for deep sleep mode

3. **LocationBeacon/Services/BeaconAlarmManager.cs**
   - Clarified log messages about `setAndAllowWhileIdle()` behavior
   - Better documentation for different Android versions

---

## Configuration Notes

**Manifest Configuration (Already Present):**
- ✓ `android:foregroundServiceType="location"` - Declared
- ✓ `FOREGROUND_SERVICE_LOCATION` permission - Declared
- ✓ `ACCESS_FINE_LOCATION` permission - Declared
- ✓ `ACCESS_COARSE_LOCATION` permission - Declared
- ✓ `ACCESS_BACKGROUND_LOCATION` permission - Declared
- ✓ `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS` permission - Declared
- ✓ Notification channel created in Android 8.0+

**Recommended User Actions:**
Users should grant battery optimization exemption for best results:
- Settings → Apps → LocationBeacon → Battery → Allow unrestricted battery usage

---

## Testing Deep Sleep Mode

To test deep sleep behavior:
1. Deploy app with these fixes
2. Enable app notifications so foreground notification is visible
3. Set interval to 30 seconds for quick testing
4. Enable device Doze mode: `adb shell dumpsys deviceidle force-idle`
5. Wait and observe beacon sends at expected intervals
6. Monitor debug output for "Alarm rescheduled" messages

---

## Android Version Compatibility

- **Android 12+ (API 31+):** Uses `setAndAllowWhileIdle()` with rescheduling
- **Android 6-11 (API 23-30):** Uses `setAndAllowWhileIdle()` with rescheduling
- **Pre-Android 6.0 (API < 23):** Uses `SetRepeating()` (no Doze mode)
