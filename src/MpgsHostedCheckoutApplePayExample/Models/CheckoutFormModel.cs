namespace MpgsHostedCheckoutApplePayExample.Models;

public sealed class CheckoutFormModel
{
    public string Amount { get; set; } = "100.00";

    public string Currency { get; set; } = "USD";

    public string Description { get; set; } = "Demo order from ASP.NET Core MVC";
}
