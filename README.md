# MPGS Hosted Checkout + Apple Pay (.NET 8 MVC Example)

This repository contains a minimal **ASP.NET Core MVC (.NET 8)** sample for:

1. Creating an MPGS Hosted Checkout session via `INITIATE_CHECKOUT`
2. Redirecting the payer with `Checkout.showPaymentPage()`
3. Handling the return URL and comparing `resultIndicator` with `successIndicator`

> Apple Pay in MPGS Hosted Checkout is enabled on the gateway/acquirer side and appears on compatible Apple devices/browsers in **full-page redirect** mode when your profile is enabled.

## Project location

`src/MpgsHostedCheckoutApplePayExample`

## MVC structure

- `Controllers/HomeController.cs`
  - `GET /`
  - `POST /checkout`
  - `GET /checkout/return`
- `Views/Home/Index.cshtml`
- `Views/Home/RedirectToGateway.cshtml`
- `Views/Home/Return.cshtml`
- `MpgsGatewayClient.cs` for MPGS API calls

## MPGS flow implemented

- `POST /checkout` (server-side, HomeController):
  - Calls:
    - `POST {gatewayBaseUrl}/api/rest/version/{apiVersion}/merchant/{merchantId}/session`
  - Sends payload:
    - `apiOperation: "INITIATE_CHECKOUT"`
    - `interaction.operation: "PURCHASE"`
    - `interaction.returnUrl`
    - `interaction.merchant.name`
    - `order.id`, `order.amount`, `order.currency`, `order.description`
  - Stores `successIndicator` in a short-lived cookie for demo verification.
- Razor redirect view:
  - Loads `https://<gateway>/static/checkout/checkout.min.js`
  - Calls `Checkout.configure({ session: { id } })`
  - Calls `Checkout.showPaymentPage()`
- `GET /checkout/return`:
  - Reads `resultIndicator`
  - Compares against stored `successIndicator`

## Configure credentials

Edit `src/MpgsHostedCheckoutApplePayExample/appsettings.json`:

```json
"Mpgs": {
  "GatewayBaseUrl": "https://na.gateway.mastercard.com",
  "ApiVersion": "100",
  "MerchantId": "REPLACE_WITH_YOUR_MERCHANT_ID",
  "ApiPassword": "REPLACE_WITH_YOUR_API_PASSWORD",
  "MerchantDisplayName": "Demo Store"
}
```

## Run

```bash
cd src/MpgsHostedCheckoutApplePayExample
dotnet run
```

Then open the URL shown in console.

## Important production notes

- Do not keep gateway credentials in `appsettings.json` for production; use a secret manager/environment variables.
- Validate final payment state from your backend (for example, retrieve order/transaction status) before fulfillment.
- Ensure Apple Pay is enabled on your MPGS merchant profile by your acquirer/bank.
