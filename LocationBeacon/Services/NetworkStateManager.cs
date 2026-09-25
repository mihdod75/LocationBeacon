#if __ANDROID__
using Android.Content;
using Android.Net;
using System.Diagnostics;
using DebugWriter = System.Diagnostics.Debug;

namespace LocationBeacon.Services;

/// <summary>
/// Monitors network connectivity state changes on Android.
/// Provides methods to check network availability and handle Doze mode transitions.
/// </summary>
public class NetworkStateManager
{
    private readonly Context _context;
    private readonly ConnectivityManager _connectivityManager;
    private NetworkCallback? _networkCallback;
    private bool _isNetworkAvailable;

    public event EventHandler<bool>? NetworkAvailabilityChanged;

    public bool IsNetworkAvailable => _isNetworkAvailable;

    public NetworkStateManager(Context context)
    {
        _context = context;
        _connectivityManager = (ConnectivityManager?)_context.GetSystemService(Context.ConnectivityService)
            ?? throw new InvalidOperationException("ConnectivityManager not available");

        _isNetworkAvailable = CheckNetworkAvailability();
    }

    /// <summary>
    /// Checks if network is currently available (working or connecting).
    /// </summary>
    public bool CheckNetworkAvailability()
    {
        try
        {
            var network = _connectivityManager.ActiveNetwork;
            if (network == null)
            {
                DebugWriter.WriteLine("⚠ No active network detected");
                return false;
            }

            var capabilities = _connectivityManager.GetNetworkCapabilities(network);
            if (capabilities == null)
            {
                DebugWriter.WriteLine("⚠ No network capabilities available");
                return false;
            }

            bool hasInternet = capabilities.HasCapability(NetCapability.Internet);
            bool hasValidated = capabilities.HasCapability(NetCapability.Validated);

            // Consider network available if we have internet capability and validated access
            bool available = hasInternet && hasValidated;

            DebugWriter.WriteLine($"✓ Network check - Internet:{hasInternet} Validated:{hasValidated} Available:{available}");
            return available;
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"✗ Error checking network availability: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Starts monitoring network state changes.
    /// Call when beacon service starts.
    /// </summary>
    public void StartMonitoring()
    {
        try
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.N)
            {
                _networkCallback = new NetworkCallback(this);
                var request = new NetworkRequest.Builder()
                    .AddCapability(NetCapability.Internet)
                    .Build();

                _connectivityManager.RegisterNetworkCallback(request, _networkCallback);
                DebugWriter.WriteLine("✓ Network state monitoring started");
            }
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"✗ Failed to start network monitoring: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops monitoring network state changes.
    /// Call when beacon service stops.
    /// </summary>
    public void StopMonitoring()
    {
        try
        {
            if (_networkCallback != null && Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.N)
            {
                _connectivityManager.UnregisterNetworkCallback(_networkCallback);
                _networkCallback = null;
                DebugWriter.WriteLine("✓ Network state monitoring stopped");
            }
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"✗ Failed to stop network monitoring: {ex.Message}");
        }
    }

    /// <summary>
    /// Waits for network to become available after a disconnect.
    /// Returns true if network became available within timeout, false if timeout.
    /// Used during Doze recovery.
    /// </summary>
    public async Task<bool> WaitForNetworkAsync(int timeoutMs = 5000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            if (CheckNetworkAvailability())
            {
                DebugWriter.WriteLine($"✓ Network restored after {stopwatch.ElapsedMilliseconds}ms");
                return true;
            }
            await Task.Delay(500);
        }

        DebugWriter.WriteLine($"✗ Network recovery timeout after {timeoutMs}ms");
        return false;
    }

    /// <summary>
    /// Internal callback for network state changes.
    /// </summary>
    private class NetworkCallback : ConnectivityManager.NetworkCallback
    {
        private readonly NetworkStateManager _manager;
        private DateTime _lastChange = DateTime.UtcNow;

        public NetworkCallback(NetworkStateManager manager)
        {
            _manager = manager;
        }

        public override void OnAvailable(Network network)
        {
            // Debounce rapid changes (Doze can cause multiple quick events)
            if ((DateTime.UtcNow - _lastChange).TotalMilliseconds < 1000)
            {
                return;
            }
            _lastChange = DateTime.UtcNow;

            _manager._isNetworkAvailable = true;
            DebugWriter.WriteLine("⚡ Network became available (Doze recovery?)");
            _manager.NetworkAvailabilityChanged?.Invoke(_manager, true);
        }

        public override void OnLost(Network network)
        {
            // Debounce rapid changes
            if ((DateTime.UtcNow - _lastChange).TotalMilliseconds < 1000)
            {
                return;
            }
            _lastChange = DateTime.UtcNow;

            _manager._isNetworkAvailable = false;
            DebugWriter.WriteLine("⚠ Network lost (entering Doze?)");
            _manager.NetworkAvailabilityChanged?.Invoke(_manager, false);
        }

        public override void OnCapabilitiesChanged(Network network, NetworkCapabilities capabilities)
        {
            bool hasInternet = capabilities.HasCapability(NetCapability.Internet);
            bool wasAvailable = _manager._isNetworkAvailable;

            _manager._isNetworkAvailable = hasInternet;

            if (hasInternet && !wasAvailable)
            {
                DebugWriter.WriteLine("⚡ Network capabilities restored");
                _manager.NetworkAvailabilityChanged?.Invoke(_manager, true);
            }
            else if (!hasInternet && wasAvailable)
            {
                DebugWriter.WriteLine("⚠ Network capabilities lost");
                _manager.NetworkAvailabilityChanged?.Invoke(_manager, false);
            }
        }
    }
}

#endif
