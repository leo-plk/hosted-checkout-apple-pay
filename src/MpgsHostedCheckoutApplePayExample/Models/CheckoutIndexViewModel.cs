namespace MpgsHostedCheckoutApplePayExample.Models;

public sealed class CheckoutIndexViewModel
{
    public required CheckoutFormModel Form { get; init; }

    public required string MerchantDisplayName { get; init; }

    public required string GatewayBaseUrl { get; init; }

    public string? ErrorMessage { get; init; }
}
