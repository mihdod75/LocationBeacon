using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Content;

namespace LocationBeacon
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private static bool _batteryWhitelistRequested = false;
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Request notification permission for Android 13+
            RequestNotificationPermission();

            // Battery whitelist exemption is requested on-demand when user starts beacon
            // NOT on app startup to avoid minimizing the app
        }

        private void RequestNotificationPermission()
        {
            try
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu) // Android 13+
                {
                    // Check if permission is already granted
                    if (CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
                    {
                        System.Diagnostics.Debug.WriteLine("⚠ Requesting POST_NOTIFICATIONS permission for Android 13+");
                        RequestPermissions([Android.Manifest.Permission.PostNotifications], 1);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("✓ POST_NOTIFICATIONS permission already granted");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error requesting notification permission: {ex.Message}");
            }
        }

        private void RequestBatteryWhitelistExemption()
        {
            try
            {
                // Only request once per app session to avoid repeatedly minimizing the app
                if (_batteryWhitelistRequested)
                {
                    return;
                }

                if (Build.VERSION.SdkInt >= BuildVersionCodes.S) // Android 12+
                {
                    var powerManager = GetSystemService(PowerService) as Android.OS.PowerManager;
                    if (powerManager != null && !powerManager.IsIgnoringBatteryOptimizations(PackageName))
                    {
                        _batteryWhitelistRequested = true;
                        System.Diagnostics.Debug.WriteLine("⚠ Requesting battery optimization whitelist exemption");

                        var intent = new Intent(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
                        intent.SetData(Android.Net.Uri.Parse("package:" + PackageName));

                        try
                        {
                            StartActivity(intent);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Could not open battery settings: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error requesting battery whitelist: {ex.Message}");
            }
        }

        /// <summary>
        /// Public method to request battery exemption on-demand (e.g., when user starts beacon)
        /// This should only be called in response to user action, not on app startup
        /// </summary>
        public void RequestBatteryExemptionOnDemand()
        {
            RequestBatteryWhitelistExemption();
        }
    }
}
