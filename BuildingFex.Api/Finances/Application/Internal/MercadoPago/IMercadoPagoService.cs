namespace BuildingFex.Api.Finances.Application.Internal.MercadoPago;

public interface IMercadoPagoService
{
    Task<PreferenceResult> CreatePreferenceAsync(CreatePreferenceRequest request, CancellationToken ct = default);
    Task<CardPaymentResult> ProcessCardPaymentAsync(CardPaymentRequest request, CancellationToken ct = default);
    Task<WebhookResult> HandleWebhookAsync(long mpPaymentId, CancellationToken ct = default);
}
