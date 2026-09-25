#if __ANDROID__
using Android.App;
using Android.Content;
using Android.OS;
using DebugWriter = System.Diagnostics.Debug;

namespace LocationBeacon.Services;

/// <summary>
/// Manages AlarmManager scheduling to wake the device and trigger beacon sends
/// even when the device is in deep sleep or Doze mode.
/// Uses setAndAllowWhileIdle for Android 12+ to work around Doze restrictions.
/// </summary>
public class BeaconAlarmManager
{
    private AlarmManager? _alarmManager;
    private readonly Context _context;
    private const int AlarmRequestCode = 9789;

    public BeaconAlarmManager(Context context)
    {
        _context = context;
        _alarmManager = context.GetSystemService(Context.AlarmService) as AlarmManager;
    }

    public void ScheduleBeaconAlarm(int intervalSeconds)
    {
        try
        {
            if (_alarmManager == null)
            {
                DebugWriter.WriteLine("✗ AlarmManager is not available");
                return;
            }

            var intent = new Intent(_context, typeof(Platforms.Android.BeaconForegroundService));
            intent.SetAction("com.companyname.locationbeacon.SEND_BEACON");

            var pendingIntent = PendingIntent.GetService(
                _context,
                AlarmRequestCode,
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            _alarmManager.Cancel(pendingIntent);

            long triggerAtTime = SystemClock.ElapsedRealtime() + (intervalSeconds * 1000L);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.S) // Android 12+
            {
                // setAndAllowWhileIdle() only fires ONCE, but combined with reschedule logic
                // in BeaconService, this ensures continuous operation during Doze
                _alarmManager.SetAndAllowWhileIdle(
                    AlarmType.ElapsedRealtimeWakeup,
                    triggerAtTime,
                    pendingIntent);
                DebugWriter.WriteLine($"✓ Beacon alarm scheduled (setAndAllowWhileIdle) in {intervalSeconds}s - will be rescheduled by service");
            }
            else if (Build.VERSION.SdkInt >= BuildVersionCodes.M) // Android 6.0+
            {
                // For Android 6-11, also use setAndAllowWhileIdle for Doze compatibility
                _alarmManager.SetAndAllowWhileIdle(
                    AlarmType.ElapsedRealtimeWakeup,
                    triggerAtTime,
                    pendingIntent);
                DebugWriter.WriteLine($"✓ Beacon alarm scheduled (setAndAllowWhileIdle) in {intervalSeconds}s");
            }
            else
            {
                // Pre-Android 6.0: use SetRepeating (no Doze mode)
                _alarmManager.SetRepeating(
                    AlarmType.ElapsedRealtimeWakeup,
                    triggerAtTime,
                    (intervalSeconds * 1000L),
                    pendingIntent);
                DebugWriter.WriteLine($"✓ Beacon alarm scheduled (setRepeating) every {intervalSeconds}s");
            }
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"✗ Failed to schedule beacon alarm: {ex.Message}");
            DebugWriter.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    public void CancelBeaconAlarm()
    {
        try
        {
            if (_alarmManager == null)
                return;

            var intent = new Intent(_context, typeof(Platforms.Android.BeaconForegroundService));
            intent.SetAction("com.companyname.locationbeacon.SEND_BEACON");

            var pendingIntent = PendingIntent.GetService(
                _context,
                AlarmRequestCode,
                intent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            _alarmManager.Cancel(pendingIntent);
            pendingIntent?.Cancel();

            DebugWriter.WriteLine("✓ Beacon alarm cancelled");
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"✗ Failed to cancel beacon alarm: {ex.Message}");
        }
    }

    public bool IsDozeExempt()
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                var powerManager = _context.GetSystemService(Context.PowerService) as Android.OS.PowerManager;
                if (powerManager != null)
                {
                    return powerManager.IsIgnoringBatteryOptimizations(_context.PackageName);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"✗ Error checking Doze exemption: {ex.Message}");
            return false;
        }
    }

    public void LogDozeStatus()
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                var powerManager = _context.GetSystemService(Context.PowerService) as Android.OS.PowerManager;
                if (powerManager != null)
                {
                    bool isExempt = powerManager.IsIgnoringBatteryOptimizations(_context.PackageName);
                    if (isExempt)
                    {
                        DebugWriter.WriteLine("✓ App is exempt from Doze mode (battery optimization disabled)");
                    }
                    else
                    {
                        DebugWriter.WriteLine("⚠ App is NOT exempt from Doze mode - beacons may be delayed");
                        DebugWriter.WriteLine("⚠ User should grant exemption: Settings > Apps > LocationBeacon > Battery");
                    }
                }
            }
            else
            {
                DebugWriter.WriteLine("✓ Device is pre-Android 6.0 (no Doze mode)");
            }
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"Error logging Doze status: {ex.Message}");
        }
    }
}

#endif
