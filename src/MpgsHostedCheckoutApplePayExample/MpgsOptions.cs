namespace MpgsHostedCheckoutApplePayExample;

public sealed class MpgsOptions
{
    public const string SectionName = "Mpgs";

    public string GatewayBaseUrl { get; set; } = "https://na.gateway.mastercard.com";

    public string ApiVersion { get; set; } = "100";

    public string MerchantId { get; set; } = "REPLACE_WITH_YOUR_MERCHANT_ID";

    public string ApiPassword { get; set; } = "REPLACE_WITH_YOUR_API_PASSWORD";

    public string MerchantDisplayName { get; set; } = "Demo Store";
}
