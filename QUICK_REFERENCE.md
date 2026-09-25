# QUICK REFERENCE - ANDROID PERMISSION FIX

## The Problem (What Users See)
```
ERROR: java.lang.SecurityException: Starting FGS with type location requires permissions
```

## The Root Cause (What's Wrong)
App tries to start foreground service without checking if permissions granted

## The Solution (What We Fixed)

### 1️⃣ REQUEST permission from user (MainPage.xaml.cs)
```csharp
var permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
```

### 2️⃣ VALIDATE permission exists (BeaconService.cs)
```csharp
if (!PermissionManager.HasRequiredLocationPermissions(context)) return;
```

### 3️⃣ CHECK permission at boot (BootReceiver.cs)
```csharp
if (!PermissionManager.HasRequiredLocationPermissions(context)) return;
```

### 4️⃣ DON'T request from background (LocationService.cs)
```csharp
// ✓ Check only, don't request
var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
```

---

## Files Changed

| File | What | Why |
|------|------|-----|
| MainPage.xaml.cs | Added permission dialog | Request from UI thread |
| BeaconService.cs | Added permission check | Prevent exception |
| BootReceiver.cs | Added permission check | Safe restart |
| LocationService.cs | Removed permission request | Avoid background thread issues |
| PermissionManager.cs | **NEW** - Check permissions | Centralized logic |
| PermissionException.cs | **NEW** - Custom exception | Better error handling |

---

## How It Works Now

```
User clicks "Start Beacon"
		↓
Permission dialog appears
		↓
User grants permission ✓
		↓
Permission validated ✓
		↓
Service starts ✓
		↓
Beacon sends location every 10s ✓
```

---

## Testing (Minimum)

```bash
# Test 1: Normal flow
1. Click "Start Beacon"
2. Grant permission when asked
3. Verify beacon sends (logs show updates)
Result: ✅ Should work

# Test 2: Permission deny
1. Click "Start Beacon"
2. Deny permission when asked
3. See error message
Result: ✅ Should show error, not crash

# Test 3: Boot restart
1. Beacon running
2. Restart device
3. Check if beacon resumes automatically
Result: ✅ Should resume (permissions were granted)

# Test 4: No permission at boot
1. Revoke location permission (Settings)
2. Restart device
3. Check logs
4. Launch app and grant permission
5. Beacon should start
Result: ✅ Should handle gracefully
```

---

## Logs to Look For

### Success
```
✓ Foreground service started (will run in background)
✓ Location permissions granted
✓ Location obtained: Lat=..., Lon=..., Accuracy=...
```

### Failure
```
✗ Location permission denied by user
✗ Location permissions not granted
✗ Boot: Location permissions not granted - skipping
```

---

## One-Liner Explanation
**Before**: App crashed if permissions missing
**After**: App asks for permission, validates it exists, handles denial gracefully

---

## For Android 15 Specifically
- ✅ Requires FOREGROUND_SERVICE_LOCATION permission (we check this)
- ✅ Requires ACCESS_FINE_LOCATION OR ACCESS_COARSE_LOCATION (we check this)
- ✅ App must be "eligible state" (foreground service helps with this)

---

## Still Needed (Optional but Recommended)
Add this to MainPage.xaml.cs in StartBeaconAsync():
```csharp
// Request background location for true background operation
var bgStatus = await Permissions.RequestAsync<Permissions.LocationAlways>();
```
See: MainPage_Updates.txt

---

## Support Checklist

User reports error:
- [ ] Did we REQUEST permission? YES (MainPage)
- [ ] Did we VALIDATE permission? YES (BeaconService, BootReceiver)  
- [ ] Did user GRANT permission? (Settings → Apps → LocationBeacon → Location)
- [ ] Is app in ELIGIBLE STATE? (Recent launch, foreground service running)

If all YES → Should work ✓
If any NO → Fix that step

---

**Status**: Deploy Ready
**Files Modified**: 6
**Lines Added**: ~70
**Lines Removed/Changed**: ~25
**New Files**: 2

