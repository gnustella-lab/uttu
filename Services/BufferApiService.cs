using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Uttu.Models;

namespace Uttu.Services;

public class BufferApiService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.bufferapp.com/1/";

    public BufferApiService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Uttu-Desktop", "1.0"));
    }

    public async Task<(bool Success, BufferUser? User, string ErrorMessage)> GetUserAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return (false, null, "Buffer Access Token not provided.");
        }

        try
        {
            string url = $"{BaseUrl}user.json?access_token={Uri.EscapeDataString(accessToken)}";
            using var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return (false, null, "Invalid or expired Buffer token (HTTP 401 Unauthorized).");
            }

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                return (false, null, $"Buffer API error: {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var user = new BufferUser
            {
                Id = root.TryGetProperty("id", out var idP) ? idP.GetString() ?? "" : "",
                Email = root.TryGetProperty("email", out var eP) ? eP.GetString() ?? "" : "",
                Plan = root.TryGetProperty("plan", out var plP) ? plP.GetString() ?? "Free" : "Free",
                Timezone = root.TryGetProperty("timezone", out var tzP) ? tzP.GetString() ?? "UTC" : "UTC"
            };

            if (root.TryGetProperty("created_at", out var catP) && catP.TryGetInt64(out long createdUnix))
            {
                user.CreatedAt = DateTimeOffset.FromUnixTimeSeconds(createdUnix).LocalDateTime;
            }

            return (true, user, string.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BufferApiService] GetUserAsync error: {ex.Message}");
            return (false, null, $"Buffer connection error: {ex.Message}");
        }
    }

    public async Task<(bool Success, List<BufferProfile> Profiles, string ErrorMessage)> GetProfilesAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return (false, new List<BufferProfile>(), "Buffer Access Token not provided.");
        }

        try
        {
            string url = $"{BaseUrl}profiles.json?access_token={Uri.EscapeDataString(accessToken)}";
            using var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return (false, new List<BufferProfile>(), "Invalid Buffer token (HTTP 401 Unauthorized).");
            }

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                return (false, new List<BufferProfile>(), $"Buffer API error ({(int)response.StatusCode}): {body}");
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var profiles = new List<BufferProfile>();
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in doc.RootElement.EnumerateArray())
                {
                    string id = p.TryGetProperty("id", out var idP) ? idP.GetString() ?? "" : "";
                    string service = p.TryGetProperty("service", out var sP) ? sP.GetString() ?? "x" : "x";
                    string formattedUser = p.TryGetProperty("formatted_username", out var fuP) 
                        ? fuP.GetString() ?? "" 
                        : (p.TryGetProperty("service_username", out var suP) ? suP.GetString() ?? "@user" : "@user");
                    string? avatar = p.TryGetProperty("avatar", out var aP) ? aP.GetString() : null;
                    string timezone = p.TryGetProperty("timezone", out var tzP) ? tzP.GetString() ?? "UTC" : "UTC";

                    int pendingCount = 0;
                    int sentCount = 0;
                    if (p.TryGetProperty("counts", out var cP))
                    {
                        if (cP.TryGetProperty("pending", out var pendP)) pendingCount = pendP.GetInt32();
                        if (cP.TryGetProperty("sent", out var sentP)) sentCount = sentP.GetInt32();
                    }

                    profiles.Add(new BufferProfile
                    {
                        Id = id,
                        Service = service,
                        FormattedUsername = formattedUser,
                        Avatar = avatar,
                        Timezone = timezone,
                        PendingUpdatesCount = pendingCount,
                        SentUpdatesCount = sentCount,
                        IsSelected = true
                    });
                }
            }

            return (true, profiles, string.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BufferApiService] GetProfilesAsync error: {ex.Message}");
            return (false, new List<BufferProfile>(), $"Error fetching Buffer channels: {ex.Message}");
        }
    }

    public async Task<(bool Success, List<BufferUpdate> Updates, string ErrorMessage)> GetPendingUpdatesAsync(string accessToken, string profileId)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(profileId))
        {
            return (false, new List<BufferUpdate>(), "Invalid parameters.");
        }

        try
        {
            string url = $"{BaseUrl}profiles/{Uri.EscapeDataString(profileId)}/updates/pending.json?access_token={Uri.EscapeDataString(accessToken)}";
            using var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                return (false, new List<BufferUpdate>(), $"Error fetching pending queue: {body}");
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var updates = new List<BufferUpdate>();

            if (doc.RootElement.TryGetProperty("updates", out var upArr) && upArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in upArr.EnumerateArray())
                {
                    string id = item.TryGetProperty("id", out var idP) ? idP.GetString() ?? "" : "";
                    string text = item.TryGetProperty("text", out var tP) ? tP.GetString() ?? "" : "";
                    string status = item.TryGetProperty("status", out var sP) ? sP.GetString() ?? "buffer" : "buffer";

                    DateTime? dueAt = null;
                    if (item.TryGetProperty("due_at", out var dueP) && dueP.TryGetInt64(out long dueUnix))
                    {
                        dueAt = DateTimeOffset.FromUnixTimeSeconds(dueUnix).LocalDateTime;
                    }

                    string? photo = null;
                    if (item.TryGetProperty("media", out var mP) && mP.TryGetProperty("photo", out var photoP))
                    {
                        photo = photoP.GetString();
                    }

                    updates.Add(new BufferUpdate
                    {
                        Id = id,
                        ProfileId = profileId,
                        Text = text,
                        Status = status,
                        DueAt = dueAt,
                        MediaPhoto = photo
                    });
                }
            }

            return (true, updates, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, new List<BufferUpdate>(), $"Error fetching pending updates: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? UpdateId, string Message, string ErrorMessage)> CreateUpdateAsync(
        string accessToken,
        List<string> profileIds,
        string text,
        bool publishNow = false,
        DateTime? scheduledAt = null,
        string? photoUrl = null)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return (false, null, "", "Buffer Access Token not provided.");
        }

        if (profileIds == null || profileIds.Count == 0)
        {
            return (false, null, "", "No Buffer channel selected.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return (false, null, "", "Post text cannot be empty.");
        }

        try
        {
            string url = $"{BaseUrl}updates/create.json";

            var formFields = new List<KeyValuePair<string, string>>
            {
                new("access_token", accessToken),
                new("text", text),
                new("now", publishNow ? "true" : "false")
            };

            foreach (var pid in profileIds)
            {
                formFields.Add(new KeyValuePair<string, string>("profile_ids[]", pid));
            }

            if (scheduledAt.HasValue && !publishNow)
            {
                long unixSec = new DateTimeOffset(scheduledAt.Value).ToUnixTimeSeconds();
                formFields.Add(new KeyValuePair<string, string>("scheduled_at", unixSec.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(photoUrl))
            {
                formFields.Add(new KeyValuePair<string, string>("media[photo]", photoUrl));
            }

            using var formContent = new FormUrlEncodedContent(formFields);
            using var response = await _httpClient.PostAsync(url, formContent);

            string body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (false, null, "", $"Buffer API error ({(int)response.StatusCode}): {body}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            bool success = root.TryGetProperty("success", out var sP) && sP.GetBoolean();
            string message = root.TryGetProperty("message", out var mP) ? mP.GetString() ?? "" : "";

            string? updateId = null;
            if (root.TryGetProperty("updates", out var upArr) && upArr.ValueKind == JsonValueKind.Array && upArr.GetArrayLength() > 0)
            {
                updateId = upArr[0].TryGetProperty("id", out var idP) ? idP.GetString() : null;
            }
            else if (root.TryGetProperty("update", out var upObj) && upObj.TryGetProperty("id", out var upIdP))
            {
                updateId = upIdP.GetString();
            }

            if (string.IsNullOrEmpty(message))
            {
                message = publishNow 
                    ? "Post published immediately via Buffer." 
                    : "Post added to Buffer queue successfully.";
            }

            return (success, updateId, message, string.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BufferApiService] CreateUpdateAsync error: {ex.Message}");
            return (false, null, "", $"Exception sending update to Buffer: {ex.Message}");
        }
    }

    public async Task<(bool Success, string ErrorMessage)> DestroyUpdateAsync(string accessToken, string updateId)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(updateId))
        {
            return (false, "Invalid parameters.");
        }

        try
        {
            string url = $"{BaseUrl}updates/{Uri.EscapeDataString(updateId)}/destroy.json";
            var formFields = new List<KeyValuePair<string, string>>
            {
                new("access_token", accessToken)
            };

            using var formContent = new FormUrlEncodedContent(formFields);
            using var response = await _httpClient.PostAsync(url, formContent);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                return (false, $"Error canceling update in Buffer: {body}");
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Exception canceling update: {ex.Message}");
        }
    }

    public async Task<(bool Success, string ErrorMessage)> ShareNowAsync(string accessToken, string updateId)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(updateId))
        {
            return (false, "Invalid parameters.");
        }

        try
        {
            string url = $"{BaseUrl}updates/{Uri.EscapeDataString(updateId)}/share.json";
            var formFields = new List<KeyValuePair<string, string>>
            {
                new("access_token", accessToken)
            };

            using var formContent = new FormUrlEncodedContent(formFields);
            using var response = await _httpClient.PostAsync(url, formContent);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync();
                return (false, $"Error sharing immediately: {body}");
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Exception sharing update: {ex.Message}");
        }
    }
}
