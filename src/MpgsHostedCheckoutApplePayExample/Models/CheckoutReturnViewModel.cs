namespace MpgsHostedCheckoutApplePayExample.Models;

public sealed class CheckoutReturnViewModel
{
    public required string Status { get; init; }

    public required string ResultIndicator { get; init; }

    public required string ExpectedIndicator { get; init; }

    public bool IndicatorVerified { get; init; }
}
