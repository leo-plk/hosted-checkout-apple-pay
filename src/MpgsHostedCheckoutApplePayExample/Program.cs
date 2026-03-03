using System.Globalization;
using System.Net;
using System.Text.Encodings.Web;
using MpgsHostedCheckoutApplePayExample;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient<MpgsGatewayClient>();

var app = builder.Build();

app.MapGet("/", (IConfiguration configuration) =>
{
    var options = GetOptions(configuration);
    return Results.Content(RenderIndexPage(options, null), "text/html");
});

app.MapPost("/checkout", async (
    HttpContext context,
    IConfiguration configuration,
    MpgsGatewayClient gatewayClient,
    CancellationToken cancellationToken) =>
{
    var options = GetOptions(configuration);
    var form = await context.Request.ReadFormAsync(cancellationToken);

    var amountInput = form["amount"].ToString().Trim();
    var currencyInput = form["currency"].ToString().Trim().ToUpperInvariant();
    var descriptionInput = form["description"].ToString().Trim();

    if (!decimal.TryParse(
            amountInput,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var amount) ||
        amount <= 0)
    {
        return Results.Content(
            RenderIndexPage(options, "Enter a valid amount using dot notation (for example 100.00)."),
            "text/html",
            statusCode: StatusCodes.Status400BadRequest);
    }

    var currency = string.IsNullOrWhiteSpace(currencyInput) ? "USD" : currencyInput;
    if (currency.Length != 3 || !currency.All(char.IsLetter))
    {
        return Results.Content(
            RenderIndexPage(options, "Currency must be a 3-letter ISO code (for example USD)."),
            "text/html",
            statusCode: StatusCodes.Status400BadRequest);
    }

    if (!IsConfigured(options, out var configurationError))
    {
        return Results.Content(
            RenderIndexPage(options, configurationError),
            "text/html",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    var orderDescription = string.IsNullOrWhiteSpace(descriptionInput)
        ? "MPGS Hosted Checkout Apple Pay demo"
        : descriptionInput;

    var orderId = $"ORD-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..30];
    var returnUrl = $"{context.Request.Scheme}://{context.Request.Host}/checkout/return";
    var request = new InitiateCheckoutRequest(orderId, currency, amount, orderDescription, returnUrl);

    InitiateCheckoutResponse initiateResponse;
    try
    {
        initiateResponse = await gatewayClient.InitiateCheckoutAsync(request, cancellationToken);
    }
    catch (Exception ex)
    {
        return Results.Content(
            RenderIndexPage(
                options,
                $"Failed to create MPGS checkout session: {WebUtility.HtmlEncode(ex.Message)}"),
            "text/html",
            statusCode: StatusCodes.Status502BadGateway);
    }

    if (!string.IsNullOrWhiteSpace(initiateResponse.SuccessIndicator))
    {
        context.Response.Cookies.Append(
            "mpgs_success_indicator",
            initiateResponse.SuccessIndicator,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(20)
            });
    }

    var checkoutJsUrl = $"{options.GatewayBaseUrl.TrimEnd('/')}/static/checkout/checkout.min.js";
    var html = RenderHostedCheckoutRedirectPage(checkoutJsUrl, initiateResponse.SessionId);
    return Results.Content(html, "text/html");
});

app.MapGet("/checkout/return", (HttpContext context) =>
{
    var resultIndicator = context.Request.Query["resultIndicator"].ToString();
    var status = context.Request.Query["status"].ToString();
    var expectedIndicator = context.Request.Cookies["mpgs_success_indicator"];

    context.Response.Cookies.Delete("mpgs_success_indicator");

    var indicatorVerified =
        !string.IsNullOrWhiteSpace(expectedIndicator) &&
        !string.IsNullOrWhiteSpace(resultIndicator) &&
        string.Equals(expectedIndicator, resultIndicator, StringComparison.Ordinal);

    var html = RenderReturnPage(status, resultIndicator, expectedIndicator, indicatorVerified);
    return Results.Content(html, "text/html");
});

app.Run();

static MpgsOptions GetOptions(IConfiguration configuration) =>
    configuration.GetSection(MpgsOptions.SectionName).Get<MpgsOptions>() ?? new MpgsOptions();

static bool IsConfigured(MpgsOptions options, out string error)
{
    if (string.IsNullOrWhiteSpace(options.GatewayBaseUrl) ||
        string.IsNullOrWhiteSpace(options.MerchantId) ||
        string.IsNullOrWhiteSpace(options.ApiPassword))
    {
        error = "Missing MPGS settings. Populate the Mpgs section in appsettings.json first.";
        return false;
    }

    if (options.MerchantId.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase) ||
        options.ApiPassword.Contains("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
    {
        error = "Replace placeholder MPGS credentials in appsettings.json before running checkout.";
        return false;
    }

    error = string.Empty;
    return true;
}

static string RenderIndexPage(MpgsOptions options, string? errorMessage)
{
    var merchantDisplay = WebUtility.HtmlEncode(options.MerchantDisplayName);
    var gatewayDisplay = WebUtility.HtmlEncode(options.GatewayBaseUrl);
    var maybeError = string.IsNullOrWhiteSpace(errorMessage)
        ? string.Empty
        : $"<div style=\"padding:12px;border-radius:8px;background:#fee2e2;color:#7f1d1d;margin-bottom:16px;\"><strong>Error:</strong> {errorMessage}</div>";

    return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>MPGS Hosted Checkout Apple Pay (.NET)</title>
</head>
<body style="font-family:Arial,sans-serif;max-width:900px;margin:32px auto;line-height:1.45;padding:0 16px;">
  <h1>MPGS Hosted Checkout + Apple Pay (.NET Core example)</h1>
  <p>This demo calls <code>INITIATE_CHECKOUT</code> on your server, then redirects the browser to the hosted payment page.</p>
  <ul>
    <li>Merchant display name: <strong>{{merchantDisplay}}</strong></li>
    <li>Gateway URL: <strong>{{gatewayDisplay}}</strong></li>
  </ul>
  <p><strong>Apple Pay note:</strong> In Hosted Checkout, Apple Pay availability is controlled by your MPGS profile and device/browser capability. It appears on full-page redirect when enabled by your acquirer/bank.</p>
  {{maybeError}}

  <form method="post" action="/checkout" style="display:grid;gap:12px;max-width:480px;">
    <label>
      Amount
      <input name="amount" value="100.00" required />
    </label>
    <label>
      Currency (ISO alpha-3)
      <input name="currency" value="USD" required maxlength="3" />
    </label>
    <label>
      Description
      <input name="description" value="Demo order from ASP.NET Core" />
    </label>
    <button type="submit" style="padding:10px 14px;">Proceed to Hosted Checkout</button>
  </form>

  <p style="margin-top:20px;color:#374151;">
    This sample stores MPGS <code>successIndicator</code> in a short-lived cookie and compares it against the
    <code>resultIndicator</code> query parameter on return.
  </p>
</body>
</html>
""";
}

static string RenderHostedCheckoutRedirectPage(string checkoutJsUrl, string sessionId)
{
    var scriptUrl = WebUtility.HtmlEncode(checkoutJsUrl);
    var sessionIdJs = JavaScriptEncoder.Default.Encode(sessionId);

    return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Redirecting to MPGS Hosted Checkout</title>
  <script>
    function errorCallback(error) {
      console.error("MPGS Checkout error", error);
      document.getElementById("status").textContent = "Could not open Hosted Checkout. See browser console.";
    }

    function cancelCallback() {
      window.location.href = "/checkout/return?status=cancelled";
    }

    Checkout.configure({
      session: {
        id: "{{sessionIdJs}}"
      }
    });
  </script>
  <script src="{{scriptUrl}}" data-error="errorCallback" data-cancel="cancelCallback"></script>
</head>
<body style="font-family:Arial,sans-serif;max-width:900px;margin:32px auto;padding:0 16px;">
  <h1>Redirecting to Hosted Checkout...</h1>
  <p id="status">If redirect does not start automatically, click the button below.</p>
  <button type="button" onclick="Checkout.showPaymentPage();" style="padding:10px 14px;">
    Open Hosted Checkout
  </button>
  <script>
    Checkout.showPaymentPage();
  </script>
</body>
</html>
""";
}

static string RenderReturnPage(
    string status,
    string resultIndicator,
    string? expectedIndicator,
    bool indicatorVerified)
{
    var safeStatus = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(status) ? "returned" : status);
    var safeResultIndicator = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(resultIndicator) ? "(missing)" : resultIndicator);
    var safeExpectedIndicator = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(expectedIndicator) ? "(missing)" : expectedIndicator);
    var verificationMessage = indicatorVerified
        ? "resultIndicator matches successIndicator (session appears consistent)."
        : "resultIndicator could not be verified against successIndicator.";
    var verificationColor = indicatorVerified ? "#065f46" : "#7f1d1d";
    var verificationBackground = indicatorVerified ? "#d1fae5" : "#fee2e2";

    return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>MPGS Return</title>
</head>
<body style="font-family:Arial,sans-serif;max-width:900px;margin:32px auto;line-height:1.45;padding:0 16px;">
  <h1>Hosted Checkout Return</h1>
  <p>Return status: <strong>{{safeStatus}}</strong></p>

  <div style="padding:12px;border-radius:8px;background:{{verificationBackground}};color:{{verificationColor}};margin-bottom:16px;">
    {{verificationMessage}}
  </div>

  <ul>
    <li><code>resultIndicator</code>: <code>{{safeResultIndicator}}</code></li>
    <li>Saved <code>successIndicator</code>: <code>{{safeExpectedIndicator}}</code></li>
  </ul>

  <p>Next step for production: query the order/transaction via backend API and fulfill only after your server confirms the final payment state.</p>
  <p><a href="/">Start a new checkout</a></p>
</body>
</html>
""";
}
