using System.Diagnostics;
using Microsoft.Maui.Devices.Sensors;

namespace LocationBeacon.Services;

/// <summary>
/// Location service with adaptive timeout strategy.
/// No caching: always requests fresh location when called.
/// On failure, escalates timeout for next acquisition attempt (persists across beacon cycles).
/// Only resets attempt count on successful acquisition.
/// </summary>
public class LocationService
{
    private int _locationAttemptCount = 0;
    private long _totalTimeSpentMs = 0;
    private const int MaxTotalTimeoutMs = 60000;  // 60 second ceiling across all attempts in a cycle
    private const int BaseTimeoutSeconds = 3;
    private const int TimeoutIncrementSeconds = 2;
    private const int MaxSingleAttemptSeconds = 20;

    /// <summary>
    /// Attempts to acquire location with adaptive timeout.
    /// Timeout escalates with each failed attempt: 3s, 5s, 7s, ... up to 20s per attempt.
    /// Across all attempts in a cycle, total timeout is capped at 60 seconds.
    /// Returns (0, 0, 0) if location cannot be acquired.
    /// Resets attempt count to 0 on success.
    /// </summary>
    public async Task<(double Latitude, double Longitude, double Accuracy)> TryGetLocationWithAdaptiveTimeout()
    {
        try
        {
            // Check permissions (required before any location call)
            var backgroundStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            var foregroundStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            Debug.WriteLine($"[LocationService] Permission check - Background: {backgroundStatus}, Foreground: {foregroundStatus}");

            if (backgroundStatus != PermissionStatus.Granted && foregroundStatus != PermissionStatus.Granted)
            {
                Debug.WriteLine("✗ [LocationService] Location permissions not granted");
                return (0, 0, 0);
            }

            // Calculate adaptive timeout for this attempt
            int timeoutSeconds = BaseTimeoutSeconds + (_locationAttemptCount * TimeoutIncrementSeconds);
            if (timeoutSeconds > MaxSingleAttemptSeconds)
                timeoutSeconds = MaxSingleAttemptSeconds;

            // Check if we've exceeded total 60-second ceiling
            if (_totalTimeSpentMs >= MaxTotalTimeoutMs)
            {
                Debug.WriteLine($"⚠ [LocationService] Timeout ceiling (60s) reached for location acquisition after {_locationAttemptCount} attempts");
                Debug.WriteLine($"⊘ [LocationService] Skipping send: location acquisition failed after 60s total timeout");
                return (0, 0, 0);
            }

            Debug.WriteLine($"[LocationService] Attempt #{_locationAttemptCount + 1}: Trying location with {timeoutSeconds}s timeout (total spent: {_totalTimeSpentMs}ms / {MaxTotalTimeoutMs}ms)");

            var stopwatch = Stopwatch.StartNew();
            var location = await TryGetLocationWithTimeout(TimeSpan.FromSeconds(timeoutSeconds));
            stopwatch.Stop();

            _totalTimeSpentMs += (long)stopwatch.Elapsed.TotalMilliseconds;

            if (location != null)
            {
                // Success: reset attempt counter for next beacon cycle
                Debug.WriteLine($"✓ [LocationService] Location obtained after {stopwatch.Elapsed.TotalSeconds:F2}s: Lat={location.Latitude:F6}, Lon={location.Longitude:F6}, Accuracy={location.Accuracy}m");
                _locationAttemptCount = 0;
                _totalTimeSpentMs = 0;
                return (location.Latitude, location.Longitude, location.Accuracy ?? 0);
            }
            else
            {
                // Failure: increment attempt count for next beacon cycle
                _locationAttemptCount++;
                Debug.WriteLine($"✗ [LocationService] Location acquisition failed in {stopwatch.Elapsed.TotalSeconds:F2}s. Attempt count now: {_locationAttemptCount}");
                return (0, 0, 0);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"✗ [LocationService] Error during location acquisition: {ex.Message}");
            _locationAttemptCount++;
            return (0, 0, 0);
        }
    }

    private async Task<Location?> TryGetLocationWithTimeout(TimeSpan timeout)
    {
        try
        {
            var request = new GeolocationRequest(
                accuracy: GeolocationAccuracy.Default,
                timeout: timeout);

            return await Geolocation.Default.GetLocationAsync(request);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocationService] Geolocation request failed: {ex.Message}");
            return null;
        }
    }

    public int GetBatteryLevel()
    {
        try
        {
            return (int)Math.Round(Battery.Default.ChargeLevel * 100);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting battery level: {ex.Message}");
            return 50; // Default fallback
        }
    }
}