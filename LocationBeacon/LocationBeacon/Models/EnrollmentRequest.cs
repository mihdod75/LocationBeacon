namespace LocationBeacon.Models;

public class EnrollmentRequest
{
    public string EnrollmentCode { get; set; } = string.Empty;
    public string PairingWord { get; set; } = string.Empty;
    public string DeviceLabel { get; set; } = string.Empty;
}
