# ANDROID PERMISSION ARCHITECTURE - FINAL DESIGN

## Permission Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    ANDROID PERMISSION SYSTEM                    │
│                    (Android 15 / API Level 35)                  │
└─────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ MANIFEST DECLARATION (AndroidManifest.xml)                       │
├──────────────────────────────────────────────────────────────────┤
│ ✓ android.permission.ACCESS_FINE_LOCATION                        │
│ ✓ android.permission.ACCESS_COARSE_LOCATION                      │  
│ ✓ android.permission.ACCESS_BACKGROUND_LOCATION                  │
│ ✓ android.permission.FOREGROUND_SERVICE                          │
│ ✓ android.permission.FOREGROUND_SERVICE_LOCATION                 │
│ (+ other permissions: Internet, Wake Lock, Battery, etc)         │
└──────────────────────────────────────────────────────────────────┘
						  ↓
					Runtime Binding

┌──────────────────────────────────────────────────────────────────┐
│ PHASE 1: APP LAUNCH (Main Thread - Eligible State)              │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  User launches app                                               │
│     ↓                                                             │
│  MainActivity.OnCreate()                                          │
│     ├─ Checks notification permission (Android 13+)              │
│     └─ Updates UI                                                │
│     ↓                                                             │
│  MainPage loads                                                  │
│     ├─ Loads preferences                                         │
│     ├─ Wires up event handlers                                   │
│     └─ Displays UI                                               │
│                   [App is "Eligible" now]                         │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ PHASE 2: USER CLICKS "START BEACON"                             │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  OnToggleBeaconClicked() [ASYNC]                                 │
│     ↓                                                             │
│  StartBeaconAsync() [Runs on Main Thread]                        │
│     ├─ Validates enrollment (pairing key exists)                 │
│     │                                                             │
│     ├─ ┌─ REQUEST PHASE (Main Thread) ──────────────────┐       │
│     │ │                                                  │       │
│     │ ├─ Permissions.CheckStatusAsync<LocationWhenInUse>       │
│     │ │  ├─ If Granted → Continue                       │       │
│     │ │  └─ If NotGranted → Request                    │       │
│     │ │                                                  │       │
│     │ └─ Permissions.RequestAsync<LocationWhenInUse>   │       │
│     │    ├─ System shows permission dialog              │       │
│     │    ├─ User grants → PermissionStatus.Granted      │       │
│     │    └─ User denies → PermissionStatus.Denied       │       │
│     │                                                    │       │
│     │ If Denied:                                         │       │
│     │   ├─ AddLog("ERROR: Location permission denied")  │       │
│     │   ├─ DisplayAlert("Permission Required", ...)     │       │
│     │   └─ Return (exit gracefully)                     │       │
│     └─────────────────────────────────────────────────────┘      │
│     ↓                                                             │
│     ├─ AddLog("✓ Location permissions granted")                 │
│     │                                                             │
│     └─ _beaconService.StartBeaconAsync()                        │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ PHASE 3: BEACON SERVICE STARTUP                                 │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  BeaconService.StartBeaconAsync()                                │
│     ├─ Mark _isRunning = true                                    │
│     │                                                             │
│     ├─ ┌─ VALIDATION PHASE ────────────────────────────────┐   │
│     │ │                                                     │   │
│     │ ├─ PermissionManager.HasRequiredLocationPermissions()     │
│     │ │  ├─ CheckSelfPermission(ACCESS_FINE_LOCATION)   │   │
│     │ │  ├─ CheckSelfPermission(ACCESS_COARSE_LOCATION) │   │
│     │ │  └─ Return (both must be Granted)               │   │
│     │ │                                                     │   │
│     │ │  If Any Missing:                                 │   │
│     │ │    ├─ ErrorOccurred?.Invoke(PermissionException) │   │
│     │ │    └─ Return (exit)                              │   │
│     │ │                                                     │   │
│     │ ├─ PermissionManager.HasForegroundServiceLocation()     │
│     │ │  ├─ For Android 12+: Check FOREGROUND_SERVICE   │   │
│     │ │  ├─ For Android 13+: Check FOREGROUND_SERVICE_LOCATION│
│     │ │  └─ Return Granted/NotGranted                   │   │
│     │ │                                                     │   │
│     │ │  If Not Granted:                                 │   │
│     │ │    ├─ ErrorOccurred?.Invoke(PermissionException) │   │
│     │ │    └─ Return (exit)                              │   │
│     │ │                                                     │   │
│     │ └─────────────────────────────────────────────────────┘   │
│     ├─ AddLog("✓ Required permissions verified")                │
│     │                                                             │
│     ├─ ┌─ SERVICE START PHASE ─────────────────────────────┐   │
│     │ │                                                     │   │
│     │ ├─ context.StartForegroundService(intent)          │   │
│     │ │  └─ Starts BeaconForegroundService               │   │
│     │ │                                                     │   │
│     │ └─────────────────────────────────────────────────────┘   │
│     │                                                             │
│     └─ NetworkStateManager.StartMonitoring()                    │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ PHASE 4: FOREGROUND SERVICE RUNNING                             │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  BeaconForegroundService.OnStartCommand()                        │
│     ├─ [Runs on Background Thread]                              │
│     ├─ Acquire WakeLock                                          │
│     ├─ CreateNotificationChannel()                               │
│     ├─ BuildNotification()                                       │
│     │                                                             │
│     └─ StartForeground(notificationId, notification, TypeLocation)
│        ├─ Tells Android: "This service runs visibly"             │
│        ├─ Displays persistent notification                       │
│        └─ Permissions already verified ✓                         │
│                                                                   │
│  [In background]                                                 │
│  Task.Run(async () =>                                            │
│  {                                                                │
│     Loop (10s interval):                                         │
│       ├─ Task.Delay(10000)                                       │
│       ├─ LocationService.GetLocationAsync()                      │
│       │  ├─ [Background thread context]                         │
│       │  ├─ Permissions.CheckStatusAsync() [Check only]          │
│       │  │  └─ NO RequestAsync from background thread!           │
│       │  ├─ Geolocation.Default.GetLocationAsync()               │
│       │  └─ Return coordinates or (0,0,0)                        │
│       │                                                           │
│       └─ BeaconService.SendBeaconDirectAsync()                  │
│          ├─ Validate beacon data                                 │
│          ├─ POST to server                                       │
│          └─ Update notification                                  │
│  })                                                               │
│                                                                   │
│  [Service remains running] ← Foreground service + notification   │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ PHASE 5: DEVICE REBOOT                                          │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Device reboots                                                  │
│     ↓                                                             │
│  Android system broadcasts Intent.ActionBootCompleted            │
│     ↓                                                             │
│  BootReceiver.OnReceive()                                        │
│  [Limited context - eligible state NOT guaranteed]               │
│     ├─ If intent.Action == BOOT_COMPLETED:                      │
│     │  ├─ ┌─ PERMISSION CHECK PHASE ──────────────────────┐    │
│     │  │ │                                                  │    │
│     │  │ ├─ PermissionManager.HasRequiredLocationPermissions()  │
│     │  │ │  └─ Check both FINE + COARSE location          │    │
│     │  │ │                                                  │    │
│     │  │ │  If Not Granted:                                │    │
│     │  │ │    ├─ AddLog("✗ Boot: Permissions not granted") │    │
│     │  │ │    └─ Return (graceful - user will start later) │    │
│     │  │ │                                                  │    │
│     │  │ ├─ PermissionManager.HasForegroundServiceLocation()    │
│     │  │ │  └─ Check FG location permission               │    │
│     │  │ │                                                  │    │
│     │  │ │  If Not Granted:                                │    │
│     │  │ │    ├─ AddLog("✗ Boot: FG location not granted") │    │
│     │  │ │    └─ Return                                    │    │
│     │  │ └──────────────────────────────────────────────────┘   │
│     │  │                                                         │
│     │  └─ If all permissions OK:                               │
│     │     ├─ context.StartForegroundService(intent)             │
│     │     ├─ Permissions MUST still be valid                   │
│     │     └─ Service resumes ✓                                  │
│     │                                                            │
│     └─ Catch Exception → Log error, continue             │
│                                                                   │
│  Result: Beacon resumes IFF permissions still granted ✓          │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘

