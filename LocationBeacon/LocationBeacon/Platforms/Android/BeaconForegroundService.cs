#if __ANDROID__
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using System.Diagnostics;
using DebugWriter = System.Diagnostics.Debug;
using static Android.OS.PowerManager;

namespace LocationBeacon.Platforms.Android;

[Service(Enabled = true, Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
public class BeaconForegroundService : Service
{
    private const int NotificationId = 1;
    private const string ChannelId = "location_beacon_channel";
    private Task? _beaconTask;
    private Services.BeaconService? _beaconService;
    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private NotificationManager? _notificationManager;
    private WakeLock? _wakeLock;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        DebugWriter.WriteLine("✓ BeaconForegroundService.OnStartCommand called");

        try
        {
            // Acquire wake lock to keep CPU awake when screen is off
            if (_wakeLock == null)
            {
                var powerManager = GetSystemService(PowerService) as PowerManager;
                _wakeLock = powerManager?.NewWakeLock(WakeLockFlags.Partial, "LocationBeacon:BeaconWakeLock");
                _wakeLock?.Acquire();
                DebugWriter.WriteLine("✓ Wake lock acquired");
            }

            CreateNotificationChannel();
            var notification = BuildNotification();

            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            {
                StartForeground(NotificationId, notification, ForegroundService.TypeLocation);
                DebugWriter.WriteLine("✓ StartForeground called with TypeLocation (Android 12+)");
            }
            else
            {
                StartForeground(NotificationId, notification);
                DebugWriter.WriteLine("✓ StartForeground called (Pre Android 12)");
            }
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"✗ Error starting foreground service: {ex.Message}");
            if (ex.InnerException != null)
            {
                DebugWriter.WriteLine($"✗ Inner exception: {ex.InnerException.Message}");
            }
            throw;
        }

        if (_beaconTask == null || _beaconTask.IsCompleted)
        {
            DebugWriter.WriteLine("✓ Starting new beacon task in foreground service");
            _beaconTask = Task.Run(async () =>
            {
                try
                {
                    _beaconService = new Services.BeaconService();
                    var preferencesService = new Services.PreferencesService();

                    // Do while beacon is set to emit
                    do
                    {
                        try
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                                break;

                            // Get the configured sending interval (in seconds)
                            int sendingIntervalSeconds = preferencesService.GetSendingIntervalSeconds();
                            int sendingIntervalMs = sendingIntervalSeconds * 1000;
                            DebugWriter.WriteLine($"[BeaconLoop] Sending interval: {sendingIntervalSeconds}s");

                            // Try to acquire location with adaptive timeout
                            var (latitude, longitude, accuracy) = await _beaconService.GetLocationForBeaconAsync();

                            if (latitude != 0 || longitude != 0)
                            {
                                // Location found: send beacon
                                DebugWriter.WriteLine($"✓ [BeaconLoop] Location acquired: Lat={latitude:F6}, Lon={longitude:F6}");

                                // Acquire wake lock for beacon send operation
                                var powerManager = GetSystemService(PowerService) as PowerManager;
                                using (var tempWakeLock = powerManager?.NewWakeLock(WakeLockFlags.Partial, "LocationBeacon:BeaconSend"))
                                {
                                    tempWakeLock?.Acquire();
                                    try
                                    {
                                        DebugWriter.WriteLine($"⏱ [BeaconLoop] Sending beacon from foreground service at {DateTime.UtcNow:u}");
                                        await _beaconService.SendBeaconDirectAsync();
                                        UpdateNotification();
                                        DebugWriter.WriteLine($"✓ [BeaconLoop] Beacon sent successfully");
                                    }
                                    finally
                                    {
                                        tempWakeLock?.Release();
                                    }
                                }
                            }
                            else
                            {
                                // Location not available: skip send
                                DebugWriter.WriteLine($"⊘ [BeaconLoop] Skipped send: no location available");
                            }

                            // Wait for sending interval before next attempt
                            DebugWriter.WriteLine($"[BeaconLoop] Waiting {sendingIntervalSeconds}s before next beacon attempt...");
                            await Task.Delay(sendingIntervalMs, _cancellationTokenSource.Token);
                        }
                        catch (System.OperationCanceledException)
                        {
                            DebugWriter.WriteLine("[BeaconLoop] Beacon loop cancelled");
                            break;
                        }
                        catch (Exception ex)
                        {
                            DebugWriter.WriteLine($"✗ [BeaconLoop] Error in beacon cycle: {ex.Message}");
                            DebugWriter.WriteLine($"Stack trace: {ex.StackTrace}");

                            // Wait before retrying on error
                            try
                            {
                                await Task.Delay(5000, _cancellationTokenSource.Token);
                            }
                            catch { }
                        }
                    } while (!_cancellationTokenSource.Token.IsCancellationRequested);

                    DebugWriter.WriteLine("[BeaconLoop] Beacon task loop exited");
                }
                catch (Exception ex)
                {
                    DebugWriter.WriteLine($"✗ Beacon task error: {ex.Message}");
                    DebugWriter.WriteLine($"Stack trace: {ex.StackTrace}");
                }
                finally
                {
                    _beaconService = null;
                    DebugWriter.WriteLine("[BeaconLoop] Beacon task cleanup complete");
                }
            });
        }
        else
        {
            DebugWriter.WriteLine("✓ Beacon task already running, reusing existing task");
        }

        return StartCommandResult.Sticky;
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                ChannelId,
                "Location Beacon",
                NotificationImportance.High)
            {
                Description = "Sends periodic location updates even in sleep mode"
            };

            channel.EnableVibration(false);
            channel.EnableLights(false);
            channel.SetSound(null, null);

            var notificationManager = GetSystemService(NotificationService) as NotificationManager;
            notificationManager?.CreateNotificationChannel(channel);
            _notificationManager = notificationManager;
            DebugWriter.WriteLine("✓ Notification channel created (silent: no sound/vibration/lights)");
        }
    }

    private Notification BuildNotification()
    {
        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("Location Beacon")
            .SetContentText("Sending location updates in background")
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetPriority(NotificationCompat.PriorityHigh)
            .SetCategory(Notification.CategoryService)
            .SetForegroundServiceBehavior(NotificationCompat.ForegroundServiceImmediate)
            .SetOngoing(true)
            .SetShowWhen(true)
            .SetAutoCancel(false)
            .SetSilent(true);

        return builder.Build();
    }

    private void UpdateNotification()
    {
        try
        {
            if (_notificationManager != null)
            {
                var notification = BuildNotification();
                _notificationManager.Notify(NotificationId, notification);
                DebugWriter.WriteLine("✓ Notification updated");
            }
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"✗ Error updating notification: {ex.Message}");
        }
    }

    public override void OnDestroy()
    {
        DebugWriter.WriteLine("✓ BeaconForegroundService.OnDestroy called");
        _cancellationTokenSource.Cancel();

        // Release wake lock
        if (_wakeLock?.IsHeld == true)
        {
            _wakeLock.Release();
            DebugWriter.WriteLine("✓ Wake lock released");
        }

        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }

    public override IBinder? OnBind(Intent intent)
    {
        return null;
    }
}
#endif
