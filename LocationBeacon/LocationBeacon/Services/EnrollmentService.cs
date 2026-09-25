using LocationBeacon.Models;
using System.Diagnostics;
using System.Text.Json;

namespace LocationBeacon.Services;

public class EnrollmentService
{
    private readonly HttpClient _httpClient;
    private readonly PreferencesService _preferencesService;
    private readonly DeviceInfoService _deviceInfoService;
    private CancellationTokenSource? _pollCts;

    private readonly string _enrollUrl = "https://kid-safe-spots.lovable.app/api/public/beacon-enroll";
    private readonly string _statusUrl = "https://kid-safe-spots.lovable.app/api/public/beacon-enroll-status";

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<Exception>? ErrorOccurred;
    public event EventHandler<string>? Approved; // secret
    public event EventHandler<(string Code, string PairingWord)>? EnrollmentGenerated;

    public EnrollmentService()
    {
        _httpClient = new HttpClient();
        _preferencesService = new PreferencesService();
        _deviceInfoService = new DeviceInfoService();
    }

    public async Task StartEnrollmentAsync()
    {
        try
        {
            // Generate enrollment code and pairing word
            var enrollmentCode = Guid.NewGuid().ToString();
            var pairingWord = _deviceInfoService.GeneratePairingWord();
            var deviceLabel = _preferencesService.GetDeviceLabel();
            if (string.IsNullOrWhiteSpace(deviceLabel))
                deviceLabel = _deviceInfoService.GetDeviceLabel();

            _preferencesService.SetEnrollmentCode(enrollmentCode);
            _preferencesService.SetPairingWord(pairingWord);
            _preferencesService.SetDeviceLabel(deviceLabel);
            _preferencesService.SetEnrollmentStatus("pending");

            // Notify UI of generated values
            EnrollmentGenerated?.Invoke(this, (enrollmentCode, pairingWord));

            var request = new EnrollmentRequest
            {
                EnrollmentCode = enrollmentCode,
                PairingWord = pairingWord,
                DeviceLabel = deviceLabel
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            StatusChanged?.Invoke(this, "Sending enrollment request...");
            var resp = await _httpClient.PostAsync(_enrollUrl, content);
            if (resp.IsSuccessStatusCode)
            {
                StatusChanged?.Invoke(this, "Enrollment request submitted; awaiting approval");
                // Start polling
                StartPolling();
            }
            else
            {
                var body = await resp.Content.ReadAsStringAsync();
                StatusChanged?.Invoke(this, $"Enrollment submission failed: {resp.StatusCode}");
                Debug.WriteLine($"Enrollment submission failed: {body}");
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
            StatusChanged?.Invoke(this, $"Enrollment error: {ex.Message}");
        }
    }

    private void StartPolling()
    {
        StopPolling();
        _pollCts = new CancellationTokenSource();
        _ = PollLoopAsync(_pollCts.Token);
    }

    public void StopPolling()
    {
        try
        {
            _pollCts?.Cancel();
            _pollCts = null;
        }
        catch { }
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), token);

                var enrollmentCode = _preferencesService.GetEnrollmentCode();
                if (string.IsNullOrWhiteSpace(enrollmentCode))
                {
                    StatusChanged?.Invoke(this, "Enrollment code missing, stopping poll");
                    _preferencesService.SetEnrollmentStatus("none");
                    Debug.WriteLine("❌ Enrollment code is empty, stopping poll");
                    return;
                }

                var payload = new { enrollment_code = enrollmentCode };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                Debug.WriteLine($"📡 Polling enrollment status with code: {enrollmentCode}");

                try
                {
                    var resp = await _httpClient.PostAsync(_statusUrl, content, token);
                    var body = await resp.Content.ReadAsStringAsync(token);
                    
                    Debug.WriteLine($"📥 Status poll response: {resp.StatusCode} - {body}");

                    if (resp.IsSuccessStatusCode)
                    {
                        var status = JsonSerializer.Deserialize<EnrollmentStatus>(body);
                        if (status != null)
                        {
                            Debug.WriteLine($"✓ Parsed status: {status.Status}");

                            if (status.Status == "pending")
                            {
                                StatusChanged?.Invoke(this, "Enrollment pending");
                                Debug.WriteLine("⏳ Still pending...");
                            }
                            else if (status.Status == "rejected")
                            {
                                StatusChanged?.Invoke(this, "Enrollment rejected");
                                _preferencesService.SetEnrollmentStatus("rejected");
                                Debug.WriteLine("❌ Enrollment rejected");
                                StopPolling();
                                return;
                            }
                            else if (status.Status == "approved")
                            {
                                if (!string.IsNullOrWhiteSpace(status.SecretCode))
                                {
                                    Debug.WriteLine($"✅ APPROVED! Secret: {status.SecretCode}");
                                    // store secret once
                                    await _preferencesService.SetPairingKeyAsync(status.SecretCode);
                                    _preferencesService.SetEnrollmentStatus("approved");
                                    StatusChanged?.Invoke(this, "Enrollment approved");
                                    Approved?.Invoke(this, status.SecretCode);
                                    StopPolling();
                                    return;
                                }
                                else
                                {
                                    Debug.WriteLine("⚠️ Approved but no secret_code received");
                                }
                            }
                            else if (status.Status == "expired")
                            {
                                StatusChanged?.Invoke(this, "Enrollment expired");
                                _preferencesService.SetEnrollmentStatus("expired");
                                Debug.WriteLine("⏰ Enrollment expired");
                                StopPolling();
                                return;
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"❌ Failed to parse response: {body}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"❌ HTTP error {resp.StatusCode}: {body}");
                    }
                }
                catch (TaskCanceledException)
                {
                    Debug.WriteLine("Polling canceled");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Polling error: {ex}");
                    ErrorOccurred?.Invoke(this, ex);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ PollLoop error: {ex}");
            ErrorOccurred?.Invoke(this, ex);
        }
    }
}
