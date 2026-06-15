namespace BuildingFex.Api.Finances.Application.Internal.MercadoPago;

public record CreatePreferenceRequest(string ReceiptExternalId, string OwnerAdminExternalId);

public record PreferenceResult(string PreferenceId, string? InitPoint);

public record CardPaymentRequest(
    string Token,
    string PaymentMethodId,
    decimal TransactionAmount,
    int Installments,
    string PayerEmail,
    string ReceiptExternalId,
    string OwnerAdminExternalId,
    string? IssuerId = null);

public record CardPaymentResult(
    string PaymentId,
    string Status,
    string StatusDetail,
    decimal TransactionAmount,
    string? PaymentMethodId,
    string? DateApproved);

public record WebhookResult(bool Processed, string Status);
