using System.Text.Json.Serialization;

namespace LocationBeacon.Models;

public class BeaconPayload
{
    [JsonPropertyName("pairing_key")]
    public string PairingKey { get; set; } = string.Empty;

    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("battery_level")]
    public int BatteryLevel { get; set; }

    [JsonPropertyName("accuracy_m")]
    public double AccuracyM { get; set; }

    [JsonPropertyName("recorded_at")]
    public string RecordedAt { get; set; } = string.Empty;
}