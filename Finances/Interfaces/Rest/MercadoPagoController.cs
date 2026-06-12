using System.Text.Json;
using BuildingFex.Api.Finances.Application.Internal.MercadoPago;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuildingFex.Api.Finances.Interfaces.Rest;

[ApiController]
[Route("api/v1/payments")]
[Tags("Mercado Pago")]
public class MercadoPagoController(
    IMercadoPagoService mercadoPagoService,
    ILogger<MercadoPagoController> logger) : ControllerBase
{
    // ─────────────────────────────────────────────────────────────
    // POST /api/v1/payments/preference
    // Creates a Checkout Pro preference for a given receipt.
    // ─────────────────────────────────────────────────────────────
    [HttpPost("preference")]
    [Authorize]
    public async Task<IActionResult> CreatePreference(
        [FromBody] JsonElement body, CancellationToken ct)
    {
        if (!body.TryGetProperty("receiptId", out var receiptIdProp))
            return BadRequest(new { code = "RECEIPT_ID_REQUIRED", message = "receiptId is required." });

        var receiptId = receiptIdProp.ToString();

        var ownerAdminId = body.TryGetProperty("ownerAdminId", out var ownerProp)
            ? ownerProp.GetString() ?? string.Empty
            : string.Empty;

        try
        {
            var result = await mercadoPagoService.CreatePreferenceAsync(
                new CreatePreferenceRequest(receiptId, ownerAdminId), ct);

            return Ok(new
            {
                preferenceId = result.PreferenceId,
                initPoint = result.InitPoint,
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "RECEIPT_NOT_FOUND", message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating MercadoPago preference for receipt {ReceiptId}", receiptId);
            return StatusCode(500, new { code = "MP_ERROR", message = "Could not create payment preference." });
        }
    }

    // ─────────────────────────────────────────────────────────────
    // POST /api/v1/payments/process
    // Processes a card payment using Bricks token data.
    // ─────────────────────────────────────────────────────────────
    [HttpPost("process")]
    [Authorize]
    public async Task<IActionResult> ProcessCardPayment(
        [FromBody] JsonElement body, CancellationToken ct)
    {
        // Validate required fields
        if (!body.TryGetProperty("token", out var tokenProp) ||
            !body.TryGetProperty("transaction_amount", out var amountProp))
        {
            return BadRequest(new
            {
                code = "INVALID_PAYLOAD",
                message = "token and transaction_amount are required."
            });
        }

        var receiptId = body.TryGetProperty("receiptId", out var rProp)
            ? rProp.ToString()
            : body.TryGetProperty("external_reference", out var refProp)
                ? refProp.GetString() ?? string.Empty
                : string.Empty;

        var ownerAdminId = body.TryGetProperty("ownerAdminId", out var ownerProp)
            ? ownerProp.GetString() ?? string.Empty
            : string.Empty;

        var request = new CardPaymentRequest(
            Token: tokenProp.GetString() ?? string.Empty,
            PaymentMethodId: body.TryGetProperty("payment_method_id", out var pmProp)
                ? pmProp.GetString() ?? "visa" : "visa",
            TransactionAmount: amountProp.GetDecimal(),
            Installments: body.TryGetProperty("installments", out var instProp)
                ? instProp.GetInt32() : 1,
            PayerEmail: body.TryGetProperty("payer", out var payerProp) &&
                        payerProp.TryGetProperty("email", out var emailProp)
                ? emailProp.GetString() ?? "guest@buildingfex.com"
                : "guest@buildingfex.com",
            ReceiptExternalId: receiptId,
            OwnerAdminExternalId: ownerAdminId,
            IssuerId: body.TryGetProperty("issuer_id", out var issProp)
                ? issProp.ToString() : null);

        try
        {
            var result = await mercadoPagoService.ProcessCardPaymentAsync(request, ct);

            return Ok(new
            {
                id = result.PaymentId,
                status = result.Status,
                status_detail = result.StatusDetail,
                transaction_amount = result.TransactionAmount,
                payment_method_id = result.PaymentMethodId,
                date_approved = result.DateApproved,
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "RECEIPT_NOT_FOUND", message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing card payment for receipt {ReceiptId}", receiptId);
            return StatusCode(500, new { code = "MP_ERROR", message = "Could not process payment." });
        }
    }

    // ─────────────────────────────────────────────────────────────
    // POST /api/v1/payments/webhook
    // Receives IPN notifications from Mercado Pago. No auth required.
    // ─────────────────────────────────────────────────────────────
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook([FromBody] JsonElement body, CancellationToken ct)
    {
        // Mercado Pago sends: { "action": "payment.created", "data": { "id": 123456 }, "type": "payment" }
        // Or: { "id": 123456, "topic": "payment" } (older IPN format)
        logger.LogInformation("MercadoPago webhook received: {Body}", body.GetRawText());

        long mpPaymentId = 0;

        // New webhook format
        if (body.TryGetProperty("data", out var dataProp) &&
            dataProp.TryGetProperty("id", out var dataIdProp))
        {
            mpPaymentId = dataIdProp.ValueKind == JsonValueKind.Number
                ? dataIdProp.GetInt64()
                : long.TryParse(dataIdProp.GetString(), out var parsed) ? parsed : 0;
        }
        // Older IPN format
        else if (body.TryGetProperty("id", out var idProp) &&
                 body.TryGetProperty("topic", out var topicProp) &&
                 topicProp.GetString() == "payment")
        {
            mpPaymentId = idProp.ValueKind == JsonValueKind.Number
                ? idProp.GetInt64()
                : long.TryParse(idProp.GetString(), out var parsed) ? parsed : 0;
        }

        // Also handle query-string based IPN: ?topic=payment&id=12345
        if (mpPaymentId == 0)
        {
            var qId = HttpContext.Request.Query["id"].FirstOrDefault();
            var qTopic = HttpContext.Request.Query["topic"].FirstOrDefault();
            if (qTopic == "payment" && long.TryParse(qId, out var qParsed))
                mpPaymentId = qParsed;
        }

        if (mpPaymentId == 0)
        {
            // Not a payment notification — acknowledge to stop retries
            return Ok(new { status = "ignored" });
        }

        try
        {
            var result = await mercadoPagoService.HandleWebhookAsync(mpPaymentId, ct);
            return Ok(new { processed = result.Processed, status = result.Status });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing webhook for MP payment {MpId}", mpPaymentId);
            // Return 200 to prevent MercadoPago retries on internal errors
            return Ok(new { processed = false, status = "internal_error" });
        }
    }
}
