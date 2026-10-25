/*
 * RESOURCES
 * https://www.w3.org/TR/webdriver/
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Calls the W3C and SAP endpoints of a SAP driver server over HTTP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Paths are relative to the driver base address, such as <c>http://localhost:5556/wd/hub/</c>. Every response
    /// envelope is unwrapped to its <c>value</c> member. W3C error responses become <see cref="SapDriverException"/>
    /// with the driver's error code, SAP error kind, and message, so the inspector reports driver failures exactly as
    /// it reports local ones. An unreachable driver becomes an <c>unknown error</c> that names the address.
    /// </para>
    /// <para>
    /// The client is safe for concurrent calls. It disposes its <see cref="HttpClient"/> only when it created it.
    /// </para>
    /// </remarks>
    public sealed class SapDriverClient : IDisposable
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the default driver address, the port the SAP driver server listens on.
        /// </summary>
        public const string DefaultDriverUrl = "http://localhost:5556/wd/hub";

        // Names the JSON media type used for every request body.
        private const string JsonMediaType = "application/json";

        // Serializes request bodies and reads responses with the camel-case names the driver uses.
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        // Bounds every call, so an unresponsive driver cannot hang the inspector.
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(60);
        #endregion

        #region *** Fields       ***
        // Sends the requests; its base address ends with a slash so relative paths append to it.
        private readonly HttpClient _client;

        // Records whether this client created the HttpClient and therefore disposes it.
        private readonly bool _isOwner;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="SapDriverClient"/> class for a driver address.
        /// </summary>
        /// <param name="driverUrl">The driver address, such as <c>http://localhost:5556/wd/hub</c>.</param>
        /// <exception cref="ArgumentException">The address is not an absolute HTTP or HTTPS URL.</exception>
        public SapDriverClient(string driverUrl)
            : this(client: new HttpClient { BaseAddress = ConvertToBaseAddress(driverUrl), Timeout = RequestTimeout }, isOwner: true)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SapDriverClient"/> class over an existing HTTP client.
        /// </summary>
        /// <param name="client">The HTTP client; its base address is the driver base address.</param>
        /// <param name="isOwner">True when this instance disposes the client.</param>
        public SapDriverClient(HttpClient client, bool isOwner)
        {
            // Assert a client with a base address; every path is relative to it.
            AssertArguments(client);

            // Keep the client and its ownership.
            _client = client;
            _isOwner = isOwner;

            return;

            // Requires the HTTP client and its base address before the driver client relies on relative paths.
            // The helper reads its parameter only and reports both failures against the client argument.
            static void AssertArguments(HttpClient client)
            {
                // Require the client itself.
                ArgumentNullException.ThrowIfNull(argument: client);

                // Require the base address, because every request path is relative to it.
                if (client.BaseAddress is null)
                {
                    throw new ArgumentException("The HTTP client needs a base address.", nameof(client));
                }
            }
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the driver base address.
        /// </summary>
        public Uri BaseAddress => _client.BaseAddress;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Converts a user-entered driver address to a base address that relative paths append to.
        /// </summary>
        /// <param name="driverUrl">The driver address.</param>
        /// <returns>The absolute address with a trailing slash.</returns>
        /// <exception cref="ArgumentException">The address is not an absolute HTTP or HTTPS URL.</exception>
        public static Uri ConvertToBaseAddress(string driverUrl)
        {
            // Accept only absolute HTTP addresses; anything else cannot reach a driver.
            var isValid = Uri.TryCreate(driverUrl?.Trim(), UriKind.Absolute, out var address)
                && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps);
            if (!isValid)
            {
                throw new ArgumentException($"'{driverUrl}' is not an HTTP address, such as {DefaultDriverUrl}.", nameof(driverUrl));
            }

            // Add the trailing slash so relative paths keep the /wd/hub prefix.
            var text = address.AbsoluteUri;
            return new Uri(text.EndsWith('/') ? text : $"{text}/");
        }

        /// <summary>
        /// Clears a text control.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <param name="elementId">The element reference.</param>
        /// <returns>A task that completes when the driver cleared the control.</returns>
        public Task ClearAsync(string sessionId, string elementId)
        {
            return SendAsync(HttpMethod.Post, $"session/{sessionId}/element/{elementId}/clear", body: new { });
        }

        /// <summary>
        /// Performs the driver's click equivalent on a control.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <param name="elementId">The element reference.</param>
        /// <returns>A task that completes when the driver performed the click.</returns>
        public Task ClickAsync(string sessionId, string elementId)
        {
            return SendAsync(HttpMethod.Post, $"session/{sessionId}/element/{elementId}/click", body: new { });
        }

        /// <summary>
        /// Deletes a WebDriver session; the driver only detaches from SAP GUI.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <returns>A task that completes when the session is deleted.</returns>
        public Task DeleteSessionAsync(string sessionId)
        {
            return SendAsync(HttpMethod.Delete, $"session/{sessionId}", body: null);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Dispose the HTTP client only when this instance created it.
            if (_isOwner)
            {
                _client.Dispose();
            }
        }

        /// <summary>
        /// Finds the first control that matches a locator.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <param name="locator">The locator.</param>
        /// <returns>The element reference, or null when nothing matches.</returns>
        public async Task<string> FindElementAsync(string sessionId, LocationStrategyModel locator)
        {
            // Treat "no such element" as a normal miss.
            var (isFound, value) = await TrySendAsync(HttpMethod.Post, $"session/{sessionId}/element", body: locator).ConfigureAwait(false);

            return isFound ? GetElementReference(value) : null;
        }

        /// <summary>
        /// Finds the control under a screen point.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <param name="x">The horizontal screen coordinate.</param>
        /// <param name="y">The vertical screen coordinate.</param>
        /// <returns>The control, or null when no control of the session is at the point.</returns>
        public async Task<SapElementAtModel> FindElementAtAsync(string sessionId, int x, int y)
        {
            // Treat "no such element" as a point outside the session's controls.
            var body = new SapPointModel { X = x, Y = y };
            var (isFound, value) = await TrySendAsync(HttpMethod.Post, $"session/{sessionId}/sap/element/at", body).ConfigureAwait(false);

            return isFound ? value.Deserialize<SapElementAtModel>(JsonOptions) : null;
        }

        /// <summary>
        /// Finds every control that matches a locator.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <param name="locator">The locator.</param>
        /// <returns>The element references in document order.</returns>
        public async Task<IList<string>> FindElementsAsync(string sessionId, LocationStrategyModel locator)
        {
            // Read the reference out of each W3C element object.
            var value = await SendAsync(HttpMethod.Post, $"session/{sessionId}/elements", body: locator).ConfigureAwait(false);

            return value.ValueKind == JsonValueKind.Array
                ? [.. value.EnumerateArray().Select(GetElementReference)]
                : [];
        }

        /// <summary>
        /// Reads a whitelisted attribute of a control.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <param name="elementId">The element reference.</param>
        /// <param name="name">The attribute name, such as <c>tooltip</c>.</param>
        /// <returns>The value; empty when the attribute is unknown or not exposed.</returns>
        public async Task<string> GetAttributeAsync(string sessionId, string elementId, string name)
        {
            // Read the attribute; the driver returns an empty string for unknown or unexposed names.
            var value = await SendAsync(HttpMethod.Get, $"session/{sessionId}/element/{elementId}/attribute/{Uri.EscapeDataString(name)}", body: null).ConfigureAwait(false);

            return value.ValueKind == JsonValueKind.String ? value.GetString() : string.Empty;
        }

        /// <summary>
        /// Reads the screen rectangle of a control.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <param name="elementId">The element reference.</param>
        /// <returns>The W3C rectangle.</returns>
        public async Task<ElementRectModel> GetRectAsync(string sessionId, string elementId)
        {
            // Read the W3C rectangle; a missing body reads as an empty rectangle.
            var value = await SendAsync(HttpMethod.Get, $"session/{sessionId}/element/{elementId}/rect", body: null).ConfigureAwait(false);

            return value.Deserialize<ElementRectModel>(JsonOptions) ?? new ElementRectModel();
        }

        /// <summary>
        /// Captures the session's current SAP window.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <returns>The PNG bytes.</returns>
        public async Task<byte[]> GetScreenshotAsync(string sessionId)
        {
            // Decode the base64 PNG the W3C screenshot command returns.
            var value = await SendAsync(HttpMethod.Get, $"session/{sessionId}/screenshot", body: null).ConfigureAwait(false);

            return Convert.FromBase64String(value.GetString() ?? string.Empty);
        }

        /// <summary>
        /// Reads the page source: the session's controls as XML.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <returns>The XML document text.</returns>
        public async Task<string> GetSourceAsync(string sessionId)
        {
            // Read the XML, which the driver returns as a JSON string.
            var value = await SendAsync(HttpMethod.Get, $"session/{sessionId}/source", body: null).ConfigureAwait(false);

            return value.GetString() ?? string.Empty;
        }

        /// <summary>
        /// Reads the server status, including the open session identifiers and SAP GUI reachability.
        /// </summary>
        /// <returns>The status.</returns>
        public async Task<SessionStatusModel> GetStatusAsync()
        {
            // Read the status, which also reports SAP GUI reachability on the driver machine.
            var value = await SendAsync(HttpMethod.Get, "status", body: null).ConfigureAwait(false);

            return value.Deserialize<SessionStatusModel>(JsonOptions) ?? new SessionStatusModel();
        }

        /// <summary>
        /// Reads the status bar of the session's main window.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <returns>The status bar message.</returns>
        public async Task<SapStatusBarModel> GetStatusBarAsync(string sessionId)
        {
            // Read the main window's status bar message.
            var value = await SendAsync(HttpMethod.Get, $"session/{sessionId}/sap/statusbar", body: null).ConfigureAwait(false);

            return value.Deserialize<SapStatusBarModel>(JsonOptions) ?? new SapStatusBarModel();
        }

        /// <summary>
        /// Reads the handle of the session's current window.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <returns>The hexadecimal window handle; empty when the window has none.</returns>
        public async Task<string> GetWindowHandleAsync(string sessionId)
        {
            // The driver returns the UIA shape: a JSON string that itself holds {"handle": "...", "name": "..."}.
            var value = await SendAsync(HttpMethod.Get, $"session/{sessionId}/window", body: null).ConfigureAwait(false);
            var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
            var handle = JsonNode.Parse(text ?? "{}")?["handle"];

            return handle?.GetValue<string>() ?? string.Empty;
        }

        /// <summary>
        /// Maximizes the session's current window.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <returns>A task that completes when the window is maximized.</returns>
        public Task MaximizeWindowAsync(string sessionId)
        {
            return SendAsync(HttpMethod.Post, $"session/{sessionId}/window/maximize", body: new { });
        }

        /// <summary>
        /// Creates a WebDriver session that attaches to a SAP GUI session.
        /// </summary>
        /// <param name="capabilities">The capabilities placed in <c>alwaysMatch</c>, such as a <c>sap:options</c> object.</param>
        /// <returns>The new session identifier.</returns>
        public async Task<string> NewSessionAsync(JsonObject capabilities)
        {
            // Wrap the capabilities in the W3C new session shape.
            var body = new JsonObject
            {
                ["capabilities"] = new JsonObject { ["alwaysMatch"] = capabilities?.DeepClone() ?? new JsonObject() }
            };
            var value = await SendAsync(HttpMethod.Post, "session", body).ConfigureAwait(false);

            return value.GetProperty("sessionId").GetString();
        }

        /// <summary>
        /// Sends a SAP virtual key to a window of the session.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <param name="virtualKey">The key and the target window name.</param>
        /// <returns>A task that completes when the key was sent.</returns>
        public Task SendVirtualKeyAsync(string sessionId, SapVirtualKeyModel virtualKey)
        {
            return SendAsync(HttpMethod.Post, $"session/{sessionId}/sap/vkey", virtualKey);
        }

        /// <summary>
        /// Replaces the text of a control, or selects a combo box entry by key.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <param name="elementId">The element reference.</param>
        /// <param name="text">The text.</param>
        /// <returns>A task that completes when the driver wrote the text.</returns>
        public Task SetValueAsync(string sessionId, string elementId, string text)
        {
            return SendAsync(HttpMethod.Post, $"session/{sessionId}/element/{elementId}/value", body: new TextInputModel { Text = text ?? string.Empty });
        }

        /// <summary>
        /// Makes a window the session's current window.
        /// </summary>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <param name="window">A window handle, a SAP window name such as <c>wnd[1]</c>, or a title.</param>
        /// <returns>A task that completes when the window is current.</returns>
        public Task SwitchToWindowAsync(string sessionId, string window)
        {
            return SendAsync(HttpMethod.Post, $"session/{sessionId}/window", body: new WindowHandleRequestModel { Handle = window });
        }

        // Reads the reference out of a W3C element object.
        private static string GetElementReference(JsonElement element)
        {
            // Return null for objects without the W3C element key, such as an empty find result.
            return element.TryGetProperty(ElementProperties.ElementReference, out var reference) ? reference.GetString() : null;
        }

        // Converts a W3C error envelope to the exception the inspector reports, keeping the code, SAP kind, and message.
        private static SapDriverException NewDriverException(int statusCode, JsonElement value)
        {
            // Read the members defensively; a proxy or an older driver can return a different shape.
            var isObject = value.ValueKind == JsonValueKind.Object;
            var error = isObject && value.TryGetProperty("error", out var errorValue) ? errorValue.GetString() : null;
            var message = isObject && value.TryGetProperty("message", out var messageValue) ? messageValue.GetString() : null;
            var errorKind = isObject
                && value.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty(SapErrorKinds.DataKey, out var kind) ? kind.GetString() : null;

            return new SapDriverException(
                error ?? WebDriverErrorCodes.UnknownError,
                errorKind,
                statusCode,
                message ?? $"The driver answered HTTP {statusCode}.");
        }

        // Sends one request and returns the value member; every error response becomes a SapDriverException.
        private async Task<JsonElement> SendAsync(HttpMethod method, string path, object body)
        {
            // Send and read; failures are thrown with the driver's own error details.
            var (statusCode, value) = await SendRequestAsync(method, path, body).ConfigureAwait(false);
            if (statusCode >= 400)
            {
                throw NewDriverException(statusCode, value);
            }

            return value;
        }

        // Sends one request and returns the status code and value member; an unreachable driver becomes a SapDriverException.
        private async Task<(int StatusCode, JsonElement Value)> SendRequestAsync(HttpMethod method, string path, object body)
        {
            // Serialize the body with the driver's camel-case names; requests without a body send none.
            using var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                var json = body is JsonNode node ? node.ToJsonString() : JsonSerializer.Serialize(body, body.GetType(), JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, JsonMediaType);
            }

            try
            {
                // Read the envelope; an empty body (rare, from proxies) is treated as a null value.
                using var response = await _client.SendAsync(request).ConfigureAwait(false);
                var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var value = default(JsonElement);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    using var document = JsonDocument.Parse(text);
                    value = document.RootElement.TryGetProperty("value", out var member) ? member.Clone() : document.RootElement.Clone();
                }

                return ((int)response.StatusCode, value);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                // Name the address, so a wrong port or a stopped driver is obvious.
                var message = $"The driver at {BaseAddress} did not answer: {exception.GetBaseException().Message}";
                throw new SapDriverException(WebDriverErrorCodes.UnknownError, errorKind: null, statusCode: 503, message, data: null, innerException: exception);
            }
        }

        // Sends one request that can legitimately find nothing; "no such element" returns false instead of throwing.
        private async Task<(bool IsFound, JsonElement Value)> TrySendAsync(HttpMethod method, string path, object body)
        {
            // Throw every error except the miss the caller expects.
            var (statusCode, value) = await SendRequestAsync(method, path, body).ConfigureAwait(false);
            if (statusCode < 400)
            {
                return (true, value);
            }

            var exception = NewDriverException(statusCode, value);
            return exception.Error == WebDriverErrorCodes.NoSuchElement ? (false, default) : throw exception;
        }
        #endregion
    }
}
