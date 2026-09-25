#if __ANDROID__
using Android.Content;
using Android.OS;
using AndroidX.Work;
using Java.Util.Concurrent;
using System.Diagnostics;
using DebugWriter = System.Diagnostics.Debug;

namespace LocationBeacon.Services;

/// <summary>
/// Uses WorkManager (Google's recommended API) for reliable background task scheduling.
/// WorkManager handles Doze mode better than AlarmManager for periodic tasks.
/// </summary>
public class BeaconWorkManager
{
    private readonly Context _context;
    private const string BeaconWorkTag = "location_beacon_work";
    private const string BeaconWorkName = "location_beacon_periodic";

    public BeaconWorkManager(Context context)
    {
        _context = context;
    }

    public void ScheduleBeaconWork(int intervalSeconds)
    {
        try
        {
            // Convert seconds to minutes (WorkManager uses minutes as minimum interval)
            long intervalMinutes = Math.Max(15, intervalSeconds / 60); // WorkManager minimum is 15 minutes
            if (intervalSeconds > 0 && intervalSeconds < 900)
            {
                intervalMinutes = 15; // Use minimum allowed by WorkManager
                DebugWriter.WriteLine($"⚠ Note: Requested {intervalSeconds}s interval adjusted to {intervalMinutes}m (WorkManager minimum)");
            }

            DebugWriter.WriteLine($"✓ Scheduling WorkManager beacon task every {intervalMinutes} minutes");

            // Create constraints
            var constraints = new Constraints.Builder()
                .SetRequiredNetworkType(NetworkType.Connected)
                .Build();

            // Create periodic work request builder
            var builder = new PeriodicWorkRequest.Builder(typeof(BeaconWorker), intervalMinutes, TimeUnit.Minutes);
            builder.SetConstraints(constraints);
            builder.AddTag(BeaconWorkTag);
            builder.SetBackoffCriteria(BackoffPolicy.Exponential, PeriodicWorkRequest.MinBackoffMillis, TimeUnit.Milliseconds);

            var beaconWorkRequest = builder.Build();

            // Enqueue the work
            WorkManager.GetInstance(_context).EnqueueUniquePeriodicWork(
                BeaconWorkName,
                ExistingPeriodicWorkPolicy.Keep,
                beaconWorkRequest);

            DebugWriter.WriteLine($"✓ WorkManager beacon task scheduled ({intervalMinutes} minute intervals)");
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"✗ Failed to schedule beacon work: {ex.Message}");
            DebugWriter.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    public void CancelBeaconWork()
    {
        try
        {
            WorkManager.GetInstance(_context).CancelUniqueWork(BeaconWorkName);
            DebugWriter.WriteLine("✓ Beacon work cancelled");
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"✗ Failed to cancel beacon work: {ex.Message}");
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
                        DebugWriter.WriteLine("⚠ App is NOT exempt from Doze mode - WorkManager will handle delays");
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

/// <summary>
/// Worker that runs periodic beacon tasks.
/// WorkManager will call this even during Doze mode with better reliability than AlarmManager.
/// </summary>
public class BeaconWorker : Worker
{
    private BeaconService? _beaconService;

    public BeaconWorker(Context context, WorkerParameters workerParams)
        : base(context, workerParams)
    {
    }

    public override Result DoWork()
    {
        try
        {
            DebugWriter.WriteLine("✓ BeaconWorker.DoWork() triggered by WorkManager");

            _beaconService = new BeaconService();

            // Run the beacon send asynchronously and wait for it
            var sendTask = _beaconService.SendBeaconDirectAsync();
            sendTask.Wait(TimeSpan.FromSeconds(60)); // Wait up to 60 seconds

            DebugWriter.WriteLine("✓ BeaconWorker completed successfully");
            return Result.InvokeSuccess();
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"✗ BeaconWorker error: {ex.Message}");
            DebugWriter.WriteLine($"Stack trace: {ex.StackTrace}");

            // Return retry to allow WorkManager to retry with backoff
            return Result.InvokeRetry();
        }
        finally
        {
            _beaconService = null;
        }
    }
}

#endif
