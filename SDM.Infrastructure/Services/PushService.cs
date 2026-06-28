using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SDM.Application.Interfaces;
using SDM.Infrastructure.Data;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.IO;

namespace SDM.Infrastructure.Services
{
    public class PushService : IPushService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<PushService> _logger;
        private readonly IConfiguration _config;
        private readonly HttpClient _http;

        public PushService(ApplicationDbContext db, ILogger<PushService> logger, IConfiguration config, IHttpClientFactory httpFactory)
        {
            _db = db;
            _logger = logger;
            _config = config;
            _http = httpFactory.CreateClient();
        }

        // Support both legacy ServerKey (Firebase:ServerKey) and HTTP v1 using a service account JSON
        public async Task<bool> SendToDeviceAsync(Guid deviceId, string title, string body, object data)
        {
            var token = _db.DevicePushTokens
                .Where(t => t.DeviceId == deviceId && t.IsActive)
                .OrderByDescending(t => t.CreatedOn)
                .Select(t => t.Token)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("No push token for device {DeviceId}", deviceId);
                return false;
            }

            // Prefer HTTP v1 with service account if configured
            var serviceAccountPath = _config["Firebase:ServiceAccountPath"];
            if (!string.IsNullOrEmpty(serviceAccountPath) && File.Exists(serviceAccountPath))
            {
                try
                {
                    var accessToken = await GetAccessTokenFromServiceAccountAsync(serviceAccountPath);
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        _logger.LogWarning("Failed to obtain access token from service account");
                        return false;
                    }

                    // Build v1 message
                    var projectId = await GetProjectIdFromServiceAccountAsync(serviceAccountPath);
                    if (string.IsNullOrEmpty(projectId))
                    {
                        _logger.LogWarning("Failed to determine project id from service account");
                        return false;
                    }

                    var fcmData = BuildFcmData(title, body, data);
                    var message = new
                    {
                        message = new
                        {
                            token = token,
                            data = fcmData
                        }
                    };

                    var json = JsonSerializer.Serialize(message);
                    var req = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send");
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                    req.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    var resp = await _http.SendAsync(req);
                    if (!resp.IsSuccessStatusCode)
                    {
                        var bodyText = await resp.Content.ReadAsStringAsync();
                        _logger.LogWarning("FCM HTTP v1 send failed: {Status} - {Body}", resp.StatusCode, bodyText);
                    }

                    return resp.IsSuccessStatusCode;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending FCM v1 message");
                    return false;
                }
            }

            // Fallback to legacy server key if provided
            var serverKey = _config["Firebase:ServerKey"];
            if (string.IsNullOrEmpty(serverKey))
            {
                _logger.LogWarning("Neither Firebase ServiceAccountPath nor ServerKey configured");
                return false;
            }

            var payloadLegacy = new
            {
                to = token,
                data = BuildFcmData(title, body, data)
            };

            var jsonLegacy = JsonSerializer.Serialize(payloadLegacy);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://fcm.googleapis.com/fcm/send");
            request.Headers.TryAddWithoutValidation("Authorization", $"key={serverKey}");
            request.Content = new StringContent(jsonLegacy);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("FCM legacy send failed: {Status} - {Body}", response.StatusCode, responseBody);
            }

            return response.IsSuccessStatusCode;
        }

        // Builds the flat string-to-string FCM data map.
        // Outer object properties (e.g. commandId) and inner Swagger payload properties
        // (e.g. packageName, url, minLength, blockedUrls) are all promoted to top-level keys
        // so the Android agent can read them directly from data["key"].
        private static Dictionary<string, string> BuildFcmData(string title, string body, object data)
        {
            var serialized = JsonSerializer.Serialize(data);
            var dict = new Dictionary<string, string>
            {
                { "title", title },
                { "body", body },
                { "payload", serialized }
            };

            try
            {
                using var doc = JsonDocument.Parse(serialized);

                // Promote outer properties (commandId, etc.)
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var val = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? string.Empty
                        : prop.Value.ToString();
                    dict.TryAdd(prop.Name, val);
                }

                // Also promote the inner Swagger payload's fields (packageName, url,
                // minLength, quality, blockedUrls, allowedUrls, etc.)
                if (doc.RootElement.TryGetProperty("payload", out var innerEl) &&
                    innerEl.ValueKind == JsonValueKind.String)
                {
                    var innerStr = innerEl.GetString();
                    if (!string.IsNullOrEmpty(innerStr))
                    {
                        using var innerDoc = JsonDocument.Parse(innerStr);
                        if (innerDoc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var innerProp in innerDoc.RootElement.EnumerateObject())
                            {
                                var val = innerProp.Value.ValueKind == JsonValueKind.String
                                    ? innerProp.Value.GetString() ?? string.Empty
                                    : innerProp.Value.ToString();
                                dict.TryAdd(innerProp.Name, val);
                            }
                        }
                    }
                }
            }
            catch { /* non-JSON payload: top-level keys already set */ }

            return dict;
        }

        // Load service account JSON and generate OAuth2 access token using JWT assertion (server-to-server)
        private async Task<string?> GetAccessTokenFromServiceAccountAsync(string serviceAccountPath)
        {
            var json = await File.ReadAllTextAsync(serviceAccountPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var clientEmail = root.GetProperty("client_email").GetString();
            var privateKey = root.GetProperty("private_key").GetString();
            var token_uri = root.GetProperty("token_uri").GetString();

            if (string.IsNullOrEmpty(clientEmail) || string.IsNullOrEmpty(privateKey) || string.IsNullOrEmpty(token_uri))
                return null;

            var now = DateTimeOffset.UtcNow;
            var header = new { alg = "RS256", typ = "JWT" };
            var payload = new Dictionary<string, object>
            {
                { "iss", clientEmail },
                { "scope", "https://www.googleapis.com/auth/firebase.messaging" },
                { "aud", token_uri },
                { "exp", now.ToUnixTimeSeconds() + 3600 },
                { "iat", now.ToUnixTimeSeconds() }
            };

            string Encode(object o) => Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(o));

            var headerEncoded = Encode(header);
            var payloadEncoded = Encode(payload);
            var unsignedJwt = headerEncoded + "." + payloadEncoded;

            // Sign using RSA private key in PEM format
            var rsa = RSA.Create();
            var pk = privateKey;
            // strip PEM headers
            var pkClean = pk.Replace("-----BEGIN PRIVATE KEY-----", string.Empty)
                               .Replace("-----END PRIVATE KEY-----", string.Empty)
                               .Replace("\n", string.Empty)
                               .Replace("\r", string.Empty);
            var pkBytes = Convert.FromBase64String(pkClean);
            rsa.ImportPkcs8PrivateKey(pkBytes, out _);

            var bytesToSign = Encoding.UTF8.GetBytes(unsignedJwt);
            var signature = rsa.SignData(bytesToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var signatureEncoded = Base64UrlEncode(signature);

            var jwtAssertion = unsignedJwt + "." + signatureEncoded;

            var form = new Dictionary<string, string>
            {
                { "grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer" },
                { "assertion", jwtAssertion }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, token_uri) { Content = new FormUrlEncodedContent(form) };
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            var respText = await resp.Content.ReadAsStringAsync();
            using var respDoc = JsonDocument.Parse(respText);
            if (respDoc.RootElement.TryGetProperty("access_token", out var at)) return at.GetString();
            return null;
        }

        private static string Base64UrlEncode(byte[] arg)
        {
            return Convert.ToBase64String(arg)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
        {
            return Base64UrlEncode(bytes.ToArray());
        }

        private static string Base64UrlEncode(string s)
        {
            return Base64UrlEncode(Encoding.UTF8.GetBytes(s));
        }

        private async Task<string?> GetProjectIdFromServiceAccountAsync(string serviceAccountPath)
        {
            var json = await File.ReadAllTextAsync(serviceAccountPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("project_id", out var pid)) return pid.GetString();
            return null;
        }
    }
}