```

## Permission Dependency Tree

```
FOREGROUND SERVICE LOCATION (Android 15+)
  ├─ Requires: FOREGROUND_SERVICE (manifest)
  ├─ Requires: FOREGROUND_SERVICE_LOCATION (manifest)
  └─ Requires: At least one of:
	 ├─ ACCESS_FINE_LOCATION (runtime permission + manifest)
	 └─ ACCESS_COARSE_LOCATION (runtime permission + manifest)

Optional for better function:
  └─ ACCESS_BACKGROUND_LOCATION (manifest only)
	 └─ Enables: LocationAlways permission in MAUI

Runtime Requirements (Android 15+):
  ├─ User must grant location permission
  ├─ App must be in "eligible state":
  │  ├─ Foreground notification helps
  │  ├─ Recent user interaction helps
  │  └─ Battery optimization exemption helps
  └─ Manifest must declare all permissions
```

## Security Gate Architecture

```
					┌──────────────────────────┐
					│   User Input Required    │
					│  (Permission Dialog)     │
					└────────┬─────────────────┘
							 │
					┌────────▼──────────────┐
		 ┌─────────►│  Gate 1: MainPage    │
		 │          │                       │
		 │          │ Permissions.Request  │
		 │          │ LocationWhenInUse    │
		 │          │                       │
		 │          │ User Grants? YES ─►  │
		 │          │ User Denies?  NO ─►  │
		 │          └────────┬──────────────┘
		 │                   │ (Permission granted)
		 │                   ▼
		 │          ┌────────────────────────┐
		 │   ┌─────►│  Gate 2: Service      │
		 │   │      │                        │
		 │   │      │ PermissionManager     │
		 │   │      │ .HasRequired...()     │
		 │   │      │                        │
		 │   │      │ All OK? YES ─────►   │
		 │   │      │ Any Missing? NO ──►  │
		 │   │      └────────┬──────────────┘
		 │   │               │ (Service ready)
		 │   │               ▼
		 │   │      ┌────────────────────────┐
		 │   │      │  Gate 3: Foreground   │
		 │   │      │                        │
		 │   │      │ AndroidRuntime        │
		 │   │      │ StartForegroundService│
		 │   │      │                        │
		 │   │      │ Internal check ◄──────►DEFENSIVE
		 │   │      └────────┬──────────────┘
		 │   │               │ (Service starts)
		 │   │               ▼
		 │   │      ┌────────────────────────┐
		 │   └─────►│  FAILURE GRACEFULLY   │
		 │          │                        │
		 │          │ ErrorOccurred event   │
		 │          │ Logs error message    │
		 │          │ UI shows error        │
		 │          │ User can retry        │
		 │          └────────────────────────┘
		 │
		 └─────────────────────────────────────────►
				  [Multiple layers prevent crash]

