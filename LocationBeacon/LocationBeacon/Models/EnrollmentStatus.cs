using System.Text.Json.Serialization;

namespace LocationBeacon.Models;

public class EnrollmentStatus
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("secret_code")]
    public string? SecretCode { get; set; }

    [JsonPropertyName("beacon_name")]
    public string? BeaconName { get; set; }
}