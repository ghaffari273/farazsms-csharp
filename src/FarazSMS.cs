// FarazSMS · IranPayamak (فراز اس ام اس · ایران پیامک) — https://farazsms.com · https://iranpayamak.com

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FarazSMS
{
    /// <summary>
    /// Exception thrown when the FarazSMS / IranPayamak API returns an error envelope
    /// (<c>status == "error"</c>) or a non-success HTTP status code.
    /// </summary>
    public class FarazException : Exception
    {
        /// <summary>The HTTP status code returned by the API (0 if unknown).</summary>
        public int StatusCode { get; }

        public FarazException(string message, int statusCode)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }

    /// <summary>
    /// Official .NET client for the FarazSMS / IranPayamak web services.
    /// </summary>
    public class FarazSMS : IDisposable
    {
        /// <summary>Default sender line number.</summary>
        public const string DefaultLine = "90008361";

        private readonly HttpClient _http;
        private readonly bool _ownsHttp;

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Creates a new client.
        /// </summary>
        /// <param name="apiKey">Your FarazSMS / IranPayamak API key. Sent as the <c>Api-Key</c> header.</param>
        /// <param name="baseUrl">API base URL. Defaults to https://api.iranpayamak.com</param>
        /// <param name="httpClient">Optional custom <see cref="HttpClient"/>. If null, one is created and owned by this instance.</param>
        public FarazSMS(string apiKey, string baseUrl = "https://api.iranpayamak.com", HttpClient httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("apiKey must not be empty.", nameof(apiKey));

            if (httpClient != null)
            {
                _http = httpClient;
                _ownsHttp = false;
            }
            else
            {
                _http = new HttpClient();
                _ownsHttp = true;
            }

            _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            _http.DefaultRequestHeaders.Remove("Api-Key");
            _http.DefaultRequestHeaders.Add("Api-Key", apiKey);
        }

        // -------------------------------------------------------------------
        // Low-level request
        // -------------------------------------------------------------------

        /// <summary>
        /// Low-level request that reaches any endpoint. Throws <see cref="FarazException"/>
        /// when the response is a non-success HTTP status or carries an error envelope.
        /// </summary>
        /// <param name="method">HTTP method, e.g. "GET", "POST".</param>
        /// <param name="path">Path relative to the base URL, e.g. "/ws/v1/account/balance".</param>
        /// <param name="body">Optional request body, serialized as JSON.</param>
        /// <param name="query">Optional raw query string (with or without leading '?').</param>
        public async Task<JsonElement> Request(string method, string path, object body = null, string query = "")
        {
            var url = path.TrimStart('/');
            if (!string.IsNullOrEmpty(query))
                url += (url.Contains("?") ? "&" : "?") + query.TrimStart('?');

            using (var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), url))
            {
                if (body != null)
                {
                    var json = JsonSerializer.Serialize(body, SerializerOptions);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                using (var response = await _http.SendAsync(request).ConfigureAwait(false))
                {
                    var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var httpCode = (int)response.StatusCode;

                    JsonDocument doc;
                    try
                    {
                        doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                    }
                    catch (JsonException)
                    {
                        if (!response.IsSuccessStatusCode)
                            throw new FarazException(text, httpCode);
                        throw new FarazException("Invalid JSON response from server.", httpCode);
                    }

                    using (doc)
                    {
                        var root = doc.RootElement;

                        // Check the response envelope.
                        if (root.ValueKind == JsonValueKind.Object &&
                            root.TryGetProperty("status", out var statusProp) &&
                            statusProp.ValueKind == JsonValueKind.String &&
                            string.Equals(statusProp.GetString(), "error", StringComparison.OrdinalIgnoreCase))
                        {
                            var message = root.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String
                                ? msgProp.GetString()
                                : "FarazSMS API error.";
                            throw new FarazException(message, httpCode);
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            var message = root.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String
                                ? msgProp.GetString()
                                : "HTTP " + httpCode;
                            throw new FarazException(message, httpCode);
                        }

                        // Clone so the element survives disposal of the JsonDocument.
                        return root.Clone();
                    }
                }
            }
        }

        // -------------------------------------------------------------------
        // Account
        // -------------------------------------------------------------------

        /// <summary>GET /ws/v1/account/balance</summary>
        public Task<JsonElement> Balance() => Request("GET", "/ws/v1/account/balance");

        /// <summary>GET /ws/v1/account/profile</summary>
        public Task<JsonElement> Profile() => Request("GET", "/ws/v1/account/profile");

        /// <summary>GET /ws/v1/lines/accessible</summary>
        public Task<JsonElement> Lines() => Request("GET", "/ws/v1/lines/accessible");

        // -------------------------------------------------------------------
        // Send
        // -------------------------------------------------------------------

        /// <summary>POST /ws/v1/sms/pattern</summary>
        public Task<JsonElement> SendPattern(string code, string recipient, object attributes, string line = DefaultLine)
        {
            var body = new Dictionary<string, object>
            {
                ["code"] = code,
                ["recipient"] = recipient,
                ["attributes"] = attributes,
                ["line_number"] = line,
                ["number_format"] = "english"
            };
            return Request("POST", "/ws/v1/sms/pattern", body);
        }

        /// <summary>POST /ws/v1/sms/simple</summary>
        public Task<JsonElement> SendSimple(string text, string[] recipients, string line = DefaultLine)
        {
            var body = new Dictionary<string, object>
            {
                ["text"] = text,
                ["recipients"] = recipients,
                ["line_number"] = line,
                ["number_format"] = "english"
            };
            return Request("POST", "/ws/v1/sms/simple", body);
        }

        /// <summary>POST /ws/v1/sms/keywords</summary>
        public Task<JsonElement> SendVariable(string text, object recipients, string line = DefaultLine)
        {
            var body = new Dictionary<string, object>
            {
                ["text"] = text,
                ["recipients"] = recipients,
                ["line_number"] = line,
                ["number_format"] = "english"
            };
            return Request("POST", "/ws/v1/sms/keywords", body);
        }

        // -------------------------------------------------------------------
        // Patterns
        // -------------------------------------------------------------------

        /// <summary>POST /ws/v1/patterns</summary>
        public Task<JsonElement> CreatePattern(object payload) => Request("POST", "/ws/v1/patterns", payload);

        /// <summary>GET /ws/v1/patterns</summary>
        public Task<JsonElement> Patterns(string query = "") => Request("GET", "/ws/v1/patterns", null, query);

        // -------------------------------------------------------------------
        // Reports
        // -------------------------------------------------------------------

        /// <summary>GET /ws/v1/inbox?page=&amp;limit=</summary>
        public Task<JsonElement> Inbox(int page = 1, int limit = 20)
            => Request("GET", "/ws/v1/inbox", null, "page=" + page + "&limit=" + limit);

        /// <summary>GET /ws/v1/send_request</summary>
        public Task<JsonElement> SendRequests(string query = "") => Request("GET", "/ws/v1/send_request", null, query);

        /// <summary>GET /ws/v1/send_request/{id}/items</summary>
        public Task<JsonElement> SendRequestItems(string id, string query = "")
            => Request("GET", "/ws/v1/send_request/" + Uri.EscapeDataString(id) + "/items", null, query);

        // -------------------------------------------------------------------
        // Phonebook
        // -------------------------------------------------------------------

        /// <summary>GET /ws/v1/phone_book</summary>
        public Task<JsonElement> Phonebooks() => Request("GET", "/ws/v1/phone_book");

        /// <summary>POST /ws/v1/phone_book_data</summary>
        public Task<JsonElement> AddContact(object payload) => Request("POST", "/ws/v1/phone_book_data", payload);

        // -------------------------------------------------------------------
        // Reference
        // -------------------------------------------------------------------

        /// <summary>GET /provinces</summary>
        public Task<JsonElement> Provinces() => Request("GET", "/provinces");

        /// <summary>GET /ws/v1/number_bank</summary>
        public Task<JsonElement> NumberBanks() => Request("GET", "/ws/v1/number_bank");

        // -------------------------------------------------------------------

        public void Dispose()
        {
            if (_ownsHttp)
                _http?.Dispose();
        }
    }
}
