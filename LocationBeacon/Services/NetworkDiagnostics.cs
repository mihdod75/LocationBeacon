#if __ANDROID__
using Android.Content;
using Android.Net;
using Android.Telephony;
using System.Diagnostics;
using DebugWriter = System.Diagnostics.Debug;

namespace LocationBeacon.Services;

/// <summary>
/// Provides detailed network diagnostics to help identify connectivity issues.
/// Used when beacon sends fail to inform user of specific problems.
/// </summary>
public class NetworkDiagnostics
{
    private readonly Context _context;
    private readonly ConnectivityManager _connectivityManager;
    private readonly TelephonyManager _telephonyManager;

    public NetworkDiagnostics(Context context)
    {
        _context = context;
        _connectivityManager = (ConnectivityManager?)context.GetSystemService(Context.ConnectivityService)
            ?? throw new InvalidOperationException("ConnectivityManager not available");
        _telephonyManager = (TelephonyManager?)context.GetSystemService(Context.TelephonyService)
            ?? throw new InvalidOperationException("TelephonyManager not available");
    }

    /// <summary>
    /// Diagnoses why beacon send failed and returns user-friendly message.
    /// </summary>
    public string GetNetworkDiagnosticsMessage()
    {
        try
        {
            // Check if we have an active network (FIRST check - most basic)
            var activeNetwork = _connectivityManager.ActiveNetwork;
            if (activeNetwork == null)
            {
                DebugWriter.WriteLine("✗ DIAGNOSIS: No active network");
                return "📡 No network connection detected. Check WiFi or mobile signal.";
            }

            // Check network capabilities
            var capabilities = _connectivityManager.GetNetworkCapabilities(activeNetwork);
            if (capabilities == null)
            {
                DebugWriter.WriteLine("✗ DIAGNOSIS: No network capabilities");
                return "📡 Network connection unstable. Try reconnecting.";
            }

            // Check internet capability
            if (!capabilities.HasCapability(NetCapability.Internet))
            {
                DebugWriter.WriteLine("✗ DIAGNOSIS: No internet capability");
                return "🌐 Internet access not available. Check your plan or signal.";
            }

            // Check validated capability (actual working internet)
            if (!capabilities.HasCapability(NetCapability.Validated))
            {
                DebugWriter.WriteLine("✗ DIAGNOSIS: Network not validated");
                return "🌐 Internet connection not validated. Captive portal may be blocking access.";
            }

            // If we have an active cellular network, check if mobile data is enabled
            // (this is only relevant when on cellular, not on WiFi)
            if (IsCellularConnection(capabilities))
            {
                if (!IsMobileDataEnabled())
                {
                    DebugWriter.WriteLine("✗ DIAGNOSIS: Mobile data is disabled");
                    return "📱 Mobile data is not enabled. Please enable it in Settings.";
                }
            }

            // If we get here, network seems OK - don't show a message
            // The child can't fix transient network issues
            DebugWriter.WriteLine("✗ DIAGNOSIS: Network seems OK but send failed (likely transient issue)");
            return null; // No actionable message
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"✗ Error getting diagnostics: {ex.Message}");
            return null; // No actionable message
        }
    }

    /// <summary>
    /// Checks if mobile data is enabled at the system level.
    /// </summary>
    private bool IsMobileDataEnabled()
    {
        try
        {
            // Use reflection to check mobile data setting (it's a hidden API)
            var method = _telephonyManager.Class.GetMethod("getDataEnabled");
            if (method != null)
            {
                var result = method.Invoke(_telephonyManager, null);
                if (result != null && result is bool)
                {
                    bool enabled = (bool)result;
                    DebugWriter.WriteLine($"Mobile data enabled: {enabled}");
                    return enabled;
                }
            }

            // Fallback: check if there's an active cellular network
            var activeNetwork = _connectivityManager.ActiveNetwork;
            if (activeNetwork != null)
            {
                var capabilities = _connectivityManager.GetNetworkCapabilities(activeNetwork);
                if (capabilities != null && IsCellularConnection(capabilities))
                {
                    DebugWriter.WriteLine("Mobile data appears to be enabled (active cellular network)");
                    return true;
                }
            }

            DebugWriter.WriteLine("Mobile data appears to be disabled");
            return false;
        }
        catch (Exception ex)
        {
            DebugWriter.WriteLine($"✗ Error checking mobile data: {ex.Message}");
            // Can't determine, assume it might be the issue
            return false;
        }
    }

    /// <summary>
    /// Checks if the active network is cellular (mobile data).
    /// </summary>
    private bool IsCellularConnection(NetworkCapabilities capabilities)
    {
        try
        {
            return capabilities.HasTransport(TransportType.Cellular);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets full network status summary for logging.
    /// </summary>
    public string GetDetailedNetworkStatus()
    {
        try
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine("=== Network Status ===");

            // Mobile data
            summary.AppendLine($"Mobile Data: {(IsMobileDataEnabled() ? "✓ Enabled" : "✗ Disabled")}");

            // Active network
            var activeNetwork = _connectivityManager.ActiveNetwork;
            if (activeNetwork == null)
            {
                summary.AppendLine("Active Network: ✗ None");
                return summary.ToString();
            }

            summary.AppendLine("Active Network: ✓ Connected");

            // Capabilities
            var capabilities = _connectivityManager.GetNetworkCapabilities(activeNetwork);
            if (capabilities != null)
            {
                summary.AppendLine($"Internet: {(capabilities.HasCapability(NetCapability.Internet) ? "✓" : "✗")}");
                summary.AppendLine($"Validated: {(capabilities.HasCapability(NetCapability.Validated) ? "✓" : "✗")}");

                // Transport type
                if (capabilities.HasTransport(TransportType.Wifi))
                    summary.AppendLine("Type: WiFi");
                else if (capabilities.HasTransport(TransportType.Cellular))
                {
                    summary.AppendLine("Type: Cellular");
                }
                else
                    summary.AppendLine("Type: Other");
            }

            return summary.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting network status: {ex.Message}";
        }
    }
}
#endif
