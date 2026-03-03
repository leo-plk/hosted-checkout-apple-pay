namespace MpgsHostedCheckoutApplePayExample.Models;

public sealed class HostedCheckoutRedirectViewModel
{
    public required string CheckoutJsUrl { get; init; }

    public required string SessionId { get; init; }
}
