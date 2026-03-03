using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MpgsHostedCheckoutApplePayExample;

public sealed record InitiateCheckoutRequest(
    string OrderId,
    string Currency,
    decimal Amount,
    string Description,
    string ReturnUrl,
    string Operation = "PURCHASE");

public sealed record InitiateCheckoutResponse(string SessionId, string SuccessIndicator);

public sealed class MpgsGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly MpgsOptions _options;

    public MpgsGatewayClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _options = configuration.GetSection(MpgsOptions.SectionName).Get<MpgsOptions>() ?? new MpgsOptions();
    }

    public async Task<InitiateCheckoutResponse> InitiateCheckoutAsync(
        InitiateCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var uri = BuildSessionUri();
        var payload = new
        {
            apiOperation = "INITIATE_CHECKOUT",
            interaction = new
            {
                operation = request.Operation,
                returnUrl = request.ReturnUrl,
                merchant = new
                {
                    name = _options.MerchantDisplayName
                }
            },
            order = new
            {
                id = request.OrderId,
                currency = request.Currency,
                amount = request.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                description = request.Description
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri);
        httpRequest.Headers.Authorization = BuildBasicAuthHeader();
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"INITIATE_CHECKOUT failed with HTTP {(int)response.StatusCode}: {responseBody}");
        }

        using var json = JsonDocument.Parse(responseBody);
        if (!json.RootElement.TryGetProperty("session", out var session) ||
            !session.TryGetProperty("id", out var sessionIdElement))
        {
            throw new InvalidOperationException(
                $"INITIATE_CHECKOUT succeeded but session.id was missing. Response: {responseBody}");
        }

        var sessionId = sessionIdElement.GetString();
        var successIndicator = json.RootElement.TryGetProperty("successIndicator", out var indicatorElement)
            ? indicatorElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new InvalidOperationException(
                $"INITIATE_CHECKOUT returned an empty session.id. Response: {responseBody}");
        }

        return new InitiateCheckoutResponse(
            sessionId!,
            successIndicator ?? string.Empty);
    }

    private Uri BuildSessionUri()
    {
        var baseUrl = (_options.GatewayBaseUrl ?? string.Empty).TrimEnd('/');
        var merchantId = _options.MerchantId;
        var apiVersion = string.IsNullOrWhiteSpace(_options.ApiVersion) ? "100" : _options.ApiVersion;

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(merchantId))
        {
            throw new InvalidOperationException(
                $"MPGS options are not configured. Check {MpgsOptions.SectionName} in appsettings.");
        }

        return new Uri($"{baseUrl}/api/rest/version/{apiVersion}/merchant/{merchantId}/session");
    }

    private AuthenticationHeaderValue BuildBasicAuthHeader()
    {
        var credentials = $"merchant.{_options.MerchantId}:{_options.ApiPassword}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        return new AuthenticationHeaderValue("Basic", encoded);
    }
}
