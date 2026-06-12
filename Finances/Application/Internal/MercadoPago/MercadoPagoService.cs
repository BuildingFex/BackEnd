using BuildingFex.Api.Finances.Domain.Model.Aggregates;
using BuildingFex.Api.Finances.Domain.Repositories;
using BuildingFex.Api.Shared.Domain.Repositories;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.Payment;
using Microsoft.Extensions.Options;

namespace BuildingFex.Api.Finances.Application.Internal.MercadoPago;

public class MercadoPagoService(
    IOptions<MercadoPagoSettings> settings,
    IReceiptRepository receiptRepository,
    IPaymentRepository paymentRepository,
    FinanceOwnerResolver ownerResolver,
    IUnitOfWork unitOfWork,
    ILogger<MercadoPagoService> logger) : IMercadoPagoService
{
    private readonly MercadoPagoSettings _settings = settings.Value;

    private void EnsureSdkConfigured()
    {
        if (string.IsNullOrWhiteSpace(MercadoPagoConfig.AccessToken))
            MercadoPagoConfig.AccessToken = _settings.AccessToken;
    }

    // ─── Checkout Pro Preference ────────────────────────────────
    public async Task<PreferenceResult> CreatePreferenceAsync(
        CreatePreferenceRequest request, CancellationToken ct = default)
    {
        EnsureSdkConfigured();

        var receipt = await receiptRepository.FindByExternalIdAsync(request.ReceiptExternalId, ct)
            ?? throw new KeyNotFoundException($"Receipt '{request.ReceiptExternalId}' not found.");

        var totalAmount = receipt.Amount + receipt.LateFee + receipt.ExtraCharges;

        var preferenceRequest = new PreferenceRequest
        {
            Items =
            [
                new PreferenceItemRequest
                {
                    Title = $"BuildingFex – Recibo {receipt.ExternalId}",
                    Description = receipt.Concept ?? "Pago de expensas",
                    Quantity = 1,
                    CurrencyId = "PEN",
                    UnitPrice = totalAmount,
                }
            ],
            ExternalReference = receipt.ExternalId,
            BackUrls = new PreferenceBackUrlsRequest
            {
                Success = "https://buildingfex.com/payment/success",
                Failure = "https://buildingfex.com/payment/failure",
                Pending = "https://buildingfex.com/payment/pending",
            },
            AutoReturn = "approved",
            NotificationUrl = "https://YOUR-NGROK-URL/api/v1/payments/webhook",
        };

        var client = new PreferenceClient();
        var preference = await client.CreateAsync(preferenceRequest, cancellationToken: ct);

        logger.LogInformation(
            "MercadoPago preference created: {PreferenceId} for receipt {ReceiptId}, amount {Amount}",
            preference.Id, receipt.ExternalId, totalAmount);

        return new PreferenceResult(preference.Id!, preference.InitPoint);
    }

    // ─── Card Payment (Bricks token) ────────────────────────────
    public async Task<CardPaymentResult> ProcessCardPaymentAsync(
        CardPaymentRequest request, CancellationToken ct = default)
    {
        EnsureSdkConfigured();

        // Receipt is optional for generic payments
        Receipt? receipt = null;
        if (!string.IsNullOrWhiteSpace(request.ReceiptExternalId))
        {
            receipt = await receiptRepository.FindByExternalIdAsync(request.ReceiptExternalId, ct);
        }

        var owner = await ownerResolver.ResolveOwnerAdminAsync(request.OwnerAdminExternalId, ct)
            ?? throw new InvalidOperationException("Valid ownerAdminId is required.");

        // ─── DEMO MODE BYPASS ───
        if (request.Token == "DEMO-TOKEN")
        {
            logger.LogInformation("Demo payment processed for {Email} amount {Amount}", request.PayerEmail, request.TransactionAmount);
            
            // Reconcile generic demo payment if receipt exists
            if (receipt != null)
            {
                await ReconcilePaymentAsync(
                    receipt, owner.Id, request.TransactionAmount,
                    $"DEMO-{Guid.NewGuid():N}",
                    DateTimeOffset.UtcNow.ToString("o"),
                    ct);
            }

            return new CardPaymentResult(
                PaymentId: $"DEMO-{Guid.NewGuid().ToString()[..8]}",
                Status: "approved",
                StatusDetail: "accredited",
                TransactionAmount: request.TransactionAmount,
                PaymentMethodId: request.PaymentMethodId,
                DateApproved: DateTimeOffset.UtcNow.ToString("o"));
        }

        var paymentRequest = new PaymentCreateRequest
        {
            Token = request.Token,
            TransactionAmount = request.TransactionAmount,
            PaymentMethodId = request.PaymentMethodId,
            Installments = request.Installments,
            Payer = new PaymentPayerRequest { Email = request.PayerEmail },
            ExternalReference = receipt?.ExternalId ?? "GENERAL",
            Description = receipt != null ? $"BuildingFex – Recibo {receipt.ExternalId}" : "BuildingFex – Pago General",
        };

        if (!string.IsNullOrWhiteSpace(request.IssuerId))
            paymentRequest.IssuerId = request.IssuerId;

        var client = new PaymentClient();
        var mpPayment = await client.CreateAsync(paymentRequest, cancellationToken: ct);

        logger.LogInformation(
            "MercadoPago payment processed: {MpId} status={Status} for receipt {ReceiptId}",
            mpPayment.Id, mpPayment.Status, receipt?.ExternalId ?? "GENERAL");

        // If approved → reconcile (only if we have a specific receipt)
        if (string.Equals(mpPayment.Status, "approved", StringComparison.OrdinalIgnoreCase) && receipt != null)
        {
            await ReconcilePaymentAsync(
                receipt, owner.Id, request.TransactionAmount,
                mpPayment.Id?.ToString() ?? string.Empty,
                mpPayment.DateApproved?.ToString("o") ?? DateTimeOffset.UtcNow.ToString("o"),
                ct);
        }

        return new CardPaymentResult(
            PaymentId: mpPayment.Id?.ToString() ?? string.Empty,
            Status: mpPayment.Status ?? "unknown",
            StatusDetail: mpPayment.StatusDetail ?? string.Empty,
            TransactionAmount: mpPayment.TransactionAmount ?? request.TransactionAmount,
            PaymentMethodId: mpPayment.PaymentMethodId,
            DateApproved: mpPayment.DateApproved?.ToString("o"));
    }

    // ─── Webhook / IPN ──────────────────────────────────────────
    public async Task<WebhookResult> HandleWebhookAsync(long mpPaymentId, CancellationToken ct = default)
    {
        EnsureSdkConfigured();

        var client = new PaymentClient();
        global::MercadoPago.Resource.Payment.Payment mpPayment;

        try
        {
            mpPayment = await client.GetAsync(mpPaymentId, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch MercadoPago payment {MpId}", mpPaymentId);
            return new WebhookResult(false, "fetch_error");
        }

        if (!string.Equals(mpPayment.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Webhook for MP payment {MpId}: status={Status}, skipping.", mpPaymentId, mpPayment.Status);
            return new WebhookResult(false, mpPayment.Status ?? "not_approved");
        }

        var externalRef = mpPayment.ExternalReference;
        if (string.IsNullOrWhiteSpace(externalRef))
        {
            logger.LogWarning("MercadoPago payment {MpId} has no external_reference.", mpPaymentId);
            return new WebhookResult(false, "no_external_reference");
        }

        // Idempotency: check if we already recorded this payment
        var mpReferenceId = $"MP-{mpPaymentId}";
        var existing = await paymentRepository.FindByExternalIdAsync(mpReferenceId, ct);
        if (existing is not null)
        {
            logger.LogInformation("Webhook: payment {MpRef} already recorded, skipping.", mpReferenceId);
            return new WebhookResult(true, "already_processed");
        }

        var receipt = await receiptRepository.FindByExternalIdAsync(externalRef, ct);
        if (receipt is null)
        {
            logger.LogWarning("Webhook: receipt {ExtRef} not found for MP payment {MpId}.", externalRef, mpPaymentId);
            return new WebhookResult(false, "receipt_not_found");
        }

        await ReconcilePaymentAsync(
            receipt, receipt.OwnerAdminId,
            mpPayment.TransactionAmount ?? receipt.Amount,
            mpPaymentId.ToString(),
            mpPayment.DateApproved?.ToString("o") ?? DateTimeOffset.UtcNow.ToString("o"),
            ct);

        logger.LogInformation("Webhook: reconciled MP payment {MpId} → receipt {ReceiptId}.", mpPaymentId, externalRef);
        return new WebhookResult(true, "approved");
    }

    // ─── Shared reconciliation logic ────────────────────────────
    private async Task ReconcilePaymentAsync(
        Receipt receipt, int ownerAdminId, decimal amount,
        string mpPaymentId, string paidAt, CancellationToken ct)
    {
        // 1. Mark receipt as paid
        receipt.MarkAsPaid();
        receiptRepository.Update(receipt);

        // 2. Register payment record
        var paymentExternalId = $"MP-{mpPaymentId}";

        // Idempotency guard
        var existingPayment = await paymentRepository.FindByExternalIdAsync(paymentExternalId, ct);
        if (existingPayment is null)
        {
            var payment = Domain.Model.Aggregates.Payment.Create(
                paymentExternalId,
                ownerAdminId,
                receipt.ResidentExternalId,
                amount,
                feeExternalId: null,
                feeMonth: null,
                paidAt: paidAt,
                method: "Mercado Pago",
                reference: $"MP-{mpPaymentId}");

            await paymentRepository.AddAsync(payment, ct);
        }

        await unitOfWork.CompleteAsync(ct);
    }
}
