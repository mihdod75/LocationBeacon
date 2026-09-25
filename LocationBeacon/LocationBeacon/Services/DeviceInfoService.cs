using Microsoft.Maui.Devices;

namespace LocationBeacon.Services;

public class DeviceInfoService
{
    public string GetDeviceLabel()
    {
        try
        {
            var manufacturer = DeviceInfo.Manufacturer;
            var model = DeviceInfo.Model;
            if (string.IsNullOrWhiteSpace(manufacturer))
                return model ?? string.Empty;
            return $"{manufacturer} {model}".Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    public string GeneratePairingWord(int length = 6)
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // avoid ambiguous
        var rng = new Random();
        var sb = new System.Text.StringBuilder(length);
        for (int i = 0; i < length; i++)
            sb.Append(chars[rng.Next(chars.Length)]);
        return sb.ToString();
    }
}