Result: NO SecurityException propagates to user
		IF any gate fails → Graceful error handling
```

## Execution Context Summary

| Component | Thread | Context | Can Request? | Action |
|-----------|--------|---------|--------------|--------|
| MainPage | Main | UI | ✅ YES | Request permissions |
| BeaconService | Main | UI callback | ✅ YES | Check permissions |
| BootReceiver | System | Limited | ❌ NO | Check only |
| BeaconForegroundService.OnStartCommand | Main | Service setup | ✅ YES | Check permissions |
| LocationService.GetLocationAsync | Background | Service task | ❌ NO | Check only |
| AlarmManager callback | Background | Limited | ❌ NO | Check only |

## Thread Safety Analysis

```
SAFE (Can request permissions):
  ✓ MainPage event handlers (UI thread)
  ✓ MainPage.StartBeaconAsync (async on UI thread)
  ✓ BeaconService methods (called from UI)

UNSAFE (Cannot request permissions):
  ✗ LocationService.GetLocationAsync (called from foreground service background task)
  ✗ BeaconForegroundService background task loop
  ✗ BootReceiver.OnReceive (broadcast receiver context)
  ✗ Alarm callbacks (limited context)

SOLUTION IMPLEMENTED:
  • Move all permission requests to MainPage (safe context)
  • Have service tasks check-only (no requests)
  • Document why checks are in place
  • Log permission status for debugging
```

## Error Recovery Tree

```
					SecurityException thrown
							  │
					┌─────────┴────────┐
					│                  │
			  From Service      From MainPage
			  Startup               Permission Request
					│                  │
			Caught by                Caught by
			try/catch              Promise chain
					│                  │
		 LogException          ErrorDialog shown
		 ErrorEvent fired       User can retry
			  │                  │
		 Beacon stops      Permission re-requested
		 User sees logs        │
		 Can restart app    Success or Denial
						   │
					  Try Again or
					  Go to Settings
```

---

## Key Design Principles Applied

1. **Defense in Depth**: Multiple permission gates instead of single check
2. **Fail Gracefully**: Return default values instead of throwing exceptions
3. **Inform User**: Show dialogs and log messages about permission status
4. **Right Context**: Request permissions always on main thread
5. **Check Before Call**: Validate permissions immediately before system API call
6. **No Silent Failures**: Always log permission issues for debugging
7. **Backward Compat**: Handle Android 6 through Android 15 appropriately

