using System.Globalization;
using MpgsHostedCheckoutApplePayExample.Models;
using Microsoft.AspNetCore.Mvc;

namespace MpgsHostedCheckoutApplePayExample.Controllers;

[Route("")]
public sealed class HomeController : Controller
{
    private const string SuccessIndicatorCookieName = "mpgs_success_indicator";

    private readonly IConfiguration _configuration;
    private readonly MpgsGatewayClient _gatewayClient;

    public HomeController(IConfiguration configuration, MpgsGatewayClient gatewayClient)
    {
        _configuration = configuration;
        _gatewayClient = gatewayClient;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View(CreateIndexViewModel(new CheckoutFormModel(), errorMessage: null));
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(CheckoutFormModel form, CancellationToken cancellationToken)
    {
        var amountInput = (form.Amount ?? string.Empty).Trim();
        var currencyInput = (form.Currency ?? string.Empty).Trim().ToUpperInvariant();
        var descriptionInput = (form.Description ?? string.Empty).Trim();

        if (!decimal.TryParse(
                amountInput,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var amount) ||
            amount <= 0)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(
                "Index",
                CreateIndexViewModel(form, "Enter a valid amount using dot notation (for example 100.00)."));
        }

        var currency = string.IsNullOrWhiteSpace(currencyInput) ? "USD" : currencyInput;
        if (currency.Length != 3 || !currency.All(char.IsLetter))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(
                "Index",
                CreateIndexViewModel(form, "Currency must be a 3-letter ISO code (for example USD)."));
        }

        var options = GetOptions();
        if (!IsConfigured(options, out var configurationError))
        {
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return View("Index", CreateIndexViewModel(form, configurationError));
        }

        var orderDescription = string.IsNullOrWhiteSpace(descriptionInput)
            ? "MPGS Hosted Checkout Apple Pay demo"
            : descriptionInput;
        var orderId = $"ORD-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..30];
        var returnUrl = Url.Action(nameof(Return), "Home", null, Request.Scheme)
            ?? $"{Request.Scheme}://{Request.Host}/checkout/return";

        var request = new InitiateCheckoutRequest(orderId, currency, amount, orderDescription, returnUrl);

        InitiateCheckoutResponse initiateResponse;
        try
        {
            initiateResponse = await _gatewayClient.InitiateCheckoutAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            Response.StatusCode = StatusCodes.Status502BadGateway;
            return View(
                "Index",
                CreateIndexViewModel(form, $"Failed to create MPGS checkout session: {ex.Message}"));
        }

        if (!string.IsNullOrWhiteSpace(initiateResponse.SuccessIndicator))
        {
            Response.Cookies.Append(
                SuccessIndicatorCookieName,
                initiateResponse.SuccessIndicator,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    MaxAge = TimeSpan.FromMinutes(20)
                });
        }

        var redirectModel = new HostedCheckoutRedirectViewModel
        {
            CheckoutJsUrl = $"{options.GatewayBaseUrl.TrimEnd('/')}/static/checkout/checkout.min.js",
            SessionId = initiateResponse.SessionId
        };

        return View("RedirectToGateway", redirectModel);
    }

    [HttpGet("checkout/return")]
    public IActionResult Return([FromQuery] string? status, [FromQuery] string? resultIndicator)
    {
        var expectedIndicator = Request.Cookies[SuccessIndicatorCookieName];
        Response.Cookies.Delete(SuccessIndicatorCookieName);

        var safeResultIndicator = string.IsNullOrWhiteSpace(resultIndicator) ? "(missing)" : resultIndicator;
        var safeExpectedIndicator = string.IsNullOrWhiteSpace(expectedIndicator) ? "(missing)" : expectedIndicator;
        var indicatorVerified =
            !string.IsNullOrWhiteSpace(expectedIndicator) &&
            !string.IsNullOrWhiteSpace(resultIndicator) &&
            string.Equals(expectedIndicator, resultIndicator, StringComparison.Ordinal);

        var model = new CheckoutReturnViewModel
        {
            Status = string.IsNullOrWhiteSpace(status) ? "returned" : status,
            ResultIndicator = safeResultIndicator,
            ExpectedIndicator = safeExpectedIndicator,
            IndicatorVerified = indicatorVerified
        };

        return View(model);
    }

    private MpgsOptions GetOptions()
    {
        return _configuration.GetSection(MpgsOptions.SectionName).Get<MpgsOptions>() ?? new MpgsOptions();
    }

    private static bool IsConfigured(MpgsOptions options, out string error)
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

    private CheckoutIndexViewModel CreateIndexViewModel(CheckoutFormModel form, string? errorMessage)
    {
        var options = GetOptions();
        return new CheckoutIndexViewModel
        {
            Form = form,
            MerchantDisplayName = options.MerchantDisplayName,
            GatewayBaseUrl = options.GatewayBaseUrl,
            ErrorMessage = errorMessage
        };
    }
}
