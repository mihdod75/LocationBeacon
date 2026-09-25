using Microsoft.Maui.Storage;

namespace LocationBeacon.Services;

public class PreferencesService
{
    private const string PairingKeyKey = "pairing_key";
    private const string SendingIntervalKey = "sending_interval_seconds";
    private const int DefaultSendingInterval = 60;
    private const string EnrollmentCodeKey = "enrollment_code";
    private const string PairingWordKey = "pairing_word";
    private const string DeviceLabelKey = "device_label";
    private const string EnrollmentStatusKey = "enrollment_status"; // pending/approved/rejected/expired/none

    public string GetPairingKey()
    {
        try
        {
            return SecureStorage.Default.GetAsync(PairingKeyKey).Result ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task SetPairingKeyAsync(string pairingKey)
    {
        if (string.IsNullOrWhiteSpace(pairingKey))
        {
            pairingKey = string.Empty;
        }
        await SecureStorage.Default.SetAsync(PairingKeyKey, pairingKey);
    }

    // Enrollment related preferences
    public string GetEnrollmentCode()
    {
        return Preferences.Default.Get(EnrollmentCodeKey, string.Empty);
    }

    public void SetEnrollmentCode(string code)
    {
        Preferences.Default.Set(EnrollmentCodeKey, code ?? string.Empty);
    }

    public string GetPairingWord()
    {
        return Preferences.Default.Get(PairingWordKey, string.Empty);
    }

    public void SetPairingWord(string word)
    {
        Preferences.Default.Set(PairingWordKey, word ?? string.Empty);
    }

    public string GetDeviceLabel()
    {
        return Preferences.Default.Get(DeviceLabelKey, string.Empty);
    }

    public void SetDeviceLabel(string label)
    {
        Preferences.Default.Set(DeviceLabelKey, label ?? string.Empty);
    }

    public string GetEnrollmentStatus()
    {
        return Preferences.Default.Get(EnrollmentStatusKey, "none");
    }

    public void SetEnrollmentStatus(string status)
    {
        Preferences.Default.Set(EnrollmentStatusKey, status ?? "none");
    }

    public void ClearEnrollment()
    {
        SetEnrollmentCode(string.Empty);
        SetPairingWord(string.Empty);
        SetDeviceLabel(string.Empty);
        SetEnrollmentStatus("none");
    }

    public int GetSendingIntervalSeconds()
    {
        var value = Preferences.Default.Get(SendingIntervalKey, DefaultSendingInterval);
        return value;
    }

    public void SetSendingIntervalSeconds(int seconds)
    {
        if (seconds < 0)
            seconds = 0; // Allow 0 seconds for continuous sending

        Preferences.Default.Set(SendingIntervalKey, seconds);
    }
}